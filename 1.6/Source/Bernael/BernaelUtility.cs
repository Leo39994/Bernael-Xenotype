using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public static class BernaelUtility
    {

        private static HediffDef _soulDrainedHediff;

        public static HediffDef cachedSoulDrainedHediff
        {
            get
            {
                if (_soulDrainedHediff == null)
                {
                    AbilityDef abilityDef = BernaelDefOf.BX_SoulFeeding;
                    if (abilityDef != null)
                    {
                        foreach (AbilityCompProperties comp in abilityDef.comps)
                        {
                            if (comp is not CompProperties_AbilitySoulDrain soulDrainComp) continue;
                            _soulDrainedHediff = soulDrainComp.hediffToGiveTarget;
                            break;
                        }
                        if (_soulDrainedHediff == null)
                        {
                            _soulDrainedHediff = BernaelDefOf.BX_SoulDrained;
                        }
                    }
                }
                return _soulDrainedHediff;
            }
        }


    public static void TurnIntoXenotype(this Pawn pawn, XenotypeDef xenotypeDef)
        {
            for (int i = pawn.genes.GenesListForReading.Count - 1; i >= 0; i--)
            {
                Gene gene = pawn.genes.GenesListForReading[i];
                pawn.genes.RemoveGene(gene);
            }

            if (pawn.Map != null)
            {
                FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastSkipInnerExit);
            }

            pawn.genes.SetXenotype(xenotypeDef);
        }

        public static object GetGraceOutcomeDoer(this ThingDef thingDef)
        {
            if (!ModsConfig.IsActive("EBSG.Framework") || thingDef.ingestible == null || thingDef.ingestible.outcomeDoers.NullOrEmpty()) return null;
            Type type = AccessTools.TypeByName("EBSGFramework.IngestionOutcomeDoer_OffsetResource");
            if (type == null) return type;
            foreach (IngestionOutcomeDoer ingestionOutcomeDoer in thingDef.ingestible.outcomeDoers)
            {
                if (ingestionOutcomeDoer.GetType() != type || (GeneDef)(AccessTools.Field(type, "mainResourceGene").GetValue(ingestionOutcomeDoer)) != BernaelDefOf.GS_Grace_New) continue;
                return ingestionOutcomeDoer;
            }
            return null;
        }

        public static Gamecomponent_PsychicSight GetGamePsychicSightComp(this Game game)
        {
            var comp = game.GetComponent<Gamecomponent_PsychicSight>();

            if (comp == null)
            {
                comp = new Gamecomponent_PsychicSight(game);
                game.components.Add(comp);
            }
            return comp;
        }
    }
}
