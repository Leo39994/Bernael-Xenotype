using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class Gas_WingBurst : Gas
    {
        private int duration;
        private float baseDensity = 1f;

        public float GasDensity
        {
            get
            {
                return baseDensity * Lifespan();
            }
            set
            {
                baseDensity = value;
            }
        }

        public override string Label => base.Label + " (" + GasDensity.ToStringPercent("0") + ")";

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // The haze settles in over its first half second, while the landing cloud bursts out over it.
            if (WingBurstVisuals.Ready())
                WingBurstVisuals.DrawLingeringSmoke(drawLoc, GasDensity, thingIDNumber, WingBurstVisuals.Ease(AgeTicks / 30f));
            else base.DrawAt(drawLoc, flip);
        }

        // Ticks since the smoke settled on this cell.
        private float AgeTicks => duration <= 0 ? 30f : duration - (destroyTick - Find.TickManager.TicksGame);

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                baseDensity = 1f;
                duration = destroyTick - Find.TickManager.TicksGame;
            }
            // The vanilla gas layer leaves out blind smoke under this smoke (Patch_SectionLayer_Gas_ColorAt).
            map.mapDrawer.MapMeshDirty(Position, (ulong)MapMeshFlagDefOf.Gas);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = Map;
            IntVec3 cell = Position;
            base.DeSpawn(mode);
            map?.mapDrawer.MapMeshDirty(cell, (ulong)MapMeshFlagDefOf.Gas);
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (Destroyed || Map == null) return;

            List<Thing> thingList = Position.GetThingList(Map);
            for (int i = thingList.Count - 1; i >= 0; i--)
            {
                if (thingList[i] is Fire fire)
                {
                    fire.Destroy();
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref duration, "duration", 0);
            Scribe_Values.Look(ref baseDensity, "baseDensity", 1f);
        }

        private float Lifespan()
        {
            if (duration <= 0) return 0f;
            return Mathf.Clamp01((destroyTick - (float)Find.TickManager.TicksGame) / duration);
        }
    }
}
