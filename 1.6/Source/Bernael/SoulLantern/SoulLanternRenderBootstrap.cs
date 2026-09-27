using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public static class SoulLanternRenderBootstrap
    {
        public static void EnsurePatched()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            MethodInfo target = AccessTools.Method(
                typeof(PawnRenderUtility),
                nameof(PawnRenderUtility.DrawEquipmentAiming));
            if (target == null)
            {
                return;
            }

            Patches patchInfo = Harmony.GetPatchInfo(target);
            bool alreadyInstalled = patchInfo?.Prefixes.Any(patch => patch.owner == HarmonyId) == true;
            if (!alreadyInstalled)
            {
                Harmony harmony = new Harmony(HarmonyId);
                HarmonyMethod prefix = new HarmonyMethod(
                    typeof(Patch_PawnRenderUtility_DrawEquipmentAiming),
                    nameof(Patch_PawnRenderUtility_DrawEquipmentAiming.DrawGlow));
                harmony.Patch(target, prefix: prefix);
            }
        }

        private const string HarmonyId = "BernaelXenotype.Harmony.SoulLantern";

        private static bool initialized;
    }
}
