using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class JobDriver_PrisonerSoulFeed : JobDriver
    {
        public const float BloodLoss = 0.4499f;

        public const int WaitTicks = 120;

        private const float HemogenGain = 0.2f;

        private const float VictimResistance = 0.1f;

        protected Pawn Prisoner => (Pawn)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Prisoner.IsPrisonerOfColony || !Prisoner.guest.PrisonerIsSecure || Prisoner.InAggroMentalState || Prisoner.guest.IsInteractionDisabled(PrisonerInteractionModeDefOf.Bloodfeed));
            yield return Toils_Interpersonal.GotoPrisoner(pawn, Prisoner, PrisonerInteractionModeDefOf.Bloodfeed);
            yield return Toils_General.WaitWith(TargetIndex.A, WaitTicks, useProgressBar: true).PlaySustainerOrSound(SoundDefOf.Bloodfeed_Cast);
            yield return Toils_General.Do(delegate
            {
                SoulUtility.DoDrain(pawn, Prisoner, HemogenGain, VictimResistance, BernaelUtility.cachedSoulDrainedHediff, BloodLoss, BernaelDefOf.BX_FedOn, BernaelDefOf.BX_FedOn_Social);
            });
            yield return Toils_Interpersonal.SetLastInteractTime(TargetIndex.A);
        }
    }
}
