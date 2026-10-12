using Verse;

namespace Bernael_Xenotype
{
    public class HediffCompProperties_ImmuneWithoutWeakDamage : HediffCompProperties
    {
        public DamageDef weakDamageDef;

        public HediffCompProperties_ImmuneWithoutWeakDamage()
        {
            compClass = typeof(HediffComp_ImmuneWithoutWeakDamage);
        }
    }

    public class HediffComp_ImmuneWithoutWeakDamage : HediffComp
    {
        public HediffCompProperties_ImmuneWithoutWeakDamage Props => (HediffCompProperties_ImmuneWithoutWeakDamage)props;

        public virtual bool CheckPreApplyDamage(ref DamageInfo dinfo)
        {
            if (Props.weakDamageDef != null && dinfo.Def != Props.weakDamageDef)
            {
                return true;
            }

            return false;
        }
    }
}