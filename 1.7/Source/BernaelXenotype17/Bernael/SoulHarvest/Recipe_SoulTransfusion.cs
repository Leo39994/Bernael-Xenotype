using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype

{
	// The def carried vanilla Recipe_BloodTransfusion until now, and an ingredient
	// filter of BX_BottledSoul was the only thing about it that was ours. Vanilla
	// counts ThingDefOf.HemogenPack off the map rather than the filter, wants
	// HediffDefOf.BloodLoss on the patient, and heals that hediff plus hemogen -
	// so the recipe was never offered on a drained soul, and would have healed the
	// wrong thing if it had been. This is to Recipe_BloodTransfusion what
	// Recipe_ExtractSoul is to Recipe_ExtractHemogen.
	public class Recipe_SoulTransfusion : Recipe_Surgery
	{
		public const float SoulDrainHealedPerBottle = 0.35f;

		public override bool CompletableEver(Pawn surgeryTarget)
		{
			if (!surgeryTarget.health.hediffSet.HasHediff(BernaelUtility.cachedSoulDrainedHediff))
			{
				return false;
			}
			return base.CompletableEver(surgeryTarget);
		}

		public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
		{
			if (thing.MapHeld == null)
			{
				return false;
			}
			if (BottledSoulsOn(thing.MapHeld) <= 0)
			{
				return false;
			}
			if (thing is Pawn pawn && !pawn.health.hediffSet.HasHediff(BernaelUtility.cachedSoulDrainedHediff))
			{
				return false;
			}
			return base.AvailableOnNow(thing, part);
		}

		// Left empty on purpose, exactly as vanilla leaves it: the bill must not eat
		// the bottles when it picks them up, because ApplyOnPawn reads their stack
		// counts to decide how much it heals and destroys them itself afterwards.
		public override void ConsumeIngredient(Thing ingredient, RecipeDef recipe, Map map)
		{
		}

		public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
		{
			if (!ModLister.CheckBiotech("Soul transfusion"))
			{
				return;
			}
			float healed = 0f;
			float soulGained = 0f;
			for (int i = 0; i < ingredients.Count; i++)
			{
				// Medicine rides along on a surgery bill without being an ingredient
				// of this recipe, so it heals nothing here.
				if (ingredients[i].def.IsMedicine)
				{
					continue;
				}
				healed += SoulDrainHealedPerBottle * ingredients[i].stackCount;
				soulGained += JobGiver_GetBottledSoul.BottledSoulEnergyGain * ingredients[i].stackCount;
			}
			if (healed > 0f)
			{
				Hediff drained = pawn.health.hediffSet.GetFirstHediffOfDef(BernaelUtility.cachedSoulDrainedHediff);
				if (drained != null)
				{
					drained.Severity -= healed;
				}
			}
			// A pawn without a soul resource keeps the healing and gains nothing else -
			// OffsetSoul looks for the gene itself and returns quietly when there is none.
			if (soulGained > 0f)
			{
				SoulUtility.OffsetSoul(pawn, soulGained);
			}
			for (int j = 0; j < ingredients.Count; j++)
			{
				ingredients[j].Destroy();
			}
		}

		// What the bill asks to be carried over: never more bottles than the drain
		// needs, and never more than the map has.
		public override float GetIngredientCount(IngredientCount ing, Bill bill)
		{
			if (bill.billStack?.billGiver is not Pawn pawn)
			{
				return base.GetIngredientCount(ing, bill);
			}
			Hediff drained = pawn.health.hediffSet.GetFirstHediffOfDef(BernaelUtility.cachedSoulDrainedHediff);
			if (drained == null)
			{
				return base.GetIngredientCount(ing, bill);
			}
			return Mathf.Min(BottledSoulsOn(bill.Map), drained.Severity / SoulDrainHealedPerBottle);
		}

		// Stack counts, not stacks: five bottles in one pile are five transfusions,
		// and vanilla's own count of hemogen packs is the only reason it reads as
		// Count there - the bill's ingredient count sums stacks too.
		private static int BottledSoulsOn(Map map)
		{
			if (map == null)
			{
				return 0;
			}
			int total = 0;
			List<Thing> bottles = map.listerThings.ThingsOfDef(BernaelDefOf.BX_BottledSoul);
			for (int i = 0; i < bottles.Count; i++)
			{
				total += bottles[i].stackCount;
			}
			return total;
		}
	}
}
