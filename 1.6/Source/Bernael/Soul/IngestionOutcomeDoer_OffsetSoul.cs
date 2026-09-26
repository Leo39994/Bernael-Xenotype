using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
namespace Bernael_Xenotype
{
    public class IngestionOutcomeDoer_OffsetSoul : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            SoulUtility.OffsetSoul(pawn, offset * ingestedCount);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(ThingDef parentDef)
        {
            if (ModsConfig.BiotechActive)
            {
                string arg = (offset >= 0f) ? "+" : string.Empty;
                yield return new StatDrawEntry(StatCategoryDefOf.BasicsNonPawnImportant, "BX_SoulEnergy".Translate().CapitalizeFirst(), arg + Mathf.RoundToInt(offset * 100f), "BX_SoulEnergy_Desc".Translate(), 1000);
            }
            yield break;
        }

        public float offset;
    }
}
