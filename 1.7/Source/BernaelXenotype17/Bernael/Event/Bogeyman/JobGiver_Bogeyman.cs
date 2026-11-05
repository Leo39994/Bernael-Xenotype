using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class JobGiver_Bogeyman : ThinkNode_JobGiver
    {
        public const float VictimSearchRadius = 999f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (WorldComponent_TraverseTheFold.Contains(pawn)) return null;
            if (pawn.carryTracker == null || pawn.carryTracker.CarriedThing is not Pawn)
            {
                if (BogeymanKidnapAIUtility.TryFindBabyOrChildVictim(pawn, VictimSearchRadius, out var victim))
                {
                    Job kidnapJob = JobMaker.MakeJob(BernaelDefOf.BX_BogeymanKidnap);
                    kidnapJob.targetA = victim;
                    kidnapJob.count = 1;
                    kidnapJob.playerForced = true;
                    return kidnapJob;
                }
                return null;
            }
            if (!RCellFinder.TryFindBestExitSpot(pawn, out var spot, TraverseMode.PassDoors, canBash: true))
            {
                return null;
            }
            Job escapeJob = JobMaker.MakeJob(BernaelDefOf.BX_BogeymanEscape, spot);
		    escapeJob.exitMapOnArrival = true;
            escapeJob.count = 1;
            escapeJob.playerForced = true;
            return escapeJob;
        }
    }
}