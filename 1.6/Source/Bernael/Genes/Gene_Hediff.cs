using System.Collections.Generic;
using Verse;

namespace Bernael_Xenotype
{
    public class Gene_Hediff : Gene
    {
        GeneAddHediffExtension ModExt => def?.GetModExtension<GeneAddHediffExtension>();

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
            List<HediffDef> hediffs = ModExt?.hediffsToAdd;
            if (pawn?.health?.hediffSet == null || hediffs == null) return;
            foreach (HediffDef hediffDef in hediffs)
            {
                if (hediffDef != null) pawn.health.GetOrAddHediff(hediffDef);
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();
            List<HediffDef> hediffs = ModExt?.hediffsToAdd;
            if (pawn?.health?.hediffSet == null || hediffs == null) return;
            foreach (HediffDef hediffDef in hediffs)
            {
                if (hediffDef == null) continue;
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                if (hediff == null) continue;
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}
