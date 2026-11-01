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

        private Toil drainToil;

        public bool IsDraining(Pawn victim) => HaveCurToil && CurToil == drainToil && Prisoner == victim;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Prisoner.IsPrisonerOfColony || !Prisoner.guest.PrisonerIsSecure || Prisoner.InAggroMentalState || Prisoner.guest.IsInteractionDisabled(PrisonerInteractionModeDefOf.Bloodfeed));
            yield return Toils_Interpersonal.GotoPrisoner(pawn, Prisoner, PrisonerInteractionModeDefOf.Bloodfeed);
            drainToil = Toils_General.WaitWith(TargetIndex.A, WaitTicks, useProgressBar: true)
                .PlaySustainerOrSound(SoundDefOf.Bloodfeed_Cast);
            bool visualsStarted = false;
            void EnsureVisuals()
            {
                if (visualsStarted) return;
                visualsStarted = true;
                SoulDrainVisuals.BeginFeeding(pawn, Prisoner, ticksLeftThisToil);
            }
            drainToil.AddPreInitAction(EnsureVisuals);
            // Toil init actions do not replay on load. Reuse the saved link or start
            // one for the remaining wait when loading a save from before this VFX.
            drainToil.AddPreTickAction(EnsureVisuals);
            yield return drainToil;
            yield return Toils_General.Do(delegate
            {
                SoulUtility.DoDrain(pawn, Prisoner, HemogenGain, VictimResistance, BernaelUtility.cachedSoulDrainedHediff, BloodLoss, BernaelDefOf.BX_FedOn, BernaelDefOf.BX_FedOn_Social);
            });
            yield return Toils_Interpersonal.SetLastInteractTime(TargetIndex.A);
        }
    }
}
