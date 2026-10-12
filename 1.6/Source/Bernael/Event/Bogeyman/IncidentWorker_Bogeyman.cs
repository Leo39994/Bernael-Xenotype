using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace Bernael_Xenotype
{
    public class IncidentWorker_MakeInitialBogeyman : IncidentWorker_MakeGameCondition
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            return HasBabyOrChildVictim(map) && RCellFinder.TryFindRandomPawnEntryCell(out parms.spawnCenter, map, CellFinder.EdgeRoadChance_Hostile);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
	    {
	    	GameConditionManager gameConditionManager = parms.target.GameConditionManager;
	    	GameCondition cond = GameConditionMaker.MakeConditionPermanent(GetGameConditionDef(parms));
	    	gameConditionManager.RegisterCondition(cond);
	    	return true;
	    }

        private bool HasBabyOrChildVictim(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned.Any(p => p.DevelopmentalStage.Baby() || p.DevelopmentalStage.Child());
        }
    }

    public class IncidentWorker_Bogeyman : IncidentWorker
    {
        private const float SmokeRadius = 3.5f;
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
		    IntVec3 result = parms.spawnCenter;
		    if (!result.IsValid && !RCellFinder.TryFindRandomPawnEntryCell(out result, map, CellFinder.EdgeRoadChance_Hostile))
		    {
		    	return false;
		    }

            IntVec3 spawnCell = TryFindSpawnCell(map);
            if (!spawnCell.IsValid) return false;

            PawnKindDef bogeymanKind = BernaelDefOf.BX_Bernael_Warlock; // PawnKindDef
            Faction faction = FactionUtility.DefaultFactionFrom(bogeymanKind.defaultFactionDef) ?? Find.FactionManager.RandomEnemyFaction();

            Pawn bogeyman = PawnGenerator.GeneratePawn(bogeymanKind, faction);
            GenSpawn.Spawn(bogeyman, spawnCell, map, WipeMode.Vanish);

            FleckMaker.Static(spawnCell, map, FleckDefOf.PsycastSkipInnerExit);
            FleckMaker.Static(spawnCell, map, FleckDefOf.PsycastSkipOuterRingExit);
            SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(spawnCell, map));
            GenExplosion.DoExplosion(spawnCell, map, SmokeRadius, DamageDefOf.Extinguish, null, -1, -1f, null, null, null, null, BernaelDefOf.BX_WingBurstSmoke, 1, 1, GasType.BlindSmoke);

            LordMaker.MakeNewLord(faction, new LordJob_BogeymanKidnap(), map, new[] { bogeyman });

            SendStandardLetter(parms, bogeyman, bogeyman.Named("PAWN"));
            return true;
        }

        private IntVec3 TryFindSpawnCell(Map map)
        {
            Building_Bed babyBed = map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().FirstOrDefault(b => (b.def.building.bed_crib || b.def.building.bed_maxBodySize < 1f) && b.AnyUnownedSleepingSlot);
            if (babyBed != null)
            {
                return CellFinder.RandomClosewalkCellNear(babyBed.Position, map, 2);
            }

            Pawn targetChild = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.DevelopmentalStage.Baby() || p.DevelopmentalStage.Child());
            if (targetChild != null)
            {
                return CellFinder.RandomClosewalkCellNear(targetChild.Position, map, 2);
            }

            return IntVec3.Invalid;
        }
    }
}