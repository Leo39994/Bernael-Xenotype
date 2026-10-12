using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class JobDriver_BogeymanKidnap : JobDriver_TakeAndExitMap
    {
        protected Pawn Takee => (Pawn)Item;

        public override string GetReport()
        {
            if (Takee == null || pawn.HostileTo(Takee))
            {
                return base.GetReport();
            }
            return JobUtility.GetResolvedJobReport(JobDefOf.Rescue.reportString, Takee);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => {
                if (Takee == null || Takee.Destroyed) return true;

                if (!Takee.DevelopmentalStage.Baby() && !Takee.DevelopmentalStage.Child())
                {
                    return !Takee.Downed && Takee.Awake();
                }

                return false;
            });

            foreach (Toil item in base.MakeNewToils())
            {
                Action originalTickAction = item.tickAction;
                item.tickAction = delegate
                {
                    if (Map != null && Map.exitMapGrid.IsExitCell(pawn.Position))
                    {
                        TryConvertVictim();
                    }
                    originalTickAction?.Invoke();
                };

                Action originalInitAction = item.initAction;
                item.initAction = delegate
                {
                    if (Map != null && (pawn.Position.OnEdge(Map) || Map.exitMapGrid.IsExitCell(pawn.Position)))
                    {
                        TryConvertVictim();
                    }
                    originalInitAction?.Invoke();
                };

                yield return item;
            }
        }

        private void TryConvertVictim()
        {
            Pawn victim = Takee;
            if (victim == null || victim.Destroyed) return;

            if (pawn.Faction != null && victim.Faction != pawn.Faction)
            {
                victim.SetFaction(pawn.Faction);
            }

            if (ModsConfig.BiotechActive && victim.genes != null)
            {
                victim.genes.SetXenotype(BernaelDefOf.BX_Bernael);
            }

            MakeAdult(victim);
        }

        private void MakeAdult(Pawn victim)
        {
            if (victim?.ageTracker == null) return;

            long minAdultTicks = victim.ageTracker.AdultMinAgeTicks;
            if (victim.ageTracker.AgeBiologicalTicks < minAdultTicks)
            {
                victim.ageTracker.AgeBiologicalTicks = minAdultTicks;
                victim.Drawer?.renderer?.SetAllGraphicsDirty();
                PawnComponentsUtility.AddAndRemoveDynamicComponents(victim);
            }
        }
    }
}