using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedMeleeDamageAmount),
        new Type[] { typeof(Tool), typeof(Pawn), typeof(Thing), typeof(HediffComp_VerbGiver) })]
    public static class LifeVanquisherDamagePatch
    {
        private const float MaximumSensitivity = 3f;

        public static void Postfix(Pawn attacker, Thing equipment, ref float __result)
        {
            if (attacker == null || equipment?.def != BernaelDefOf.BX_LifeVanquisher)
            {
                return;
            }

            float sensitivity = Mathf.Clamp(attacker.GetStatValue(StatDefOf.PsychicSensitivity), 0f, MaximumSensitivity);
            __result *= 0.5f + 0.5f * sensitivity;
        }
    }
}
