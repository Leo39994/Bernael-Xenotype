using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
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
}
