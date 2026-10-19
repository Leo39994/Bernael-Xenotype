using Verse;

namespace Bernael_Xenotype
{
    public class HediffCompProperties_GiveHediffOnLoss : HediffCompProperties
    {
        public HediffDef hediffDef;
        public float hediffSeverity = 1.0f;

        public HediffCompProperties_GiveHediffOnLoss()
        {
            compClass = typeof(HediffComp_GiveHediffOnLoss);
        }
    }

    public class HediffComp_GiveHediffOnLoss : HediffComp
    {
        public HediffCompProperties_GiveHediffOnLoss Props => (HediffCompProperties_GiveHediffOnLoss)props;

        public override void CompPostPostRemoved()
        {
            if (Pawn != null && !Pawn.Dead)
            {
                Hediff hediff = Pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef) ?? Pawn.health.AddHediff(Props.hediffDef);
                if (hediff != null)
                {
                    hediff.Severity = Props.hediffSeverity;
                }
            }
        }
    }
}