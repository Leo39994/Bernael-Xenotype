using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{

    public class ThoughtWorker_BabyBond_Mood : ThoughtWorker
    {
        private const float Miserable = 0.20f;
        private const float Sad = 0.40f;
        private const float Neutral = 0.60f;
        private const float Content = 0.80f;

        protected override ThoughtState CurrentStateInternal(Pawn feeder)
        {
            HediffComp_BabyBond_Feeder hediffComp = BabyBondUtility.GetFeederComp(feeder);
            if (hediffComp == null || hediffComp.BondedBabies.Count == 0)
            {
                return ThoughtState.Inactive;
            }

            float moodSum = 0f;
            int validCount = 0;

            List<Pawn> babies = hediffComp.BondedBabies;
            for (int i = 0; i < babies.Count; i++)
            {
                Pawn baby = babies[i];
                if (baby == null || baby.Dead || baby.needs?.mood == null)
                {
                    continue;
                }
                moodSum += baby.needs.mood.CurLevel;
                validCount++;
            }

            if (validCount == 0)
            {
                return ThoughtState.Inactive;
            }

            float avgMood = moodSum / validCount;
            switch (avgMood)
            {
                case < Miserable:
                    return ThoughtState.ActiveAtStage(0);
                case < Sad:
                    return ThoughtState.ActiveAtStage(1);
                case < Neutral:
                    return ThoughtState.ActiveAtStage(2);
                case < Content:
                    return ThoughtState.ActiveAtStage(3);
                default:
                    return ThoughtState.ActiveAtStage(4);
            }
        }
    }
}