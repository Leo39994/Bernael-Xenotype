using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class Ability_ToggleHediff : Ability_Toggle
    {
        public Ability_ToggleHediff(Pawn pawn) : base(pawn) { }
        public Ability_ToggleHediff(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public virtual HediffDef HediffDef
        {
            get
            {
                if (def is AbilityDef_Toggle toggleDef && toggleDef.toggleHediffDef != null)
                {
                    return toggleDef.toggleHediffDef;
                }
                return null;
            }
        }

        public override bool IsActive
        {
            get
            {
                return pawn?.health?.hediffSet != null && HediffDef != null && pawn.health.hediffSet.HasHediff(HediffDef);
            }
        }

        public override void TurnOff()
        {
            if (HediffDef != null && pawn?.health?.hediffSet != null)
            {
                Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef);
                if (firstHediffOfDef != null)
                {
                    pawn.health.RemoveHediff(firstHediffOfDef);
                }
            }
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (HediffDef != null && pawn?.health?.hediffSet != null)
            {
                Hediff existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef);
                if (existingHediff != null)
                {
                    TurnOff();
                    PreActivate(target);
                    return true;
                }
                else
                {
                    Hediff hediff = HediffMaker.MakeHediff(HediffDef, pawn, pawn.health.hediffSet.GetBrain());
                    pawn.health.AddHediff(hediff);
                    PreActivate(target);
                    return true;
                }
            }
            return base.Activate(target, dest);
        }
    }
}