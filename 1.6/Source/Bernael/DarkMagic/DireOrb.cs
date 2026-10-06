using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Bernael_Xenotype
{
    public sealed class CompProperties_DireOrb : CompProperties_AbilityLaunchProjectile
    {
        public CompProperties_DireOrb() { compClass = typeof(CompAbilityEffect_DireOrb); }
    }

    public sealed class CompAbilityEffect_DireOrb : CompAbilityEffect_LaunchProjectile
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (DarkMagicAssets.Ready()) return base.Valid(target, throwMessages);
            if (throwMessages) Messages.Message("BX_DarkMagicShaderMissing".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!Valid(target, true)) return;
            base.Apply(target, dest);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (target.Cell.InBounds(parent.pawn.Map))
                GenDraw.DrawRadiusRing(target.Cell, Props.projectileDef.projectile.explosionRadius);
        }
    }

    // Deals no damage itself: the burst it leaves behind implodes, then detonates.
    [StaticConstructorOnStartup]
    public sealed class Projectile_DireOrb : Projectile
    {
        // DireOrb.shader assumes these quad sizes.
        private const float OrbSize = 4f;
        private const float TrailLength = 2.6f;
        private const float TrailWidth = 1.2f;
        private const float ChargeSeconds = 0.4f;
        private static MaterialPropertyBlock properties;

        public float Seed => thingIDNumber % 97 * 0.13f;
        public float Flight => Mathf.Max(0f, StartingTicksToImpact - ticksToImpact) / 60f;
        public float Travelled => (ExactPosition - origin).MagnitudeHorizontal();

        public Vector3 Heading
        {
            get
            {
                Vector3 heading = destination - origin;
                heading.y = 0f;
                return heading.sqrMagnitude > 1e-4f ? heading.normalized : Vector3.right;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (DarkMagicAssets.Ready()) DrawOrb(drawLoc, Heading, Flight, Travelled, 0f, Seed);
        }

        // The orb and the wisps it sheds. time counts from launch; once the orb lands, DireOrbMapComponent
        // keeps drawing it where it struck, with landed counting the seconds since, while it collapses.
        public static void DrawOrb(Vector3 at, Vector3 heading, float time, float travelled, float landed, float seed)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            at.y = DireOrbMapComponent.Altitude;
            // Trail below the orb, so the orb's own flame sits on top of it.
            properties.Clear();
            properties.SetFloat("_Phase", time);
            properties.SetFloat("_Seed", seed);
            properties.SetFloat("_Opacity", 1f);
            properties.SetFloat("_Landed", landed);
            properties.SetFloat("_Mode", 1f);
            properties.SetFloat("_Length", TrailLength);
            properties.SetFloat("_Travel", travelled);
            Vector3 trail = at - heading * (TrailLength * 0.5f) - Vector3.up * (Altitudes.AltInc * 0.3f);
            DarkMagicAssets.Draw(DarkMagicAssets.Orb, trail, DarkMagicAssets.Along(-heading),
                new Vector3(TrailLength, 1f, TrailWidth), properties);

            properties.SetFloat("_Mode", 0f);
            properties.SetFloat("_Charge", Mathf.Clamp01(time / ChargeSeconds));
            properties.SetVector("_Direction", new Vector4(heading.x, heading.z, 0f, 0f));
            DarkMagicAssets.Draw(DarkMagicAssets.Orb, at, Quaternion.identity, Vector3.one * OrbSize, properties);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map?.GetComponent<DireOrbMapComponent>().Burst(this, launcher, hitThing as Pawn, DamageAmount,
                ArmorPenetration, blockedByShield);
            base.Impact(hitThing, blockedByShield);
        }
    }

    public sealed class DireOrbBurst : IExposable
    {
        public ThingDef projectile;
        public Pawn caster;
        public Faction faction;
        // Struck directly, so it is caught by the blast even when it is not hostile.
        public Pawn struck;
        public Vector3 center;
        public IntVec3 cell;
        public int startTick;
        public float amount;
        public float armorPenetration;
        public float seed;
        // Carried over from the projectile, so the landed orb and its wisps pick up where it left off.
        public Vector3 heading;
        public float travelled;
        public float flight;
        public bool blocked;
        public bool detonated;

        public float Radius => projectile.projectile.explosionRadius;

        public bool Affects(Pawn pawn) => pawn == struck ||
            (faction != null ? pawn.HostileTo(faction) : caster != null && pawn.HostileTo(caster));

        public void ExposeData()
        {
            Scribe_Defs.Look(ref projectile, "projectile");
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref struck, "struck");
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref heading, "heading");
            Scribe_Values.Look(ref travelled, "travelled");
            Scribe_Values.Look(ref flight, "flight");
            Scribe_Values.Look(ref blocked, "blocked");
            Scribe_Values.Look(ref detonated, "detonated");
        }
    }

    public sealed class DireOrbMapComponent : MapComponent
    {
        // Authored against DireOrb.shader: the landed orb collapses for 0.3 s, then bursts into cold flames that
        // have all burned out by 1.7 s.
        private const int DetonateTicks = 18;
        private const int LifetimeTicks = 102;
        private const int StaggerTicks = 95;
        private const float EdgeDamageFactor = 0.55f;
        // Room past the blast radius for the flames at its edge, which rise above it, and their glow.
        private const float BurstMargin = 1.9f;
        // Over pawns and everything else on the map, like vanilla's psycast skip flashes. DireOrb.shader's render
        // queue keeps it clear of the map's lighting.
        public static float Altitude => AltitudeLayer.VisEffects.AltitudeFor();
        private List<DireOrbBurst> bursts = new List<DireOrbBurst>();
        private MaterialPropertyBlock properties;
        public DireOrbMapComponent(Map map) : base(map) { }

        public void Burst(Projectile_DireOrb orb, Thing launcher, Pawn struck, float amount, float armorPenetration,
            bool blocked)
        {
            if (orb.def.projectile.explosionRadius <= 0f) return;
            Vector3 center = orb.ExactPosition;
            center.y = 0f;
            bursts.Add(new DireOrbBurst {
                projectile = orb.def, caster = launcher as Pawn, faction = launcher?.Faction, struck = struck,
                // Wall hits keep the last open cell, so line of sight starts in front of the wall.
                center = center, cell = orb.Position, startTick = Find.TickManager.TicksGame,
                amount = amount, armorPenetration = armorPenetration, seed = orb.Seed,
                heading = orb.Heading, travelled = orb.Travelled, flight = orb.Flight, blocked = blocked
            });
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                DireOrbBurst burst = bursts[i];
                if (!burst.detonated && now - burst.startTick >= DetonateTicks) Detonate(burst);
                if (now - burst.startTick >= LifetimeTicks) bursts.RemoveAt(i);
            }
        }

        private void Detonate(DireOrbBurst burst)
        {
            burst.detonated = true;
            burst.projectile.projectile.soundExplode?.PlayOneShot(new TargetInfo(burst.cell, map));
            // A shield swallows the blast; only the light show remains.
            if (burst.blocked) return;
            float radius = burst.Radius;
            DamageDef damage = burst.projectile.projectile.damageDef;
            foreach (Pawn victim in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (victim.Dead || victim.Downed || !burst.Affects(victim) ||
                    !victim.Position.InHorDistOf(burst.cell, radius) ||
                    !GenSight.LineOfSight(burst.cell, victim.Position, map, skipFirstCell: true)) continue;
                Vector3 away = victim.DrawPos - burst.center;
                away.y = 0f;
                float falloff = Mathf.Lerp(1f, EdgeDamageFactor, Mathf.Clamp01(away.magnitude / radius));
                victim.TakeDamage(new DamageInfo(damage, burst.amount * falloff, burst.armorPenetration,
                    away.AngleFlat(), burst.caster));
                if (!victim.Dead && !victim.Downed) victim.stances?.stagger.StaggerFor(StaggerTicks);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref bursts, "direOrbBursts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (bursts == null) bursts = new List<DireOrbBurst>();
                bursts.RemoveAll(b => b?.projectile?.projectile == null);
            }
        }

        public override void MapRemoved() => bursts.Clear();

        public override void MapComponentDraw()
        {
            if (map != Find.CurrentMap || bursts.Count == 0 || !DarkMagicAssets.Ready()) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            int now = Find.TickManager.TicksGame;
            foreach (DireOrbBurst burst in bursts)
            {
                if (burst.cell.Fogged(map)) continue;
                float elapsed = (now - burst.startTick) / 60f;
                // Until it detonates, the orb hangs where it struck and collapses.
                if (now - burst.startTick < DetonateTicks)
                {
                    Projectile_DireOrb.DrawOrb(burst.center, burst.heading, burst.flight + elapsed, burst.travelled, elapsed,
                        burst.seed);
                    continue;
                }
                properties.Clear();
                properties.SetFloat("_Phase", elapsed);
                properties.SetFloat("_Seed", burst.seed);
                properties.SetFloat("_Opacity", 1f);
                properties.SetFloat("_Mode", 2f);
                properties.SetFloat("_Radius", burst.Radius);
                Vector3 position = burst.center;
                position.y = Altitude;
                DarkMagicAssets.Draw(DarkMagicAssets.Orb, position, Quaternion.identity,
                    Vector3.one * ((burst.Radius + BurstMargin) * 2f), properties);
            }
        }
    }
}
