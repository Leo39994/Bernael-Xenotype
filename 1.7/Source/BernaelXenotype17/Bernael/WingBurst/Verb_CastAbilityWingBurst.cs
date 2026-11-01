using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class Verb_CastAbilityWingBurst : Verb_CastAbilityJump
    {
        public override ThingDef JumpFlyerDef => BernaelDefOf.BX_WingBurstFlyer;
    }
}