using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class IncidentWorker_PsychicRupture : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (parms.target is not Map map) return false;
            
            IReadOnlyList<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0) return false;

            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn == null || pawn.Dead) continue;

                // Check Psylink
                if (!pawn.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier))
                {
                    // Randomly select mild, moderate, and severe cases of mental breakdown (1 = Minor, 2 = Major, 3 = Extreme)
                    MentalBreakIntensity randomIntensity = (MentalBreakIntensity)Rand.RangeInclusive(1, 3);
                    if (pawn.mindState?.mentalBreaker != null && pawn.mindState.mentalBreaker.TryGetRandomMentalBreak(randomIntensity, out MentalBreakDef breakDef))
                        pawn.mindState.mentalStateHandler.TryStartMentalState(breakDef.mentalState, "Psychic Rupture", forced: true, forceWake: false, causedByMood: false);
                }
            }

            SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, LookTargets.Invalid);
            return true;
        }
    }
}