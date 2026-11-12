using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class PawnFlyer_WingBurst : PawnFlyer
    {
        private static readonly float smokeRadius = 3.5f;
        // The streamer behind the flying caster: its recent draw positions, newest first.
        // Visual only and not saved; a reloaded flight grows a new one within a few ticks.
        private const int TrailPoints = 30;
        private const float TrailLength = 3.8f;
        private readonly List<Vector3> trail = new List<Vector3>(TrailPoints + 1);
        private Mesh trailMesh;

        private Vector3 Direction => (DestinationPos - startVec).Yto0().normalized;

        // The game draws the flying pawn itself (PawnFlyer.DynamicDrawPhaseAt),
        // so the caster stays in view for the whole leap; base.DrawAt adds its shadow.
        // The streamer is drawn after it.
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (trail.Count == 0 || !WingBurstVisuals.Ready()) return;
            // It tears loose over the first ticks of the leap and thins as the caster comes down.
            float opacity = WingBurstVisuals.Ease(ticksFlying / 6f) *
                (1f - 0.60f * WingBurstVisuals.Ease((ticksFlying - (ticksFlightTime - 10)) / 10f));
            trailMesh ??= new Mesh { name = "Wing Burst trail" };
            WingBurstVisuals.DrawTrail(trailMesh, DrawPos, trail, ticksFlying / 60f, thingIDNumber, opacity);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            if (trailMesh != null) Object.Destroy(trailMesh);
            trailMesh = null;
        }

        protected override void TickInterval(int delta)
        {
            if (ticksFlying < ticksFlightTime)
            {
                WingBurstMapComponent smoke = Map.GetComponent<WingBurstMapComponent>();
                Vector3 direction = Direction;
                if (ticksFlying == 0) smoke.TakeOff(startVec, direction, thingIDNumber);
                if (ticksFlying % 3 == 0) smoke.Wake(DrawPos, direction, thingIDNumber + ticksFlying);
            }
            base.TickInterval(delta);
            if (!Destroyed) Record(DrawPos);
        }

        private void Record(Vector3 at)
        {
            if (trail.Count > 0 && (trail[0] - at).Yto0().sqrMagnitude < 0.0025f) return;
            trail.Insert(0, at);
            float length = 0;
            for (int i = 1; i < trail.Count; i++)
            {
                length += (trail[i - 1] - trail[i]).Yto0().magnitude;
                if (length >= TrailLength || i >= TrailPoints - 1)
                {
                    // Keep the point that reaches the full length; drop the older ones.
                    trail.RemoveRange(i + 1, trail.Count - i - 1);
                    break;
                }
            }
        }

        protected override void RespawnPawn()
        {
            IntVec3 landCell = DestinationPos.ToIntVec3();
            Map targetMap = base.Map;
            Pawn pawn = FlyingPawn;
            Vector3 direction = Direction;

            base.RespawnPawn();

            if (!landCell.IsValid || targetMap == null) return;

            WingBurstMapComponent smoke = targetMap.GetComponent<WingBurstMapComponent>();
            smoke.Shed(trail, direction, thingIDNumber);
            smoke.Land(landCell.ToVector3Shifted(), direction, thingIDNumber);

            // The dark landing cloud replaces the explosion's own cell flashes; damage, gas and smoke are unchanged.
            GenExplosion.DoExplosion(
                center: landCell,
                map: targetMap,
                radius: smokeRadius,
                damType: DamageDefOf.Smoke,
                instigator: pawn,
                damAmount: 0,
                armorPenetration: -1f,
                explosionSound: null,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: BernaelDefOf.BX_WingBurstSmoke,
                postExplosionSpawnChance: 1f,
                postExplosionSpawnThingCount: 1,
                postExplosionGasType: GasType.BlindSmoke,
                doVisualEffects: false
            );
        }
    }
}
