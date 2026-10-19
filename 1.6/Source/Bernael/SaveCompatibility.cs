using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    // Migrate the in-memory save before Scribe resolves classes, Defs or cross references.
    // The source file is unchanged; subsequent saves use the current schema.
    [HarmonyPatch(typeof(ScribeLoader), nameof(ScribeLoader.InitLoading))]
    public static class SaveCompatibility
    {
        public static void Postfix(ScribeLoader __instance)
        {
            XmlNode root = __instance.curXmlParent;
            if (root == null || Scribe.mode != LoadSaveMode.LoadingVars) return;

            foreach (XmlNode node in root.SelectNodes(".//*[@Class='Bernael.DarkMirageDemo.DarkMirage']"))
                node.Attributes["Class"].Value = typeof(DarkMirage).FullName;

            // BX_DepravedHead became a cosmetic-only Verse.Gene when psychic sight moved to
            // BX_BernaelBlind. Older saves still name its former custom class, which must be
            // rewritten before Scribe attempts to instantiate the saved gene node.
            foreach (XmlNode node in root.SelectNodes(".//*[@Class='Bernael_Xenotype.Gene_DepravedHead']"))
                node.Attributes["Class"].Value = typeof(Gene).FullName;

            var removedGenes = new HashSet<string>();
            var removedNodes = new List<XmlNode>();
            foreach (XmlNode node in root.SelectNodes(
                ".//li[def='BX_NourishingDarkness' or def='BX_Gene_NourishingDarkness_Hediff' " +
                "or @Class='Bernael_Xenotype.Hediff_NourishingDarkness' " +
                "or (not(*) and text()='BX_NourishingDarkness')]"))
            {
                if (node["def"]?.InnerText == "BX_NourishingDarkness")
                    removedGenes.Add("Gene_" + (node["loadID"]?.InnerText ?? "0"));
                removedNodes.Add(node);
            }
            foreach (XmlNode node in removedNodes) node.ParentNode.RemoveChild(node);
            if (removedGenes.Count == 0) return;
            foreach (XmlNode node in root.SelectNodes(".//overriddenByGene"))
                if (removedGenes.Contains(node.InnerText)) node.InnerText = "null";
        }
    }

    // Psychic sight originally lived on the cosmetic BX_DepravedHead gene. Preserve that
    // behavior in existing saves by adding the new dedicated gene once, with the same gene type.
    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.ExposeData))]
    public static class Patch_Pawn_GeneTracker_ExposeData
    {
        public static void Postfix(Pawn_GeneTracker __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit || __instance.GetGene(BernaelDefOf.BX_BernaelBlind) != null) return;

            Gene depravedHead = __instance.GetGene(BernaelDefOf.BX_DepravedHead);
            if (depravedHead == null) return;

            __instance.AddGene(BernaelDefOf.BX_BernaelBlind, __instance.IsXenogene(depravedHead));
        }
    }

    // BX_SoulFeeding moved from Verb_CastAbilityTouch to Verb_CastAbility. On load VerbTracker
    // drops a saved verb whose class no longer matches and creates a fresh one, but nothing
    // hands that fresh verb its Ability, so every targeting frame throws on ability.def.
    [HarmonyPatch(typeof(Ability), nameof(Ability.ExposeData))]
    public static class Patch_Ability_ExposeData
    {
        public static void Postfix(Ability __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit || __instance.def == null) return;
            if (__instance.verb is IAbilityVerb abilityVerb && abilityVerb.Ability == null)
                abilityVerb.Ability = __instance;
        }
    }
}
