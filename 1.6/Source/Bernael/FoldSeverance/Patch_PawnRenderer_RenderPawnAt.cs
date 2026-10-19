using System.Collections.Generic;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class Patch_PawnRenderer_RenderPawnAt
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn, Vector3 drawLoc)
        {
            if (___pawn?.health?.hediffSet?.hediffs == null) return true;

            List<Hediff> hediffs = ___pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is HediffWithComps hediffWithComps && hediffWithComps.comps != null)
                {
                    for (int j = 0; j < hediffWithComps.comps.Count; j++)
                    {
                        if (hediffWithComps.comps[j] is HediffComp_FoldShiftVisual visualComp)
                        {
                            visualComp.Draw(drawLoc, ___pawn);
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}