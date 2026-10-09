using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
    public static class Patch_ShotReport_HitReportFor
    {
        private static readonly FieldInfo FactorFromCoveringGasField = AccessTools.Field(typeof(ShotReport), "factorFromCoveringGas");

        public static void Postfix(Thing caster, ref ShotReport __result)
        {
            Pawn pawn = caster as Pawn;
            if (pawn == null)
            {
                return;
            }

            Gamecomponent_PsychicSight gameComp = Current.Game.GetGamePsychicSightComp();
            if (gameComp == null)
            {
                return;
            }

            gameComp.psychicSeers ??= new HashSet<Pawn>();
            if (!gameComp.psychicSeers.Contains(pawn))
            {
                return;
            }

            FactorFromCoveringGasField.SetValueDirect(__makeref(__result), 1f);
        }
    }
}
