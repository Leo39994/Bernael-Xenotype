using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public abstract class Ability_Toggle : Ability
    {
        public Ability_Toggle() : base() { }
        public Ability_Toggle(Pawn pawn) : base(pawn) { }
        public Ability_Toggle(Pawn pawn, AbilityDef def) : base(pawn, def) { }
        public Ability_Toggle(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def) { }

        public abstract bool IsActive { get; }

        public virtual void TurnOff() { }

        public override IEnumerable<Command> GetGizmos()
        {
            if (!pawn.Drafted || def.showWhenDrafted)
            {
                gizmo ??= new Command_ToggleAbility(this, pawn, () => IsActive)
                    {
                        Order = def.uiOrder
                    };
                yield return gizmo;
            }
            if (DebugSettings.ShowDevGizmos && OnCooldown && CanCooldown)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Reset cooldown",
                    action = ResetCooldown
                };
            }
        }
    }
}