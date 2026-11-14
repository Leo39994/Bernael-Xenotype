using Verse;
using UnityEngine;

namespace Bernael_Xenotype
{
    [StaticConstructorOnStartup]
    public class Mote_AnimaRoots : Mote
    {
        public float radius = 2.9f;
        public float progress = 0f;

        private static readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private static readonly Material material = MaterialPool.MatFrom("Other/BX_Roots", ShaderDatabase.MoteGlow);
        private Color rootsStartColor = new Color(0.6f, 0.8f, 1f, 1f);
        private Color rootsEndColor = new Color(0.66f, 0f, 0f, 1f);

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float diameter = radius * 2f;

            float openFactor = Mathf.InverseLerp(0f, 0.2f, progress);
            float closeFactor = Mathf.InverseLerp(1.0f, 0.66f, progress);
            float lifecycleFactor = Mathf.Min(openFactor, closeFactor);
            float scaleX = diameter * Mathf.Lerp(0.5f, 1.0f, closeFactor);
            float scaleZ = diameter * Mathf.Lerp(0.5f, 1.0f, closeFactor);

            exactPosition.y = AltitudeLayer.Filth.AltitudeFor();

            float colorProgress = Mathf.InverseLerp(0.33f, 0.66f, progress);
            Color currentColor = Color.Lerp(rootsStartColor, rootsEndColor, colorProgress);

            Vector3 scale = new Vector3(scaleX, 1f, scaleZ);
            Matrix4x4 matrix = Matrix4x4.TRS(exactPosition, Quaternion.identity, scale);
            propertyBlock.SetColor("_Color", new Color(currentColor.r, currentColor.g, currentColor.b, Mathf.Lerp(0.1f, 0.5f, lifecycleFactor)));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0, null, 0, propertyBlock);
        }
    }
}