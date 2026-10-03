using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEditor.Build.Reporting;

namespace Metin2.Frontend.EditorTools
{
    /// <summary>
    /// One-click Android APK build for the demo. Usable from the menu
    /// (Metin2 → Build Android APK) or headless:
    ///   Unity.exe -batchmode -quit -projectPath &lt;repo&gt;
    ///     -executeMethod Metin2.Frontend.EditorTools.AndroidBuildScript.BuildApk
    ///     -logFile Builds/build.log
    ///
    /// Requires the "Android Build Support" editor module (OpenJDK + SDK/NDK).
    /// The INTERNET permission is forced: the client speaks raw TCP
    /// (AGENT_DEVELOPMENT_GUIDE.md §5/§6 — server socket access is mandatory).
    /// </summary>
    public static class AndroidBuildScript
    {
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string ApkPath = "Builds/Metin2Demo.apk";

        [MenuItem("Metin2/Build Android APK")]
        public static void BuildApk()
        {
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException($"Scene not found: {ScenePath}");
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "YusuffEren";
            PlayerSettings.productName = "Metin2 Demo";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.yusufferen.metin2demo");

            // Landscape only (guide §9.1: reference gameplay orientation).
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Raw TCP sockets need android.permission.INTERNET in every build type.
            PlayerSettings.Android.forceInternetPermission = true;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                // Unity 6: SwitchActiveBuildTarget returns bool (the
                // SwitchActiveBuildTargetStatus enum was removed). Note: not
                // callable in batch mode — use the -buildTarget Android
                // command line argument for headless builds.
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    throw new InvalidOperationException(
                        "Failed to switch to the Android build target. Is 'Android Build Support' installed for this editor?");
                }
            }

            Directory.CreateDirectory("Builds");
            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { ScenePath },
                ApkPath,
                BuildTarget.Android,
                BuildOptions.None);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Android build FAILED: {report.summary.result}, errors: {report.summary.totalErrors}");
                throw new InvalidOperationException($"Android build failed: {report.summary.result}");
            }

            Debug.Log($"Android build OK: {Path.GetFullPath(ApkPath)} ({report.summary.totalSize} bytes)");
            Console.WriteLine($"Android build OK: {Path.GetFullPath(ApkPath)} ({report.summary.totalSize} bytes)");
        }
    }
}
