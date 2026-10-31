using System.Collections.Generic;
using RimWorld;
using Verse;
namespace Bernael_Xenotype
{
    public class CompProperties_AbilitySoulDrain : CompProperties_AbilityEffect
    {
        public CompProperties_AbilitySoulDrain()
        {
            compClass = typeof(CompAbilityEffect_SoulDrain);
        }

        public override IEnumerable<string> ExtraStatSummary()
        {
            yield return "AbilitySoulGain".Translate() + ": " + (soulGain * 100f).ToString("F0");
            yield break;
        }

        public float soulGain;
        public ThoughtDef thoughtDefToGiveTarget;
        public ThoughtDef opinionThoughtDefToGiveTarget;
        public float resistanceGain;
        public HediffDef hediffToGiveTarget;
        public float hediffSeverity = 0.4499f;

    }
}