using Verse;

namespace Bernael_Xenotype
{
    public static class AbyssalReachUtility
    {
        public const float RangeFactor = 1.15f;

        public static bool HasActiveGene(Pawn pawn)
        {
            Gene gene = pawn?.genes?.GetGene(BernaelDefOf.BX_PsychicReach);
            return gene != null && gene.Active;
        }
    }
}
