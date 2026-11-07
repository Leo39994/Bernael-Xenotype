using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class JobDriver_BogeymanEscape : JobDriver
    {
        private const int CheckIntervalTicks = 1200;

        protected IntVec3 ExitCell => job.GetTarget(TargetIndex.A).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil gotoThingToil = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);

            Toil findShortestPathMapToil = ToilMaker.MakeToil("FindShortestPath");
            findShortestPathMapToil.initAction = delegate
            {
                if (TryFindAndInterceptBlockingBuilding(pawn, out Building blockingBuilding))
                {
                    job.SetTarget(TargetIndex.B, blockingBuilding);
                    JumpToToil(gotoThingToil);
                    return;
                }
            };
            findShortestPathMapToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findShortestPathMapToil;

            Toil gotoCellToil = Toils_Goto.GotoCell(ExitCell, PathEndMode.OnCell);
            gotoCellToil.tickAction = delegate
            {
                if (pawn.Position.OnEdge(Map) || Map.exitMapGrid.IsExitCell(pawn.Position))
                {
                    pawn.ExitMap(allowedToJoinOrCreateCaravan: true, CellRect.WholeMap(Map).GetClosestEdge(pawn.Position));
                }
                if (pawn.IsHashIntervalTick(CheckIntervalTicks))
                {
                    JumpToToil(findShortestPathMapToil);
                    return;
                }
            };
            gotoCellToil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return gotoCellToil;

            Toil exitMapToil = ToilMaker.MakeToil("ExitMap");
            exitMapToil.initAction = delegate
            {
                if (pawn.Position.OnEdge(Map) || Map.exitMapGrid.IsExitCell(pawn.Position))
                {
                    if (pawn.carryTracker?.CarriedThing is Pawn victim)
                    {
                        TryConvertVictim(pawn, victim);
                    }
                    if (!job.failIfCantJoinOrCreateCaravan || CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(pawn))
                    {
                        pawn.ExitMap(allowedToJoinOrCreateCaravan: true, CellRect.WholeMap(Map).GetClosestEdge(pawn.Position));
                    }
                }
            };
            exitMapToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return exitMapToil;

            gotoThingToil.FailOnDespawnedOrNull(TargetIndex.B);
            yield return gotoThingToil;

            Toil castVerbToil = ToilMaker.MakeToil("CastVerb");
            castVerbToil.initAction = delegate
            {
                if (job.GetTarget(TargetIndex.B).Thing is not Building blockingBuilding)
                {
                    JumpToToil(gotoCellToil);
                    return;
                }
                bool hasHediff = pawn.health.hediffSet.HasHediff(BernaelDefOf.BX_FoldShift_Hediff);
                if (hasHediff)
                {
                    Ability traverseTheFold = pawn.abilities?.GetAbility(BernaelDefOf.BX_TraverseTheFold);
                    if (traverseTheFold != null && traverseTheFold.CanCast)
                    {
                        LocalTargetInfo target = new(blockingBuilding);
                        if (traverseTheFold.CanApplyOn(target))
                        {
                            Job traverseJob = traverseTheFold.GetJob(target, LocalTargetInfo.Invalid);
                            if (traverseJob != null)
                            {
                                traverseJob.playerForced = true;
                                pawn.jobs.StartJob(traverseJob, JobCondition.InterruptForced, null, false, true, null, null, false, false);
                                return;
                            }
                        }
                    }
                }
                else
                {
                    Ability foldShift = pawn.abilities?.GetAbility(BernaelDefOf.BX_FoldShift);
                    if (foldShift != null && foldShift.CanCast && foldShift.CanApplyOn((LocalTargetInfo)pawn))
                    {
                        foldShift.Activate(pawn, LocalTargetInfo.Invalid);
                        JumpToToil(gotoThingToil);
                        return;
                    }
                }
                JumpToToil(gotoCellToil);
            };
            castVerbToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return castVerbToil;
        }

        private bool TryFindAndInterceptBlockingBuilding(Pawn pawn, out Building blockingBuilding)
        {
            blockingBuilding = null;
            Ability traverseTheFold = pawn.abilities?.GetAbility(BernaelDefOf.BX_TraverseTheFold);
            if (traverseTheFold == null || !traverseTheFold.CanCast) return false;

            using PawnPath path = pawn.Map.pathFinder.FindPathNow(pawn.Position, ExitCell, TraverseParms.For(pawn, Danger.Some, TraverseMode.PassAllDestroyableThings));
            if (path.FirstBlockingBuilding(out IntVec3 cellBefore, pawn) is Building foundBuilding && traverseTheFold.CompOfType<CompAbilityEffect_TraverseTheFold>()?.Valid(foundBuilding.Position, false) == true)
            {
                int maxPassWall = GetMaxPassWall(traverseTheFold);
                if (CanTraverseBlockingBuildingWithVector(pawn.Map, cellBefore, foundBuilding.Position, maxPassWall))
                {
                    blockingBuilding = foundBuilding;
                    return true;
                }
            }
            return false;
        }

        private static bool CanTraverseBlockingBuildingWithVector(Map map, IntVec3 cellBefore, IntVec3 targetCell, int maxPassWall)
        {
            int dx = targetCell.x - cellBefore.x;
            int dz = targetCell.z - cellBefore.z;
            if (dx == 0 && dz == 0) return false;
            int stepX = 0;
            int stepZ = 0;
            if (Mathf.Abs(dx) >= Mathf.Abs(dz))
            {
                stepX = dx > 0 ? 1 : -1;
            }
            else
            {
                stepZ = dz > 0 ? 1 : -1;
            }

            int blockedCellCount = 0;
            bool insideBlockingBuilding = false;
            for (int i = 1; i < 100; i++)
            {
                IntVec3 checkCell = new(cellBefore.x + stepX * i, cellBefore.y, cellBefore.z + stepZ * i);
                if (!checkCell.InBounds(map)) break;

                TerrainDef terrain = checkCell.GetTerrain(map);
                if (terrain != null && terrain.passability == Traversability.Impassable)
                {
                    return false;
                }
                Building edifice = checkCell.GetEdifice(map);
                if (edifice != null && !edifice.def.building.claimable && !edifice.def.building.IsDeconstructible && edifice.def.building.neverBuildable)
                {
                    return false;
                }

                bool isBlocked = !checkCell.Walkable(map) || checkCell.Impassable(map);
                if (isBlocked)
                {
                    insideBlockingBuilding = true;
                    blockedCellCount++;
                    if (blockedCellCount > maxPassWall) return false;
                }
                else if (insideBlockingBuilding && !isBlocked)
                {
                    return true;
                }
            }
            return false;
        }

        private static int GetMaxPassWall(Ability ability)
        {
            if (ability?.def?.comps != null)
            {
                foreach (var compProps in ability.def.comps)
                {
                    if (compProps is CompProperties_AbilityTraverseTheFold props)
                        return props.maxPassWall;
                }
            }
            return 5;
        }

        private static void TryConvertVictim(Pawn kidnapper, Pawn victim)
        {
            if (victim == null || victim.Destroyed) return;
            if (kidnapper.Faction != null && victim.Faction != kidnapper.Faction)
            {
                victim.SetFaction(kidnapper.Faction);
            }
            if (ModsConfig.BiotechActive && victim.genes != null)
            {
                victim.genes.SetXenotype(BernaelDefOf.BX_Bernael);
            }
            MakeAdult(victim);
        }

        private static void MakeAdult(Pawn victim)
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