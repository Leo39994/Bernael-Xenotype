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
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            if (parent?.pawn != null)
            {
                WorldComponent_NoPathCost.Add(parent.pawn);
            }
        }

        public override void CompPostPostRemoved()
        {
            if (parent?.pawn != null)
            {
                Pawn pawn = parent.pawn;
                bool hasOtherComp = false;
                if (pawn.health?.hediffSet?.hediffs != null)
                {
                    foreach (var hediff in pawn.health.hediffSet.hediffs)
                    {
                        if (hediff != parent && hediff.TryGetComp<HediffComp_NoPathCost>() != null)
                        {
                            hasOtherComp = true;
                            break;
                        }
                    }
                }
                if (!hasOtherComp)
                {
                    WorldComponent_NoPathCost.Remove(pawn);
                }
            }
        }
    }
}