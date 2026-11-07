using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(FloatMenuOptionProvider_ExtinguishFires), "GetSingleOption")]
    public static class Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption
    {
        public static bool Prefix(FloatMenuContext context, ref FloatMenuOption __result)
        {
            Map map = context.FirstSelectedPawn?.Map;
            if (map == null) return true;

            bool hasNormalFire = false;
            List<Thing> things = context.ClickedCell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Fire fire && fire.GetType() == typeof(Fire))
                {
                    hasNormalFire = true;
                    break;
                }
            }
            if (!hasNormalFire)
            {
                __result = null;
                return false; 
            }
            return true;
        }
    }
}