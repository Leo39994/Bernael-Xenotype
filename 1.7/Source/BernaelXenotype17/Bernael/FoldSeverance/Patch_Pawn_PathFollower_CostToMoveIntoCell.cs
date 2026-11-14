using Verse;
using Verse.AI;
using HarmonyLib;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(Pawn_PathFollower), "CostToMoveIntoCell", new[] { typeof(Pawn), typeof(IntVec3) })]
    public static class Patch_Pawn_PathFollower_CostToMoveIntoCell
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, IntVec3 c, ref float __result)
        {
            if (pawn == null) return;
            if (WorldComponent_NoPathCost.Contains(pawn))
            {
                float baseTicks = (c.x != pawn.Position.x && c.z != pawn.Position.z) ? pawn.TicksPerMoveDiagonal : pawn.TicksPerMoveCardinal;
                __result = baseTicks;
            }
        }
    }
}