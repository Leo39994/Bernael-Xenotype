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

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            baseDensity = 1f;
            duration = destroyTick - Find.TickManager.TicksGame;
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