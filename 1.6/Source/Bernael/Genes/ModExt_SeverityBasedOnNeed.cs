using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class ModExt_SeverityBasedOnNeed : DefModExtension
    {
        public class SeverityModifierByNeed
        {
            public NeedDef needDef;
            public SimpleCurve severity;
        }

        public SeverityModifierByNeed severityFromNeed;
    }
}
