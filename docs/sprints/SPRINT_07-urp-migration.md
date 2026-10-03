# Sprint 7 — Render Pipeline Migration (BIRP → URP) + iOS Hazırlığı

**Durum:** ✅ Tamamlandı (URP migration, iOS Build Support, IosBuildScript,
Xcode projesi üretimi). İmzalama/cihaz dağıtımı Sprint 8'de.
**Tarih:** 2026-10-03 · **Teslim:** `uzunbugra/metin2-mobil-ios` (iOS line'ın
ilk push'u; repo kökü `metin2-ios/`, geçmişin tamamı taşındı).

## Problem

Unity 6.5+ ile Built-in Render Pipeline (BIRP) deprecated edildi; proje
Unity 6000.6.4f1'de açıldığında "the built-in render pipeline is deprecated"
uyarısı veriyor. Resmi öneri: URP'ye migrasyon
(docs.unity3d.com/6000.6 — "Migrating from the Built-In Render Pipeline").

## Çözüm

Proje tamamen prosedürel görsel üretiyor (serialize edilmiş materyal yok;
`DemoWorldBehaviour.Colorize` zaten önce `Universal Render Pipeline/Lit`
shader'ını arıyor), dolayısıyla migration settings-tarafı:

1. **Paket:** `com.unity.render-pipelines.universal` **17.6.0** (Unity
   6000.6 ile eşleşen sürüm; packages-lock çözümlemesi doğrulandı).
2. **Asset'ler:** `Assets/Settings/Metin2URP.asset` (pipeline) +
   `Metin2URPRenderer.asset` (Forward renderer, feature'sız). Pipeline
   asset'i URP'nin kendi factory'siyle (`UniversalRenderPipelineAsset.Create`)
   üretildi — default shader resource'ları dolu geliyor.
3. **Atama:** `GraphicsSettings.defaultRenderPipeline` + 6 Quality
   seviyesinin `customRenderPipeline`'ı → Metin2URP. URP global settings
   (`Assets/UniversalRenderPipelineGlobalSettings.asset` +
   `DefaultVolumeProfile.asset`) URP paketinin kendi AssetPostprocessor'ı
   tarafından domain reload'da otomatik oluşturuldu/kayıtlı.
4. **Renk uzayı:** Gamma → **Linear** (URP standardı; bake'li asset yok,
   prosedürel dünya etkilenmiyor).
5. **Editor aracı:** `UrpMigration.cs` (`Metin2.Frontend.EditorTools`) —
   idempotent; menüden (Metin2 → Migrate to URP) veya headless:
   `Unity -batchmode -quit -executeMethod Metin2.Frontend.EditorTools.UrpMigration.Migrate`.
   Yeniden üretilebilirlik için settings değişiklikleri kodla yapıldı,
   elle YAML editlenmedi.

## Yanında düzeltilenler

- `AndroidBuildScript.cs`: Unity 6.6'da `UnityEditor.Build.Reporting.
  SwitchActiveBuildTargetStatus` tipi yok (derleme hatası!) —
  `EditorUserBuildSettings.SwitchActiveBuildTarget` artık `bool` döndürüyor.
  `SetApplicationIdentifier` → `NamedBuildTarget.Android` (obsolete uyarısı).
  Not: batch mode'da build target değişimi desteklenmiyor; headless build
  `-buildTarget <hedef>` arg'ıyla yapılmalı (iOS scriptinde kullanılacak).
- `Metin2.Frontend.Editor.asmdef`: `Unity.RenderPipelines.Universal.Runtime`
  + `Unity.RenderPipelines.Core.Runtime` referansları eklendi.
- Repo hijyeni: `Assets/Scripts/Frontend/Editor/**.meta` dosyaları commit
  dışı kalmıştı (GUID referans riski) — bu sprint'te repoya alındı.

## Eklenen (adım 4)

- `IosBuildScript.cs` (`Metin2.Frontend.EditorTools`): Android aynısı iOS
  uyarlaması — Xcode projesi üretimi (`Builds/iOS`), landscape-only,
  `com.bugrauzun.metin2demo` bundle id. Menü: Metin2 → Build iOS Xcode
  Project; headless `-buildTarget iOS -executeMethod …IosBuildScript.
  BuildXcodeProject`. İmzalama/cihaz dağıtımı Sprint 8'de (Xcode).
  iOS'ta raw TCP için ek izin gerekmez (ATS yalnız HTTP için geçerli).

## Repo yapısı (iOS hattı)

Kullanıcı kararı: iOS hattı `metin2-ios/` kökünde yaşar; `metin2-android/`
(upstream klon) ve `fulldosya/` (server workspace) `.gitignore`'dadır —
yalnızca local referans, push'lanmaz. Upstream'e (YusuffEren/metin2-android)
**push yapılmaz**; teslim push'u `uzunbugra/metin2-mobil-ios`'a yapılır.

## Doğrulama

- Migration batch run: `exit 0`, log: `activePipeline=Metin2URP`,
  `colorSpace=Linear`, 6 quality level atalı.
- `ProjectSettings/GraphicsSettings.asset`: `m_CustomRenderPipeline` →
  Metin2URP guid'i; `m_RenderPipelineGlobalSettingsMap` → global settings kayıtlı.
- `ProjectSettings/QualitySettings.asset`: 6/6 seviye `customRenderPipeline` atalı.
- EditMode testleri: Unity CLI Test Runner bu ortamda `IPrebuildSetup`
  aşamasında tutarlı şekilde kilitleniyor (graphics'sız/batch denemeleri
  dahil) — 685 test headless `dotnet test` yoluyla doğrulandı:
  **685/685, 442 ms** (test host: portable .NET 10 SDK, kurulusuz).
  Not: ardışık koşularda loopback TIME_WAIT birikimi 1-3 ağ testini
  düşürebiliyor (izolasyon/cooldown ile temiz geçiyor) — çevresel
  flakiness, kod hatası değil.
- **iOS Build Support** kurulu (Unity Hub CLI:
  `Unity Hub --headless install-modules -m ios -v 6000.6.4f1`).
- **iOS Xcode projesi headless üretildi**:
  `Unity -batchmode -quit -buildTarget iOS -executeMethod
  …IosBuildScript.BuildXcodeProject` → `Builds/iOS/Unity-iPhone.xcodeproj`
  (IL2CPP çıktısı dahil, ~960 MB). Build script'i PlayerSettings'ı
  yazdı: `bugrauzun` / `Metin2 Demo` / `com.bugrauzun.metin2demo`,
  landscape-only. URP asset'lerinde Unity'nin versiyon-yükseltme
  serialization geçişi (renderer GUID bağlantısı korunarak) commit'lendi.

## Bilinen riskler

- `Shader.Find("Universal Render Pipeline/Lit")` runtime materyal üretimi
  cihaz build'lerinde stripping'e takılabilir → ilk cihaz build'inde
  doğrula, gerekirse Always Included Shaders'a ekle (Sprint 8).
- Xcode imzalama henüz yapılmadı (Sprint 8: team id + provisioning).
