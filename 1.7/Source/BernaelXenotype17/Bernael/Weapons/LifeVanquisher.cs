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
            __result *= sensitivity;
        }
    }

    // Recovered from the LV2 assembly (3e96d89), which shipped without its source. Reports no
    // noxious haze for an unspawned Life Vanquisher held on a map that has no MapInfo.
    [HarmonyPatch(typeof(StatPart_NoxiousHaze), "ActiveFor")]
    public static class LifeVanquisherNoxiousHazeInfoCardPatch
    {
        public static bool Prefix(Thing t, ref bool __result)
        {
            if (t?.def != BernaelDefOf.BX_LifeVanquisher || t.Spawned)
            {
                return true;
            }
            for (IThingHolder holder = t.ParentHolder; holder != null; holder = holder.ParentHolder)
            {
                if (holder is Map map && map.info == null)
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }
    }
}
