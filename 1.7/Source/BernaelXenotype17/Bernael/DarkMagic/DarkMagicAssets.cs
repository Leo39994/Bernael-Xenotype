using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace Bernael_Xenotype
{
    // Shared shader resources for Malicious Seal and Dire Orb, loaded once on the main thread.
    [StaticConstructorOnStartup]
    public static class DarkMagicAssets
    {
        private static bool attemptedLoad;
        public static Material Seal { get; private set; }
        public static Material Orb { get; private set; }
        public static Mesh Quad { get; private set; }

        public static bool Ready()
        {
            if (Seal != null) return true;
            if (attemptedLoad) return false;
            attemptedLoad = true;
            Shader seal = DefDatabase<ShaderDef>.GetNamed("BX_MaliciousSeal").Shader;
            Shader orb = DefDatabase<ShaderDef>.GetNamed("BX_DireOrb").Shader;
            if (seal == null || !seal.isSupported || orb == null || !orb.isSupported)
            {
                Log.Warning("[Bernael] Malicious Seal / Dire Orb native shaders are unavailable; using vanilla fallback visuals.");
                return false;
            }
            Seal = new Material(seal) { name = "Bernael Malicious Seal (shared)" };
            Orb = new Material(orb) { name = "Bernael Dire Orb (shared)" };
            // Use the same authored flames as the lasting ground fire.
            Orb.SetTexture("_FlameA", ContentFinder<Texture2D>.Get("Things/CursedFlame/CursedFlame_A"));
            Orb.SetTexture("_FlameB", ContentFinder<Texture2D>.Get("Things/CursedFlame/CursedFlame_B"));
            Orb.SetTexture("_FlameC", ContentFinder<Texture2D>.Get("Things/CursedFlame/CursedFlame_C"));
            Quad = new Mesh { name = "Dark magic UV plane" };
            Quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f),
                new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            Quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            Quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            Quad.RecalculateBounds();
            Quad.UploadMeshData(true);
            return true;
        }

        // Rotates the quad so uv.x runs along delta; both shaders put their strip start at uv.x = 0.
        public static Quaternion Along(Vector3 delta) => Quaternion.LookRotation(new Vector3(-delta.z, 0, delta.x));

        public static void Draw(Material material, Vector3 position, Quaternion rotation, Vector3 scale,
            MaterialPropertyBlock properties)
        {
            Graphics.DrawMesh(Quad, Matrix4x4.TRS(position, rotation, scale), material, 0, null, 0,
                properties, ShadowCastingMode.Off, false);
        }
    }
}
