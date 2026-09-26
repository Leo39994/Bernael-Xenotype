using Verse;

namespace Bernael_Xenotype
{
    public class Gene_Hediff : Gene
    {
        GeneAddHediffExtension ModExt => def.GetModExtension<GeneAddHediffExtension>();

        public override void PostAdd()
        {
            base.PostAdd();
            AddHediffs();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            AddHediffs();
        }

        private void AddHediffs()
        {
            if (pawn.health?.hediffSet == null) return;
            foreach (HediffDef hediffDef in ModExt.hediffsToAdd)
            {
                pawn.health.GetOrAddHediff(hediffDef);
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();
            if (pawn.health?.hediffSet == null) return;
            foreach (HediffDef hediffDef in ModExt.hediffsToAdd)
            {
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                if (hediff == null) continue;
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}
