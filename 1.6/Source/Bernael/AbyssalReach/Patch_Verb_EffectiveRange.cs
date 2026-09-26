using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(Verb), nameof(Verb.EffectiveRange), MethodType.Getter)]
    public static class Patch_Verb_EffectiveRange
    {
        public static void Postfix(Verb __instance, ref float __result)
        {
            Verb_CastAbility abilityVerb = __instance as Verb_CastAbility;
            if (abilityVerb?.Ability?.def?.IsPsycast != true)
            {
                return;
            }

            if (AbyssalReachUtility.HasActiveGene(__instance.CasterPawn))
            {
                __result *= AbyssalReachUtility.RangeFactor;
            }
        }
    }
}
