using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class CompProperties_AbilityRequiresHediff : CompProperties_AbilityEffect
    {
        public HediffDef requiresHediffDef;
        public bool hideGizmoOnUndesirable  = true;

        public CompProperties_AbilityRequiresHediff()
        {
            compClass = typeof(CompAbilityEffect_RequireHediff);
        }
    }

    public class CompAbilityEffect_RequireHediff : CompAbilityEffect
    {
        public new CompProperties_AbilityRequiresHediff Props => (CompProperties_AbilityRequiresHediff)props;

        private bool HasEnoughHediff
        {
            get
            {
                Pawn pawn = parent?.pawn;
                if (pawn?.health?.hediffSet == null || Props.requiresHediffDef == null)
                {
                    return false;
                }
                return pawn.health.hediffSet.HasHediff(Props.requiresHediffDef);
            }
        }

        public override bool ShouldHideGizmo => Props.hideGizmoOnUndesirable && !HasEnoughHediff;

        public override bool GizmoDisabled(out string reason)
        {
            if (!HasEnoughHediff)
            {
                string label = Props.requiresHediffDef != null ? Props.requiresHediffDef.label : "BX_AbilityDisabledNoHeiffDefault".Translate();
                reason = "BX_AbilityDisabledNoHeiff".Translate(label);
                return true;
            }
            reason = null;
            return false;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
	    {
	    	return HasEnoughHediff;
	    }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!HasEnoughHediff)
            {
                if (throwMessages && GizmoDisabled(out string reason))
                    Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }
    }
}
