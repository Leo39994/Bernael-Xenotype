using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace Bernael_Xenotype
{
    public sealed class FoldGate : IExposable
    {
        public Pawn pawn;
        public Vector3 entry, exit, direction;
        public int openedTick, closedTick = -1;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref entry, "entry");
            Scribe_Values.Look(ref exit, "exit");
            Scribe_Values.Look(ref direction, "direction");
            Scribe_Values.Look(ref openedTick, "openedTick");
            Scribe_Values.Look(ref closedTick, "closedTick", -1);
        }
    }

    public sealed class FoldGateMapComponent : MapComponent
    {
        private List<FoldGate> gates = new List<FoldGate>();
        public FoldGateMapComponent(Map map) : base(map) { }
        public override void FinalizeInit()
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                if (pawn.CurJobDef == BernaelDefOf.BX_TraverseTheFold_Job) WorldComponent_TraverseTheFold.Add(pawn);
        }
        public FoldGate GateFor(Pawn pawn)
        {
            for (int i=0;i<gates.Count;i++)
                if (gates[i].pawn==pawn && gates[i].closedTick<0) return gates[i];
            return null;
        }

        public void Begin(Pawn pawn, IntVec3 start, IntVec3 wall, IntVec3 destination)
        {
            if (GateFor(pawn) != null || !start.IsValid || !wall.IsValid || !destination.IsValid) return;
            Vector3 delta = (destination - start).ToVector3();
            Vector3 direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)
                ? new Vector3(Mathf.Sign(delta.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(delta.z));
            if (delta.sqrMagnitude < 0.01f) return;
            gates.Add(new FoldGate { pawn = pawn, direction = direction,
                entry = wall.ToVector3Shifted() - direction * 0.5f,
                exit = destination.ToVector3Shifted() - direction * 0.5f,
                openedTick = Find.TickManager.TicksGame });
        }

        public void End(Pawn pawn)
        {
            FoldGate gate = GateFor(pawn);
            if (gate != null) gate.closedTick = Find.TickManager.TicksGame;
        }

        public float Opacity(Pawn pawn, Vector3 at)
        {
            FoldGate gate = GateFor(pawn);
            if (gate == null) return 1f;
            float along = Vector3.Dot(at - gate.entry, gate.direction);
            float length = Vector3.Dot(gate.exit - gate.entry, gate.direction);
            if (length < 0.2f) return 1f;
            float entering = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.44f, 0.08f, along));
            float emerging = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(length - 0.08f, length + 0.44f, along));
            return Mathf.Max(entering, emerging);
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = gates.Count - 1; i >= 0; i--)
            {
                FoldGate gate = gates[i];
                if (gate.closedTick < 0 && (gate.pawn == null || !gate.pawn.Spawned || gate.pawn.Map != map ||
                    gate.pawn.CurJobDef != BernaelDefOf.BX_TraverseTheFold_Job)) gate.closedTick = now;
                if (gate.closedTick >= 0 && now - gate.closedTick >= 18) gates.RemoveAt(i);
            }
        }

        public override void MapComponentDraw()
        {
            if (map != Find.CurrentMap || gates.Count == 0 || !FoldGateVisuals.Ready()) return;
            int now = Find.TickManager.TicksGame;
            foreach (FoldGate gate in gates)
            {
                float open = Mathf.Clamp01((now - gate.openedTick) / 20f);
                float close = gate.closedTick < 0 ? 0 : Mathf.Clamp01((now - gate.closedTick) / 18f);
                float time = (now - gate.openedTick) / 60f;
                Vector3? at = gate.pawn != null && gate.pawn.Spawned && gate.pawn.Map == map ? gate.pawn.DrawPos : (Vector3?)null;
                if (gate.entry.ToIntVec3().InBounds(map) && !gate.entry.ToIntVec3().Fogged(map))
                    FoldGateVisuals.Draw(gate.entry, -gate.direction, time, open, close, at, false, gate.pawn?.thingIDNumber ?? 0);
                if (gate.exit.ToIntVec3().InBounds(map) && !gate.exit.ToIntVec3().Fogged(map))
                    FoldGateVisuals.Draw(gate.exit, gate.direction, time, open, close, at, true, gate.pawn?.thingIDNumber ?? 0);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref gates, "foldGates", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) gates ??= new List<FoldGate>();
        }
        public override void MapRemoved() => gates.Clear();
    }

    [StaticConstructorOnStartup]
    public static class FoldGateVisuals
    {
        // The rift's quad: Length along the wall face, Depth from inside the wall out over the floor. The face sits
        // at 0.3 of the depth (BX_FoldGate.frag.glsl Face).
        private const float Length = 2.5f;
        private const float Depth = 1.5f;
        private const float Face = 0.3f;
        private static Material material;
        private static Mesh quad;
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        public static bool Ready()
        {
            if (material != null) return true;
            Shader shader = DefDatabase<ShaderDef>.GetNamed("BX_FoldGate").Shader;
            if (shader == null || !shader.isSupported) return false;
            material = new Material(shader) { name = "Bernael fold rift" };
            // Piloto Studio masks; see Common/Textures/Effects/Piloto/SOURCES.json.
            material.SetTexture("_FlowTex", ContentFinder<Texture2D>.Get("Effects/Piloto/FogFlow"));
            quad = new Mesh { name = "Fold gate UV plane" };
            quad.vertices = new[] { new Vector3(-0.5f,0,-0.5f), new Vector3(-0.5f,0,0.5f),
                new Vector3(0.5f,0,0.5f), new Vector3(0.5f,0,-0.5f) };
            quad.uv = new[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) };
            quad.triangles = new[] { 0,1,2,0,2,3 };
            quad.RecalculateBounds(); quad.UploadMeshData(true);
            return true;
        }

        // The caster parts the veil at the point where it crosses the wall face.
        public static void Draw(Vector3 face, Vector3 outward, float seconds, float open, float close, Vector3? pawn,
            bool exit, int seed)
        {
            Quaternion rotation = Quaternion.LookRotation(outward);
            Vector3 side = rotation * Vector3.right;
            float push = 0f, pushAt = 0.5f;
            if (pawn.HasValue)
            {
                Vector3 offset = (pawn.Value - face).Yto0();
                float along = Vector3.Dot(offset, outward);
                push = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.83f, Mathf.Abs(along)));
                pushAt = Mathf.Clamp01(0.5f + Vector3.Dot(offset, side) / Length);
            }
            properties.Clear();
            properties.SetFloat("_Phase", seconds);
            properties.SetFloat("_Opacity", 1f);
            properties.SetFloat("_Exit", exit ? 1 : 0);
            properties.SetFloat("_Push", push);
            properties.SetFloat("_PushAt", pushAt);
            properties.SetFloat("_Open", open);
            properties.SetFloat("_Close", close);
            properties.SetFloat("_Seed", (seed % 31) * 0.37f + (exit ? 5.3f : 0f));
            Vector3 at = face + outward * ((0.5f - Face) * Depth);
            at.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            Graphics.DrawMesh(quad, Matrix4x4.TRS(at, rotation, new Vector3(Length, 1, Depth)),
                material, 0, null, 0, properties, ShadowCastingMode.Off, false);
        }
    }
}
