using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class Gene_SoulDrain : Gene, IGeneResourceDrain
    {

        public Gene_Resource Resource
        {
            get
            {
                if (cachedSoulGene == null || !cachedSoulGene.Active)
                {
                    cachedSoulGene = pawn.genes.GetFirstGeneOfType<Gene_Soul>();
                }
                return cachedSoulGene;
            }
        }

        public bool CanOffset => Active && !pawn.Deathresting;

        public float ResourceLossPerDay => def.resourceLossPerDay;

        public Pawn Pawn => pawn;

        public string DisplayLabel => Label + " (" + "Gene".Translate() + ")";

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            SoulUtility.TickResourceDrainInterval(this, delta);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (!Active)
            {
                yield break;
            }
            foreach (Gizmo gizmo in SoulUtility.GetResourceDrainGizmos(this))
            {
                yield return gizmo;
            }
        }

        [Unsaved()]
        private Gene_Soul cachedSoulGene;

        private const float MinAgeForDrain = 3f;
    }
}
