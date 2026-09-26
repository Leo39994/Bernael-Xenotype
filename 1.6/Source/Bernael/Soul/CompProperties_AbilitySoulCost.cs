using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
namespace Bernael_Xenotype
{
    public class CompProperties_AbilitySoulCost : CompProperties_AbilityEffect
    {
        public CompProperties_AbilitySoulCost()
        {
            compClass = typeof(CompAbilityEffect_SoulCost);
        }

        public override IEnumerable<string> ExtraStatSummary()
        {
            yield return "MB_AbilitySoulCost".Translate() + ": " + Mathf.RoundToInt(soulCost * 100f);
        }

        public float soulCost;
    }
}