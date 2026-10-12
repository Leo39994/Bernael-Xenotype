using System;
using Verse;
using UnityEngine;

namespace Bernael_Xenotype
{
    public sealed class FoldShiftVisuals : IDisposable
    {
        public const int CaptureSize = 512;
        public const float WorldSize = 2f;
        private Texture2D snapshot;
        private Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private Rot4 cachedFacing = Rot4.Invalid;

        public void InitOrUpdate(Pawn pawn, Shader customShader)
        {
            if (snapshot == null || cachedFacing != pawn.Rotation)
            {
                Dispose();
                Capture(pawn, pawn.Rotation);
                cachedFacing = pawn.Rotation;
                
                snapshot.name = "FoldShift snapshot " + pawn.ThingID;
                snapshot.wrapMode = TextureWrapMode.Clamp;
                snapshot.filterMode = FilterMode.Bilinear;

                material = new Material(customShader) { name = "FoldShift Mat " + pawn.ThingID, mainTexture = snapshot };
            }
        }

        private void Capture(Pawn pawn, Rot4 facing)
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

            var capture = new RenderTexture(CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "FoldShift temporary capture",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            try
            {
                capture.Create();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.rect = new Rect(0f, 0f, 1f, 1f);

                Find.PawnCacheRenderer.RenderPawn(pawn, capture, Vector3.zero, 2f / WorldSize, 0f, facing, renderHead: true, renderHeadgear: true, renderClothes: true, portrait: false);

                camera.targetTexture = capture;
                camera.orthographicSize = WorldSize * 0.5f;

                RenderTexture.active = capture;
                snapshot = new Texture2D(CaptureSize, CaptureSize, TextureFormat.RGBA32, false);
                snapshot.ReadPixels(new Rect(0, 0, CaptureSize, CaptureSize), 0, 0, false);
                snapshot.Apply(false, false);
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
                capture.Release();
                UnityEngine.Object.Destroy(capture);
            }
        }

        public void Draw(Vector3 position, Color color)
        {
            if (material == null) return;

            position.y = AltitudeLayer.Pawn.AltitudeFor() + 0.05f;
            properties.SetColor("_Color", color);

            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(WorldSize, 1f, WorldSize)), material, 0, null, 0, properties, UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }

        public void Dispose()
        {
            if (material != null) UnityEngine.Object.Destroy(material);
            if (snapshot != null) UnityEngine.Object.Destroy(snapshot);
            material = null;
            snapshot = null;
            cachedFacing = Rot4.Invalid;
        }
    }
}