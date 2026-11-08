using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
	public class JobGiver_AnimaFeeding : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			PawnDuty duty = pawn.mindState.duty;
			if (duty == null)
			{
				return null;
			}
			if (!pawn.CanReserveAndReach(duty.focus, PathEndMode.OnCell, Danger.Deadly))
			{
				return null;
			}
			Thing targetThing = duty.focusSecond.Thing;
			if (targetThing == null)
			{
				return null;
			}
			if (pawn.Dead || pawn.Faction != Faction.OfPlayer)
			{
				return null;
			}
			if (pawn.GetPsylinkLevel() >= pawn.GetMaxPsylinkLevel())
			{
				return null;
			}
			Gene gene = pawn?.genes?.GetGene(BernaelDefOf.BX_Hexmaster);
			if (gene == null || !gene.Active)
			{
				return null;
			}
			if (!pawn.Map.reservationManager.CanReserve(pawn, targetThing))
			{
				return null;
			}
            if (!TryFindLinkSpot(pawn, targetThing, out LocalTargetInfo spot))
            {
                return null;
            }
            return JobMaker.MakeJob(BernaelDefOf.BX_FeedPsylinkable, duty.focusSecond, duty.focus);
		}

		public static bool TryFindLinkSpot(Pawn pawn, Thing targetThing, out LocalTargetInfo spot)
		{
			spot = MeditationUtility.FindMeditationSpot(pawn).spot;
			if (CanUseSpot(pawn, targetThing, spot))
			{
				return true;
			}
			int num = GenRadial.NumCellsInRadius(2.9f);
			int num2 = GenRadial.NumCellsInRadius(3.9f);
			for (int i = num; i < num2; i++)
			{
				IntVec3 intVec = targetThing.Position + GenRadial.RadialPattern[i];
				if (CanUseSpot(pawn, targetThing, intVec))
				{
					spot = intVec;
					return true;
				}
			}
			spot = IntVec3.Zero;
			return false;
		}

		private static bool CanUseSpot(Pawn pawn, Thing targetThing, LocalTargetInfo spot)
		{
			IntVec3 cell = spot.Cell;
			if (cell.DistanceTo(targetThing.Position) > 3.9f)
			{
				return false;
			}
			if (!cell.Standable(targetThing.Map))
			{
				return false;
			}
			if (!GenSight.LineOfSight(cell, targetThing.Position, targetThing.Map))
			{
				return false;
			}
			if (!pawn.CanReach(spot, PathEndMode.OnCell, Danger.Deadly))
			{
				return false;
			}
			return true;
		}
	}
}