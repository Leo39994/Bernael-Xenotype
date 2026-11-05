using Verse;
using Verse.AI.Group;

namespace Bernael_Xenotype
{
    public class LordJob_Bogeyman : LordJob
    {
        public override bool GuiltyOnDowned => true;

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            LordToil_Bogeyman toil = new LordToil_Bogeyman();
            stateGraph.AddToil(toil);
            stateGraph.StartingToil = toil;

            return stateGraph;
        }

        public override bool CanOpenAnyDoor(Pawn p)
        {
            return true;
        }

	    public override void ExposeData()
	    {
	    }
    }
}