using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    [DefOf]
    public class BernaelDefOf
    {
        public BernaelDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BernaelDefOf));
        }

        public static GeneDef BX_DarkSpeech;
        public static GeneDef BX_AbyssalReach;
        public static GeneDef BX_DepravedHead;
        public static TaleDef BX_ConsumedGrace;
        public static XenotypeDef BX_Bernael;
        public static HediffDef BX_Blindness;
        public static HediffDef BX_SoulDrained;
        public static HediffDef BX_SoulCraving;
        public static HediffDef BX_BabyBond_Feeder;
        public static HediffDef BX_BabyBond_Victim;
        public static AbilityDef BX_SoulFeeding;
        public static ThoughtDef BX_ConsumedGraceThoughtMood;
        public static ThoughtDef BX_HeardDarkSpeech;
        public static ThoughtDef BX_FedOn;
        public static ThoughtDef BX_FedOn_Social;

        public static HistoryEventDef BX_ExtractedSoul;

        public static StatDef BX_SoulGainFactor;
        public static GeneDef BX_SoulStarved;
        public static GeneDef BX_SoulFeeder;

        public static ThingDef BX_BottledSoul;
        public static ThingDef BX_LifeVanquisher;
        public static RecipeDef BX_ExtractSoul;

        public static JobDef BX_PrisonerSoulFeed;

        [MayRequire("Sov.Nephilim")]
        public static GeneDef GS_Grace_New;
        [MayRequire("Sov.Nephilim")]
        public static GeneDef BX_Grace_Galling;
        [MayRequire("Sov.Nephilim")]
        public static GeneDef BX_GracePendulum;

        [MayRequire("Sov.Nephilim")]
        public static XenotypeDef GS_Nephilim;

        [MayRequire("Sov.Nephilim")]
        public static HediffDef BX_Mutation_Hediff;
    }
}
