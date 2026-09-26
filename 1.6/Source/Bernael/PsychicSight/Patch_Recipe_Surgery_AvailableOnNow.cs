using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(Recipe_Surgery), nameof(Recipe_Surgery.AvailableOnNow))]
    public static class Patch_Recipe_Surgery_AvailableOnNow
    {
        public static void Postfix(Thing thing, BodyPartRecord part, ref bool __result)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null || pawn.genes?.GetGene(BernaelDefOf.BX_DepravedHead) == null || part?.def != BodyPartDefOf.Eye) return;
            __result = false;
        }
    }
}
