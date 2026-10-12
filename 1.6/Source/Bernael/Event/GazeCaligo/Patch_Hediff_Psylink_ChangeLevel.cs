using RimWorld;
using Verse;
using HarmonyLib;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.ChangeLevel))]
    [HarmonyPatch(new[] { typeof(int), typeof(bool) })]
    public static class Patch_Hediff_Psylink_ChangeLevel
    {
        public static void Postfix(Hediff_Psylink __instance)
        {
            Pawn pawn = __instance.pawn;
            Map map = pawn.Map;

            if (pawn == null || map == null || !pawn.IsColonistPlayerControlled)
            {
                return;
            }

            int currentLevel = __instance.level;
            if (currentLevel < 3) return;

            IncidentDef incidentDef = BernaelDefOf.BX_GazeCaligo;
            if (Rand.Chance(incidentDef.baseChance))
            {
                Find.Storyteller.TryFire(new FiringIncident(incidentDef, null, StorytellerUtility.DefaultParmsNow(incidentDef.category, map)));
            }
        }
    }
}