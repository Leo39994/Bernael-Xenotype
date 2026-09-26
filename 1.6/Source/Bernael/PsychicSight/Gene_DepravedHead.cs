using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class Gene_DepravedHead : Gene
    {
        Gamecomponent_PsychicSight gameComp => Current.Game.GetGamePsychicSightComp();

        public override void PostAdd()
        {
            base.PostAdd();
            if (pawn.health == null) return;

            Gamecomponent_PsychicSight sightComp = gameComp;
            if (sightComp == null)
            {
                Log.Error("The psychic sight game component was missing, this isn't supposed to happen.");
            }
            else
            {
                if (sightComp.psychicSeers.NullOrEmpty())
                    sightComp.psychicSeers = new HashSet<Pawn>();
                sightComp.psychicSeers.Add(pawn);
            }


            // AllParts is the anatomy of the race, not the body of this pawn: it always lists both eyes,
            // even on a pawn that lost one. AddDirect refuses a part that is not in GetNotMissingParts,
            // logs a red error and adds nothing, so the pawn silently stayed sighted on that side.
            // The list is copied first because AddHediff dirties the cache GetNotMissingParts reads from.
            List<BodyPartRecord> eyes = pawn.health.hediffSet.GetNotMissingParts()
                .Where(bodyPart => bodyPart.def.defName.ToLowerInvariant().Contains("eye"))
                .ToList();

            foreach (BodyPartRecord eye in eyes)
            {
                if (pawn.health.hediffSet.HasDirectlyAddedPartFor(eye)) continue;
                pawn.health.AddHediff(HediffMaker.MakeHediff(BernaelDefOf.BX_Blindness, pawn, eye));
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();
            if (pawn.health == null) return;

            if (gameComp != null && !gameComp.psychicSeers.NullOrEmpty() && gameComp.psychicSeers.Contains(pawn))
                gameComp.psychicSeers.Remove(pawn);

            for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (hediff.def != BernaelDefOf.BX_Blindness) continue;
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}
