using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Sets up Android player settings + a bootstrap scene, and builds the APK.
/// Used by GitHub Actions (BuildScript.BuildAndroid) and available from the editor menu.
/// </summary>
public static class BuildScript
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string OutputPath = "build/Android/BlockBlast.apk";

    // >>> change these two to your own <<<
    const string CompanyName = "YourName";
    const string BundleId = "com.yourname.blockblast";

    [MenuItem("BlockBlast/Apply Android Settings")]
    public static void Configure()
    {
        Directory.CreateDirectory("Assets/Scenes");
        if (!File.Exists(ScenePath))
        {
            // The game builds itself at runtime, so the scene can be completely empty.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        var android = NamedBuildTarget.Android;
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = "Block Blast";
        PlayerSettings.SetApplicationIdentifier(android, BundleId);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        EditorUserBuildSettings.buildAppBundle = false;   // .apk, not .aab
    }

    public static void BuildAndroid()
    {
        Configure();
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
