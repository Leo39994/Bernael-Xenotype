using RimWorld;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    public class Hediff_ReapersEcstasy : HediffWithComps
    {
        private int nextDecayTick = -1;
        private ReaperModExt modExt;

        public ReaperModExt ModExt => modExt ??= def.GetModExtension<ReaperModExt>();

        private int DecayTickInterval => ModExt?.decayTickInterval ?? 2500;

        private int StackCount => Mathf.Max(0, Mathf.FloorToInt(Severity - 1f));

        public override bool Visible => StackCount > 0 && base.Visible;

        public override string LabelInBrackets => StackCount.ToString();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextDecayTick, "BX_ReaperEcstasyNextDecayTick", -1);
        }

        public override void Tick()
        {
            base.Tick();
            if (Severity <= 1f)
            {
                nextDecayTick = -1;
                return;
            }
            int ticksGame = Find.TickManager.TicksGame;
            if (nextDecayTick < 0)
            {
                nextDecayTick = ticksGame + DecayTickInterval;
            }
            else if (ticksGame >= nextDecayTick)
            {
                Severity -= 1f;
                nextDecayTick = Severity > 1f ? ticksGame + DecayTickInterval : -1;
            }
        }

        public override void Notify_KilledPawn(Pawn victim, DamageInfo? dinfo)
        {
            base.Notify_KilledPawn(victim, dinfo);
            ReaperModExt reaperModExt = ModExt;
            if (reaperModExt == null || !victim.HostileTo(pawn.Faction)) return;
            if (pawn?.genes?.GetGene(BernaelDefOf.BX_SoulStarved) is not Gene_Soul gene_Soul) return;

            gene_Soul.Value += reaperModExt.killRefillPct / 100f * gene_Soul.Max;
            Severity += 1f;
            nextDecayTick = Find.TickManager.TicksGame + DecayTickInterval;
        }
    }
}
