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
        static Gamecomponent_PsychicSight gameComp => Current.Game.GetGamePsychicSightComp();
        static FieldInfo fieldInfo => AccessTools.Field(typeof(ShotReport), "factorFromCoveringGas");

        public static void Postfix(Thing caster, ref ShotReport __result)
        {
            if (gameComp == null || caster as Pawn == null) return;

            if (gameComp.psychicSeers.NullOrEmpty())
                gameComp.psychicSeers = new HashSet<Pawn>();

            if (!gameComp.psychicSeers.Contains((Pawn)caster)) return;

            fieldInfo.SetValueDirect(__makeref(__result), 1f);
        }
    }
}
