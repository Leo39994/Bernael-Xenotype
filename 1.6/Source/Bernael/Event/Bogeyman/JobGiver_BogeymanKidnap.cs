using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class JobGiver_BogeymanKidnap : ThinkNode_JobGiver
    {
        public const float VictimSearchRadius = 999f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!RCellFinder.TryFindBestExitSpot(pawn, out var spot, TraverseMode.PassDoors, canBash: true))
            {
                return null;
            }

            if (BogeymanKidnapAIUtility.TryFindBabyOrChildVictim(pawn, VictimSearchRadius, out var victim))
            {
                Job job = JobMaker.MakeJob(BernaelDefOf.BX_BogeymanKidnap);
                job.targetA = victim;
                job.targetB = spot;
                job.count = 1;
                job.playerForced = true;
                return job;
            }

            return null;
        }
    }
}