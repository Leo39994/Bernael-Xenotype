using Verse;
using Verse.AI.Group;

namespace Bernael_Xenotype
{
    public class LordJob_BogeymanKidnap : LordJob
    {
        public override bool GuiltyOnDowned => true;

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            LordToil_BogeymanKidnap toilKidnap = new LordToil_BogeymanKidnap();
            stateGraph.AddToil(toilKidnap);
            stateGraph.StartingToil = toilKidnap;

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