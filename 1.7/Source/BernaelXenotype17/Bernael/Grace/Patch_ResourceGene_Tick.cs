using System;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch]
    public static class Patch_ResourceGene_Tick
    {
        public static MethodBase TargetMethod()
        {
            if (!ModsConfig.IsActive("EBSG.Framework")) return null;
            var type = AccessTools.TypeByName("EBSGFramework.ResourceGene");
            return type != null ? AccessTools.Method(type, "Tick") : null;
        }

        [UsedImplicitly]
        private static bool Prepare()
        {
            return ModsConfig.IsActive("Sov.Nephilim") && ModsConfig.IsActive("EBSG.Framework");
        }

        static readonly Type type = AccessTools.TypeByName("EBSGFramework.ResourceGene");
        static readonly FieldInfo geneDef = type != null ? AccessTools.Field(type, "def") : null;
        static readonly FieldInfo creature = type != null ? AccessTools.Field(type, "pawn") : null;

        public static void Postfix(ref Gene_Resource __instance)
        {
            if (type == null || geneDef == null || creature == null || __instance.Value > 0) return;
            Pawn pawn = (Pawn)(creature.GetValue(__instance));
            if (!pawn.IsHashIntervalTick(180) || !pawn.InMentalState || (GeneDef)(geneDef.GetValue(__instance)) != BernaelDefOf.GS_Grace_New || !typeof(Gene_Resource).IsAssignableFrom(__instance.def.geneClass)) return;

            pawn.TurnIntoXenotype(BernaelDefOf.BX_Bernael);
        }
    }
}
