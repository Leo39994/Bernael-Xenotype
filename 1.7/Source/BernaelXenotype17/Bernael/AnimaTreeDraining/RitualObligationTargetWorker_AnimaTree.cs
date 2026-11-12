using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class RitualObligationTargetWorker_AnimaTree : RitualObligationTargetFilter
    {
        public RitualObligationTargetWorker_AnimaTree() : base()
        {
        }

        public RitualObligationTargetWorker_AnimaTree(RitualObligationTargetFilterDef def) : base(def)
        {
        }

        public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map)
        {
            foreach (Thing plant in map.listerThings.ThingsOfDef(ThingDefOf.Plant_TreeAnima))
            {
                if (plant.Spawned && !plant.Destroyed)
                {
                    yield return plant;
                }
            }
        }

        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {
            Thing thing = target.Thing;
            if (thing == null || thing.def != ThingDefOf.Plant_TreeAnima || thing.Destroyed)
            {
                return false;
            }
            CompPsylinkable compPsylinkable = thing.TryGetComp<CompPsylinkable>();
            if (compPsylinkable == null)
            {
                return false;
            }
            bool hasValidPawn = false;
            foreach (Pawn item in target.Map.mapPawns.FreeColonistsSpawned)
            {
                if (item.Dead) continue;
                if (item.GetPsylinkLevel() >= item.GetMaxPsylinkLevel()) continue;
                Gene gene = item?.genes?.GetGene(BernaelDefOf.BX_Hexmaster);
                if (gene != null && gene.Active)
                {
                    hasValidPawn = true;
                    break;
                }
            }
            if (!hasValidPawn)
            {
                return "BX_RitualTargetAnimaTreeMustBeCapableOfHexmaster".Translate();
            }
            return true;
        }

        public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
        {
            yield return "RitualTargetAnimaTreeInfo".Translate();
        }
    }
}