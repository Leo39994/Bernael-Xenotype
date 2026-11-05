using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    /// <summary>
    /// Handles uninterruptible phasing movement through walls, inspects the next path cell, counts impassable or blocked cells, and triggers a turn-around return journey upon reaching maxPassWall limit.
    /// </summary>
    public class JobDriver_TraverseTheFold : JobDriver_Goto
    {
        private IntVec3 startPosition = IntVec3.Invalid;
        private IntVec3 lastCheckedNextCell = IntVec3.Invalid;
        private int impassableCellCount = 0;
        private bool isReturning = false;

        public override bool PlayerInterruptable => false;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            WorldComponent_TraverseTheFold.Add(pawn);
            if (!startPosition.IsValid)
            {
                startPosition = pawn.Position;
            }
            if (pawn.health != null)
            {
                AddHediffIfNotExists(BernaelDefOf.BX_FoldShift_Hediff);
                AddHediffIfNotExists(BernaelDefOf.BX_TraverseTheFold_Hediff);
            }
            AddFinishAction(delegate (JobCondition condition)
            {
                WorldComponent_TraverseTheFold.Remove(pawn);
                if (pawn?.health != null)
                {
                    RemoveHediffIfExists(BernaelDefOf.BX_FoldShift_Hediff);
                    RemoveHediffIfExists(BernaelDefOf.BX_TraverseTheFold_Hediff);
                }
            });
        }

        private void AddHediffIfNotExists(HediffDef hediffDef)
        {
            if (hediffDef != null && pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) == null)
            {
                pawn.health.AddHediff(hediffDef);
            }
        }

        private void RemoveHediffIfExists(HediffDef hediffDef)
        {
            if (hediffDef != null)
            {
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            IEnumerable<Toil> baseToils = base.MakeNewToils();
            foreach (Toil toil in baseToils)
            {
                toil.AddPreTickAction(delegate
                {
                    // Skip counting if already on the return trip
                    if (isReturning) return;
                    if (pawn == null || !pawn.Spawned || pawn.pather == null) return;

                    IntVec3 nextCell = pawn.pather.nextCell;
                    if (!nextCell.IsValid) return;
                    if (nextCell != lastCheckedNextCell)
                    {
                        lastCheckedNextCell = nextCell;
                        TerrainDef terrain = nextCell.GetTerrain(pawn.Map);
                        if ((terrain != null && terrain.passability == Traversability.Impassable) || BuildingUnphasableNextPathCell() != null)
                        {
                            TriggerTurnAround();
                            return;
                        }
                        if (BuildingBlockingNextPathCell() != null)
                        {
                            impassableCellCount++;
                            int maxPassWall = GetMaxPassWall();
                            if (impassableCellCount > maxPassWall)
                            {
                                TriggerTurnAround();
                            }
                        }
                    }
                });
                yield return toil;
            }
        }

        private Building BuildingBlockingNextPathCell()
        {
            if (pawn?.pather == null) return null;
            IntVec3 nextCell = pawn.pather.nextCell;
            if (!nextCell.IsValid) return null;

            Building edifice = nextCell.GetEdifice(pawn.Map);
            if (edifice != null && nextCell.Impassable(pawn.Map))
            {
                return edifice;
            }
            return null;
        }

        private Building BuildingUnphasableNextPathCell()
        {
            if (pawn?.pather == null) return null;
            IntVec3 nextCell = pawn.pather.nextCell;
            if (!nextCell.IsValid) return null;

            Building edifice = nextCell.GetEdifice(pawn.Map);
            if (edifice != null && !edifice.def.building.claimable && !edifice.def.building.IsDeconstructible && edifice.def.building.neverBuildable)
            {
                return edifice;
            }
            return null;
        }

        private int GetMaxPassWall()
        {
            if (job.ability?.def?.comps != null)
            {
                foreach (var compProps in job.ability.def.comps)
                {
                    if (compProps is CompProperties_AbilityTraverseTheFold props)
                    {
                        return props.maxPassWall;
                    }
                }
            }
            return 5;
        }

        /// <summary>
        /// Turns around instantly without creating a new job, heading back to the start position.
        /// </summary>
        private void TriggerTurnAround()
        {
            if (pawn == null || pawn.Map == null || !startPosition.IsValid) return;
            isReturning = true;
            Messages.Message($"Traverse the Fold encountered an special barrier or invalid terrain and is returning.", new TargetInfo(pawn.Position, pawn.Map), MessageTypeDefOf.CautionInput, historical: false);
            job.targetA = startPosition;
            pawn.pather.StartPath(startPosition, PathEndMode.OnCell);
        }
    }
}