using Verse;
using Verse.AI;
using HarmonyLib;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StartPath))]
    public static class Patch_Pawn_PathFollower_StartPath
    {
        public static Pawn currentPatherPawn;

        public static void Prefix(Pawn ___pawn)
        {
            currentPatherPawn = ___pawn;
        }

        public static void Postfix()
        {
            currentPatherPawn = null;
        }
    }
}