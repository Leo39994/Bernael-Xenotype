using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(Bill_Medical), "PawnAllowedToStartAnew")]
    public static class Patch_Bill_Medical_PawnAllowedToStartAnew
    {
        static void Postfix(Bill_Medical __instance, Pawn pawn, ref bool __result)
        {
            if (!__result) return;
            if (__instance.recipe == BernaelDefOf.BX_ExtractSoul)
            {
                __result = pawn.genes?.HasActiveGene(BernaelDefOf.BX_SoulStarved) ?? false;
            }
        }
    }
}
