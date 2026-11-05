using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class LordToil_Bogeyman : LordToil_DoOpportunisticTaskOrCover
    {
        public LordToil_Bogeyman()
        {
            useAvoidGrid = true;
            cover = false;
        }

        protected override DutyDef DutyDef => BernaelDefOf.BX_Bogeyman_Duty;

        public override bool ForceHighStoryDanger => true;

        public override bool AllowSelfTend => false;

        protected override bool TryFindGoodOpportunisticTaskTarget(Pawn pawn, out Thing target, List<Thing> alreadyTakenTargets)
        {
            if (pawn.mindState.duty != null && pawn.carryTracker.CarriedThing is Pawn carriedPawn)
            {
                target = carriedPawn;
                return true;
            }
            bool result = BogeymanKidnapAIUtility.TryFindBabyOrChildVictim(pawn, JobGiver_Bogeyman.VictimSearchRadius, out Pawn victim, alreadyTakenTargets);
            target = victim;
            return result;
        }
    }
}