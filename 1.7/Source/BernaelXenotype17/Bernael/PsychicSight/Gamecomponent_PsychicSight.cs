using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bernael_Xenotype
{
    public class Gamecomponent_PsychicSight : GameComponent
    {
        public HashSet<Pawn> psychicSeers = new HashSet<Pawn>();
        public Gamecomponent_PsychicSight(Game game)
        {

        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref psychicSeers, "BX_PsySeers", LookMode.Reference);
        }
    }
}
