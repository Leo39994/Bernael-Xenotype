using Verse;

namespace Bernael_Xenotype
{
    public class HediffCompProperties_NoPathCost : HediffCompProperties
    {
        public HediffCompProperties_NoPathCost()
        {
            compClass = typeof(HediffComp_NoPathCost);
        }
    }

    public class HediffComp_NoPathCost : HediffComp
    {
    }
}