using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class CompProperties_AbilitySoulCostPerSecond : CompProperties_AbilityEffect
    {
        public float soulCostPerSecond = 0.01f;

        public CompProperties_AbilitySoulCostPerSecond()
        {
            compClass = typeof(CompAbilityEffect_SoulCostPerSecond);
        }

        public override IEnumerable<string> ExtraStatSummary()
        {
            yield return "MB_AbilitySoulCostPerSecond".Translate() + ": " + Mathf.RoundToInt(soulCostPerSecond * 100f);
        }
    }

    public class CompAbilityEffect_SoulCostPerSecond : CompAbilityEffect
    {
        public new CompProperties_AbilitySoulCostPerSecond Props => (CompProperties_AbilitySoulCostPerSecond)props;

        private float CostPerInterval => Props.soulCostPerSecond;

        private bool IsActive
        {
            get
            {
                if (parent is Ability_Toggle toggleAbility)
                {
                    return toggleAbility.IsActive;
                }
                return false;
            }
        }

        private bool HasEnoughSoul
        {
            get
            {
                Pawn_GeneTracker genes = parent.pawn.genes;
                Gene_Soul gene_Soul = genes?.GetFirstGeneOfType<Gene_Soul>();
                return gene_Soul != null && gene_Soul.Value >= CostPerInterval;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (IsActive && parent.pawn.IsHashIntervalTick(60))
            {
                Pawn pawn = parent.pawn;
                Gene_Soul gene_Soul = pawn.genes?.GetFirstGeneOfType<Gene_Soul>();
                if (gene_Soul == null || gene_Soul.Value < CostPerInterval)
                {
                    if (parent is Ability_Toggle toggleAbility)
                    {
                        toggleAbility.TurnOff();
                    }
                    Messages.Message("MB_AbilityDisabledNoSoul".Translate(pawn), pawn, MessageTypeDefOf.NegativeEvent);
                    return;
                }
                SoulUtility.OffsetSoul(pawn, -CostPerInterval);
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn_GeneTracker genes = parent.pawn.genes;
            Gene_Soul gene_Soul = genes?.GetFirstGeneOfType<Gene_Soul>();
            if (gene_Soul == null)
            {
                reason = "MB_AbilityDisabledNoSoulGene".Translate(parent.pawn);
                return true;
            }
            if (!IsActive && gene_Soul.Value < CostPerInterval)
            {
                reason = "MB_AbilityDisabledNoSoul".Translate(parent.pawn);
                return true;
            }

            reason = null;
            return false;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return HasEnoughSoul;
        }
    }
}