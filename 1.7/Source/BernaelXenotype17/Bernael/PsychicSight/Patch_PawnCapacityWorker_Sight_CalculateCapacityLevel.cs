using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(PawnCapacityWorker_Sight), nameof(PawnCapacityWorker_Sight.CalculateCapacityLevel))]
    public static class Patch_PawnCapacityWorker_Sight_CalculateCapacityLevel
    {
        public static void Postfix(HediffSet diffSet, ref float __result)
        {
            if (__result == 1.25f)
            {
                return;
            }

            Gamecomponent_PsychicSight gameComp = Current.Game.GetGamePsychicSightComp();
            if (gameComp != null && !gameComp.psychicSeers.NullOrEmpty() && gameComp.psychicSeers.Contains(diffSet.pawn))
            {
                __result = 1.25f;
            }
        }
    }
}
