using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    // Swap only this draw's materials, after parallel preparation has finished.
    // This includes hair, clothes and headgear and also works in the pawn atlas/portraits.
    [HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.Draw))]
    public static class SoulDrainAshenRenderer
    {
        private static readonly Stack<List<Material>> restorePool = new Stack<List<Material>>();

        public static void Prefix(PawnDrawParms parms, List<PawnGraphicDrawRequest> ___drawRequests,
            out List<Material> __state)
        {
            __state = null;
            if (parms.pawn?.health?.hediffSet == null || parms.flags.FlagSet(PawnRenderFlags.Invisible)) return;
            Hediff_SoulDrainPallor pallor = null;
            foreach (Hediff hediff in parms.pawn.health.hediffSet.hediffs)
                if (hediff is Hediff_SoulDrainPallor found) { pallor = found; break; }
            SoulDrainAshenMaterials materials = pallor?.Materials;
            float amount = pallor == null ? 0f : 1f;
            var component = parms.pawn.Map?.GetComponent<SoulDrainMapComponent>();
            if (component != null && component.TryGetPallor(parms.pawn, out var activeMaterials, out float activeAmount)
                && activeAmount > amount)
            { materials = activeMaterials; amount = activeAmount; }
            if (materials == null || amount <= 0.001f || !SoulDrainMapComponent.EnsureMaterial()) return;
            List<Material> originals = restorePool.Count > 0 ? restorePool.Pop() : new List<Material>(16);
            foreach (PawnGraphicDrawRequest request in ___drawRequests) originals.Add(request.material);
            __state = originals;
            for (int i = 0; i < ___drawRequests.Count; i++)
            {
                PawnGraphicDrawRequest request = ___drawRequests[i];
                request.material = materials.MaterialFor(request.material, amount);
                ___drawRequests[i] = request;
            }
        }

        public static Exception Finalizer(Exception __exception, List<PawnGraphicDrawRequest> ___drawRequests,
            List<Material> __state)
        {
            if (__state != null)
            {
                for (int i = 0; i < Math.Min(___drawRequests.Count, __state.Count); i++)
                {
                    PawnGraphicDrawRequest request = ___drawRequests[i];
                    request.material = __state[i];
                    ___drawRequests[i] = request;
                }
                __state.Clear();
                restorePool.Push(__state);
            }
            return __exception;
        }
    }
}
