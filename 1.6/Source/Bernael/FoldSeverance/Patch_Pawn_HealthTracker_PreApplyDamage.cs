using System.Collections.Generic;
using Verse;
using HarmonyLib;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PreApplyDamage))]
    public static class Patch_Pawn_HealthTracker_PreApplyDamage
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (___pawn?.health?.hediffSet?.hediffs == null) return true;

            List<Hediff> hediffs = ___pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is HediffWithComps hediffWithComps && hediffWithComps.comps != null)
                {
                    for (int j = 0; j < hediffWithComps.comps.Count; j++)
                    {
                        if (hediffWithComps.comps[j] is HediffComp_ImmuneWithoutWeakDamage immuneWithoutWeakDamage)
                        {
                            if (immuneWithoutWeakDamage.CheckPreApplyDamage(ref dinfo))
                            {
                                absorbed = true;
                                return !absorbed;
                            }
                        }
                    }
                }
            }

            return true;
        }
    }
}