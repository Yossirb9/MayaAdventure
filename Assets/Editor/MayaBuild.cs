using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace MayaAdventure.EditorTools
{
    /// <summary>
    /// Maya > Build Android APK (or batch mode: -executeMethod MayaAdventure.EditorTools.MayaBuild.BuildAndroid).
    /// Output: Builds/MayaAdventure.apk (debug-signed, ARM64, landscape).
    /// </summary>
    public static class MayaBuild
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ApkPath = "Builds/MayaAdventure.apk";
        public const string GameName = "מאיה ומסע הכוכבים";
        const string IconPath = "Assets/Icons/AppIcon.png";
        const string IconForegroundPath = "Assets/Icons/AppIconForeground.png";

        [MenuItem("Maya/Build Android APK")]
        public static void BuildAndroid()
        {
            var target = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Yossi";
            PlayerSettings.productName = GameName;
            PlayerSettings.SetApplicationIdentifier(target, "com.yossi.mayaadventure");
            PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUserBuildSettings.buildAppBundle = false;

            // No "Made with Unity" splash: the game opens straight on its own title screen.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            ApplyIcons();

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            MayaURP.EnsureRuntimeShaders(); // without these the phone cannot build a single material
            MayaURP.UpdateQualityAssets();
            if (!System.IO.File.Exists("Assets/Resources/Fonts/SecularOne SDF.asset")) MayaURP.BakeTmpFont();
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[MayaBuild] {summary.result}: {summary.outputPath} ({summary.totalSize / (1024f * 1024f):F1} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalTime.TotalMinutes:F1} min)");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void ApplyIcons()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogWarning($"[MayaBuild] Icon not found: {IconPath}");
                return;
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
#if UNITY_ANDROID
            var empty = AssetDatabase.LoadAssetAtPath<Texture2D>(IconForegroundPath);
            // Adaptive (Android 8+): the whole picture as the background layer, an empty foreground.
            var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            foreach (var i in adaptive) i.SetTextures(icon, empty);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, adaptive);
#endif
        }
    }
}
