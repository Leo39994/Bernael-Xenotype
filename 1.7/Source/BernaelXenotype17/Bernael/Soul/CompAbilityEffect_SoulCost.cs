using RimWorld;
using Verse;
using Verse.AI;
namespace Bernael_Xenotype
{
    public class CompAbilityEffect_SoulCost : CompAbilityEffect
    {
        public new CompProperties_AbilitySoulCost Props
        {
            get
            {
                return (CompProperties_AbilitySoulCost)props;
            }
        }
        private bool HasEnoughSoul
        {
            get
            {
                Pawn_GeneTracker genes = parent.pawn.genes;
                Gene_Soul gene_Soul = (genes != null) ? genes.GetFirstGeneOfType<Gene_Soul>() : null;
                return gene_Soul != null && gene_Soul.Value >= Props.soulCost;
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            SoulUtility.OffsetSoul(parent.pawn, -Props.soulCost);
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn_GeneTracker genes = parent.pawn.genes;
            Gene_Soul gene_Soul = (genes != null) ? genes.GetFirstGeneOfType<Gene_Soul>() : null;
            if (gene_Soul == null)
            {
                reason = "MB_AbilityDisabledNoSoulGene".Translate(parent.pawn);
                return true;
            }
            if (gene_Soul.Value < Props.soulCost)
            {
                reason = "MB_AbilityDisabledNoSoul".Translate(parent.pawn);
                return true;
            }
            float num = TotalSoulCostOfQueuedAbilities();
            float num2 = Props.soulCost + num;
            if (Props.soulCost > 1E-45f && num2 > gene_Soul.Value)
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

        private float TotalSoulCostOfQueuedAbilities()
        {
            Pawn_JobTracker jobs = parent.pawn.jobs;
            object obj;
            if (jobs == null)
            {
                obj = null;
            }
            else
            {
                Job curJob = jobs.curJob;
                obj = ((curJob != null) ? curJob.verbToUse : null);
            }
            Verb_CastAbilitySoul verb_CastAbility = obj as Verb_CastAbilitySoul;
            float num;
            if (verb_CastAbility == null)
            {
                num = 0f;
            }
            else
            {
                AbilitySoul ability = verb_CastAbility.ability;
                num = ((ability != null) ? ability.SoulCost() : 0f);
            }
            float num2 = num;
            if (parent.pawn.jobs != null)
            {
                for (int i = 0; i < parent.pawn.jobs.jobQueue.Count; i++)
                {
                    Verb_CastAbilitySoul verb_CastAbility2;
                    if ((verb_CastAbility2 = (parent.pawn.jobs.jobQueue[i].job.verbToUse as Verb_CastAbilitySoul)) != null)
                    {
                        float num3 = num2;
                        AbilitySoul ability2 = verb_CastAbility2.ability;
                        num2 = num3 + ((ability2 != null) ? ability2.SoulCost() : 0f);
                    }
                }
            }
            return num2;
        }
    }
}
