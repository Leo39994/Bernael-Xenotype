using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Bernael_Xenotype

{
	public class Recipe_ExtractSoul : Recipe_Surgery
	{
		private const float BloodlossSeverity = 0.45f;

		private const float MinBloodlossForFailure = 0.45f;

		public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
		{
			if (thing is not Pawn pawn)
			{
				return false;
			}
			if (!pawn.health.CanBleed)
			{
				return false;
			}
			foreach (Pawn colonist in pawn.Map.mapPawns.FreeColonists)
			{
				if (colonist.genes.HasActiveGene(BernaelDefOf.BX_SoulStarved))
				{
					return base.AvailableOnNow(thing, part);
				}
			}
			return false;

		}

		public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
		{
			if (thing is Pawn pawn && pawn.DevelopmentalStage.Baby())
			{
				return "TooSmall".Translate();
			}
			return base.AvailableReport(thing, part);
		}

		public override bool CompletableEver(Pawn surgeryTarget)
		{
			if (base.CompletableEver(surgeryTarget))
			{
				return PawnHasEnoughBloodForExtraction(surgeryTarget);
			}
			return false;
		}

		public override void CheckForWarnings(Pawn medPawn)
		{
			base.CheckForWarnings(medPawn);
			if (medPawn?.genes == null || medPawn.genes.HasActiveGene(BernaelDefOf.BX_SoulStarved))
			{
				Messages.Message("MessageCannotStartHemogenExtraction".Translate(medPawn.Named("PAWN")), medPawn, MessageTypeDefOf.NeutralEvent, historical: false);
			}
		}

		public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
		{
			if (!ModLister.CheckBiotech("Hemogen extraction"))
			{
				return;
			}
			if (!PawnHasEnoughBloodForExtraction(pawn))
			{
				Messages.Message("MessagePawnHadNotEnoughBloodToProduceHemogenPack".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
				return;
			}
			if (SoulUtility.TryConvertBaby(billDoer, pawn))
			{
				return;
			}
			Hediff hediff = HediffMaker.MakeHediff(BernaelUtility.cachedSoulDrainedHediff, pawn);
			hediff.Severity += BloodlossSeverity;


			pawn.health.AddHediff(hediff);
			OnSurgerySuccess(pawn, part, billDoer, ingredients, bill);
			if (IsViolationOnPawn(pawn, part, Faction.OfPlayer))
			{
				ReportViolation(pawn, billDoer, pawn.HomeFaction, -1, BernaelDefOf.BX_ExtractedSoul);
			}
		}

		protected override void OnSurgerySuccess(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
		{
			if (!GenPlace.TryPlaceThing(ThingMaker.MakeThing(BernaelDefOf.BX_BottledSoul), pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near))
			{
				Log.Error("Could not drop hemogen pack near " + pawn.PositionHeld.ToString());
			}
		}

		private static bool PawnHasEnoughBloodForExtraction(Pawn pawn)
		{
			Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(BernaelUtility.cachedSoulDrainedHediff);
			if (firstHediffOfDef != null)
			{
				return firstHediffOfDef.Severity < MinBloodlossForFailure;
			}
			return true;
		}
	}
}
