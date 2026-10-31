using System;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    public static class SoulDrainPawnCapture
    {
        public const int Resolution = 384;
        public const float WorldSize = 3f;

        public static Texture2D Capture(Pawn pawn)
        {
            pawn.Drawer.renderer.EnsureGraphicsInitialized();
            Camera camera = Find.PawnCacheCamera;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Color previousBackground = camera.backgroundColor;
            CameraClearFlags previousClear = camera.clearFlags;
            Vector3 previousPosition = camera.transform.position;
            float previousSize = camera.orthographicSize;
            Rect previousRect = camera.rect;
            RenderTexture capture = RenderTexture.GetTemporary(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
            Texture2D result = null;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.rect = new Rect(0, 0, 1, 1);
                // Do not assign Camera.aspect: the pawn cache camera must retain automatic aspect.
                Find.PawnCacheRenderer.RenderPawn(pawn, capture, Vector3.zero, 2f / WorldSize,
                    pawn.Drawer.renderer.BodyAngle(PawnRenderFlags.None), pawn.Rotation,
                    renderHead: true, renderHeadgear: true, renderClothes: true, portrait: false);
                RenderTexture.active = capture;
                result = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false) {
                    name = "Soul Drain victim silhouette", wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                result.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0, false);
                result.Apply(false, false);
                return result;
            }
            catch
            {
                if (result != null) UnityEngine.Object.Destroy(result);
                throw;
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                camera.backgroundColor = previousBackground;
                camera.clearFlags = previousClear;
                camera.transform.position = previousPosition;
                camera.orthographicSize = previousSize;
                camera.rect = previousRect;
                RenderTexture.ReleaseTemporary(capture);
            }
        }
    }
}
