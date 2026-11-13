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
        private IntVec3 originalDestination = IntVec3.Invalid;
        private bool visualsRegistered;
        private bool cleanupRegistered;
        private List<IntVec3> countedWalls = new List<IntVec3>();

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
            originalDestination = job.targetA.Cell;
            if (pawn.health != null)
            {
                AddHediffIfNotExists(BernaelDefOf.BX_FoldShift_Hediff);
                AddHediffIfNotExists(BernaelDefOf.BX_TraverseTheFold_Hediff);
            }
            EnsureVisuals();
        }

        public override void ExposeData()
        {
            // Base SetupToils runs during PostLoadInit, after these values were read in LoadingVars.
            base.ExposeData();
            Scribe_Values.Look(ref startPosition, "foldStart", IntVec3.Invalid);
            Scribe_Values.Look(ref originalDestination, "foldDestination", IntVec3.Invalid);
            Scribe_Values.Look(ref lastCheckedNextCell, "foldLastChecked", IntVec3.Invalid);
            Scribe_Values.Look(ref impassableCellCount, "foldWallCount");
            Scribe_Values.Look(ref isReturning, "foldReturning");
            Scribe_Collections.Look(ref countedWalls, "foldCountedWalls", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) countedWalls ??= new List<IntVec3>();
        }

        private void FinishTraversal(JobCondition condition)
        {
            if (pawn?.Spawned == true && pawn.Position.Impassable(pawn.Map) && startPosition.IsValid)
            {
                // Forced interruptions must not leave a pawn stranded after its phasing permission ends.
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(startPosition, 3.9f, true))
                    if (cell.InBounds(pawn.Map) && cell.Walkable(pawn.Map) && !cell.Impassable(pawn.Map))
                    {
                        pawn.Position = cell;
                        pawn.Notify_Teleported(endCurrentJob: false);
                        break;
                    }
            }
            WorldComponent_TraverseTheFold.Remove(pawn);
            pawn?.Map?.GetComponent<FoldGateMapComponent>().End(pawn);
            if (pawn?.health != null)
            {
                RemoveHediffIfExists(BernaelDefOf.BX_FoldShift_Hediff);
                RemoveHediffIfExists(BernaelDefOf.BX_TraverseTheFold_Hediff);
            }
        }

        private void EnsureVisuals()
        {
            WorldComponent_TraverseTheFold.Add(pawn);
            if (visualsRegistered || pawn?.Map == null) return;
            if (!startPosition.IsValid) startPosition = pawn.Position;
            if (!originalDestination.IsValid) originalDestination = job.targetA.Cell;
            IntVec3 entry = job.targetB.IsValid ? job.targetB.Cell : pawn.Position;
            pawn.Map.GetComponent<FoldGateMapComponent>().Begin(pawn, startPosition, entry, originalDestination);
            visualsRegistered = true;
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
            // SetupToils is also called on load; both the pathing permission and cleanup must be restored.
            WorldComponent_TraverseTheFold.Add(pawn);
            if (!cleanupRegistered) { AddFinishAction(FinishTraversal); cleanupRegistered = true; }
            IEnumerable<Toil> baseToils = base.MakeNewToils();
            foreach (Toil toil in baseToils)
            {
                toil.AddPreTickAction(delegate
                {
                    EnsureVisuals();
                    // Skip counting if already on the return trip
                    if (isReturning) return;
                    if (pawn == null || !pawn.Spawned || pawn.pather == null) return;
                    if (!pawn.health.hediffSet.HasHediff(BernaelDefOf.BX_FoldShift_Hediff))
                    {
                        TriggerTurnAround();
                        return;
                    }

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
                        if (BuildingBlockingNextPathCell() != null && !countedWalls.Contains(nextCell))
                        {
                            countedWalls.Add(nextCell);
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
            Messages.Message($"BX_TraverseTheFold_Returned".Translate(), new TargetInfo(pawn.Position, pawn.Map), MessageTypeDefOf.CautionInput, historical: false);
            job.targetA = startPosition;
            pawn.pather.StartPath(startPosition, PathEndMode.OnCell);
        }
    }
}
