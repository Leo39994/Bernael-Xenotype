using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch]
    public static class Patch_VEFAbility_GetRangeForPawn
    {
        private const string VpePackageId = "VanillaExpanded.VPsycastsE";
        private const string VefAbilityTypeName = "VEF.Abilities.Ability";
        private const string VpePsycastExtensionTypeName = "VanillaPsycastsExpanded.AbilityExtension_Psycast";

        private static readonly Dictionary<Def, bool> PsycastDefs = new Dictionary<Def, bool>();
        private static Type abilityType;
        private static Type psycastExtensionType;
        private static AccessTools.FieldRef<object, Pawn> pawnAccessor;
        private static AccessTools.FieldRef<object, Def> defAccessor;

        private static bool Prepare()
        {
            if (!ModsConfig.IsActive(VpePackageId))
            {
                return false;
            }

            abilityType = AccessTools.TypeByName(VefAbilityTypeName);
            psycastExtensionType = AccessTools.TypeByName(VpePsycastExtensionTypeName);
            if (abilityType == null || psycastExtensionType == null)
            {
                return false;
            }

            FieldInfo pawnField = AccessTools.Field(abilityType, "pawn");
            FieldInfo defField = AccessTools.Field(abilityType, "def");
            if (pawnField == null || defField == null)
            {
                return false;
            }

            pawnAccessor = AccessTools.FieldRefAccess<Pawn>(abilityType, "pawn");
            defAccessor = AccessTools.FieldRefAccess<Def>(abilityType, "def");
            return true;
        }

        private static MethodBase TargetMethod()
        {
            Type targetType = abilityType ?? AccessTools.TypeByName(VefAbilityTypeName);
            return targetType == null ? null : AccessTools.Method(targetType, "GetRangeForPawn", Type.EmptyTypes);
        }

        public static void Postfix(object __instance, ref float __result)
        {
            Pawn pawn = pawnAccessor(__instance);
            if (!AbyssalReachUtility.HasActiveGene(pawn))
            {
                return;
            }

            Def abilityDef = defAccessor(__instance);
            if (IsVpePsycast(abilityDef))
            {
                __result *= AbyssalReachUtility.RangeFactor;
            }
        }

        private static bool IsVpePsycast(Def abilityDef)
        {
            if (abilityDef == null)
            {
                return false;
            }

            bool isPsycast;
            if (PsycastDefs.TryGetValue(abilityDef, out isPsycast))
            {
                return isPsycast;
            }

            isPsycast = false;
            List<DefModExtension> extensions = abilityDef.modExtensions;
            if (extensions != null)
            {
                for (int i = 0; i < extensions.Count; i++)
                {
                    if (extensions[i] != null && psycastExtensionType.IsInstanceOfType(extensions[i]))
                    {
                        isPsycast = true;
                        break;
                    }
                }
            }

            PsycastDefs.Add(abilityDef, isPsycast);
            return isPsycast;
        }
    }
}
