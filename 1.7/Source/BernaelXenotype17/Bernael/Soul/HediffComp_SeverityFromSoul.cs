using RimWorld;
using Verse;
namespace Bernael_Xenotype
{
    public class HediffComp_SeverityFromSoul : HediffComp
    {
        public HediffCompProperties_SeverityFromSoul Props => props as HediffCompProperties_SeverityFromSoul;

        public override bool CompShouldRemove
        {
            get
            {
                Pawn_GeneTracker genes = Pawn.genes;
                if (genes?.GetFirstGeneOfType<Gene_Soul>() == null)
                {
                    return true;
                }
                return false;
            }
        }

        private Gene_Soul Soul
        {
            get
            {
                if (cachedSoulGene == null)
                {
                    cachedSoulGene = Pawn.genes.GetFirstGeneOfType<Gene_Soul>();
                }
                return cachedSoulGene;
            }
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (Soul != null)
            {
                severityAdjustment += (Soul.Value > 0f ? Props.severityPerHourSoul : Props.severityPerHourEmpty) / 2500f * delta;
            }
        }

        private Gene_Soul cachedSoulGene;
    }
}
