using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildSoulDrain
{
    public static void Build()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../.."));
        string output = Path.Combine(Application.dataPath, "../Library/SoulDrainBundles");
        Directory.CreateDirectory(output);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        const string asset = "Assets/Shaders/SoulDrain.shader";
        const string ashenAsset = "Assets/Shaders/SoulDrainAshen.shader";
        const string smokeAsset = "Assets/Textures/SoulDrainSmoke.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(smokeAsset);
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(asset);
        Shader ashen = AssetDatabase.LoadAssetAtPath<Shader>(ashenAsset);
        if (shader == null) throw new Exception("Soul Drain shader missing");
        var manifest = BuildPipeline.BuildAssetBundles(output, new[] {
            new AssetBundleBuild { assetBundleName = "souldrain_win", assetNames = new[] { asset, ashenAsset, smokeAsset } }
        }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        var errors = ShaderUtil.GetShaderMessages(shader).Concat(ShaderUtil.GetShaderMessages(ashen))
            .Where(x => x.severity == ShaderCompilerMessageSeverity.Error).ToArray();
        foreach (var error in errors) Debug.LogError(error.message);
        if (manifest == null || errors.Length > 0) throw new Exception("Soul Drain bundle compilation failed");
        string destination = Path.Combine(root, "1.6/AssetBundles/souldrain_win");
        File.Copy(Path.Combine(output, "souldrain_win"), destination, true);
        Debug.Log("SOUL_DRAIN_BUILD_OK: " + destination);
    }
}
