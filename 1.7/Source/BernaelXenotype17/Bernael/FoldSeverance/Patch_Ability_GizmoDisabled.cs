using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(Ability))]
    public static class Patch_Ability_GizmoDisabled
    {
        [HarmonyPatch(nameof(Ability.GizmoDisabled))]
        [HarmonyPostfix]
        public static void Postfix(Ability __instance, ref bool __result, ref string reason)
        {
            if (__result) return;

            Pawn pawn = __instance.pawn;
            if (WorldComponent_FoldShiftVisual.Contains(pawn))
            {
                string defName = __instance.def.defName;
                if (defName != "BX_FoldShift" && 
                    defName != "BX_TraverseTheFold" &&
                    defName != "BX_WingBurst")
                {
                    __result = true;
                    reason = "BX_FoldShift_RestrictedAbility".Translate();
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_VEF_AbilityIsEnabled
        {
            public static bool Prepare()
            {
                return ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core");
            }

            public static System.Reflection.MethodBase TargetMethod()
            {
                return AccessTools.Method("VEF.Abilities.Ability:IsEnabledForPawn");
            }

            public static bool Prefix(object __instance, ref string reason, ref bool __result)
            {
                var pawnField = __instance.GetType().GetField("pawn");
                var defField = __instance.GetType().GetField("def");

                Pawn pawn = pawnField?.GetValue(__instance) as Pawn;
                var abilityDef = defField?.GetValue(__instance);

                string defName = abilityDef?.GetType().GetField("defName")?.GetValue(abilityDef) as string;
                if (pawn != null && !string.IsNullOrEmpty(defName))
                {
                    if (WorldComponent_FoldShiftVisual.Contains(pawn))
                    {
                        if (defName != "BX_FoldShift" && 
                            defName != "BX_TraverseTheFold" &&
                            defName != "BX_WingBurst")
                        {
                            reason = "BX_FoldShift_RestrictedAbility".Translate();
                            __result = false;
                            return false;
                        }
                    }
                }
                return true;
            }
        }
    }
}