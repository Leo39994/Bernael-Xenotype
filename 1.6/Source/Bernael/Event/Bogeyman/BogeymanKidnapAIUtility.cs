using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public static class BogeymanKidnapAIUtility
    {
        public static bool TryFindBabyOrChildVictim(Pawn kidnapper, float maxDist, out Pawn victim, List<Thing> disallowed = null)
        {
            if (!kidnapper.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                victim = null;
                return false;
            }

            Predicate<Thing> validator = delegate(Thing t)
            {
                if (t is not Pawn pawn) return false;

                if (!pawn.RaceProps.Humanlike || pawn.Faction != Faction.OfPlayer) return false;

                // Baby or Child
                bool isYoung = pawn.DevelopmentalStage.Baby() || pawn.DevelopmentalStage.Child();
                if (!isYoung) return false;

                if (!kidnapper.CanReserve(pawn)) return false;

                if (disallowed != null && disallowed.Contains(pawn)) return false;

                return true;
            };

            // TraverseMode.PassDoors
            TraverseParms traverseParms = TraverseParms.For(kidnapper, Danger.Deadly, TraverseMode.PassDoors);
            victim = (Pawn)GenClosest.ClosestThingReachable(kidnapper.Position, kidnapper.Map, ThingRequest.ForGroup(ThingRequestGroup.Pawn), PathEndMode.OnCell, traverseParms, maxDist, validator);

            return victim != null;
        }
    }
}