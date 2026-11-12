using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    public sealed class FoldExit : IExposable
    {
        public Pawn pawn;
        public Vector3 position;
        public int startedTick;
        public FoldShiftVisuals visuals;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref position, "position");
            Scribe_Values.Look(ref startedTick, "startedTick");
        }
    }

    public sealed class FoldVisualMapComponent : MapComponent
    {
        private List<FoldExit> exits = new List<FoldExit>();
        private readonly Dictionary<Pawn, FoldShiftVisuals> active = new Dictionary<Pawn, FoldShiftVisuals>();
        private readonly List<Pawn> stale = new List<Pawn>();
        public FoldVisualMapComponent(Map map) : base(map) { }

        public void Track(Pawn pawn, FoldShiftVisuals visuals) => active[pawn] = visuals;

        public void EndShift(Pawn pawn, FoldShiftVisuals visuals)
        {
            active.Remove(pawn);
            // A shadow that was never drawn has nothing to burn away. Capturing one now would pop a fresh shadow
            // over the pawn the game is already drawing again.
            if (visuals == null) return;
            CancelExit(pawn);
            exits.Add(new FoldExit { pawn = pawn, position = pawn.DrawPos, visuals = visuals,
                startedTick = Find.TickManager.TicksGame });
        }

        public void CancelExit(Pawn pawn)
        {
            int expired = Find.TickManager.TicksGame - ExitTicks;
            for (int i = exits.Count - 1; i >= 0; i--)
                if (exits[i].pawn == pawn) exits[i].startedTick = expired;
        }

        private const int DissolveTicks = 32;
        // Leave a short gap after the last draw before releasing the submitted textures.
        public const int ExitTicks = 36;

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                stale.Clear();
                foreach (var entry in active)
                    if ((!entry.Key.Spawned || entry.Key.Map != map || entry.Key.Dead) && !entry.Value.WasDrawnRecently)
                    { entry.Value.Dispose(); stale.Add(entry.Key); }
                foreach (Pawn pawn in stale) active.Remove(pawn);
            }
            int now = Find.TickManager.TicksGame;
            for (int i = exits.Count - 1; i >= 0; i--)
            {
                FoldExit exit = exits[i];
                if (exit.visuals?.WasDrawnRecently == true) continue;
                if (now - exit.startedTick < ExitTicks && exit.pawn?.Spawned == true && exit.pawn.Map == map) continue;
                exit.visuals?.Dispose();
                exits.RemoveAt(i);
            }
        }

        public override void MapComponentDraw()
        {
            if (map != Find.CurrentMap || exits.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            foreach (FoldExit exit in exits)
            {
                // An exit restored from a save has no captures left; the pawn simply shows again.
                int ticks = now - exit.startedTick;
                if (ticks < 0 || ticks >= DissolveTicks || exit.visuals == null || exit.pawn == null ||
                    !exit.pawn.Spawned || exit.pawn.Map != map || exit.pawn.Position.Fogged(map))
                    continue;
                exit.visuals.DrawExit(exit.pawn, ticks / 60f, ticks / (float)DissolveTicks);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref exits, "foldVisualExits", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) exits ??= new List<FoldExit>();
        }

        public override void MapRemoved()
        {
            foreach (FoldShiftVisuals visuals in active.Values) visuals.Dispose();
            active.Clear();
            foreach (FoldExit exit in exits) exit.visuals?.Dispose();
            exits.Clear();
        }
    }
}
