using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class JobDriver_DrainPsylinkable : JobDriver
    {
        private struct RandomPosData
        {
            public Vector3 position;
            public int startTick;
        }

        private Mote_AnimaRoots activeMote;
        private readonly List<RandomPosData> activeRandomPositions = new();

        public const int LinkTimeTicks = 15000;
        public const int EffectsTickInterval = 60;
        private const int MaxRandomPositions = 4;
        private const float PawnStreamDurationTicks = 60f;
        private const float DarkStreamDurationTicks = 90f;
        private const float MinSpawnRadius = 2.9f;
        private const float MaxSpawnRadius = 4.9f;

        protected const TargetIndex PsylinkableInd = TargetIndex.A;
        protected const TargetIndex LinkSpotInd = TargetIndex.B;
        private Thing PsylinkableThing => TargetA.Thing;
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

            Toil toil = Toils_General.Wait(LinkTimeTicks);
            toil.initAction = delegate
            {
                activeRandomPositions.Clear();
                if (pawn.Drawer.renderer.CurAnimation != BernaelDefOf.BX_BernaelDraining)
                {
                    pawn.Drawer.renderer.SetAnimation(BernaelDefOf.BX_BernaelDraining);
                }
                if (PsylinkableThing != null && pawn.Map != null)
                {
                    activeMote = (Mote_AnimaRoots)GenSpawn.Spawn(BernaelDefOf.BX_Mote_AnimaRoots, PsylinkableThing.Position, pawn.Map);
                    if (activeMote != null)
                    {
                        activeMote.exactPosition = PsylinkableThing.TrueCenter();
                        activeMote.radius = MinSpawnRadius;
                    }
                }
            };
            toil.tickAction = delegate
            {
                if (pawn.Drawer.renderer.CurAnimation != BernaelDefOf.BX_BernaelDraining)
                {
                    pawn.Drawer.renderer.SetAnimation(BernaelDefOf.BX_BernaelDraining);
                }
                if (activeMote != null && !activeMote.Destroyed)
                {
                    activeMote.Maintain();
                    Lord lord = pawn.GetLord();
                    if (lord?.LordJob is LordJob_Ritual ritualJob)
                    {
                        activeMote.progress = Mathf.Clamp01(ritualJob.Progress);
                    }
                    else
                    {
                        float elapsed = LinkTimeTicks - ticksLeftThisToil;
                        activeMote.progress = Mathf.Clamp01(elapsed / LinkTimeTicks);
                    }
                }
            };
            toil.tickIntervalAction = delegate(int delta)
            {
                pawn.rotationTracker.FaceTarget(PsylinkableThing);
                Vector3 pawnCenter = pawn.TrueCenter();
                Vector3 targetCenter = PsylinkableThing.TrueCenter();
                int currentTick = Find.TickManager.TicksGame;
                for (int i = activeRandomPositions.Count - 1; i >= 0; i--)
                {
                    if (currentTick - activeRandomPositions[i].startTick >= DarkStreamDurationTicks)
                    {
                        activeRandomPositions.RemoveAt(i);
                    }
                }
                if (pawn.IsHashIntervalTick(EffectsTickInterval / 4, delta) && activeRandomPositions.Count < MaxRandomPositions)
                {
                    float r = Mathf.Sqrt(Rand.Value * (MaxSpawnRadius * MaxSpawnRadius - MinSpawnRadius * MinSpawnRadius) + MinSpawnRadius * MinSpawnRadius);
                    float theta = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector3 offset = new(r * Mathf.Cos(theta), 0f, r * Mathf.Sin(theta));
                    activeRandomPositions.Add(new RandomPosData
                    {
                        position = targetCenter + offset,
                        startTick = currentTick
                    });
                }
                if (pawn.IsHashIntervalTick(EffectsTickInterval / 12, delta))
                {
                    float tPawn = (currentTick + pawn.thingIDNumber) % PawnStreamDurationTicks / PawnStreamDurationTicks;
                    Vector3 flowPointPawn = Vector3.Lerp(targetCenter, pawnCenter, tPawn) + new Vector3(Rand.Range(-0.1f, 0.1f), 0f, Rand.Range(-0.1f, 0.1f));
                    FleckMaker.Static(flowPointPawn, pawn.Map, FleckDefOf.EntropyPulse, 1.0f);
                    for (int i = 0; i < activeRandomPositions.Count; i++)
                    {
                        RandomPosData posData = activeRandomPositions[i];
                        float t = (currentTick - posData.startTick) / DarkStreamDurationTicks;
                        float clampedT = Mathf.Clamp01(t);

                        Vector3 flowPointB = Vector3.Lerp(posData.position, targetCenter, clampedT) + new Vector3(Rand.Range(-0.2f, 0.2f), 0f, Rand.Range(-0.2f, 0.2f));
                        float targetAngle = (flowPointB - targetCenter).AngleFlat();
                        float randomRotOffset = Rand.Range(-5f, 5f);
                        float dynamicScale = Mathf.Lerp(1f, 0.1f, clampedT);

                        FleckCreationData sparkData = FleckMaker.GetDataStatic(flowPointB, pawn.Map, FleckDefOf.MicroSparks, dynamicScale);
                        sparkData.rotation = targetAngle + randomRotOffset;

                        float alpha = Mathf.Lerp(0.1f, 1f, clampedT);
                        sparkData.instanceColor = new Color(1f, 1f, 1f, alpha);

                        pawn.Map.flecks.CreateFleck(sparkData);
                    }
                }
                if (pawn.IsHashIntervalTick(EffectsTickInterval * 12, delta))
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
                activeRandomPositions.Clear();
                if (pawn?.Drawer?.renderer != null)
                {
                    pawn.Drawer.renderer.SetAnimation(null);
                }
                if (activeMote != null && !activeMote.Destroyed)
                {
                    activeMote.Destroy(DestroyMode.Vanish);
                    activeMote = null;
                }
            });
        }
    }
}