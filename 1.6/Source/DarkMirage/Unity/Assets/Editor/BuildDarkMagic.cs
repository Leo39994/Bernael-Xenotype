using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildDarkMagic
{
    public const string SealAsset = "Assets/Shaders/MaliciousSeal.shader";
    public const string OrbAsset = "Assets/Shaders/DireOrb.shader";

    public static void Build()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../.."));
        string output = Path.Combine(Application.dataPath, "../Library/DarkMagicBundles");
        Directory.CreateDirectory(output);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        Shader seal = AssetDatabase.LoadAssetAtPath<Shader>(SealAsset);
        Shader orb = AssetDatabase.LoadAssetAtPath<Shader>(OrbAsset);
        if (seal == null || orb == null) throw new Exception("Dark magic shaders missing");
        var manifest = BuildPipeline.BuildAssetBundles(output, new[] {
            new AssetBundleBuild { assetBundleName = "darkmagic_win", assetNames = new[] { SealAsset, OrbAsset } }
        }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        var errors = ShaderUtil.GetShaderMessages(seal).Concat(ShaderUtil.GetShaderMessages(orb))
            .Where(x => x.severity == ShaderCompilerMessageSeverity.Error).ToArray();
        foreach (var error in errors) Debug.LogError("Shader error: " + error.message + " line " + error.line);
        if (ShaderUtil.ShaderHasError(seal) || ShaderUtil.ShaderHasError(orb)) Debug.LogError("Shader error: dark magic shader failed to compile");
        if (manifest == null || errors.Length > 0 || ShaderUtil.ShaderHasError(seal) || ShaderUtil.ShaderHasError(orb)) throw new Exception("Dark magic bundle compilation failed");
        string destination = Path.Combine(root, "1.6/AssetBundles/darkmagic_win");
        File.Copy(Path.Combine(output, "darkmagic_win"), destination, true);
        Debug.Log("DARK_MAGIC_BUILD_OK: " + destination);
    }
}
