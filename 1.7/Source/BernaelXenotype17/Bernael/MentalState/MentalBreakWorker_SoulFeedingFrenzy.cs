using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace Bernael_Xenotype
{
    public class MentalBreakWorker_SoulFeedingFrenzy : MentalBreakWorker
    {
        private const float SearchRadius = 19.9f;
        private const float SearchRadiusSq = SearchRadius * SearchRadius;
        private static readonly Type EBSGResourceGeneType = AccessTools.TypeByName("EBSGFramework.ResourceGene");
        public override bool BreakCanOccur(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.abilities == null || pawn.genes == null)
            {
                return false;
            }

            bool hasSoulEater = pawn.genes.HasActiveGene(BernaelDefOf.BX_SoulEater);
            bool hasGrace = ModsConfig.IsActive("Sov.Nephilim") && pawn.genes.HasActiveGene(BernaelDefOf.GS_Grace_New);
            if (!hasSoulEater && !hasGrace)
            {
                return false;
            }
            if (!base.BreakCanOccur(pawn))
            {
                return false;
            }

            Gene_Soul soulGene = pawn.genes?.GetFirstGeneOfType<Gene_Soul>();
            if (soulGene != null && soulGene.Active && soulGene.Value > 0f)
            {
                return false;
            }
            if (EBSGResourceGeneType != null && hasGrace)
            {
                List<Gene> genesList = pawn.genes.GenesListForReading;
                for (int i = 0; i < genesList.Count; i++)
                {
                    Gene gene = genesList[i];
                    if (gene == null || !gene.Active) continue;

                    if (EBSGResourceGeneType.IsAssignableFrom(gene.GetType()))
                    {
                        if (gene is Gene_Resource resourceGene && resourceGene.Value > 0f)
                        {
                            return false;
                        }
                    }
                }
            }
            return HasValidSoulFeedingTarget(pawn);
        }

        private static bool HasValidSoulFeedingTarget(Pawn pawn)
        {
            Map mapHeld = pawn.MapHeld;
            if (mapHeld == null)
            {
                return false;
            }
            IntVec3 pawnPos = pawn.Position;
            Faction pawnFaction = pawn.Faction;
            IReadOnlyList<Pawn> allPawns = mapHeld.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn target = allPawns[i];
                if (target == pawn || target.Dead || !target.RaceProps.Humanlike)
                {
                    continue;
                }
                if (pawnPos.DistanceToSquared(target.Position) > SearchRadiusSq)
                {
                    continue;
                }
                if (target.Position.Fogged(mapHeld) || SoulUtility.IsSoulFeeder(target))
                {
                    continue;
                }
                if (target.Faction != null && target.Faction.HostileTo(pawnFaction) && !target.IsSlaveOfColony && !target.IsPrisonerOfColony && !target.Downed)
                {
                    continue;
                }
                if (GenSight.LineOfSight(pawnPos, target.Position, mapHeld, skipFirstCell: true))
                {
                    return true;
                }
            }
            return false;
        }
    }
}