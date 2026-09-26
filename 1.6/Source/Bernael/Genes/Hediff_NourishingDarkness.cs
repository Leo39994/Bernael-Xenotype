using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bernael_Xenotype
{
    public class Hediff_NourishingDarkness : HediffWithComps
    {
        public override void Tick()
        {
            base.Tick();
            if (!pawn.IsHashIntervalTick(180)) return;

            if (pawn.Map == null || pawn.Map.glowGrid == null)
            {
                this.Severity = 1;
                return;
            }

            IntVec3 pawnPosition = pawn.Position;

            if (pawn.Map.glowGrid.GroundGlowAt(pawnPosition) < 0.50 || DarklightUtility.IsDarklightAt(pawnPosition, pawn.Map))
                this.Severity = 1;
            else this.Severity = 2;
        }
    }
}
