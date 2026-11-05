using Verse;
using Verse.AI;
using HarmonyLib;

namespace Bernael_Xenotype
{
    /// <summary>
    /// Harmony patch for PathFinder.CreateRequest to customize pathing parameters when a Pawn is currently executing the BX_TraverseTheFold_Job.
    /// </summary>
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.CreateRequest),
    [
        typeof(IntVec3),
        typeof(LocalTargetInfo),
        typeof(IntVec3?),
        typeof(TraverseParms),
        typeof(PathFinderCostTuning?),
        typeof(PathEndMode),
        typeof(Pawn),
        typeof(PathRequest.IPathGridCustomizer)
    ])]
    public static class Patch_PathFinder_CreateRequest
    {
        public static bool Prefix(ref PathRequest __result, IntVec3 start, LocalTargetInfo target, IntVec3? dest, TraverseParms traverseParms, PathFinderCostTuning? mtuning, PathEndMode peMode, Pawn pawn, PathRequest.IPathGridCustomizer customizer)
        {
            if (WorldComponent_TraverseTheFold.Contains(pawn))
            {
                pawn.pather.lastPathedTargetPosition = target.Cell;
                int ticksGame = GenTicks.TicksGame;

                // Construct a specialized PathRequest with wall-phasing parameters
                __result = new PathRequest(pawn.Map, start, target, dest, new TraverseParms
                {
                    pawn = pawn,
                    alwaysUseAvoidGrid = false,
                    canBashDoors = true,
                    canBashFences = true,
                    fenceBlocked = false,
                    maxDanger = Danger.Deadly,
                    mode = TraverseMode.PassAllDestroyableThings // Allow pathing through walls and obstacles
                }, new PathFinderCostTuning
                {
                    // Remove all pathfinding cost penalties for walls, obstacles, and doors
                    costBlockedDoor = 0,
                    costBlockedDoorPerHitPoint = 0f,
                    costBlockedWallBase = 0,
                    costBlockedWallExtraForNaturalWalls = 0,
                    costBlockedWallExtraPerHitPoint = 0f,
                    costOffLordWalkGrid = 0
                }, peMode, pawn, ticksGame, ticksGame, ticksGame, customizer);

                return false; // Skip original method execution
            }
            return true; // Proceed with original method for non-phasing pawns
        }
    }
}