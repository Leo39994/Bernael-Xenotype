using Verse;
using HarmonyLib;

namespace Bernael_Xenotype
{
    /// <summary>
    /// Harmony patch for GenGrid.WalkableBy to override tile walkability checks during the traverse job.
    /// </summary>
    [HarmonyPatch(typeof(GenGrid), nameof(GenGrid.WalkableBy), [typeof(IntVec3), typeof(Map), typeof(Pawn)])]
    public static class Patch_GenGrid_WalkableBy
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (WorldComponent_TraverseTheFold.Contains(pawn))
            {
                __result = true; // Force cell walkability to true
                return false;    // Bypass standard walkability checks
            }
            return true;
        }
    }
}