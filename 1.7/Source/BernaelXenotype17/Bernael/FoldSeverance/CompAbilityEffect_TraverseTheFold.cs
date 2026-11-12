using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class CompProperties_AbilityTraverseTheFold : CompProperties_AbilityEffect
    {
        public int maxPassWall = 5;

        public CompProperties_AbilityTraverseTheFold()
        {
            compClass = typeof(CompAbilityEffect_TraverseTheFold);
        }
    }

    public class CompAbilityEffect_TraverseTheFold : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!Valid(target, true)) return;
            base.Apply(target, dest);

            Pawn pawn = parent.pawn;
            if (pawn == null || !pawn.Spawned) return;

            if (!TryFindExit(pawn, target.Cell, out IntVec3 finalTarget)) return;
            Job traverseJob = JobMaker.MakeJob(BernaelDefOf.BX_TraverseTheFold_Job, finalTarget);
            traverseJob.targetB = target.Cell;
            traverseJob.playerForced = true;
            traverseJob.ability = parent;
            pawn.jobs.StartJob(traverseJob, JobCondition.InterruptForced, null, false, true, null, null, false, false);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            Pawn pawn = parent.pawn;
            if (pawn == null || pawn.Map == null) return false;
            if (parent.CompOfType<CompAbilityEffect_RequireHediff>() is { } requirement && !requirement.Valid(target, throwMessages))
                return false;

            Map map = pawn.Map;
            if (!target.Cell.IsValid || !target.Cell.InBounds(map))
            {
                return false;
            }

            Building edifice = target.Cell.GetEdifice(map);
            if (edifice == null || !target.Cell.Impassable(map))
            {
                if (throwMessages) Messages.Message("BX_TraverseTheFold_TargetMustBeImpassable".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            if (edifice.def.building != null && !edifice.def.building.claimable && !edifice.def.building.IsDeconstructible && edifice.def.building.neverBuildable)
            {
                if (throwMessages) Messages.Message("BX_TraverseTheFold_CannotTargetBarrier".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            if (!TryFindExit(pawn, target.Cell, out _))
            {
                if (throwMessages) Messages.Message("BX_TraverseTheFold_NoClearPassageThroughBarrier".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }

        private static bool TryFindExit(Pawn pawn, IntVec3 targetCell, out IntVec3 finalTarget)
        {
            IntVec3 casterPos = pawn.Position;
            finalTarget = IntVec3.Invalid;
            int dx = targetCell.x - casterPos.x;
            int dz = targetCell.z - casterPos.z;
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

            Map map = pawn.Map;
            bool insideBlockingBuilding = false;
            for (int i = 1; i < 100; i++)
            {
                IntVec3 checkCell = new(casterPos.x + stepX * i, casterPos.y, casterPos.z + stepZ * i);
                if (!checkCell.InBounds(map)) break;
                if (checkCell.GetTerrain(map).passability == Traversability.Impassable) return false;
                Building edifice = checkCell.GetEdifice(map);
                if (edifice?.def.building != null && !edifice.def.building.claimable &&
                    !edifice.def.building.IsDeconstructible && edifice.def.building.neverBuildable) return false;

                bool isBlocked = !checkCell.Walkable(map) || checkCell.Impassable(map);
                if (isBlocked)
                {
                    insideBlockingBuilding = true;
                }
                else if (insideBlockingBuilding && !isBlocked)
                {
                    finalTarget = checkCell;
                    return true;
                }
            }
            return false;
        }
    }
}
