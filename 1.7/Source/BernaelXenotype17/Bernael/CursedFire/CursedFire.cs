using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class CursedFire : Fire
    {
        private int ticksSinceSpawn;
        private Sustainer sustainer;

        public override string Label
        {
            get
            {
                if (parent != null)
                {
                    return "BX_CursedFireOn".Translate(parent.LabelCap, parent);
                }
                return def.label;
            }
        }

        public override string InspectStringAddon => "BX_CursedBurning".Translate() + " (" + "FireSizeLower".Translate((fireSize * 100f).ToString("F0")) + ")";

        public override int UpdateRateTicks => 15;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksSinceSpawn, "ticksSinceSpawn", 0);
            Scribe_Values.Look(ref fireSize, "fireSize", 0.1f);
            Scribe_References.Look(ref instigator, "instigator");
        }
        
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad && parent == null && Spawned)
            {
                MergeExistingFires(map);
            }
        }

        protected override void TickInterval(int delta)
        {
            if (parent == null && Spawned)
            {
                MergeExistingFires(Map);
            }
            ticksSinceSpawn += delta;
            float maxLifetime = 600f + (fireSize * 3000f); 
            if (ticksSinceSpawn >= maxLifetime)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }
            if ((sustainer == null || sustainer.Ended) && !Position.Fogged(Map))
            {
                SoundInfo info = SoundInfo.InMap(new TargetInfo(Position, Map), MaintenanceType.PerTick);
                sustainer = SustainerAggregatorUtility.AggregateOrSpawnSustainerFor(this, SoundDefOf.FireBurning, info);
            }
            if (parent == null && Spawned)
            {
                Pawn steppingPawn = Map.thingGrid.ThingAt<Pawn>(Position);
                if (steppingPawn != null && steppingPawn.Spawned && !steppingPawn.Dead)
                {
                    if (!steppingPawn.HasAttachment(def))
                    {
                        CursedFire pawnFire = (CursedFire)ThingMaker.MakeThing(def);
                        pawnFire.fireSize = fireSize;
                        pawnFire.instigator = instigator;
                        pawnFire.AttachTo(steppingPawn);
                        GenSpawn.Spawn(pawnFire, Position, Map, Rot4.North);
                    }
                }
            }
            if (parent != null && this.IsHashIntervalTick(150, delta))
            {
                DoCursedDamage(parent);
            }
            if (Spawned)
            {
                float coldPower = fireSize * -80f;
                GenTemperature.PushHeat(Position, Map, coldPower);
                fireSize += 0.0002f * delta;
                if (fireSize > MaxFireSize) 
                {
                    fireSize = MaxFireSize;
                }
            }
        }

        private void MergeExistingFires(Map map)
        {
            bool hasMerged = false;
            List<Thing> thingList = Position.GetThingList(map);
            for (int i = thingList.Count - 1; i >= 0; i--)
            {
                Thing thing = thingList[i];
                if (thing is Fire otherFire && otherFire != this && otherFire.parent == null)
                {
                    fireSize += otherFire.fireSize;
                    if (fireSize > MaxFireSize)
                    {
                        fireSize = MaxFireSize;
                    }
                    otherFire.Destroy(DestroyMode.Vanish);
                    hasMerged = true;
                }
            }
            if (hasMerged)
            {
                ticksSinceSpawn = 0;
            }
        }

        private void DoCursedDamage(Thing target)
        {
            int damageAmount = GenMath.RoundRandom(Mathf.Clamp(0.0125f + 0.0036f * fireSize, 0.0125f, 0.05f) * 150f);
            if (damageAmount < 1)
            {
                damageAmount = 1;
            }
            if (target is Pawn pawn)
            {
                DamageInfo dinfo = new(DamageDefOf.Frostbite, damageAmount, 0f, -1f, instigator ?? this);
                dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
                pawn.TakeDamage(dinfo);
                if (pawn.apparel != null && pawn.apparel.WornApparel.TryRandomElement(out var apparelResult))
                {
                    apparelResult.TakeDamage(new DamageInfo(DamageDefOf.Frostbite, damageAmount, 0f, -1f, instigator ?? this));
                }
            }
            else
            {
                target.TakeDamage(new DamageInfo(DamageDefOf.Frostbite, damageAmount, 0f, -1f, instigator ?? this));
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            sustainer?.End();
            base.DeSpawn(mode);
        }
    }
}