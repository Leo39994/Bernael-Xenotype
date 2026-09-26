using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
namespace Bernael_Xenotype
{
    public class Alert_LowSoulEnergy : Alert
    {
        private List<GlobalTargetInfo> targets = [];
        private List<string> targetLabels = [];

        public Alert_LowSoulEnergy()
        {
            requireBiotech = true;
            defaultLabel = "MB_AlertLowSoulEnergy".Translate();
        }

        public override string GetLabel()
        {
            string text = defaultLabel;
            if (targets.Count == 1)
            {
                text = text + ": " + targetLabels[0];
            }
            return text;
        }
        private void CalculateTargets()
        {
            targets.Clear();
            targetLabels.Clear();
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive)
            {
                if (pawn.genes == null || !pawn.RaceProps.Humanlike || pawn.Faction != Faction.OfPlayer) continue;
                Gene_Soul firstGeneOfType = pawn.genes.GetFirstGeneOfType<Gene_Soul>();
                if (firstGeneOfType == null || !(firstGeneOfType.Value < firstGeneOfType.MinLevelForAlert)) continue;
                targets.Add(pawn);
                targetLabels.Add(pawn.NameShortColored.Resolve());
            }
        }

        public override TaggedString GetExplanation()
        {
            return "MB_AlertLowSoulEnergyDesc".Translate() + ":\n" + targetLabels.ToLineList("  - ");
        }

        public override AlertReport GetReport()
        {
            CalculateTargets();
            return AlertReport.CulpritsAre(targets);
        }


    }
}
