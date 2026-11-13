using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class RitualSpectatorFilter_HexmasterGene : RitualSpectatorFilter
    {
    	public override bool Allowed(Pawn p)
        {
            if (p == null || p.genes == null)
            {
                return false;
            }
            Gene gene = p.genes.GetGene(BernaelDefOf.BX_Hexmaster);
            return gene != null && gene.Active;
        }
    }
}