using System.IO;
using UnityEditor;
using UnityEngine;

namespace Metin2.Frontend.EditorTools
{
    /// <summary>
    /// SP10-5 pilot pipeline: Metin2 GR2 -> divine (LSLib) -> GLB -> Unity.
    ///
    /// "Render": wolf.glb import doğrulaması — sahneye koyup kamera ile PNG
    /// üretir ve mesh/skin istatistiklerini loglar.
    ///
    /// "BuildPrefab": wolf.glb'den demo-sahnesi prefab'ı üretir:
    ///   - 0.01 ölçek (Metin2 birimleri cm -> Unity m)
    ///   - URP Lit materyal + wolf.png dokusu (Metin2URPLit shader pin'i)
    ///   - Çıktı: Assets/Resources/GameData/Characters/wolf.prefab
    ///     (ADR-0003: DATA commit edilmez; pipeline yeniden üretir)
    ///
    /// Kullanım:
    ///   Unity -batchmode -executeMethod Metin2.Frontend.EditorTools.WolfPilot.Render
    ///   Unity -batchmode -executeMethod Metin2.Frontend.EditorTools.WolfPilot.BuildPrefab
    /// </summary>
    public static class WolfPilot
    {
        private const string GlbPath = "Assets/Art/Characters/wolf/wolf.glb";
        private const string TexturePath = "Assets/Art/Characters/wolf/wolf.png";
        private const string PrefabDir = "Assets/Resources/GameData/Characters";
        private const string PrefabPath = PrefabDir + "/wolf.prefab";
        private const string OutputDir = "Extracted/gr2-pilot";
        private const float UnitScale = 0.01f; // Metin2 cm -> Unity m

        public static void Render()
        {
            // Ham GLB: GLTFast materyali cam.Render() ile uyumlu (URP Lit değil).
            var instance = InstantiateWolf(preferPrefab: false);
            LogStats(instance);

            // Önizleme dokusu: GLTFast materyalini klonla, wolf.png ata.
            // (Metin2URPLit/URP shader'ları cam.Render() built-in yolunda
            // magenta görüntülenir; oyun içinde URP aktifken sorun yok.)
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture != null)
            {
                foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr.sharedMaterial == null) continue;
                    var previewMat = new Material(smr.sharedMaterial);
                    previewMat.mainTexture = texture;
                    smr.sharedMaterial = previewMat;
                }
                Debug.Log("[WolfPilot] önizleme dokusu atandi");
            }

            var renderer = instance.GetComponentInChildren<Renderer>();
            var bounds = renderer != null ? renderer.bounds : new Bounds(Vector3.zero, Vector3.one);

            var go = new GameObject("cam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.2f, 0.24f);
            cam.transform.position = bounds.center + new Vector3(
                bounds.size.x * 1.2f,
                bounds.size.y * 0.35f,
                -bounds.size.z * 0.9f);
            cam.transform.LookAt(bounds.center);

            AddLight(bounds, new Vector3(-3f, 4f, -3f));
            AddLight(bounds, new Vector3(3f, 4f, 3f));

            var rt = new RenderTexture(1024, 1024, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            Directory.CreateDirectory(OutputDir);
            var pngPath = Path.Combine(OutputDir, "wolf-preview.png");
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Debug.Log($"[WolfPilot] PNG yazildi: {pngPath}");

            EditorApplication.Exit(0);
        }

        public static void BuildPrefab()
        {
            var instance = InstantiateWolf();
            LogStats(instance);

            // Metin2 cm -> Unity m
            instance.transform.localScale = Vector3.one * UnitScale;

            // Doku + materyal (shader pin: Resources/Metin2URPLit.mat)
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var baseMaterial = Resources.Load<Material>("Metin2URPLit");
            if (texture != null && baseMaterial != null)
            {
                var material = Object.Instantiate(baseMaterial);
                material.name = "wolf";
                material.mainTexture = texture;
                foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    smr.sharedMaterial = material;
                }
                Debug.Log($"[WolfPilot] materyal atandi: {texture.name}");
            }
            else
            {
                Debug.LogWarning($"[WolfPilot] doku/materyal atlanamadi: texture={texture != null}, baseMat={baseMaterial != null}");
            }

            Directory.CreateDirectory(PrefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            Debug.Log($"[WolfPilot] prefab yazildi: {PrefabPath} (valid={prefab != null})");

            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        private static GameObject InstantiateWolf(bool preferPrefab = true)
        {
            // Önce pipeline prefab'ı (doku + ölçek), yoksa ham GLB.
            var prefab = preferPrefab ? Resources.Load<GameObject>("GameData/Characters/wolf") : null;;
            if (prefab != null)
            {
                var p = (GameObject)Object.Instantiate(prefab);
                // Prefab 0.01 ölçekli; render için bounds zaten gerçek ölçekte.
                p.transform.localScale = Vector3.one;
                p.name = "wolf";
                Debug.Log("[WolfPilot] prefab'dan (dokulu) yuklendi");
                return p;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(GlbPath) as GameObject;
            if (asset == null)
            {
                Debug.LogError($"[WolfPilot] GLB yüklenemedi: {GlbPath} (pipeline: divine convert-model -> {GlbPath})");
                EditorApplication.Exit(2);
                return null;
            }

            var instance = (GameObject)Object.Instantiate(asset);
            instance.name = "wolf";
            return instance;
        }

        private static void LogStats(GameObject instance)
        {
            int meshCount = 0, boneCount = 0, vertCount = 0, triCount = 0;
            foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                meshCount++;
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                vertCount += mesh.vertexCount;
                triCount += (int)(mesh.GetIndexCount(0) / 3);
                boneCount = smr.bones.Length;
                Debug.Log($"[WolfPilot] SMR '{smr.name}': {mesh.vertexCount} vert, {mesh.GetIndexCount(0)/3} tri, {smr.bones.Length} bone");
            }
            foreach (var mf in instance.GetComponentsInChildren<MeshFilter>())
            {
                meshCount++;
                vertCount += mf.sharedMesh.vertexCount;
                triCount += (int)(mf.sharedMesh.GetIndexCount(0) / 3);
            }

            var renderer = instance.GetComponentInChildren<Renderer>();
            var bounds = renderer != null ? renderer.bounds : new Bounds(Vector3.zero, Vector3.one);
            Debug.Log($"[WolfPilot] toplam: {meshCount} mesh, {vertCount} vert, {triCount} tri, {boneCount} bone, bounds={bounds.size}");
        }

        private static void AddLight(Bounds target, Vector3 offset)
        {
            var lightGo = new GameObject("light");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            // Directional ışıkta yalnızca YÖN önemli; merkeze uzaktan bak.
            var dir = offset.normalized;
            lightGo.transform.position = target.center - dir * target.size.magnitude;
            lightGo.transform.LookAt(target.center);
        }
    }
}
