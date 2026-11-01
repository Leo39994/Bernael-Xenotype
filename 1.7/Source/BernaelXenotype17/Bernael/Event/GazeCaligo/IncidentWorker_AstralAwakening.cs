using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class IncidentWorker_AstralAwakening : IncidentWorker
    {
        private static readonly List<string> tmpPawnNames = new List<string>();
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (parms.target is not Map map) return false;

            tmpPawnNames.Clear();

            IReadOnlyList<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0) return false;

            List<Pawn> candidates = [];
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn == null || pawn.Dead) continue;

                // Check Psylink
                if (pawn.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier)) continue;

                // Check Psychic Sensitivity is above average.
                if (pawn.GetStatValue(StatDefOf.PsychicSensitivity) > 1.0f)
                {
                    candidates.Add(pawn);
                }
            }
            
            if (candidates.Count > 0)
            {
                // Randomly select 1 to 2 affected pawns.
                int targetCount = Mathf.Min(candidates.Count, Rand.RangeInclusive(1, 2));
                candidates.Shuffle();

                for (int i = 0; i < targetCount; i++)
                {
                    Pawn pawn = candidates[i];

                    // Give Lv1 Psylink Hediff
                    Hediff_Psylink psylink = (Hediff_Psylink)HediffMaker.MakeHediff(HediffDefOf.PsychicAmplifier, pawn, pawn.health.hediffSet.GetBrain());
                    psylink.Severity = 1f;
                    pawn.health.AddHediff(psylink);

                    tmpPawnNames.Add(pawn.NameShortColored.Resolve());
                }

                TaggedString pawnNames = tmpPawnNames.ToCommaList(useAnd: true);
                TaggedString letterText = def.letterText.Formatted(pawnNames.Named("PAWNS"));
                SendStandardLetter(def.letterLabel, letterText, def.letterDef, parms, LookTargets.Invalid);
            }

            return true;
        }
    }
}