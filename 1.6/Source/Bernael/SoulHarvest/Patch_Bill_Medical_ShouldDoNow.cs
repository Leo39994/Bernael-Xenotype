using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(Bill_Medical), "ShouldDoNow")]
    public static class Patch_Bill_Medical_ShouldDoNow
    {
        static void Postfix(Bill_Medical __instance, ref bool __result)
        {
            if (!__result) return;
            if (__instance.recipe == BernaelDefOf.BX_ExtractSoul)
            {
                foreach (Pawn colonist in __instance.GiverPawn.Map.mapPawns.FreeColonistsSpawned)
                {
                    if (colonist.genes?.HasActiveGene(BernaelDefOf.BX_SoulStarved) == true && colonist.workSettings?.WorkIsActive(WorkTypeDefOf.Doctor) == true && !colonist.Downed)
                    {
                        __result = true;
                    }
                }
            }
        }
    }
}
