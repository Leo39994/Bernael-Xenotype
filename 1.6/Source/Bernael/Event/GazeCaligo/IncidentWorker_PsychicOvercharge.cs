using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class IncidentWorker_PsychicOvercharge : IncidentWorker
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
                if (pawn.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier))
                {
                    pawn.health.AddHediff(BernaelDefOf.BX_PsychicOverchargeHediff);
                }
            }

            SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, LookTargets.Invalid);
            return true;
        }
    }
}