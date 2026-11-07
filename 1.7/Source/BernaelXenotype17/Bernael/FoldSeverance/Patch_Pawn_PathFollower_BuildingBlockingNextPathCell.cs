using Verse;
using Verse.AI;
using HarmonyLib;

namespace Bernael_Xenotype
{
    /// <summary>
    /// Harmony patch for Pawn_PathFollower.BuildingBlockingNextPathCell to prevent buildings from blocking movement during the traverse job.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_PathFollower), "BuildingBlockingNextPathCell")]
    public static class Patch_Pawn_PathFollower_BuildingBlockingNextPathCell
    {
        public static bool Prefix(ref Building __result, Pawn ___pawn)
        {
            if (WorldComponent_TraverseTheFold.Contains(___pawn))
            {
                __result = null; // Return null to indicate no building is blocking the next cell
                return false;    // Skip default collision check
            }
            return true;
        }
    }
}