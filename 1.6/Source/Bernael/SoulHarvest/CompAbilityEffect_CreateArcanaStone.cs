using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bernael_Xenotype
{
    public class CompAbilityEffect_CreateArcanaStone : CompAbilityEffect
    {
        public new CompProperties_CreateArcanaStone Props => (CompProperties_CreateArcanaStone)base.Props;
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn pawn = parent.pawn;
            Thing thing = ThingMaker.MakeThing(Props.stoneCreated);
            if (pawn.inventory.innerContainer != null)
            {
                pawn.inventory.innerContainer.TryAdd(thing);
            }
        }
    }
}