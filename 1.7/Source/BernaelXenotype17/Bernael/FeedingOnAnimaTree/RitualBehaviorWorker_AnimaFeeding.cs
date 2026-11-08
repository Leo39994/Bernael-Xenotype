using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class RitualBehaviorWorker_AnimaFeeding : RitualBehaviorWorker
    {
    	public RitualBehaviorWorker_AnimaFeeding()
    	{
    	}

    	public RitualBehaviorWorker_AnimaFeeding(RitualBehaviorDef def)
    		: base(def)
    	{
    	}

    	public override string GetExplanation(Precept_Ritual ritual, RitualRoleAssignments assignments, float quality)
    	{
    		int count = assignments.SpectatorsForReading.Count;
    		float num = RitualOutcomeEffectWorker_FeedingOnAnimaTree.PsylinkLevelsFromQuality.Evaluate(quality);
    		TaggedString taggedString = "BX_AnimaFeedingExplanationBase".Translate(count, num);
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

    	public override string ExpectedDuration(Precept_Ritual ritual, RitualRoleAssignments assignments, float quality)
    	{
    		int count = assignments.SpectatorsForReading.Count;
    		return Mathf.RoundToInt(ritual.behavior.def.durationTicks.max / RitualStage_AnimaTreeLinking.ProgressPerParticipantCurve.Evaluate(count + 1)).ToStringTicksToPeriod(allowSeconds: false);
    	}
    }
}