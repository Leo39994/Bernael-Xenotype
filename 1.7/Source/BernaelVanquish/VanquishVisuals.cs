using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace Bernael_Xenotype
{
    public sealed class VanquishStroke : IExposable
    {
        public Pawn caster, victim;
        public ThingWithComps weapon;
        public Ability ability;
        public Vector3 impact, direction;
        public int warmupTicks, strikeTick = -1;
        public bool hit, killed, animateWeapon = true;

        public float Progress => strikeTick < 0
            ? 1 - Mathf.Clamp01((float)(ability?.verb?.WarmupTicksLeft ?? warmupTicks) / Mathf.Max(1, warmupTicks))
            : Mathf.Clamp01((Find.TickManager.TicksGame - strikeTick) / 16f);

        // The thrust (Mortis, 2026-11-09: a stab driven in from above at about 30 degrees, not a slash). Over the
        // windup the blade turns from its held angle to point down at the target from above, draws back and rises; in
        // the windup's last moments it drives forward, landing as the strike resolves; then it holds a moment and
        // recovers. lean is the share of that turn (VanquishWeaponAnimation), extension the sword's travel along the
        // thrust and raise its lift, both in cells.
        public void Pose(out float lean, out float extension, out float raise)
        {
            float p = Progress;
            if (strikeTick < 0)
            {
                if (p < 0.65f)
                {
                    float k = Mathf.SmoothStep(0, 1, p / 0.65f);
                    lean = k;
                    extension = -0.38f * k;
                    raise = 0.32f * k;
                }
                else if (p < 0.85f)
                {
                    // Coiled, gathering itself.
                    float k = (p - 0.65f) / 0.2f;
                    lean = 1;
                    extension = -0.38f - 0.06f * k;
                    raise = 0.32f + 0.04f * k;
                }
                else
                {
                    float k = (p - 0.85f) / 0.15f;
                    k *= k;
                    lean = 1;
                    extension = Mathf.Lerp(-0.44f, 0.5f, k);
                    raise = Mathf.Lerp(0.36f, 0.08f, k);
                }
                return;
            }
            if (p < 0.35f)
            {
                lean = 1;
                extension = Mathf.Lerp(0.5f, 0.44f, p / 0.35f);
                raise = 0.08f;
                return;
            }
            float r = Mathf.SmoothStep(0, 1, (p - 0.35f) / 0.65f);
            lean = 1 - r;
            extension = Mathf.Lerp(0.44f, 0f, r);
            raise = Mathf.Lerp(0.08f, 0f, r);
        }

        // Turns an aim (0 north, 90 east) by up to 30 degrees toward the bottom of the screen, so the blade comes
        // down on its target from above. Aimed straight up or down, it stays as it is.
        public static float FromAbove(float aim)
        {
            aim = Mathf.Repeat(aim, 360f);
            if (aim > 0f && aim < 180f) return Mathf.Min(30f, 180f - aim);
            if (aim > 180f) return -Mathf.Min(30f, aim - 180f);
            return 0f;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref victim, "victim");
            Scribe_References.Look(ref weapon, "weapon");
            Scribe_References.Look(ref ability, "ability");
            Scribe_Values.Look(ref impact, "impact");
            Scribe_Values.Look(ref direction, "direction");
            Scribe_Values.Look(ref warmupTicks, "warmupTicks");
            Scribe_Values.Look(ref strikeTick, "strikeTick", -1);
            Scribe_Values.Look(ref hit, "hit");
            Scribe_Values.Look(ref killed, "killed");
            Scribe_Values.Look(ref animateWeapon, "animateWeapon", true);
        }
    }

    public sealed class VanquishMapComponent : MapComponent
    {
        private List<VanquishStroke> strokes = new List<VanquishStroke>();
        public VanquishMapComponent(Map map) : base(map) { }

        public override void FinalizeInit()
        {
            // Equipment-owned abilities finish rebinding late in load. Rebuild their caster's
            // draw tree on the main thread before parallel rendering resumes.
            LongEventHandler.ExecuteWhenFinished(() => {
                foreach (VanquishStroke stroke in strokes)
                    if (stroke.caster?.Spawned == true && stroke.caster.Map == map)
                        stroke.caster.Drawer.renderer.EnsureGraphicsInitialized();
            });
        }

        public VanquishStroke StrokeFor(Thing weapon)
        {
            for (int i = strokes.Count - 1; i >= 0; i--)
                if (strokes[i].weapon == weapon && strokes[i].animateWeapon) return strokes[i];
            return null;
        }

        public void BeginWindup(Ability ability, Pawn victim, int ticks)
        {
            Pawn caster = ability.pawn;
            if (caster.equipment?.Primary == null || victim == null) return;
            foreach (VanquishStroke old in strokes)
                if (old.weapon == caster.equipment.Primary) old.animateWeapon = false;
            Vector3 direction = (victim.DrawPos - caster.DrawPos).Yto0().normalized;
            strokes.Add(new VanquishStroke {
                ability = ability, caster = caster, victim = victim, weapon = caster.equipment.Primary,
                warmupTicks = ticks, impact = victim.DrawPos, direction = direction
            });
        }

        public void Strike(Ability ability, Pawn victim, Vector3 impact, bool hit, bool killed)
        {
            VanquishStroke stroke = StrokeFor(ability.pawn.equipment?.Primary);
            if (stroke == null || stroke.strikeTick >= 0)
            {
                BeginWindup(ability, victim, 1);
                stroke = StrokeFor(ability.pawn.equipment?.Primary);
            }
            if (stroke == null) return;
            stroke.strikeTick = Find.TickManager.TicksGame;
            stroke.impact = impact;
            stroke.hit = hit;
            stroke.killed = killed;
            stroke.direction = (impact - ability.pawn.DrawPos).Yto0().normalized;
        }

        public override void MapComponentTick()
        {
            for (int i = strokes.Count - 1; i >= 0; i--)
            {
                VanquishStroke s = strokes[i];
                if (s.strikeTick >= 0)
                {
                    // A kill keeps the stroke until its soul has sunk into the wielder.
                    if (Find.TickManager.TicksGame - s.strikeTick >= (s.killed ? AbsorbDelay + AbsorbTicks : 30))
                        strokes.RemoveAt(i);
                }
                else if (s.caster == null || !s.caster.Spawned || s.caster.Map != map ||
                    s.caster.equipment?.Primary != s.weapon || s.ability?.verb?.WarmingUp != true ||
                    s.caster.CurJob?.ability != s.ability)
                    strokes.RemoveAt(i);
                else if (s.victim?.Spawned == true)
                    s.direction = (s.victim.DrawPos - s.caster.DrawPos).Yto0().normalized;
            }
        }

        // A kill's soul is taken in by the wielder (Mortis, 2026-11-09: shown on the wielder, not as a ghost flying
        // across): soul-light swirls in around them from AbsorbDelay ticks after the strike, for AbsorbTicks.
        public const int AbsorbDelay = 4;
        public const int AbsorbTicks = 54;

        public override void MapComponentDraw()
        {
            if (map != Find.CurrentMap || strokes.Count == 0 || !VanquishVisuals.Ready()) return;
            int now = Find.TickManager.TicksGame;
            foreach (VanquishStroke s in strokes)
            {
                // The thrust itself is drawn by VanquishWeaponAnimation alone and leaves no light; only its contact
                // and a kill's absorption are drawn here.
                if (s.strikeTick < 0 || s.caster?.Spawned != true || s.caster.Position.Fogged(map)) continue;
                float since = now - s.strikeTick;
                if (s.hit)
                    VanquishVisuals.Impact(s.impact, s.direction, since / 60f, s.killed,
                        s.caster.thingIDNumber + s.strikeTick);
                if (s.killed && since >= AbsorbDelay)
                    VanquishVisuals.Absorb(s.caster.DrawPos, (since - AbsorbDelay) / 60f, s.caster.thingIDNumber);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref strokes, "vanquishStrokes", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) strokes ??= new List<VanquishStroke>();
        }

        public override void MapRemoved() => strokes.Clear();
    }

    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class VanquishWeaponAnimation
    {
        // Draws the held sword through the thrust. Melee weapons inherit an equippedAngleOffset (-65 for the Life
        // Vanquisher), so the game draws the blade well off its aim, raised; and the game's mirrored mesh for westward
        // aims cannot point the blade everywhere. So during a stroke the sword is drawn here: its blade turns from the
        // game's own held angle (lean 0, so the stroke starts and ends without a jump) to the thrust direction, and it
        // moves from the game's own hand position along that direction.
        public static bool Prefix(Thing eq, Vector3 drawLoc, float aimAngle)
        {
            if (eq?.Graphic == null) return true;
            VanquishStroke s = Find.CurrentMap?.GetComponent<VanquishMapComponent>().StrokeFor(eq);
            if (s == null || s.caster?.Spawned != true || s.caster.equipment?.Primary != eq) return true;
            float p = s.Progress;
            if (s.strikeTick >= 0 && p >= 1) return true;
            s.Pose(out float lean, out float extension, out float raise);
            // The game's mesh choice, kept so the sword art never mirrors mid-stroke.
            bool flip = aimAngle > 200f && aimAngle < 340f;
            float offset = eq.def.equippedAngleOffset;
            float held = flip ? aimAngle - offset : aimAngle + offset;
            float thrust = aimAngle + VanquishStroke.FromAbove(aimAngle);
            float blade = Mathf.LerpAngle(held, thrust, lean);
            Vector3 at = drawLoc + new Vector3(0f, 0f, extension).RotatedBy(thrust) + new Vector3(0f, 0f, raise);
            // plane10 lays the texture's blade (+x) along compass 90 + angle; plane10Flip mirrors it to 270 + angle.
            float angle = blade - (flip ? 270f : 90f);
            Material material = eq.Graphic is Graphic_StackCount stack
                ? stack.SubGraphicForStackCount(1, eq.def).MatSingleFor(eq) : eq.Graphic.MatSingleFor(eq);
            Graphics.DrawMesh(flip ? MeshPool.plane10Flip : MeshPool.plane10,
                Matrix4x4.TRS(at, Quaternion.AngleAxis(angle, Vector3.up),
                    new Vector3(eq.Graphic.drawSize.x, 0f, eq.Graphic.drawSize.y)), material, 0);
            return false;
        }
    }

    [StaticConstructorOnStartup]
    public static class VanquishVisuals
    {
        private static Material material;
        private static Mesh quad;
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        public static bool Ready()
        {
            if (material != null) return true;
            Shader shader = DefDatabase<ShaderDef>.GetNamed("BX_Vanquish").Shader;
            if (shader == null || !shader.isSupported) return false;
            material = new Material(shader) { name = "Bernael Vanquish" };
            // Piloto Studio masks; see Common/Textures/Effects/Piloto/SOURCES.json.
            material.SetTexture("_ShardTex", ContentFinder<Texture2D>.Get("Effects/Piloto/ImpactShards"));
            material.SetTexture("_RingTex", ContentFinder<Texture2D>.Get("Effects/Piloto/ShockRing"));
            material.SetTexture("_RaysTex", ContentFinder<Texture2D>.Get("Effects/Piloto/LightRays"));
            quad = new Mesh { name = "Vanquish UV plane" };
            quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f),
                new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateBounds();
            quad.UploadMeshData(true);
            return true;
        }

        // A kill's soul taken in by the wielder: soul-light swirls in close around them and sinks into the body.
        public static void Absorb(Vector3 at, float seconds, int seed)
        {
            if (seconds > VanquishMapComponent.AbsorbTicks / 60f) return;
            properties.Clear();
            properties.SetFloat("_Mode", 2);
            properties.SetFloat("_Opacity", 1f);
            properties.SetFloat("_Phase", seconds);
            properties.SetFloat("_Kill", 1);
            properties.SetFloat("_Seed", seed % 19 * 0.33f);
            properties.SetFloat("_Size", 2.4f);
            Draw(at, Quaternion.identity, new Vector3(2.4f, 1, 2.4f));
        }

        public static void Impact(Vector3 at, Vector3 direction, float seconds, bool killed, int seed)
        {
            if (seconds > 0.45f) return;
            float size = killed ? 3.6f : 2.6f;
            properties.Clear();
            properties.SetFloat("_Mode", 1);
            properties.SetFloat("_Opacity", 1);
            properties.SetFloat("_Phase", seconds);
            properties.SetFloat("_Kill", killed ? 1 : 0);
            properties.SetFloat("_Seed", (seed & int.MaxValue) % 997 * 0.137f);
            properties.SetFloat("_Size", size);
            float aim = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float thrust = aim + VanquishStroke.FromAbove(aim);
            Draw(at, Quaternion.AngleAxis(thrust - 90f, Vector3.up), new Vector3(size, 1, size));
        }

        private static void Draw(Vector3 at, Quaternion rotation, Vector3 scale)
        {
            at.y = AltitudeLayer.VisEffects.AltitudeFor();
            Graphics.DrawMesh(quad, Matrix4x4.TRS(at, rotation, scale), material,
                0, null, 0, properties, ShadowCastingMode.Off, false);
        }
    }
}
