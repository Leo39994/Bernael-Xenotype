using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class CompProperties_CreateArcanaStone : CompProperties_AbilityEffect
    {
        public CompProperties_CreateArcanaStone() => compClass = typeof(CompAbilityEffect_CreateArcanaStone);

        public ThingDef stoneCreated;
    }
}
