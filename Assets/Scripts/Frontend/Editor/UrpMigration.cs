using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Metin2.Frontend.EditorTools
{
    /// <summary>
    /// Built-in Render Pipeline → Universal Render Pipeline migration.
    ///
    /// Unity 6.5+ marks the Built-in Render Pipeline as deprecated (obsolete
    /// in a future release); this project (Unity 6.6 / 6000.6.4f1) migrates to
    /// URP per the official migration guide. The demo world is fully
    /// procedural (no serialized materials), so the migration is purely
    /// settings-side: URP package (manifest.json) + the assets this script
    /// creates under Assets/Settings.
    ///
    /// What it does (idempotent — safe to re-run):
    ///   1. Creates UniversalRendererData (Forward, no features).
    ///   2. Creates UniversalRenderPipelineAsset via URP's own factory
    ///      (fills default shader resources).
    ///   3. Assigns the asset as the Graphics default + every Quality level
    ///      (URP global settings are auto-provisioned by the URP package's
    ///      own AssetPostprocessor on domain reload).
    ///   4. Switches the project to Linear color space (URP standard).
    ///
    /// Menu: Metin2 → Migrate to URP
    /// Headless:
    ///   Unity -batchmode -quit -projectPath &lt;repo&gt;
    ///     -executeMethod Metin2.Frontend.EditorTools.UrpMigration.Migrate
    /// </summary>
    public static class UrpMigration
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererPath = SettingsFolder + "/Metin2URPRenderer.asset";
        private const string PipelinePath = SettingsFolder + "/Metin2URP.asset";

        [MenuItem("Metin2/Migrate to URP")]
        public static void Migrate()
        {
            if (!AssetDatabase.IsValidFolder(SettingsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            // 1. Renderer data — Forward, default settings (demo world needs
            //    no renderer features; post-processing stays off).
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                Debug.Log($"[UrpMigration] created renderer data: {RendererPath}");
            }

            // 2. Pipeline asset — URP's own editor factory wires the default
            //    renderer and reloads null shader resources.
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipelineAsset, PipelinePath);
                Debug.Log($"[UrpMigration] created pipeline asset: {PipelinePath}");
            }
            else
            {
                EnsureRendererWired(pipelineAsset, rendererData);
            }

            // 3. Global settings: Unity's own AssetPostprocessor
            //    (UniversalRenderPipelineGlobalSettingsPostprocessor) ensures
            //    and registers them on every domain reload once the URP
            //    package is present — no manual creation needed here.

            // 4. Assign: Graphics default + every quality level override.
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;

            int currentLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelineAsset;
            }

            QualitySettings.SetQualityLevel(currentLevel, false);

            // 5. Linear color space — the URP standard (Gamma is unusual with
            //    URP and the project has no baked assets that depend on it).
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                Debug.Log("[UrpMigration] color space: Gamma -> Linear");
            }

            AssetDatabase.SaveAssets();

            string summary = $"[UrpMigration] done: pipeline={PipelinePath}, renderer={RendererPath}, " +
                $"quality levels={QualitySettings.names.Length}, colorSpace={PlayerSettings.colorSpace}, " +
                $"activePipeline={GraphicsSettings.currentRenderPipeline?.name ?? "none"}";
            Debug.Log(summary);
        }

        /// <summary>
        /// Re-wires the renderer list on an existing pipeline asset (e.g. if
        /// the renderer asset was recreated). SerializedObject path because
        /// the fields are internal to the URP package.
        /// </summary>
        private static void EnsureRendererWired(UniversalRenderPipelineAsset pipelineAsset, UniversalRendererData rendererData)
        {
            var so = new SerializedObject(pipelineAsset);
            var list = so.FindProperty("m_RendererDataList");
            if (list.arraySize > 0 && list.GetArrayElementAtIndex(0).objectReferenceValue == rendererData)
            {
                return;
            }

            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            so.FindProperty("m_DefaultRendererIndex").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[UrpMigration] re-wired renderer list on existing pipeline asset");
        }
    }
}
