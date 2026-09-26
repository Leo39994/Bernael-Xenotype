using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(IngestionOutcomeDoer), nameof(IngestionOutcomeDoer.DoIngestionOutcome))]
    public static class Patch_IngestionOutcomeDoer_DoIngestionOutcome
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return ModsConfig.IsActive("Sov.Nephilim");
        }

        public static void Postfix(Pawn pawn, Thing ingested)
        {
            if (pawn.Map == null || ingested.def.GetGraceOutcomeDoer() == null) return;
            foreach (Pawn otherPawn in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (otherPawn.genes == null || otherPawn.relations == null) continue;
                bool hasHateGene = otherPawn.genes.GetGene(BernaelDefOf.BX_Grace_Galling) != null;
                if (!hasHateGene) continue;

                ThoughtHandler memory = otherPawn.needs.mood.thoughts;
                if (memory == null) continue;

                TaleRecorder.RecordTale(BernaelDefOf.BX_ConsumedGrace, pawn, otherPawn);
            }
        }
    }
}
