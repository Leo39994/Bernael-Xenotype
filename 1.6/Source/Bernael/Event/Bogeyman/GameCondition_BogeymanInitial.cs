using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class GameCondition_BogeymanInitial : GameCondition
    {
    	private const int DurationUntilFullBogeyman = 240000; // 4 days
    	private const int BeforeFullBogeyman = 5000; // 2 hour
		private bool raidTriggered = false;

    	public override void GameConditionTick()
    	{
			IncidentDef raidIncident = IncidentDefOf.RaidEnemy;
			IncidentParms raidParms = StorytellerUtility.DefaultParmsNow(raidIncident.category, SingleMap);
			PawnKindDef bogeymanKind = BernaelDefOf.BX_Bernael_Warlock;
			raidParms.faction = FactionUtility.DefaultFactionFrom(bogeymanKind.defaultFactionDef) ?? Find.FactionManager.RandomEnemyFaction();

			if(!HasBabyOrChildVictim(SingleMap))
			{
				raidIncident.Worker.TryExecute(raidParms);
    			End();
			}

    		if (Find.TickManager.TicksGame > startTick + DurationUntilFullBogeyman - BeforeFullBogeyman && raidTriggered == false)
    		{
				raidTriggered = true;
            	if (raidParms.points <= 0f)
            	{
            	    raidParms.points = StorytellerUtility.DefaultThreatPointsNow(SingleMap) / 2;
            	}

            	raidIncident.Worker.TryExecute(raidParms);
			}

    		if (Find.TickManager.TicksGame > startTick + DurationUntilFullBogeyman)
    		{
    			IncidentDef bogeyman = BernaelDefOf.BX_Bogeyman;
    			IncidentParms parms = StorytellerUtility.DefaultParmsNow(bogeyman.category, SingleMap);
    			End();
    			bogeyman.Worker.TryExecute(parms);
    		}
    	}

        private bool HasBabyOrChildVictim(Map map)
		{
		    if (map.mapPawns.FreeColonistsSpawned.Any(p => p.DevelopmentalStage.Baby() || p.DevelopmentalStage.Child()))
		    {
		        return true;
		    }

		    List<Pawn> spawnedColonists = map.mapPawns.FreeColonistsSpawned;
		    for (int i = 0; i < spawnedColonists.Count; i++)
		    {
		        Pawn colonist = spawnedColonists[i];
		        if (colonist.carryTracker?.CarriedThing is Pawn carriedPawn)
		        {
		            if (carriedPawn.IsColonist && (carriedPawn.DevelopmentalStage.Baby() || carriedPawn.DevelopmentalStage.Child()))
		            {
		                return true;
		            }
		        }
		    }

		    return false;
		}

		public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref raidTriggered, "raidTriggered", false);
        }
    }
}