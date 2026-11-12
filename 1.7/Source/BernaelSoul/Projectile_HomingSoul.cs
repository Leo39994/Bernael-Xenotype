using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class HomingSoulProjectileExtension : DefModExtension
    {
        public int maxRetargets = 1;
        public bool retargetOnDowned = true;
        public float visualArcAmplitude = 1.1f;
        public float trackingRange = 29.5f;
    }

    public class Projectile_HomingSoul : Bullet
    {
        // The burst is opt-in so soul lantern projectiles keep their existing trajectory and art.
        public bool IsRancorSoul => rancorSoul;
        public bool IsBurstingOutward => rancorOutward;
        public bool IsTurningFromBurst => rancorTurning;
        public override int UpdateRateTicks => rancorSoul ? 1 : base.UpdateRateTicks;

        public void BeginRancorBurst(float angle)
        {
            rancorSoul = true;
            rancorOutward = true;
            rancorLaunchTick = Find.TickManager.TicksGame;
            Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector3 start = ExactPosition;
            rancorAim = destination;
            float reach = 2.7f + (thingIDNumber * 17 % 11) * 0.07f;
            Vector3 end = start + direction * reach;
            // Keep the whole release on the map even when the caster stands on its edge.
            end.x = Mathf.Clamp(end.x, 0.05f, Map.Size.x - 0.05f);
            end.z = Mathf.Clamp(end.z, 0.05f, Map.Size.z - 0.05f);
            visualTrackingEnabled = true;
            PrepareTrackingSegment(start, end);
            segmentTicksTotal = ticksToImpact = OutwardTicks;
            previousVisualPosition = start;
        }

        // The released souls fly unguided for a while before they turn on their targets (Mortis, 2026-11-09: the
        // homing began too abruptly). They burst out at about the soul bolt's speed and slow as they spread, then a
        // turn picks them up from that slow drift and hands them over to the homing flight at its full speed.
        private const int OutwardTicks = 24;
        private const int TurnTicks = 20;
        // Launch speed as a multiple of the outward leg's average; it ends at 2 - OutwardLaunch of it.
        private const float OutwardLaunch = 1.5f;

        public override Vector3 ExactPosition
        {
            get
            {
                if (!visualTrackingEnabled || segmentTicksTotal <= 0)
                {
                    return base.ExactPosition;
                }

                float distanceCoveredFraction = Mathf.Clamp01(1f - (float)ticksToImpact / segmentTicksTotal);
                if (rancorOutward)
                    distanceCoveredFraction *= OutwardLaunch + (1f - OutwardLaunch) * distanceCoveredFraction;
                if (rancorTurning)
                {
                    float t = distanceCoveredFraction, u = 1 - t;
                    return (segmentOrigin * (u*u*u) + turnControlA * (3*u*u*t) + turnControlB * (3*u*t*t) +
                        destination * (t*t*t)).Yto0() + Vector3.up * def.Altitude;
                }
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

        private HomingSoulProjectileExtension Extension
        {
            get
            {
                if (cachedExtension == null)
                {
                    cachedExtension = def.GetModExtension<HomingSoulProjectileExtension>();
                }

                return cachedExtension;
            }
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false,
            Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment,
                targetCoverDef);

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
            Scribe_Values.Look(ref rancorSoul, "rancorSoul", false);
            Scribe_Values.Look(ref rancorOutward, "rancorOutward", false);
            Scribe_Values.Look(ref rancorTurning, "rancorTurning", false);
            Scribe_Values.Look(ref turnControlA, "soulTurnA");
            Scribe_Values.Look(ref turnControlB, "soulTurnB");
            Scribe_Values.Look(ref rancorLaunchTick, "rancorLaunchTick");
            Scribe_Values.Look(ref rancorAim, "rancorAim");
            Scribe_Values.Look(ref previousVisualPosition, "soulTrailHead");
            Scribe_Collections.Look(ref trail, "soulTrail", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && trail == null) trail = new List<Vector3>(8);

            if (Scribe.mode != LoadSaveMode.PostLoadInit || !visualTrackingEnabled)
            {
                return;
            }

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

            HomingSoulProjectileExtension extension = Extension;
            if (intendedTarget.Pawn != null && extension != null && retargetCount < extension.maxRetargets)
            {
                canRetarget = true;
            }
        }

        protected override void TickInterval(int delta)
        {
            if (rancorSoul)
            {
                Vector3 point = ExactPosition + (rancorOutward || rancorTurning ? Vector3.zero : VisualArcOffset);
                if ((point - previousVisualPosition).sqrMagnitude > 0.004f)
                {
                    if (previousVisualPosition != Vector3.zero) trail.Insert(0, previousVisualPosition);
                    if (trail.Count > MaxTrailPoints) trail.RemoveAt(trail.Count - 1);
                    previousVisualPosition = point;
                }
            }
            if (rancorOutward || rancorTurning)
            {
                AdvanceProjectile(delta, ExactPosition);
                return;
            }
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
            if (rancorSoul)
            {
                Vector3 at = drawLoc + (rancorOutward || rancorTurning ? Vector3.zero : VisualArcOffset);
                Vector3 direction = rancorOutward ? destination - segmentOrigin : VisualTravelDirection;
                if (rancorTurning)
                {
                    float t = Mathf.Clamp01(1f - (float)ticksToImpact / segmentTicksTotal), u = 1 - t;
                    direction = (turnControlA - segmentOrigin) * (3*u*u) + (turnControlB - turnControlA) * (6*u*t) +
                        (destination - turnControlB) * (3*t*t);
                }
                if (RancorVisuals.DrawSoul(at, direction, trail, (Find.TickManager.TicksGame - rancorLaunchTick) / 60f,
                        thingIDNumber)) return;
            }
            base.DrawAt(drawLoc + VisualArcOffset, flip);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // Each released soul ends in a small burst where it lands, as in the Requiem reference.
            if (rancorSoul && Map != null)
                Map.GetComponent<MapComponent_RancorVolley>()?.Notify_SoulImpact(ExactPosition, thingIDNumber);
            base.Impact(hitThing, blockedByShield);
        }

        protected override void ImpactSomething()
        {
            if (rancorOutward || rancorTurning)
            {
                // Reaching the radial waypoint is not a hit. Keep the target and normal Bullet damage.
                Vector3 at = ExactPosition;
                Vector3 aim = intendedTarget.Pawn is Pawn pawn && pawn.Spawned && pawn.Map == Map
                    ? pawn.DrawPos + missOffset : rancorAim;
                if (rancorOutward)
                {
                    Vector3 outgoing = (destination - segmentOrigin).Yto0().normalized;
                    Vector3 toward = (aim - at).Yto0();
                    float reach = Mathf.Min(2.6f, toward.magnitude * 0.65f);
                    toward = toward.normalized;
                    Vector3 end = at + toward * reach;
                    // Short first handle: the turn starts at the drift's slow speed. Long last handle: it ends at
                    // about the homing speed (3 * handle / TurnTicks cells per tick).
                    turnControlA = at + outgoing * Mathf.Min(0.5f, reach * 0.3f);
                    turnControlB = end - toward * Mathf.Min(1.33f, reach * 0.55f);
                    turnControlA.x = Mathf.Clamp(turnControlA.x, 0.05f, Map.Size.x - 0.05f);
                    turnControlA.z = Mathf.Clamp(turnControlA.z, 0.05f, Map.Size.z - 0.05f);
                    turnControlB.x = Mathf.Clamp(turnControlB.x, 0.05f, Map.Size.x - 0.05f);
                    turnControlB.z = Mathf.Clamp(turnControlB.z, 0.05f, Map.Size.z - 0.05f);
                    rancorOutward = false;
                    rancorTurning = true;
                    PrepareTrackingSegment(at, end);
                    segmentTicksTotal = ticksToImpact = TurnTicks;
                    return;
                }
                rancorTurning = false;
                visualDistanceTravelled = 0f;
                initialTravelDistance = (aim - at).MagnitudeHorizontal();
                PrepareTrackingSegment(at, aim);
                return;
            }
            base.ImpactSomething();
        }

        private bool TryGetVisualArc(Vector3 remainingTravel, float remainingDistance,
            out Vector3 perpendicular, out float progress, out float totalDistance, out float amplitude)
        {
            perpendicular = Vector3.zero;
            progress = 0f;
            totalDistance = visualDistanceTravelled + remainingDistance;
            amplitude = 0f;

            HomingSoulProjectileExtension extension = Extension;
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

            HomingSoulProjectileExtension extension = Extension;
            return target.Downed && extension != null && extension.retargetOnDowned;
        }

        private bool TryRetarget(Pawn previousTarget, Vector3 projectilePosition)
        {
            HomingSoulProjectileExtension extension = Extension;
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

            for (int index = 0; index < potentialTargets.Count; index++)
            {
                Pawn candidate = potentialTargets[index].Thing as Pawn;
                if (!ValidReplacementTarget(candidate, previousTarget))
                {
                    continue;
                }

                float distanceSquared = (candidate.DrawPos - projectilePosition).Yto0().sqrMagnitude;
                if (distanceSquared >= closestDistanceSquared ||
                    !GenSight.LineOfSight(projectileCell, candidate.Position, Map))
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
            if (candidate == null || candidate == previousTarget || candidate.Dead || candidate.Downed ||
                !candidate.Spawned || candidate.Map != Map)
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
            if (verb != null && verb.EffectiveRange > 0f)
            {
                return verb.EffectiveRange;
            }

            return Extension?.trackingRange ?? 0f;
        }

        private void PrepareTrackingSegment(Vector3 start, Vector3 end)
        {
            segmentOrigin = start;
            destination = end;
            float distance = (destination - segmentOrigin).MagnitudeHorizontal();
            segmentTicksTotal = Mathf.Max(1, Mathf.CeilToInt(distance / def.projectile.SpeedTilesPerTick));
            ticksToImpact = segmentTicksTotal;
        }

        private HomingSoulProjectileExtension cachedExtension;
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
        private bool rancorSoul;
        private bool rancorOutward;
        private bool rancorTurning;
        private Vector3 turnControlA, turnControlB;
        private int rancorLaunchTick;
        private Vector3 rancorAim;
        private Vector3 previousVisualPosition;
        // About 2.5 cells of path at a released soul's speed.
        private const int MaxTrailPoints = 14;
        private List<Vector3> trail = new List<Vector3>(MaxTrailPoints + 1);
    }
}
