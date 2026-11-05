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
            base.Apply(target, dest);

            Pawn pawn = parent.pawn;
            if (pawn == null || !pawn.Spawned) return;

            IntVec3 casterPos = pawn.Position;
            IntVec3 targetCell = target.Cell;
            int dx = targetCell.x - casterPos.x;
            int dz = targetCell.z - casterPos.z;
            if (dx == 0 && dz == 0) return;
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
            IntVec3 finalTarget = casterPos;
            bool insideBlockingBuilding = false;
            for (int i = 1; i < 100; i++)
            {
                IntVec3 checkCell = new(casterPos.x + stepX * i, casterPos.y, casterPos.z + stepZ * i);
                if (!checkCell.InBounds(map)) break;

                bool isBlocked = !checkCell.Walkable(map) || checkCell.Impassable(map);
                if (isBlocked)
                {
                    insideBlockingBuilding = true;
                }
                else if (insideBlockingBuilding && !isBlocked)
                {
                    finalTarget = checkCell;
                    break;
                }
            }
            if (finalTarget == casterPos)
            {
                finalTarget = targetCell.InBounds(map) ? targetCell : casterPos;
            }
            Job traverseJob = JobMaker.MakeJob(BernaelDefOf.BX_TraverseTheFold_Job, finalTarget);
            traverseJob.playerForced = true;
            traverseJob.ability = parent;
            pawn.jobs.StartJob(traverseJob, JobCondition.InterruptForced, null, false, true, null, null, false, false);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            Pawn pawn = parent.pawn;
            if (pawn == null || pawn.Map == null) return false;

            Map map = pawn.Map;
            if (!target.Cell.IsValid || !target.Cell.InBounds(map))
            {
                return false;
            }

            Building edifice = target.Cell.GetEdifice(map);
            if (edifice == null || !target.Cell.Impassable(map))
            {
                Messages.Message("Target must be an impassable blocking structure.", MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            if (edifice.def.building != null && !edifice.def.building.claimable && !edifice.def.building.IsDeconstructible && edifice.def.building.neverBuildable)
            {
                Messages.Message("Cannot target this special barrier.", MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            return true;
        }
    }
}