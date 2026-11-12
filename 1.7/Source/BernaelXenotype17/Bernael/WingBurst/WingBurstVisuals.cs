using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace Bernael_Xenotype
{
    // Wing Burst's smoke (BX_WingSmoke.frag.glsl): soft, translucent dark puffs close to the game's own smoke, a haze
    // over the smoke's cells and a ghostly streamer trailing from the flying caster. Mortis (2026-11-09): the earlier
    // dense toon smoke looked out of place, and the caster vanishing into it for a moment read as a bug; the caster now
    // stays in view throughout.
    [StaticConstructorOnStartup]
    public static class WingBurstVisuals
    {
        private const float HazeSize = 2.4f;
        // The streamer's width in cells; BX_WingSmoke narrows it toward its end.
        private const float TrailWidth = 1.75f;
        private static Material material;
        private static Mesh quad;
        private static bool attempted;
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly List<Vector3> trailCenters = new List<Vector3>();
        private static readonly List<float> trailDistances = new List<float>();
        private static readonly List<Vector3> trailVertices = new List<Vector3>();
        private static readonly List<Vector2> trailUvs = new List<Vector2>();
        private static readonly List<int> trailIndices = new List<int>();

        public static bool Ready()
        {
            if (material != null) return true;
            if (attempted) return false;
            attempted = true;
            Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail("BX_WingSmoke")?.Shader;
            if (shader == null || !shader.isSupported)
            {
                Log.Warning("[Bernael] Wing Burst smoke shader unavailable; using the normal flyer.");
                return false;
            }
            material = new Material(shader) { name = "Bernael wing smoke" };
            // Piloto Studio masks; see Common/Textures/Effects/Piloto/SOURCES.json.
            material.SetTexture("_Puffs", ContentFinder<Texture2D>.Get("Effects/Piloto/FogPuffs"));
            quad = new Mesh { name = "Wing smoke UV plane" };
            quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f),
                new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateBounds();
            quad.UploadMeshData(true);
            return true;
        }

        // GLSL's smoothstep over 0-1. Unity's Mathf.SmoothStep(a, b, t) interpolates from a to b instead.
        public static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3 - 2 * x);
        }

        // One puff, swelling as it drifts away along its direction and thinning out over its lifetime.
        public static void DrawPuff(WingBurstPuff puff, int now)
        {
            float age = Mathf.Clamp01((float)(now - puff.startTick) / puff.lifetime);
            float size = puff.size * (0.8f + age * 0.55f);
            Vector3 at = puff.center + puff.direction * (puff.drift * (1 - Mathf.Exp(-age * 3.5f)));
            properties.Clear();
            properties.SetFloat("_Mode", 1);
            properties.SetFloat("_Age", age);
            properties.SetFloat("_Seed", puff.seed % 97 * 0.37f);
            properties.SetFloat("_Opacity", puff.opacity);
            Draw(at, puff.direction, new Vector3(size, 1, size), 0f);
        }

        // The haze over one smoke cell, translucent so pawns inside stay visible. appear runs from 0 to 1 as the
        // cloud settles after the landing; the haze then thins away with the smoke.
        public static void DrawLingeringSmoke(Vector3 at, float density, int seed, float appear)
        {
            float strength = Mathf.Clamp01(density * 2.5f) * appear;
            if (strength <= 0.01f) return;
            properties.Clear();
            properties.SetFloat("_Mode", 2);
            properties.SetFloat("_Phase", Find.TickManager.TicksGame / 60f);
            properties.SetFloat("_Seed", seed % 97 * 0.29f);
            properties.SetFloat("_Opacity", 0.2f * strength);
            float size = HazeSize * (0.8f + 0.2f * strength);
            at += new Vector3(Mathf.Sin(seed * 0.987f) * 0.22f, 0, Mathf.Cos(seed * 0.731f) * 0.22f);
            Draw(at, new Vector3(Mathf.Cos(seed), 0, Mathf.Sin(seed)), new Vector3(size, 1, size), -0.02f);
        }

        // Adjacent spans share the same edge and UVs. Separate rotated quads overlap or open gaps on bends.
        public static void DrawTrail(Mesh ribbon, Vector3 at, List<Vector3> path, float seconds, int seed, float opacity)
        {
            if (opacity <= 0.01f) return;
            trailCenters.Clear();
            trailDistances.Clear();
            trailCenters.Add(Vector3.zero);
            trailDistances.Add(0f);
            float total = 0;
            Vector3 previous = Vector3.zero;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 center = (path[i] - at).Yto0();
                float length = (previous - center).magnitude;
                if (length < 0.002f) continue;
                total += length;
                trailCenters.Add(center);
                trailDistances.Add(total);
                previous = center;
            }
            if (total < 0.1f) return;

            trailVertices.Clear();
            trailUvs.Clear();
            trailIndices.Clear();
            Vector3 incoming = (trailCenters[0] - trailCenters[1]).normalized;
            for (int i = 0; i < trailCenters.Count; i++)
            {
                Vector3 center = trailCenters[i];
                Vector3 outgoing = i + 1 < trailCenters.Count ? (center - trailCenters[i + 1]).normalized : incoming;
                Vector3 tangent = (incoming + outgoing).normalized;
                if (tangent.sqrMagnitude < 0.01f) tangent = incoming;
                // Keep the same width through turns, with a limit on how far a sharp join can extend.
                float miter = 0.5f * TrailWidth / Mathf.Max(0.72f, Vector3.Dot(tangent, incoming));
                Vector3 side = new Vector3(-tangent.z, 0f, tangent.x) * miter;
                float u = trailDistances[i] / total;
                trailVertices.Add(center - side);
                trailVertices.Add(center + side);
                trailUvs.Add(new Vector2(u, 0f));
                trailUvs.Add(new Vector2(u, 1f));
                incoming = outgoing;
                if (i == 0) continue;
                int index = i * 2;
                trailIndices.Add(index - 2);
                trailIndices.Add(index);
                trailIndices.Add(index + 1);
                trailIndices.Add(index - 2);
                trailIndices.Add(index + 1);
                trailIndices.Add(index - 1);
            }

            ribbon.Clear();
            ribbon.SetVertices(trailVertices);
            ribbon.SetUVs(0, trailUvs, 0, trailUvs.Count);
            ribbon.SetTriangles(trailIndices.ToArray(), 0);
            ribbon.RecalculateBounds();
            properties.Clear();
            properties.SetFloat("_Mode", 3);
            properties.SetFloat("_Phase", seconds);
            properties.SetFloat("_Seed", seed % 17 * 0.29f);
            properties.SetFloat("_Length", total);
            properties.SetFloat("_Opacity", opacity);
            at.y = Altitude(-0.01f);
            Graphics.DrawMesh(ribbon, Matrix4x4.TRS(at, Quaternion.identity, Vector3.one), material, 0, null, 0,
                properties, ShadowCastingMode.Off, false);
        }

        private static float Altitude(float offset) => AltitudeLayer.VisEffects.AltitudeFor() + offset;

        // The quad's uv.x runs along direction.
        private static void Draw(Vector3 at, Vector3 direction, Vector3 size, float altitude)
        {
            at.y = Altitude(altitude);
            direction.y = 0;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.right;
            Quaternion rotation = Quaternion.LookRotation(new Vector3(-direction.z, 0, direction.x));
            Graphics.DrawMesh(quad, Matrix4x4.TRS(at, rotation, size), material, 0, null, 0,
                properties, ShadowCastingMode.Off, false);
        }
    }

    public sealed class WingBurstPuff : IExposable
    {
        public Vector3 center, direction;
        public int startTick, lifetime, seed;
        public float size, drift;
        // Kept from the toon-smoke version so its saves load unchanged; the soft smoke no longer uses them.
        public float frameStart;
        public float erodeFrom = 0.72f;
        // Peak opacity: the landing cloud is fuller than the wisps of the warmup and the flight.
        public float opacity = 1f;
        public void ExposeData()
        {
            Scribe_Values.Look(ref frameStart, "frameStart", 0f);
            Scribe_Values.Look(ref erodeFrom, "erodeFrom", 0.72f);
            Scribe_Values.Look(ref opacity, "opacity", 1f);
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref direction, "direction");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref lifetime, "lifetime");
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref size, "size");
            Scribe_Values.Look(ref drift, "drift");
        }
    }

    public sealed class WingBurstMapComponent : MapComponent
    {
        private List<WingBurstPuff> puffs = new List<WingBurstPuff>();

        public WingBurstMapComponent(Map map) : base(map) { }

        // Visual only, so every variation comes from the seed rather than the game's random state. The flight sheds
        // small, faint puffs that linger a moment along the path behind the streamer, then disperse. They stay
        // slighter than the streamer, which carries the trail.
        public void Wake(Vector3 at, Vector3 direction, int seed)
        {
            Vector3 side = Quaternion.AngleAxis(seed * 137.5f % 360f, Vector3.up) * Vector3.forward;
            Add(at - direction * 0.45f + side * 0.12f, -direction * 0.6f + side * 0.6f, 0.95f + seed % 3 * 0.1f, 0.35f,
                42, seed, 0.34f);
        }

        // The leap throws the caster's residue out behind it and to its sides.
        public void TakeOff(Vector3 at, Vector3 direction, int seed)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 spread = Quaternion.AngleAxis(125 + i * 37 + seed % 20, Vector3.up) * direction;
                Add(at + spread * 0.3f, spread, 1.05f + (i % 2) * 0.15f, 0.7f, 40 + i * 4, seed + i * 13, 0.5f);
            }
        }

        // The streamer's last stretch, left behind as it lands so it dissolves instead of vanishing with the flyer.
        public void Shed(List<Vector3> path, Vector3 direction, int seed)
        {
            for (int i = 1; i < path.Count; i += 3)
                Add(path[i], -direction, 0.8f + i % 2 * 0.15f, 0.25f, 26 + i, seed + i * 5, 0.32f);
        }

        public void Land(Vector3 at, Vector3 direction, int seed)
        {
            // The cloud rolls out around the landing: a ring spreading outward and a fan carried on by the landing's
            // momentum. Every puff leaves the middle quickly, so the caster shows within the cloud.
            for (int i = 0; i < 8; i++)
            {
                // Uneven angles, sizes and reach, so the ring billows rather than forming a regular wreath.
                int vary = Mathf.Abs(seed * 7 + i * 13);
                Vector3 ring = Quaternion.AngleAxis(i * 45 + seed % 45 + (vary % 11 - 5) * 3f, Vector3.up) * direction;
                Add(at + ring * (0.6f + vary % 3 * 0.1f), ring, 1.5f + vary % 4 * 0.15f, 1.3f + vary % 5 * 0.2f,
                    60 + vary % 6 * 4, seed + 50 + i * 11, 0.72f);
            }
            for (int i = 0; i < 5; i++)
            {
                Vector3 spread = Quaternion.AngleAxis(-50 + i * 25, Vector3.up) * direction;
                Add(at + spread * 0.9f, spread, 2.0f + (i % 2) * 0.3f, 2.2f + (i % 3) * 0.3f, 76 + i * 4, seed + i * 7,
                    0.62f);
            }
        }

        // Wisps leaking from the back and wings of a caster warming up.
        private void Seep(Vector3 at, Vector3 direction, int seed)
        {
            Vector3 across = Vector3.Cross(Vector3.up, direction) * (seed % 2 == 0 ? 1f : -1f);
            Add(at - direction * 0.2f + across * 0.3f, -direction * 0.6f + across, 0.75f + seed % 3 * 0.1f, 0.45f, 40,
                seed, 0.38f);
        }

        private void Add(Vector3 center, Vector3 direction, float size, float drift, int lifetime, int seed,
            float opacity)
        {
            direction.y = 0;
            puffs.Add(new WingBurstPuff { center = center, direction = direction.normalized, size = size, drift = drift,
                lifetime = lifetime, startTick = Find.TickManager.TicksGame, seed = seed, opacity = opacity });
        }

        // A caster in its Wing Burst warmup, with the warmup's progress and the direction it will leap.
        private static bool WarmingUp(Pawn pawn, out float progress, out Vector3 direction)
        {
            progress = 0f;
            direction = Vector3.right;
            if (!(pawn.stances?.curStance is Stance_Warmup warmup) || !(warmup.verb is Verb_CastAbilityWingBurst))
                return false;
            int elapsed = Mathf.Max(0, Find.TickManager.TicksGame - warmup.startedTick);
            progress = elapsed / Mathf.Max(1f, elapsed + warmup.ticksLeft);
            Vector3 toward = (warmup.focusTarg.CenterVector3 - pawn.DrawPos).Yto0();
            if (toward.sqrMagnitude > 0.01f) direction = toward.normalized;
            return true;
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = puffs.Count - 1; i >= 0; i--)
                if (now - puffs[i].startTick >= puffs[i].lifetime) puffs.RemoveAt(i);
            if (now % 3 == 0)
            {
                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                    if (WarmingUp(pawns[i], out float progress, out Vector3 direction) && progress > 0.3f)
                        Seep(pawns[i].DrawPos, direction, pawns[i].thingIDNumber + now);
            }
        }

        public override void MapComponentDraw()
        {
            if (map != Find.CurrentMap || puffs.Count == 0 || !WingBurstVisuals.Ready()) return;
            int now = Find.TickManager.TicksGame;
            foreach (WingBurstPuff puff in puffs)
                if (puff.center.ToIntVec3().InBounds(map) && !puff.center.ToIntVec3().Fogged(map))
                    WingBurstVisuals.DrawPuff(puff, now);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref puffs, "wingBurstPuffs", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                puffs ??= new List<WingBurstPuff>();
                puffs.RemoveAll(p => p == null || p.lifetime <= 0);
            }
        }

        public override void MapRemoved() => puffs.Clear();
    }
}
