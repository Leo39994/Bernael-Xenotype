using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class Gene_BernaelBlind : Gene
    {
        private Gamecomponent_PsychicSight GameComp => Current.Game.GetGamePsychicSightComp();

        public override void PostAdd()
        {
            base.PostAdd();
            if (pawn.health == null) return;

            Gamecomponent_PsychicSight sightComp = GameComp;
            if (sightComp == null)
            {
                Log.Error("The psychic sight game component was missing, this isn't supposed to happen.");
            }
            else
            {
                sightComp.psychicSeers ??= new HashSet<Pawn>();
                sightComp.psychicSeers.Add(pawn);
            }

            // GetNotMissingParts reflects this pawn's current anatomy. Copy the eyes before adding
            // hediffs because AddHediff invalidates the part cache used by the enumerable.
            List<BodyPartRecord> eyes = pawn.health.hediffSet.GetNotMissingParts()
                .Where(bodyPart => bodyPart.def.defName.ToLowerInvariant().Contains("eye"))
                .ToList();

            foreach (BodyPartRecord eye in eyes)
            {
                if (pawn.health.hediffSet.HasDirectlyAddedPartFor(eye)) continue;
                if (pawn.health.hediffSet.hediffs.Any(hediff =>
                    hediff.def == BernaelDefOf.BX_Blindness && hediff.Part == eye)) continue;

                pawn.health.AddHediff(HediffMaker.MakeHediff(BernaelDefOf.BX_Blindness, pawn, eye));
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();
            if (pawn.health == null) return;

            Gamecomponent_PsychicSight sightComp = GameComp;
            if (sightComp?.psychicSeers != null)
            {
                sightComp.psychicSeers.Remove(pawn);
            }

            for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (hediff.def == BernaelDefOf.BX_Blindness)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }
    }
}
