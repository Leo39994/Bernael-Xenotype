using System.Collections.Generic;
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
            if (pawn?.health?.hediffSet?.hediffs == null) return;

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is HediffWithComps hediffWithComps && hediffWithComps.comps != null)
                {
                    for (int j = 0; j < hediffWithComps.comps.Count; j++)
                    {
                        if (hediffWithComps.comps[j] is HediffComp_NoPathCost)
                        {
                            float baseTicks = (c.x != pawn.Position.x && c.z != pawn.Position.z) ? pawn.TicksPerMoveDiagonal : pawn.TicksPerMoveCardinal;
                            __result = baseTicks;
                            return;
                        }
                    }
                }
            }
        }
    }
}