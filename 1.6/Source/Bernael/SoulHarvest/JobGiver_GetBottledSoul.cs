using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace Bernael_Xenotype
{
    public class JobGiver_GetBottledSoul : ThinkNode_JobGiver
    {
        public static float BottledSoulEnergyGain
        {
            get
            {
                if (cachedBottledSoulEnergyGain != null)
                {
                    return cachedBottledSoulEnergyGain.Value;
                }
                if (!ModsConfig.BiotechActive)
                {
                    cachedBottledSoulEnergyGain = 0f;
                }
                else
                {
                    IngestibleProperties ingestible = BernaelDefOf.BX_BottledSoul.ingestible;
                    object obj;
                    if (ingestible == null)
                    {
                        obj = null;
                    }
                    else
                    {
                        List<IngestionOutcomeDoer> outcomeDoers = ingestible.outcomeDoers;
                        if (outcomeDoers == null)
                        {
                            obj = null;
                        }
                        else
                        {
                            obj = outcomeDoers.FirstOrDefault(x => x is IngestionOutcomeDoer_OffsetSoul);
                        }
                    }
                    IngestionOutcomeDoer_OffsetSoul ingestionOutcomeDoer_OffsetSoul = obj as IngestionOutcomeDoer_OffsetSoul;
                    if (ingestionOutcomeDoer_OffsetSoul == null)
                    {
                        cachedBottledSoulEnergyGain = 0f;
                    }
                    else
                    {
                        cachedBottledSoulEnergyGain = ingestionOutcomeDoer_OffsetSoul.offset;
                    }
                }
                return cachedBottledSoulEnergyGain.Value;
            }
        }

        public static void ResetStaticData()
        {
            cachedBottledSoulEnergyGain = null;
        }

        public override float GetPriority(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive)
            {
                return 0f;
            }
            Pawn_GeneTracker genes = pawn.genes;
            return genes?.GetFirstGeneOfType<Gene_Soul>() == null ? 0f : 9.1f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive)
            {
                return null;
            }
            Pawn_GeneTracker genes = pawn.genes;
            Gene_Soul gene_Soul = genes?.GetFirstGeneOfType<Gene_Soul>();
            if (gene_Soul == null)
            {
                return null;
            }
            if (!gene_Soul.ShouldConsumeSoulNow())
            {
                return null;
            }
            if (pawn.IsBloodfeeder())
            {
                Pawn prisoner = GetPrisoner(pawn);
                if (prisoner != null)
                {
                    return JobMaker.MakeJob(BernaelDefOf.BX_PrisonerSoulFeed, prisoner);
                }
            }
            if (!gene_Soul.bottledSoulsAllowed) return null;
            int num = Mathf.FloorToInt((gene_Soul.Max - gene_Soul.Value) / BottledSoulEnergyGain);
            if (num <= 0) return null;

            Thing bottledSoul = GetBottledSoul(pawn, gene_Soul);
            if (bottledSoul == null) return null;
            Job job = JobMaker.MakeJob(JobDefOf.Ingest, bottledSoul);
            job.count = Mathf.Min(bottledSoul.stackCount, num);
            job.ingestTotalCount = true;
            return job;
        }

        private static Thing GetBottledSoul(Pawn pawn, Gene_Soul gene_Soul)
        {

            Thing carriedThing = pawn.carryTracker.CarriedThing;
            if (carriedThing != null)
            {
                if (carriedThing.def == BernaelDefOf.BX_BottledSoul)
                {
                    return carriedThing;
                }
            }
            for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
            {
                if (pawn.inventory.innerContainer[i].def == BernaelDefOf.BX_BottledSoul)
                {
                    return pawn.inventory.innerContainer[i];
                }
            }
            return GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, pawn.Map.listerThings.ThingsOfDef(BernaelDefOf.BX_BottledSoul), PathEndMode.OnCell, TraverseParms.For(pawn), 9999f, t => pawn.CanReserve(t) && !t.IsForbidden(pawn));

        }

        private static AcceptanceReport CanFeedOnPrisoner(Pawn bloodfeeder, Pawn prisoner)
        {
            // Same rule as the ability: our own kind is never a source.
            if (SoulUtility.IsSoulFeeder(prisoner))
            {
                return false;
            }
            if (prisoner.WouldDieFromAdditionalBloodLoss(0.4499f))
            {
                return "CannotFeedOnWouldKill".Translate(prisoner.Named("PAWN"));
            }
            if (!prisoner.IsPrisonerOfColony || !prisoner.guest.PrisonerIsSecure || prisoner.guest.IsInteractionDisabled(PrisonerInteractionModeDefOf.Bloodfeed) || prisoner.IsForbidden(bloodfeeder) || !bloodfeeder.CanReserveAndReach(prisoner, PathEndMode.OnCell, bloodfeeder.NormalMaxDanger()) || prisoner.InAggroMentalState)
            {
                return false;
            }
            return true;
        }

        private static Pawn GetPrisoner(Pawn pawn)
        {
            return (Pawn)GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, pawn.Map.mapPawns.PrisonersOfColonySpawned, PathEndMode.OnCell, TraverseParms.For(pawn), 9999f, delegate (Thing t)
            {
                Pawn prisoner;
                return (prisoner = (t as Pawn)) != null && CanFeedOnPrisoner(pawn, prisoner).Accepted;
            });
        }

        private static float? cachedBottledSoulEnergyGain;
    }
}