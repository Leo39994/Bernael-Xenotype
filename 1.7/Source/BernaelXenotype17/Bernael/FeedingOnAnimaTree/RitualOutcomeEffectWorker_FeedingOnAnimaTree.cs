using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class RitualOutcomeEffectWorker_FeedingOnAnimaTree : RitualOutcomeEffectWorker_FromQuality
    {
        public static readonly SimpleCurve PsylinkLevelsFromQuality = new SimpleCurve
        {
            new CurvePoint(0.2f, 1f),
            new CurvePoint(0.5f, 1f),
            new CurvePoint(0.8f, 2f),
            new CurvePoint(1f, 3f)
        };

        public override bool SupportsAttachableOutcomeEffect => false;

        public RitualOutcomeEffectWorker_FeedingOnAnimaTree()
        {
        }

        public RitualOutcomeEffectWorker_FeedingOnAnimaTree(RitualOutcomeEffectDef def)
            : base(def)
        {
        }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            float quality = GetQuality(jobRitual, progress);
            Pawn pawn = jobRitual.PawnWithRole("organizer");
            Thing animaTree = jobRitual.selectedTarget.Thing;
            int rawLevels = (int)PsylinkLevelsFromQuality.Evaluate(quality);
            int levelsToAdd = Mathf.Max(1, rawLevels);
            if (pawn != null)
            {
                pawn.ChangePsylinkLevel(levelsToAdd, sendLetter: false);
            }
            if (animaTree != null && !animaTree.Destroyed)
            {
                FleckMaker.Static(animaTree.Position, pawn.Map, FleckDefOf.PsycastAreaEffect, 10f);
                SoundDefOf.PsycastPsychicPulse.PlayOneShot(new TargetInfo(animaTree));
                animaTree.Destroy(DestroyMode.KillFinalize);
                Find.History.Notify_PsylinkAvailable();
            }
            string text = "BX_LetterTextFeedingRitualCompleted".Translate(pawn.Named("PAWN"), jobRitual.selectedTarget.Thing.Named("FEEDABLE"));
            text = text + "\n\n" + OutcomeQualityBreakdownDesc(quality, progress, jobRitual);
            Find.LetterStack.ReceiveLetter("BX_LetterLabelFeedingRitualCompleted".Translate(), text, LetterDefOf.RitualOutcomePositive, new LookTargets(pawn, animaTree)
            );
        }
    }
}