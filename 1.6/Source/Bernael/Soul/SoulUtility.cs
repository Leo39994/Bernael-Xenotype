using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
namespace Bernael_Xenotype
{
    public static class SoulUtility
    {
        public static void TickResourceDrainInterval(IGeneResourceDrain drain, int delta)
        {
            if (drain.CanOffset && drain.Resource != null)
            {
                OffsetResource(drain, (0f - drain.ResourceLossPerDay) * (float)delta / 60000f);
            }
        }
        public static void OffsetResource(IGeneResourceDrain drain, float amnt)
        {
            if (drain.Resource != null)
            {
                float value = drain.Resource.Value;
                drain.Resource.Value += amnt;
                PostResourceOffset(drain, value);
            }
        }
        public static void PostResourceOffset(IGeneResourceDrain drain, float oldValue)
        {
            if (oldValue > 0f && drain.Resource.Value <= 0f)
            {
                Pawn pawn = drain.Pawn;
                if (!pawn.health.hediffSet.HasHediff(BernaelDefOf.BX_SoulCraving))
                {
                    pawn.health.AddHediff(BernaelDefOf.BX_SoulCraving);
                }
            }
        }

        public static void OffsetSoul(Pawn pawn, float offset, bool applyStatFactor = true)
        {
            if (!ModsConfig.BiotechActive)
            {
                return;
            }
            if (offset > 0f && applyStatFactor)
            {
                offset *= pawn.GetStatValue(BernaelDefOf.BX_SoulGainFactor);
            }
            Pawn_GeneTracker genes = pawn.genes;
            Gene_SoulDrain gene_SoulDrain = genes?.GetFirstGeneOfType<Gene_SoulDrain>();
            if (gene_SoulDrain != null)
            {
                OffsetResource(gene_SoulDrain, offset);
                return;
            }
            Pawn_GeneTracker genes2 = pawn.genes;
            Gene_Soul gene_Soul = genes2?.GetFirstGeneOfType<Gene_Soul>();
            if (gene_Soul != null)
            {
                gene_Soul.Value += offset;
            }
        }

        // Vanilla keeps sanguophages off each other through
        // TargetingParameters.canTargetBloodfeeders, but that flag only asks
        // GeneUtility.IsBloodfeeder, which looks for GeneDefOf.Bloodfeeder alone.
        // BX_SoulFeeder is its own GeneDef, so the flag never fires for our kind
        // and the check has to live here. Carriers count even while the gene is
        // still inactive (minAgeActive 3), otherwise a Bernael baby would be a
        // valid target and TryConvertBaby would adopt it into its own xenotype.
        public static bool IsSoulFeeder(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive)
            {
                return false;
            }
            Pawn_GeneTracker genes = pawn?.genes;
            if (genes == null)
            {
                return false;
            }
            return genes.GetGene(BernaelDefOf.BX_SoulStarved) != null || genes.GetGene(BernaelDefOf.BX_SoulFeeder) != null;
        }

        public static void DoDrain(Pawn biter, Pawn victim, float targetSoulGain, float victimResistanceGain, HediffDef hediffToGiveTarget, float victimSoulDrainSeverity = 0.4999f, ThoughtDef thoughtDefToGiveTarget = null, ThoughtDef opinionThoughtToGiveTarget = null)
        {
            if (!ModLister.CheckBiotech("Sanguophage bite"))
            {
                return;
            }
            DevelopmentalStage? developmentStage = victim.ageTracker?.CurLifeStage?.developmentalStage;
            if (developmentStage is DevelopmentalStage.Baby or DevelopmentalStage.Newborn)
            {

                return;
            }
            hediffToGiveTarget ??= HediffDefOf.BloodLoss;
            float num2 = targetSoulGain * victim.BodySize;
            OffsetSoul(biter, num2);
            OffsetSoul(victim, -num2);
            Pawn_NeedsTracker needs = biter.needs;
            if (thoughtDefToGiveTarget != null)
            {
                Pawn_NeedsTracker needs2 = victim.needs;
                Need_Mood mood = needs2?.mood;
                ThoughtHandler thoughts = mood?.thoughts;
                MemoryThoughtHandler memories = thoughts?.memories;
                memories?.TryGainMemory((Thought_Memory)ThoughtMaker.MakeThought(thoughtDefToGiveTarget), biter);
            }
            if (opinionThoughtToGiveTarget != null)
            {
                Pawn_NeedsTracker needs3 = victim.needs;
                Need_Mood mood2 = needs3?.mood;
                ThoughtHandler thoughts2 = mood2?.thoughts;
                MemoryThoughtHandler memories2 = thoughts2?.memories;
                memories2?.TryGainMemory((Thought_Memory)ThoughtMaker.MakeThought(opinionThoughtToGiveTarget), biter);
            }
            if (victimSoulDrainSeverity > 0f)
            {
                Hediff hediff = HediffMaker.MakeHediff(hediffToGiveTarget, victim, null);
                hediff.Severity = victimSoulDrainSeverity;
                victim.health.AddHediff(hediff, null, null, null);
            }
            if (victim.IsPrisoner && victimResistanceGain > 0f)
            {
                victim.guest.resistance = Mathf.Min(victim.guest.resistance + victimResistanceGain, victim.kindDef.initialResistanceRange.Value.TrueMax);
            }
        }
        public static bool TryConvertBaby(Pawn drainer, Pawn victim)
        {
            DevelopmentalStage? developmentStage = victim.ageTracker?.CurLifeStage?.developmentalStage;
            if (developmentStage is not (DevelopmentalStage.Baby or DevelopmentalStage.Newborn)) return false;

            XenotypeDef targetXenotype = drainer.genes.Xenotype;
            victim.genes.xenotypeName = drainer.genes.xenotypeName;
            victim.genes.iconDef = drainer.genes.iconDef;
            victim.genes.SetXenotypeDirect(targetXenotype);
            victim.genes.ClearXenogenes();
            foreach (GeneDef gene in targetXenotype.genes)
            {
                victim.genes.AddGene(gene, xenogene: true);
            }
            List<DirectPawnRelation> relationsToRemove = [];
            List<DirectPawnRelation> relationList = victim.relations.DirectRelations;
            foreach (DirectPawnRelation relations in relationList)
            {
                if (relations.def.familyByBloodRelation)
                {
                    MemoryThoughtHandler memories = relations.otherPawn.needs.mood.thoughts.memories;
                    ThoughtDef thoughtDef = relations.def.GetGenderSpecificDiedThought(victim);
                    if (thoughtDef != null)
                    {
                        memories.RemoveMemoriesOfDefWhereOtherPawnIs(thoughtDef, victim);
                    }
                    relationsToRemove.Add(relations);
                }
            }
            foreach (DirectPawnRelation item in relationsToRemove)
            {
                victim.relations.RemoveDirectRelation(item);
            }

            BabyBondUtility.EstablishBond(drainer, victim);
            victim.relations.AddDirectRelation(PawnRelationDefOf.Parent, drainer);
            return true;
        }

        public static IEnumerable<Gizmo> GetResourceDrainGizmos(IGeneResourceDrain drain)
        {
            if (DebugSettings.ShowDevGizmos && drain.Resource != null)
            {
                Gene_Resource resource = drain.Resource;
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "DEV: " + resource.ResourceLabel + " -10";
                command_Action.action = delegate
                {
                    OffsetResource(drain, -0.1f);
                };
                yield return command_Action;
                Command_Action command_Action2 = new Command_Action();
                command_Action2.defaultLabel = "DEV: " + resource.ResourceLabel + " +10";
                command_Action2.action = delegate
                {
                    OffsetResource(drain, 0.1f);
                };
                yield return command_Action2;
            }

        }
    }
}
