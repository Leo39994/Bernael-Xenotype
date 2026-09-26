using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class Hediff_BernaelToNephilim : HediffWithComps
    {
        public int daysTillTransform = 10;
        public int cooldown = 0;
        public bool available = true;

        public override string LabelInBrackets => base.LabelInBrackets + (cooldown > 0 ? $"{cooldown / 60}s" : "BX_Mutation_CooldownFinished".Translate().ToString()) + ", " + "BX_Mutation_MutatesIn".Translate(daysTillTransform);

        public override void Tick()
        {
            base.Tick();
            cooldown--;
            if (cooldown > 0) return;
            available = true;
            if (daysTillTransform > 0) return;
            pawn.health.RemoveHediff(this);
        }

        public override void Notify_IngestedThing(Thing thing, int amount)
        {
            base.Notify_IngestedThing(thing, amount);
            if (!available || !ModsConfig.IsActive("Sov.Nephilim")) return;

            if (thing.def.GetGraceOutcomeDoer() == null) return;

            available = false;
            cooldown = 60000;
            daysTillTransform -= 1;

            var memory = pawn.needs.mood.thoughts.memories;
            if (memory != null)
                memory.TryGainMemory(BernaelDefOf.BX_ConsumedGraceThoughtMood);

            if (pawn.Map != null)
                FleckMaker.Static(pawn.DrawPos, pawn.Map, FleckDefOf.PsycastAreaEffect);

            if (daysTillTransform > 0) return;
            cooldown = 0;
            pawn.TurnIntoXenotype(BernaelDefOf.GS_Nephilim);

            if (pawn.Map != null)
                FleckMaker.Static(pawn.DrawPos, pawn.Map, FleckDefOf.PsycastSkipFlashEntry);

            if (memory != null && memory.GetFirstMemoryOfDef(BernaelDefOf.BX_ConsumedGraceThoughtMood) != null)
                memory.RemoveMemoriesOfDef(BernaelDefOf.BX_ConsumedGraceThoughtMood);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref daysTillTransform, "BX_DaysTillTransform");
            Scribe_Values.Look(ref cooldown, "BX_Cooldown");
            Scribe_Values.Look(ref available, "BX_Available");
        }
    }
}
