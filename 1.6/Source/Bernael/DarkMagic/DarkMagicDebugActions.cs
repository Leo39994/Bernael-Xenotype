using LudeonTK;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public static class DarkMagicDebugActions
    {
        [DebugAction("Bernael - Dark Magic", "Grant Malicious Seal (click pawn)", requiresRoyalty: true,
            actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void GrantMaliciousSeal(Pawn pawn) => Grant(pawn, "BX_MaliciousSeal");

        [DebugAction("Bernael - Dark Magic", "Grant Dire Orb (click pawn)", requiresRoyalty: true,
            actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void GrantDireOrb(Pawn pawn) => Grant(pawn, "BX_DireOrb");

        // Sealed on the player's behalf, so it spreads to pawns hostile to the colony.
        [DebugAction("Bernael - Dark Magic", "Apply malicious seal (click pawn)",
            actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ApplyMaliciousSeal(Pawn pawn) => MaliciousSealUtility.Brand(pawn, null, Faction.OfPlayer);

        // Stands in for the Eye of Void imps, the intended source of cursed flame, until they exist.
        [DebugAction("Bernael - Dark Magic", "Deal 10 cursed flame (click pawn)",
            actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void DealCursedFlame(Pawn pawn)
        {
            DamageWorker.DamageResult result = pawn.TakeDamage(new DamageInfo(BernaelDefOf.BX_CursedFlame, 10f));
            Messages.Message("BX_CursedFlameDealt".Translate(pawn.LabelShortCap, result.totalDamageDealt.ToString("0.#")),
                pawn, MessageTypeDefOf.NeutralEvent, false);
        }

        private static void Grant(Pawn pawn, string defName)
        {
            if (!pawn.RaceProps.Humanlike || pawn.abilities == null || pawn.Faction != Faction.OfPlayer) return;
            AbilityDef def = DefDatabase<AbilityDef>.GetNamed(defName);
            int missingLevels = def.level - pawn.GetPsylinkLevel();
            if (missingLevels > 0) pawn.ChangePsylinkLevel(missingLevels);
            pawn.abilities.GainAbility(def);
            pawn.psychicEntropy.OffsetPsyfocusDirectly(1f);
            Messages.Message("BX_DarkMagicGranted".Translate(def.LabelCap, pawn.LabelShortCap), pawn,
                MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
