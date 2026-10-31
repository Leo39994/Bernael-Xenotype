using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class Projectile_SoulLantern : Bullet
    {
        public override Vector3 ExactPosition
        {
            get
            {
                if (!visualTrackingEnabled || segmentTicksTotal <= 0)
                {
                    return base.ExactPosition;
                }

                float distanceCoveredFraction = Mathf.Clamp01(1f - (float)ticksToImpact / segmentTicksTotal);
                Vector3 travel = (destination - segmentOrigin).Yto0() * distanceCoveredFraction;
                return segmentOrigin.Yto0() + travel + Vector3.up * def.Altitude;
            }
        }

        public override Quaternion ExactRotation
        {
            get
            {
                if (!visualTrackingEnabled)
                {
                    return base.ExactRotation;
                }

                Vector3 direction = VisualTravelDirection;
                return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : base.ExactRotation;
            }
        }

        private Vector3 VisualTravelDirection
        {
            get
            {
                Vector3 remainingTravel = (destination - ExactPosition).Yto0();
                float remainingDistance = remainingTravel.magnitude;
                if (remainingDistance <= 0.0001f)
                {
                    return (destination - segmentOrigin).Yto0();
                }

                Vector3 forward = remainingTravel / remainingDistance;
                if (!TryGetVisualArc(remainingTravel, remainingDistance, out Vector3 perpendicular,
                        out float progress, out float totalDistance, out float amplitude))
                {
                    return forward;
                }

                float lateralSlope = amplitude * Mathf.PI * Mathf.Cos(progress * Mathf.PI) / totalDistance;
                return forward + perpendicular * lateralSlope;
            }
        }

        private Vector3 VisualArcOffset
        {
            get
            {
                Vector3 exactPosition = ExactPosition;
                Vector3 remainingTravel = (destination - exactPosition).Yto0();
                float remainingDistance = remainingTravel.magnitude;
                if (!TryGetVisualArc(remainingTravel, remainingDistance, out Vector3 perpendicular,
                        out float progress, out _, out float amplitude))
                {
                    return Vector3.zero;
                }

                float arcStrength = Mathf.Sin(progress * Mathf.PI);
                return perpendicular * (amplitude * arcStrength);
            }
        }

        private bool TryGetVisualArc(Vector3 remainingTravel, float remainingDistance,
            out Vector3 perpendicular, out float progress, out float totalDistance, out float amplitude)
        {
            perpendicular = Vector3.zero;
            progress = 0f;
            totalDistance = visualDistanceTravelled + remainingDistance;
            amplitude = 0f;

            SoulLanternProjectileExtension extension = Extension;
            if (!visualTrackingEnabled || extension == null || extension.visualArcAmplitude <= 0f ||
                remainingDistance <= 0.0001f || totalDistance <= 0.0001f)
            {
                return false;
            }

            progress = Mathf.Clamp01(visualDistanceTravelled / totalDistance);
            perpendicular = new Vector3(-remainingTravel.z, 0f, remainingTravel.x) / remainingDistance;
            float distanceScale = Mathf.Clamp01(initialTravelDistance / 10f);
            amplitude = visualArcSide * extension.visualArcAmplitude * distanceScale;
            return true;
        }

        private SoulLanternProjectileExtension Extension
        {
            get
            {
                if (cachedExtension == null)
                {
                    cachedExtension = def.GetModExtension<SoulLanternProjectileExtension>();
                }

                return cachedExtension;
            }
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            launchPosition = origin.ToIntVec3();
            trackingRange = ResolveTrackingRange();
            Pawn usedPawn = usedTarget.Thing as Pawn;
            Pawn intendedPawn = intendedTarget.Thing as Pawn;
            visualTrackingEnabled = intendedPawn != null;
            canRetarget = visualTrackingEnabled;

            if (!visualTrackingEnabled)
            {
                return;
            }

            bool shotResolvedAtTarget = usedPawn != null && usedPawn == intendedPawn;
            if (!shotResolvedAtTarget)
            {
                missOffset = usedTarget.CenterVector3 - intendedPawn.DrawPos;
                this.usedTarget = new LocalTargetInfo(usedTarget.Cell);
            }

            lastTrackedPosition = intendedPawn.Position;
            PrepareTrackingSegment(origin, intendedPawn.DrawPos + missOffset);
            initialTravelDistance = (destination - origin).MagnitudeHorizontal();
            visualArcSide = (thingIDNumber & 1) == 0 ? 1f : -1f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref visualTrackingEnabled, "visualTrackingEnabled", false);
            Scribe_Values.Look(ref canRetarget, "canRetarget", false);
            Scribe_Values.Look(ref retargetCount, "retargetCount", 0);
            Scribe_Values.Look(ref launchPosition, "launchPosition");
            Scribe_Values.Look(ref trackingRange, "trackingRange", 0f);
            Scribe_Values.Look(ref lastTrackedPosition, "lastTrackedPosition");
            Scribe_Values.Look(ref missOffset, "missOffset");
            Scribe_Values.Look(ref segmentOrigin, "segmentOrigin");
            Scribe_Values.Look(ref segmentTicksTotal, "segmentTicksTotal", 0);
            Scribe_Values.Look(ref visualDistanceTravelled, "visualDistanceTravelled", 0f);
            Scribe_Values.Look(ref initialTravelDistance, "initialTravelDistance", 0f);
            Scribe_Values.Look(ref visualArcSide, "visualArcSide", 0f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && visualTrackingEnabled)
            {
                if (trackingRange <= 0f)
                {
                    trackingRange = ResolveTrackingRange();
                }

                if (initialTravelDistance <= 0f)
                {
                    initialTravelDistance = (destination - ExactPosition).MagnitudeHorizontal();
                }

                if (visualArcSide == 0f)
                {
                    visualArcSide = (thingIDNumber & 1) == 0 ? 1f : -1f;
                }

                SoulLanternProjectileExtension extension = Extension;
                if (intendedTarget.Pawn != null && extension != null && retargetCount < extension.maxRetargets)
                {
                    canRetarget = true;
                }
            }
        }

        protected override void TickInterval(int delta)
        {
            if (!visualTrackingEnabled)
            {
                base.TickInterval(delta);
                return;
            }

            Vector3 currentPosition = ExactPosition;
            Pawn target = intendedTarget.Thing as Pawn;
            if (canRetarget && TargetNeedsReplacement(target))
            {
                if (!TryRetarget(target, currentPosition))
                {
                    CommitToLastTrackedDestination(currentPosition);
                    AdvanceProjectile(delta, currentPosition);
                    return;
                }

                target = intendedTarget.Pawn;
            }

            if (target == null || target.Dead || !target.Spawned || target.Map != Map)
            {
                PrepareTrackingSegment(currentPosition, destination);
                AdvanceProjectile(delta, currentPosition);
                return;
            }

            if (!WithinTrackingRange(target))
            {
                CommitToLastTrackedDestination(currentPosition);
                AdvanceProjectile(delta, currentPosition);
                return;
            }

            lastTrackedPosition = target.Position;
            PrepareTrackingSegment(currentPosition, target.DrawPos + missOffset);
            AdvanceProjectile(delta, currentPosition);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc + VisualArcOffset, flip);
        }

        private void CommitToLastTrackedDestination(Vector3 currentPosition)
        {
            LocalTargetInfo lastTrackedTarget = new LocalTargetInfo(destination.ToIntVec3());
            usedTarget = lastTrackedTarget;
            intendedTarget = lastTrackedTarget;
            canRetarget = false;
            targetCoverDef = null;
            PrepareTrackingSegment(currentPosition, destination);
        }

        private void AdvanceProjectile(int delta, Vector3 previousPosition)
        {
            base.TickInterval(delta);
            if (!Destroyed)
            {
                visualDistanceTravelled += (ExactPosition - previousPosition).MagnitudeHorizontal();
            }
        }

        private bool TargetNeedsReplacement(Pawn target)
        {
            if (target == null || target.Dead || !target.Spawned || target.Map != Map)
            {
                return true;
            }

            SoulLanternProjectileExtension extension = Extension;
            return target.Downed && extension != null && extension.retargetOnDowned;
        }

        private bool TryRetarget(Pawn previousTarget, Vector3 projectilePosition)
        {
            SoulLanternProjectileExtension extension = Extension;
            if (extension == null || retargetCount >= extension.maxRetargets)
            {
                return false;
            }

            IAttackTargetSearcher searcher = launcher as IAttackTargetSearcher;
            if (searcher == null || Map == null)
            {
                return false;
            }

            List<IAttackTarget> potentialTargets = Map.attackTargetsCache.GetPotentialTargetsFor(searcher);
            Pawn closestTarget = null;
            float closestDistanceSquared = float.MaxValue;
            IntVec3 projectileCell = projectilePosition.ToIntVec3();

            for (int i = 0; i < potentialTargets.Count; i++)
            {
                Pawn candidate = potentialTargets[i].Thing as Pawn;
                if (!ValidReplacementTarget(candidate, previousTarget))
                {
                    continue;
                }

                float distanceSquared = (candidate.DrawPos - projectilePosition).Yto0().sqrMagnitude;
                if (distanceSquared >= closestDistanceSquared || !GenSight.LineOfSight(projectileCell, candidate.Position, Map))
                {
                    continue;
                }

                closestTarget = candidate;
                closestDistanceSquared = distanceSquared;
            }

            if (closestTarget == null)
            {
                return false;
            }

            ResolveRetargetedShot(closestTarget, projectileCell);
            lastTrackedPosition = closestTarget.Position;
            retargetCount++;
            return true;
        }

        private void ResolveRetargetedShot(Pawn target, IntVec3 projectileCell)
        {
            intendedTarget = target;
            targetCoverDef = null;
            if (Rand.Chance(0.5f))
            {
                usedTarget = target;
                missOffset = Vector3.zero;
                return;
            }

            ShootLine missLine = new ShootLine(projectileCell, target.Position);
            missLine.ChangeDestToMissWild(0.5f, def.projectile.flyOverhead, Map);
            IntVec3 missCell = missLine.Dest;
            usedTarget = new LocalTargetInfo(missCell);
            missOffset = missCell.ToVector3Shifted() - target.DrawPos;
        }

        private bool ValidReplacementTarget(Pawn candidate, Pawn previousTarget)
        {
            if (candidate == null || candidate == previousTarget || candidate.Dead || candidate.Downed || !candidate.Spawned || candidate.Map != Map)
            {
                return false;
            }

            if (launcher == null || !launcher.HostileTo(candidate))
            {
                return false;
            }

            return WithinTrackingRange(candidate);
        }

        private bool WithinTrackingRange(Pawn target)
        {
            if (target == null || trackingRange <= 0f)
            {
                return false;
            }

            float trackingRangeSquared = trackingRange * trackingRange;
            return (target.Position - launchPosition).LengthHorizontalSquared <= trackingRangeSquared;
        }

        private float ResolveTrackingRange()
        {
            CompEquippable equippable = equipment?.TryGetComp<CompEquippable>();
            Verb verb = equippable?.PrimaryVerb;
            return verb?.EffectiveRange ?? 0f;
        }

        private void PrepareTrackingSegment(Vector3 start, Vector3 end)
        {
            segmentOrigin = start;
            destination = end;
            float distance = (destination - segmentOrigin).MagnitudeHorizontal();
            segmentTicksTotal = Mathf.Max(1, Mathf.CeilToInt(distance / def.projectile.SpeedTilesPerTick));
            ticksToImpact = segmentTicksTotal;
        }

        private SoulLanternProjectileExtension cachedExtension;
        private bool visualTrackingEnabled;
        private bool canRetarget;
        private int retargetCount;
        private IntVec3 launchPosition;
        private float trackingRange;
        private IntVec3 lastTrackedPosition;
        private Vector3 missOffset;
        private Vector3 segmentOrigin;
        private int segmentTicksTotal;
        private float visualDistanceTravelled;
        private float initialTravelDistance;
        private float visualArcSide;
    }
}
