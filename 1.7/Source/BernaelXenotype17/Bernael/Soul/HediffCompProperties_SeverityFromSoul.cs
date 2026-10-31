using Verse;

namespace Bernael_Xenotype
{
    public class HediffCompProperties_SeverityFromSoul : HediffCompProperties
    {
        public HediffCompProperties_SeverityFromSoul() => compClass = typeof(HediffComp_SeverityFromSoul);

        public float severityPerHourEmpty;

        public float severityPerHourSoul;
    }
}