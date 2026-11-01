using Verse;

namespace Bernael_Xenotype
{
    public class HediffCompProperties_DisappearsAndStunOnWeakDamage : HediffCompProperties
    {
        public DamageDef weakDamageDef;
        public int stunTicks = 1500;

        public HediffCompProperties_DisappearsAndStunOnWeakDamage()
        {
            compClass = typeof(HediffComp_DisappearsAndStunOnWeakDamage);
        }
    }

    public class HediffComp_DisappearsAndStunOnWeakDamage : HediffComp
    {
        public HediffCompProperties_DisappearsAndStunOnWeakDamage Props => (HediffCompProperties_DisappearsAndStunOnWeakDamage)props;

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            if (Props.weakDamageDef != null && dinfo.Def == Props.weakDamageDef)
            {
                if (Pawn.stances?.stunner != null && Props.stunTicks > 0)
                {
                    Pawn.stances.stunner.StunFor(Props.stunTicks, dinfo.Instigator, addBattleLog: false, showMote: true);
                }
                Pawn.health.RemoveHediff(parent);
            }
        }
    }
}