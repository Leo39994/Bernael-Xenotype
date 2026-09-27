using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class Hediff_SeverityBasedOnNeed : HediffWithComps
    {
        ModExt_SeverityBasedOnNeed ModExt => this.def.GetModExtension<ModExt_SeverityBasedOnNeed>();
        Need need => pawn?.needs?.TryGetNeed(ModExt.severityFromNeed.needDef);

        public override void Tick()
        {
            base.Tick();
            if (!pawn.IsHashIntervalTick(420) || need == null) return;
            Severity = ModExt.severityFromNeed.severity.Evaluate(need.CurLevel);
        }
    }
}
