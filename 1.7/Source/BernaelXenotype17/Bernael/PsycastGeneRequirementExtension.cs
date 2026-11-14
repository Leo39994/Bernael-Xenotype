using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;

namespace Bernael_Xenotype
{
    /// <summary>
    /// DefModExtension used to flag custom psycasts that require specific genes to unlock.
    /// </summary>
    public class PsycastGeneRequirementExtension : DefModExtension
    {
        public bool requireHexmaster = true;
    }

    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.TryGiveAbilityOfLevel))]
    public static class Patch_Hediff_Psylink_TryGiveAbilityOfLevel_Transpiler
    {
        private static readonly MethodInfo FilterCandidatesMethod = AccessTools.Method(
            typeof(Patch_Hediff_Psylink_TryGiveAbilityOfLevel_Transpiler),
            nameof(FilterCandidatesWithGeneCheck)
        );

        private static readonly FieldInfo HediffPawnField = AccessTools.Field(
            typeof(Hediff),
            nameof(Hediff.pawn)
        );

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;

            for (int i = 0; i < codes.Count; i++)
            {
                yield return codes[i];

                if (!patched && codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo method && method.Name == "Where")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, HediffPawnField);
                    yield return new CodeInstruction(OpCodes.Call, FilterCandidatesMethod);

                    patched = true;
                }
            }
            if (!patched)
            {
                Log.Error("[Bernael Xenotype] Transpiler failed to find Where in Hediff_Psylink.TryGiveAbilityOfLevel.");
            }
        }

        /// <summary>
        /// Filters the available psycast candidate pool based on the pawn's gene requirements.
        /// Excludes restricted psycasts if the pawn lacks the BX_Hexmaster gene.
        /// </summary>
        public static IEnumerable<AbilityDef> FilterCandidatesWithGeneCheck(IEnumerable<AbilityDef> originalCandidates, Pawn pawn)
        {
            if (originalCandidates == null) yield break;

            List<AbilityDef> validPool = new List<AbilityDef>();

            foreach (AbilityDef def in originalCandidates)
            {
                var ext = def.GetModExtension<PsycastGeneRequirementExtension>();
                bool needsHexmaster = ext != null && ext.requireHexmaster;

                if (needsHexmaster)
                {
                    bool hasGene = pawn?.genes != null && pawn.genes.HasActiveGene(BernaelDefOf.BX_Hexmaster);
                    if (!hasGene) continue;
                    validPool.Add(def);
                }
                else
                {
                    validPool.Add(def);
                }
            }
            foreach (var def in validPool)
            {
                yield return def;
            }
        }
    }
}