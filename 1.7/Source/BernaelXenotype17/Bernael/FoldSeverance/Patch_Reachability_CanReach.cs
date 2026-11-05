using Verse;
using Verse.AI;
using HarmonyLib;

namespace Bernael_Xenotype
{
    /// <summary>
    /// Harmony patch for Reachability.CanReach to bypass standard map reachability restrictions during the traverse job.
    /// </summary>
    [HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReach), [typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms)])]
    public static class Patch_Reachability_CanReach
    {
        public static bool Prefix(TraverseParms traverseParams, ref bool __result)
        {
            Pawn pawn = traverseParams.pawn ?? Patch_Pawn_PathFollower_StartPath.currentPatherPawn;
            if (WorldComponent_TraverseTheFold.Contains(pawn))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}