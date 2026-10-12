using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class PawnFlyer_WingBurst : PawnFlyer
    {
        private static readonly float smokeRadius = 3.5f;

        protected override void RespawnPawn()
        {
            IntVec3 landCell = base.Position;
            Map targetMap = base.Map;

            base.RespawnPawn();

            if (!landCell.IsValid || targetMap == null) return;

            GenExplosion.DoExplosion(
                center: landCell,
                map: targetMap,
                radius: smokeRadius,
                damType: DamageDefOf.Extinguish, // Extinguish Fire
                instigator: FlyingPawn,
                damAmount: -1,
                armorPenetration: -1f,
                explosionSound: null,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: BernaelDefOf.BX_WingBurstSmoke,
                postExplosionSpawnChance: 1f,
                postExplosionSpawnThingCount: 1,
                postExplosionGasType: GasType.BlindSmoke
            );
        }
    }
}