using Verse;
using Verse.AI;
using HarmonyLib;

namespace Bernael_Xenotype
{
    /// <summary>
    /// Harmony patch for Pawn_PathFollower.TryEnterNextPathCell to unfog fogged cells as the pawn phases through them.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
    public static class Patch_Pawn_PathFollower_TryEnterNextPathCell
    {
        /// <summary>
        /// Postfix that checks if the next cell is fogged while the pawn is executing BX_TraverseTheFold_Job, and flood-unfogs adjacent cells if necessary.
        /// </summary>
        public static void Postfix(Pawn_PathFollower __instance, Pawn ___pawn)
        {
            if (___pawn != null && ___pawn.Spawned && WorldComponent_TraverseTheFold.Contains(___pawn))
            {
                IntVec3 nextCell = __instance.nextCell;
                Map map = ___pawn.Map;
                if (nextCell.InBounds(map) && nextCell.Fogged(map))
                {
                    map.fogGrid.FloodUnfogAdjacent(nextCell);
                }
            }
        }
    }
}