using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class JobDriver_BogeymanKidnap : JobDriver
    {
        protected Pawn Takee => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Takee, job, 1, -1, null, errorOnFailed);
        }

        public override string GetReport()
        {
            if (Takee == null || pawn.HostileTo(Takee))
            {
                return base.GetReport();
            }
            return JobUtility.GetResolvedJobReport(JobDefOf.Rescue.reportString, Takee);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => {
                if (Takee == null || Takee.Destroyed) return true;
                if (!Takee.DevelopmentalStage.Baby() && !Takee.DevelopmentalStage.Child())
                {
                    return !Takee.Downed && Takee.Awake();
                }
                return false;
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Construct.UninstallIfMinifiable(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
        }
    }
}