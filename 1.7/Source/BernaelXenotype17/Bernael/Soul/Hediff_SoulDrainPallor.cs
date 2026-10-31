using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    public sealed class Hediff_SoulDrainPallor : HediffWithComps
    {
        public readonly SoulDrainAshenMaterials Materials = new SoulDrainAshenMaterials();

        public void ReleaseMaterials() => Materials.Dispose();

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            pawn.Drawer.renderer.SetAllGraphicsDirty();
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            ReleaseMaterials();
        }
    }

    public sealed class SoulDrainAshenMaterials : System.IDisposable
    {
        private readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();

        public Material MaterialFor(Material original, float amount)
        {
            if (original == null || SoulDrainMapComponent.AshenShader == null) return original;
            Material result;
            if (!materials.TryGetValue(original, out result))
            {
                bool usesMask = original.HasProperty("_MaskTex");
                result = new Material(original) {
                    shader = SoulDrainMapComponent.AshenShader,
                    name = "Soul drained " + original.name
                };
                result.SetFloat("_UseMask", usesMask ? 1f : 0f);
                materials.Add(original, result);
            }
            result.SetFloat("_DrainAmount", amount);
            return result;
        }

        public void Dispose()
        {
            foreach (Material material in materials.Values)
                if (material != null) Object.Destroy(material);
            materials.Clear();
        }

    }
}
