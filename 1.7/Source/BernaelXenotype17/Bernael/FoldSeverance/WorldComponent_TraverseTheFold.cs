using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace Bernael_Xenotype
{
    /// <summary>
    /// WorldComponent that maintains an O(1) lookup set of pawns currently casting or executing Traverse The Fold,
    /// and handles periodic cleanup every 12 in-game hours (30,000 ticks).
    /// </summary>
    public class WorldComponent_TraverseTheFold : WorldComponent
    {
        private const int CleanupIntervalTicks = 30000; // 12 in-game hours (12 * 2500 ticks)

        private static readonly HashSet<Pawn> activePawns = new();

        public WorldComponent_TraverseTheFold(World world) : base(world)
        {
        }

        public override void WorldComponentTick()
        {
            if (Find.TickManager.TicksGame % CleanupIntervalTicks == 0)
            {
                if (activePawns.Count == 0) return;
                activePawns.RemoveWhere(pawn => pawn == null || pawn.Destroyed || pawn.Dead);
            }
        }

        public override void FinalizeInit(bool fromLoad)
        {
            activePawns.Clear();
        }

        public static void Add(Pawn pawn)
        {
            if (pawn != null)
            {
                activePawns.Add(pawn);
            }
        }

        public static void Remove(Pawn pawn)
        {
            if (pawn != null)
            {
                activePawns.Remove(pawn);
            }
        }

        public static bool Contains(Pawn pawn)
        {
            return pawn != null && activePawns.Contains(pawn);
        }
    }
}