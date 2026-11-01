using Verse;
using RimWorld;

namespace Bernael_Xenotype
{
    public class HediffComp_BabyBond_Victim : HediffComp
    {
        public Pawn BondedFeeder;

        public HediffCompProperties_BabyBond_Victim Props => (HediffCompProperties_BabyBond_Victim)props;

        public override bool CompShouldRemove => base.CompShouldRemove || BondedFeeder == null || BondedFeeder.Dead;

        private int _tickCounter = 0;
        private const int CheckInterval = 250;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            _tickCounter += delta;
            if (_tickCounter >= CheckInterval)
            {
                _tickCounter = 0;
                DevelopmentalStage developmentStage = Pawn.ageTracker.CurLifeStage.developmentalStage;
                if (developmentStage is DevelopmentalStage.Adult)
                {
                    BabyBondUtility.BreakBond(BondedFeeder, Pawn);
                }
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            if (BondedFeeder == null || BondedFeeder.Dead)
            {
                return;
            }

            DamageInfo feedDinfo = new DamageInfo(DamageDefOf.Deterioration, 99999f, instigator: Pawn);
            BondedFeeder.Kill(feedDinfo, culprit);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref _tickCounter, "tickCounter", 0);
            Scribe_References.Look(ref BondedFeeder, "bondedFeeder");
        }

        public override string CompDebugString() => $"Bonded feeder: {BondedFeeder?.LabelShort ?? "none"}";
    }


}