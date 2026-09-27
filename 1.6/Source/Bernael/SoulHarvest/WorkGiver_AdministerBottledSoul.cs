using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class WorkGiver_AdministerBottledSoul : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t is not Pawn pawn2 || pawn2 == pawn)
            {
                return false;
            }
            Pawn_GeneTracker genes = pawn2.genes;
            Gene_Soul gene_Soul = genes?.GetFirstGeneOfType<Gene_Soul>();
            if (gene_Soul == null)
            {
                return false;
            }
            if (gene_Soul.ValuePercent >= 0.95f)
            {
                return false;
            }
            if (!forced && gene_Soul.Value >= 0.25f)
            {
                return false;
            }
            if (!FeedPatientUtility.ShouldBeFed(pawn2))
            {
                return false;
            }
            if (!gene_Soul.ShouldConsumeSoulNow())
            {
                JobFailReason.Is("MB_NotAllowedBottledSoul".Translate());
                return false;
            }
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            bool bottledSoulsAllowed = gene_Soul.bottledSoulsAllowed;
            bool bottledSoulExists = true;
            if (bottledSoulsAllowed)
            {
                
                if (GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(BernaelDefOf.BX_BottledSoul), PathEndMode.OnCell, TraverseParms.For(pawn), 9999f, pack => !pack.IsForbidden(pawn) && pawn.CanReserve(pack)) == null)
                {
                    bottledSoulExists = false;
                }
            }
            if (bottledSoulsAllowed && !bottledSoulExists)
            {
                JobFailReason.Is("NoIngredient".Translate(BernaelDefOf.BX_BottledSoul));
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn t2 = (Pawn)t;
            Pawn_GeneTracker genes = t2.genes;
            Gene_Soul gene_Soul = genes?.GetFirstGeneOfType<Gene_Soul>();
            Thing thing = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, pawn.Map.listerThings.ThingsOfDef(BernaelDefOf.BX_BottledSoul), PathEndMode.OnCell, TraverseParms.For(pawn), 9999f, t3 => pawn.CanReserve(t3) && !t3.IsForbidden(pawn));
            if (thing != null)
            {
                Job job = JobMaker.MakeJob(JobDefOf.FeedPatient, thing, t2);
                job.count = 1;
                return job;
            }
            return null;
        }

        private const float MinLevelForFeedingHemogenUnforced = 0.25f;

        private const float HemogenPctMax = 0.95f;
    }
}
