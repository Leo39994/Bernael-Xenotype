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

            // HasPsylink is cached per tick, so a second Gene_Hexmaster added in the same tick would see
            // no psylink and raise it to level 2; the hediff set is read directly instead.
            if (pawn.GetMainPsylinkSource() == null && ModsConfig.RoyaltyActive)
                pawn.ChangePsylinkLevel(1);
        }
    }
}
