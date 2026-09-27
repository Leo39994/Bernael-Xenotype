using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class Gene_Nihilism : Gene
    {
        private bool canDoubleResistance = true;

        public override void Tick()
        {
            base.Tick();
            if (!pawn.IsHashIntervalTick(180)) return;

            if (!pawn.IsPrisoner && !canDoubleResistance)
            {
                canDoubleResistance = true;
            }

            if (!pawn.IsPrisoner || !canDoubleResistance) return;

            Pawn_GuestTracker pawnRes = pawn.guest;
            if (pawnRes != null)
            {
                pawnRes.resistance *= 2;
            }
            canDoubleResistance = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref canDoubleResistance, "BX_CanDoubleResistance");
        }
    }
}
