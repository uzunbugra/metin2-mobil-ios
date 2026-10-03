using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Metin2.Frontend.EditorTools
{
    /// <summary>
    /// One-click iOS Xcode project build for the demo. Usable from the menu
    /// (Metin2 → Build iOS Xcode Project) or headless:
    ///   Unity -batchmode -quit -projectPath &lt;repo&gt; -buildTarget iOS
    ///     -executeMethod Metin2.Frontend.EditorTools.IosBuildScript.BuildXcodeProject
    ///     -logFile Builds/build-ios.log
    ///
    /// Requires the "iOS Build Support" editor module (Unity Hub). Produces
    /// an Xcode project under Builds/iOS; signing and device deployment
    /// happen in Xcode (Sprint 8). Raw TCP sockets need no extra permission
    /// on iOS (no NSAppTransportSecurity exception — ATS applies to HTTP
    /// only, plain TCP sockets are unaffected).
    ///
    /// Note: like the Android script, SwitchActiveBuildTarget is unavailable
    /// in batch mode — pass the -buildTarget iOS command line argument for
    /// headless builds.
    /// </summary>
    public static class IosBuildScript
    {
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string XcodeProjectPath = "Builds/iOS";

        [MenuItem("Metin2/Build iOS Xcode Project")]
        public static void BuildXcodeProject()
        {
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException($"Scene not found: {ScenePath}");
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "bugrauzun";
            PlayerSettings.productName = "Metin2 Demo";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.bugrauzun.metin2demo");

            // Landscape only (guide §9.1: reference gameplay orientation).
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
                {
                    throw new InvalidOperationException(
                        "Failed to switch to the iOS build target. Is 'iOS Build Support' installed for this editor?");
                }
            }

            Directory.CreateDirectory("Builds");
            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { ScenePath },
                XcodeProjectPath,
                BuildTarget.iOS,
                BuildOptions.None);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"iOS build FAILED: {report.summary.result}, errors: {report.summary.totalErrors}");
                throw new InvalidOperationException($"iOS build failed: {report.summary.result}");
            }

            Debug.Log($"iOS Xcode project OK: {Path.GetFullPath(XcodeProjectPath)} ({report.summary.totalSize} bytes)");
        }
    }
}
