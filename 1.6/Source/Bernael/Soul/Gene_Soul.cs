using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    public class Gene_Soul : Gene_Resource, IGeneResourceDrain
    {
        public Gene_Resource Resource => this;
        public Pawn Pawn => pawn;

        public bool CanOffset => Active && !pawn.Deathresting;

        public string DisplayLabel => Label + " (" + "Gene".Translate() + ")";

        public float ResourceLossPerDay => def.resourceLossPerDay;

        public override float InitialResourceMax => 1f;

        public override float MinLevelForAlert => 0.15f;

        public override float MaxLevelOffset => 0.1f;

        protected override Color BarColor => new ColorInt(118, 87, 145).ToColor;

        protected override Color BarHighlightColor => new ColorInt(99, 64, 114).ToColor;

        public override void PostAdd()
        {
            if (!ModLister.CheckBiotech("Hemogen"))
            {
                return;
            }
            base.PostAdd();
            Reset();
        }

        public override void Notify_IngestedThing(Thing thing, int numTaken)
        {
            if (!thing.def.IsMeat) return;
            IngestibleProperties ingestible = thing.def.ingestible;
            bool flag;
            if (ingestible == null)
            {
                flag = false;
            }
            else
            {
                ThingDef sourceDef = ingestible.sourceDef;
                bool? flag2;
                if (sourceDef == null)
                {
                    flag2 = null;
                }
                else
                {
                    RaceProperties race = sourceDef.race;
                    flag2 = ((race != null) ? new bool?(race.Humanlike) : null);
                }
                bool? flag3 = flag2;
                bool flag4 = true;
                flag = (flag3.GetValueOrDefault() == flag4 & flag3 != null);
            }
            if (flag)
            {
                SoulUtility.OffsetSoul(pawn, 0.0375f * thing.GetStatValue(StatDefOf.Nutrition) * numTaken);
            }
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            SoulUtility.TickResourceDrainInterval(this, delta);
        }

        public override void SetTargetValuePct(float val)
        {
            targetValue = Mathf.Clamp(val * Max, 0f, Max - MaxLevelOffset);
        }

        public bool ShouldConsumeSoulNow()
        {
            return Value < targetValue && bottledSoulsAllowed;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (!Active)
            {
                yield break;
            }
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            foreach (Gizmo gizmo2 in SoulUtility.GetResourceDrainGizmos(this))
            {
                yield return gizmo2;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref bottledSoulsAllowed, "soulBasicGemsAllowed", true);
        }

        public bool bottledSoulsAllowed = true;

    }
}
