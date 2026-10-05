using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    // A sealed pawn cannot cast any ability. CanCast also greys out the gizmo and stops the AI from picking one;
    // Psycast.CanCast calls this base getter first.
    [HarmonyPatch(typeof(Ability), nameof(Ability.CanCast), MethodType.Getter)]
    public static class Patch_Ability_CanCast
    {
        public static void Postfix(Ability __instance, ref AcceptanceReport __result)
        {
            if (__result.Accepted && MaliciousSealUtility.IsSealed(__instance.pawn))
                __result = "BX_SealSilenced".Translate();
        }
    }

    // Ability.Activate does not check CanCast again, so a warmup that slipped past the seal fizzles here.
    [HarmonyPatch(typeof(Verb_CastAbility), "TryCastShot")]
    public static class Patch_Verb_CastAbility_TryCastShot
    {
        public static bool Prefix(Verb_CastAbility __instance, ref bool __result)
        {
            if (!MaliciousSealUtility.IsSealed(__instance.CasterPawn)) return true;
            __result = false;
            return false;
        }
    }

    // Vanilla Expanded abilities (including VPE psycasts) have their own check, used by their gizmo,
    // their verb and their AI.
    [HarmonyPatch]
    public static class Patch_VEFAbility_IsEnabledForPawn
    {
        private const string VefPackageId = "OskarPotocki.VanillaFactionsExpanded.Core";
        private const string VefAbilityTypeName = "VEF.Abilities.Ability";

        private static Type abilityType;
        private static AccessTools.FieldRef<object, Pawn> pawnAccessor;

        private static bool Prepare()
        {
            if (!ModsConfig.IsActive(VefPackageId)) return false;
            abilityType = AccessTools.TypeByName(VefAbilityTypeName);
            if (abilityType == null || AccessTools.Field(abilityType, "pawn") == null || FindTarget() == null) return false;
            pawnAccessor = AccessTools.FieldRefAccess<Pawn>(abilityType, "pawn");
            return true;
        }

        private static MethodBase TargetMethod() => FindTarget();

        private static MethodInfo FindTarget()
        {
            Type targetType = abilityType ?? AccessTools.TypeByName(VefAbilityTypeName);
            return targetType == null ? null
                : AccessTools.Method(targetType, "IsEnabledForPawn", new[] { typeof(string).MakeByRefType() });
        }

        // __0 is the out reason, bound by position so a renamed parameter cannot break patching.
        public static void Postfix(object __instance, ref bool __result, ref string __0)
        {
            if (!__result || !MaliciousSealUtility.IsSealed(pawnAccessor(__instance))) return;
            __result = false;
            __0 = "BX_SealSilenced".Translate();
        }
    }
}
