using HarmonyLib;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    // Wing Burst draws its own soft smoke over these cells. Hide the overlapping vanilla gas layer until
    // Gas_WingBurst expires; gas density, spread and blinding remain unchanged.
    [HarmonyPatch(typeof(SectionLayer_Gas), nameof(SectionLayer_Gas.ColorAt))]
    public static class Patch_SectionLayer_Gas_ColorAt
    {
        // The colour's channels are the cell's gas densities (GasGrid.DensitiesAt); red is the blind smoke.
        public static void Postfix(IntVec3 cell, Section ___section, ref Color __result)
        {
            if (__result.r <= 0f || ___section?.map == null) return;
            if (cell.GetFirstThing<Gas_WingBurst>(___section.map) != null) __result.r = 0f;
        }
    }
}
