using System.Collections.Generic;
using System.Text;
using Verse;

namespace Bernael_Xenotype
{
    public class HediffComp_BabyBond_Feeder : HediffComp
    {
        public List<Pawn> BondedBabies = [];

        public HediffCompProperties_BabyBond_Feeder Props => (HediffCompProperties_BabyBond_Feeder)props;

        public bool IsBonded(Pawn baby) => BondedBabies.Contains(baby);

        public void AddBaby(Pawn baby)
        {
            if (baby != null && !BondedBabies.Contains(baby))
            {
                BondedBabies.Add(baby);
            }
        }

        public void RemoveBaby(Pawn baby) => BondedBabies.Remove(baby);

        public override bool CompShouldRemove
        {
            get
            {
                if (base.CompShouldRemove) return true;
                return BondedBabies.Count == 0;
            }
        }
        private int _tickCounter = 0;
        private const int CheckInterval = 250;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            _tickCounter += delta;
            if (_tickCounter >= CheckInterval)
            {
                _tickCounter = 0;
                PruneDeadBabies();
            }

        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref _tickCounter, "tickCounter", 0);
            Scribe_Collections.Look(ref BondedBabies, "bondedBabies", LookMode.Reference);

            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (BondedBabies == null)
            {
                BondedBabies = [];
            }
            else
            {
                BondedBabies.RemoveAll(p => p == null);
            }
        }

        public override string CompDebugString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Bonded babies ({BondedBabies.Count}):");
            for (int i = 0; i < BondedBabies.Count; i++)
            {
                sb.AppendLine($"  [{i}] {BondedBabies[i]?.LabelShort ?? "null"}");
            }
            return sb.ToString().TrimEnd();
        }

        private void PruneDeadBabies()
        {
            BondedBabies.RemoveAll(p => p == null || p.Dead);
        }

    }
}