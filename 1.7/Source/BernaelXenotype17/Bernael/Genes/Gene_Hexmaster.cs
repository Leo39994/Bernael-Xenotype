using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class Gene_Hexmaster : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();

            if (!pawn.HasPsylink && ModsConfig.RoyaltyActive)
                pawn.ChangePsylinkLevel(1);
        }
    }
}
