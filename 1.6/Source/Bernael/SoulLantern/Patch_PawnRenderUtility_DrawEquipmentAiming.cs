using RimWorld;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    [StaticConstructorOnStartup]
    public static class Patch_PawnRenderUtility_DrawEquipmentAiming
    {
        public static void DrawGlow(Thing eq, Vector3 drawLoc, float aimAngle)
        {
            if (eq?.def?.defName != SoulLanternDefName ||
                eq.TryGetComp<CompEquippedSoulLanternGlow>() == null)
            {
                return;
            }

            Pawn_EquipmentTracker equipmentTracker = eq.ParentHolder as Pawn_EquipmentTracker;
            Pawn wearer = equipmentTracker?.pawn;
            if (wearer == null || !wearer.Drafted || OuterGlowMaterial == null || InnerGlowMaterial == null)
            {
                return;
            }

            float weaponAngle = aimAngle - 90f;
            bool flipped = aimAngle > 200f && aimAngle < 340f;
            if (flipped)
            {
                weaponAngle -= 180f;
                weaponAngle -= eq.def.equippedAngleOffset;
            }
            else
            {
                weaponAngle += eq.def.equippedAngleOffset;
            }

            CompEquippable equippable = eq.TryGetComp<CompEquippable>();
            if (equippable != null)
            {
                Vector3 recoilOffset;
                float recoilAngle;
                EquipmentUtility.Recoil(
                    eq.def,
                    EquipmentUtility.GetRecoilVerb(equippable.AllVerbs),
                    out recoilOffset,
                    out recoilAngle,
                    aimAngle);
                drawLoc += recoilOffset;
                weaponAngle += recoilAngle;
            }

            Vector2 weaponDrawSize = eq.Graphic.drawSize;
            float horizontalFocusOffset = LanternFocusOffset * weaponDrawSize.x * (flipped ? -1f : 1f);
            Vector3 localFocusOffset = new Vector3(
                horizontalFocusOffset,
                0f,
                VerticalFocusOffset * weaponDrawSize.y);
            Vector3 glowDrawLoc = drawLoc + localFocusOffset.RotatedBy(weaponAngle);
            glowDrawLoc.y = drawLoc.y - GlowAltitudeOffset;

            DrawGlowLayer(glowDrawLoc, OuterGlowSize, OuterGlowMaterial, OuterGlowPasses);
            glowDrawLoc.y += InnerGlowAltitudeOffset;
            DrawGlowLayer(glowDrawLoc, InnerGlowSize, InnerGlowMaterial, InnerGlowPasses);
        }

        static Patch_PawnRenderUtility_DrawEquipmentAiming()
        {
            OuterGlowMaterial = MaterialPool.MatFrom(
                GlowTexturePath,
                ShaderDatabase.MoteGlow,
                OuterGlowColor);
            InnerGlowMaterial = MaterialPool.MatFrom(
                GlowTexturePath,
                ShaderDatabase.MoteGlow,
                InnerGlowColor);
        }

        private static void DrawGlowLayer(Vector3 position, float size, Material material, int passes)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(
                position,
                Quaternion.identity,
                new Vector3(size, 1f, size));
            for (int pass = 0; pass < passes; pass++)
            {
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
            }
        }

        private const string SoulLanternDefName = "BX_SoulLantern";
        private const string GlowTexturePath = "Things/Mote/FireGlow";
        private const float LanternFocusOffset = 0.265f;
        private const float VerticalFocusOffset = -0.04f;
        private const float GlowAltitudeOffset = 0.001f;
        private const float InnerGlowAltitudeOffset = 0.0001f;
        private const float OuterGlowSize = 3.4f;
        private const float InnerGlowSize = 1.75f;
        private const int OuterGlowPasses = 2;
        private const int InnerGlowPasses = 2;

        private static readonly Color OuterGlowColor = new Color(
            100f / 255f,
            200f / 255f,
            1f,
            0.65f);
        private static readonly Color InnerGlowColor = new Color(
            100f / 255f,
            200f / 255f,
            1f,
            0.95f);
        private static readonly Material OuterGlowMaterial;
        private static readonly Material InnerGlowMaterial;
    }
}
