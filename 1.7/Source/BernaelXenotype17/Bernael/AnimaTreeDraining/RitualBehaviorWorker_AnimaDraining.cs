using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class RitualBehaviorWorker_AnimaDraining : RitualBehaviorWorker
    {
        private Sustainer soundPlaying;

        public override Sustainer SoundPlaying => soundPlaying;

        public RitualBehaviorWorker_AnimaDraining()
        {
        }

        public RitualBehaviorWorker_AnimaDraining(RitualBehaviorDef def)
            : base(def)
        {
        }

        public override string GetExplanation(Precept_Ritual ritual, RitualRoleAssignments assignments, float quality)
        {
            int count = assignments.SpectatorsForReading.Count;
            float num = RitualOutcomeEffectWorker_AnimaDraining.PsylinkLevelsFromQuality.Evaluate(quality);
            TaggedString taggedString = "BX_AnimaDrainingExplanationBase".Translate(count, num);
            if (assignments.ExtraRequiredPawnsForReading.Any())
            {
                TaggedString psylinkAffectedByTraitsNegativelyWarning = RoyalTitleUtility.GetPsylinkAffectedByTraitsNegativelyWarning(assignments.ExtraRequiredPawnsForReading.FirstOrDefault());
                if (psylinkAffectedByTraitsNegativelyWarning.RawText != null)
                {
                    taggedString += "\n\n" + psylinkAffectedByTraitsNegativelyWarning.Resolve();
                }
            }
            return taggedString;
        }

        public override void Tick(LordJob_Ritual ritual)
        {
            base.Tick(ritual);
            if (Find.TickManager.TicksGame % 20 == 0)
            {
                SoundDef soundDef = null;
                Thing thing = ritual.selectedTarget.Thing;
                if (thing != null && !thing.Destroyed)
                {
                    soundDef = BernaelDefOf.BX_Ambient_Gaze;
                }
                if (soundDef != null && (soundPlaying == null || soundPlaying.def != soundDef))
                {
                    soundPlaying = soundDef.TrySpawnSustainer(SoundInfo.InMap(new TargetInfo(ritual.selectedTarget.Cell, ritual.selectedTarget.Map), MaintenanceType.PerTick));
                }
            }
            if (soundPlaying != null && !soundPlaying.Ended)
            {
                soundPlaying.Maintain();
            }
        }

        public override void Cleanup(LordJob_Ritual ritual)
        {
            base.Cleanup(ritual);
            soundPlaying = null;
        }

        public override string ExpectedDuration(Precept_Ritual ritual, RitualRoleAssignments assignments, float quality)
        {
            int count = assignments.SpectatorsForReading.Count;
            return Mathf.RoundToInt(ritual.behavior.def.durationTicks.max / RitualStage_AnimaTreeLinking.ProgressPerParticipantCurve.Evaluate(count + 1)).ToStringTicksToPeriod(allowSeconds: false);
        }
    }
}