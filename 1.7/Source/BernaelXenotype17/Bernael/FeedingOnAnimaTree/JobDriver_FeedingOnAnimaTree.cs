using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace Bernael_Xenotype
{
	public class JobDriver_FeedPsylinkable : JobDriver
	{
		public const int LinkTimeTicks = 15000;
		public const int EffectsTickInterval = 720;

		protected const TargetIndex PsylinkableInd = TargetIndex.A;

		protected const TargetIndex LinkSpotInd = TargetIndex.B;

		private Thing PsylinkableThing => base.TargetA.Thing;

		private CompPsylinkable Psylinkable => PsylinkableThing.TryGetComp<CompPsylinkable>();

		private LocalTargetInfo LinkSpot => job.targetB;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (pawn.Reserve(PsylinkableThing, job, 1, -1, null, errorOnFailed))
			{
				return pawn.Reserve(LinkSpot, job, 1, -1, null, errorOnFailed);
			}
			return false;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			if (!ModLister.CheckRoyalty("Psylinkable"))
			{
				yield break;
			}
			AddFailCondition(() => 
			{
				if (pawn.Dead || pawn.Faction != Faction.OfPlayer) return true;
				if (pawn.GetPsylinkLevel() >= pawn.GetMaxPsylinkLevel()) return true;
				Gene gene = pawn?.genes?.GetGene(BernaelDefOf.BX_Hexmaster);
				if (gene == null || !gene.Active) return true;
				if (PsylinkableThing == null || PsylinkableThing.Destroyed) return true;
				return false;
			});

			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

			Toil toil = Toils_General.Wait(15000);
            toil.initAction = delegate
            {
                if (pawn.Drawer.renderer.CurAnimation != BernaelDefOf.BX_BernaelFeeding)
                {
                    pawn.Drawer.renderer.SetAnimation(BernaelDefOf.BX_BernaelFeeding);
                }
            };
            toil.tickAction = delegate
            {
                if (pawn.Drawer.renderer.CurAnimation != BernaelDefOf.BX_BernaelFeeding)
                {
                    pawn.Drawer.renderer.SetAnimation(BernaelDefOf.BX_BernaelFeeding);
                }
            };
			toil.tickIntervalAction = delegate(int delta)
			{
				pawn.rotationTracker.FaceTarget(PsylinkableThing);
				Vector3 pawnCenter = pawn.TrueCenter();
        		Vector3 targetCenter = PsylinkableThing.TrueCenter();
				if (pawn.IsHashIntervalTick(60, delta))
    			{
    			    float cycleDuration = 480f;
    			    float t = (Find.TickManager.TicksGame + pawn.thingIDNumber) % cycleDuration / cycleDuration;
    			    Vector3 flowPoint = Vector3.Lerp(targetCenter, pawnCenter, t);
    			    FleckMaker.Static(flowPoint, pawn.Map, FleckDefOf.MicroSparks, 0.8f);
    			}
				if (pawn.IsHashIntervalTick(360, delta))
				{
        			FleckMaker.Static(targetCenter, pawn.Map, FleckDefOf.EntropyPulse, 0.6f);
				}
				if (pawn.IsHashIntervalTick(720, delta))
				{
        			Vector3 randomPoint = pawnCenter + (targetCenter - pawnCenter) * Rand.Value;
					FleckMaker.Static(randomPoint, pawn.Map, FleckDefOf.PsycastAreaEffect, 0.5f);
					Psylinkable.Props.linkSound.PlayOneShot(SoundInfo.InMap(new TargetInfo(PsylinkableThing)));
				}
			};
			toil.handlingFacing = false;
			toil.socialMode = RandomSocialMode.Off;
			yield return toil;
			AddFinishAction(delegate (JobCondition condition)
			{
				if (pawn?.Drawer?.renderer != null)
				{
					pawn.Drawer.renderer.SetAnimation(null);
				}
			});
		}
	}
}