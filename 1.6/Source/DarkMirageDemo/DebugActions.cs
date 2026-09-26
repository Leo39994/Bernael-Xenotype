using LudeonTK;
using RimWorld;
using Verse;

namespace Bernael.DarkMirageDemo
{
    public static class DebugActions
    {
        [DebugAction("Bernael - Dark Mirage", "Export shader preview (click pawn)",
            actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Export(Pawn pawn)
        {
            if (!pawn.RaceProps.Humanlike || pawn.Faction == null) return;
            ThingDef def = DefDatabase<ThingDef>.GetNamed("BX_DarkMirageDecoy");
            if (!MirageVisuals.ShaderReady(def)) return;
            for (int x=-3; x<=3; x++)
                for (int z=-3; z<=3; z++)
                {
                    IntVec3 cell = pawn.Position + new IntVec3(x,0,z);
                    if (!MirageUtility.CanSpawnAt(cell,pawn.Map)) continue;
                    DarkMirage mirage = MirageUtility.Spawn(pawn,cell,def);
                    string path = System.IO.Path.Combine(def.modContentPack.RootDir,"1.6/Source/DarkMirageDemo/QA/PlayerExport");
                    try { mirage.ExportVisualPreview(path); Log.Message("[Dark Mirage] Exported preview: " + path); }
                    finally { mirage.Dismiss(); }
                    return;
                }
        }

        [DebugAction("Bernael - Dark Mirage", "Grant Dark Mirage (click pawn)", requiresRoyalty: true,
            actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Grant(Pawn pawn)
        {
            if (!pawn.RaceProps.Humanlike || pawn.abilities == null || pawn.Faction != Faction.OfPlayer) return;
            int missingLevels = 2 - pawn.GetPsylinkLevel();
            if (missingLevels > 0) pawn.ChangePsylinkLevel(missingLevels);
            pawn.abilities.GainAbility(DefDatabase<AbilityDef>.GetNamed("BX_DarkMirage"));
            pawn.psychicEntropy.OffsetPsyfocusDirectly(1f);
            Messages.Message("BX_MirageGranted".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
