using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildMirage
{
    public static void Build()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.."));
        string output = Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/MirageBundles"));
        Directory.CreateDirectory(output);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/DarkMirage.shader");
        if (shader == null) throw new Exception("Missing DarkMirage shader");
        var manifest = BuildPipeline.BuildAssetBundles(output, new[] {
            new AssetBundleBuild {
                assetBundleName="darkmirage_win",
                assetNames=new[] { "Assets/Shaders/DarkMirage.shader" }
            }
        }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new Exception("AssetBundle build failed");
        var errors = ShaderUtil.GetShaderMessages(shader).Where(x=>x.severity == ShaderCompilerMessageSeverity.Error).ToArray();
        foreach (var error in errors) Debug.LogError(error.message);
        if (errors.Length>0) throw new Exception("Shader compilation failed");
        string deployed = Path.Combine(root,"1.6/AssetBundles");
        Directory.CreateDirectory(deployed);
        File.Copy(Path.Combine(output,"darkmirage_win"),Path.Combine(deployed,"darkmirage_win"),true);
        Debug.Log("DARK_MIRAGE_BUILD_OK: " + deployed);
    }
}
