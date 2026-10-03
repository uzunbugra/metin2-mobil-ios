# Sprint 8 — iOS Cihaz Entegrasyonu

**Durum:** 🟡 Devam ediyor (kod tarafı tamamlandı; cihaz imzalama kullanıcı
tarafını bekliyor).
**Tarih:** 2026-10-03 · **Teslim:** `uzunbugra/metin2-mobil-ios`

## İşler

### 1. Safe area (notch / Dynamic Island / home indicator) ✅
- `DemoWorldBehaviour.OnGUI`: HUD kutuları (sol-üst, sol-alt event log,
  sağ-üst controls) artık `Screen.safeArea`'dan inset alıyor — ham ekran
  köşelerinden değil. GUI koordinat dönüşümü (y-flip) belgelendi.
- `AttackZoneScreen`: SALDIR bölgesi safe area'ya göre konumlanıyor (hem
  dokunma hit-test hem görsel aynı rect'i kullanıyor — home indicator
  dokunuşları yutamaz).
- Joystick dinamik origin'li olduğu için safe area gerektirmiyor.

### 2. Yaşam döngüsü (guide §9.2) ✅
- **Sahiplik modeli:** `DemoAppBehaviour` oturumun sahibi; dünyaya
  `Attach` ile devrediyor (`_worldAttached` bayrağı). `DetachAndTearDown`
  akışı **tam bir kez** dispose ediyor (double-dispose guard'lı).
- `DemoWorldBehaviour.DetachAndTearDown()`: event pump iptali (_cts),
  entity/sun/ground GameObject'lerinin imhası, flag resetleri — idempotent.
- `DemoAppBehaviour.OnApplicationPause(true)`: arka plana geçişte oturum
  deterministic kapanır (sunucu keepalive timeout ~60 s zaten kapatır —
  zombie bağlantı yok), login ekranı yeniden kurulur + durum mesajı.
  Aktif oturum yoksa no-op (statik login ekranı bozulmaz).
- `OnApplicationQuit` aynı sahiplik modeline çekildi.
- Bilinen sınır (demo kabulü): pause, `OnSlotClicked` await'inin tam
  bitişiyle `Attach` arasındaki mikro pencereye denk gelirse continuation
  yeni login ekranına düşer — flow dispose edildiği için await'ler
  exception fırlatır, catch güvenli yol alır.

### 3. Shader stripping fix ✅
- `UrpMigration.EnsureLitShaderIncluded()`: `Assets/Resources/Metin2URPLit.mat`
  (URP Lit, beyaz) — Resources altındaki materyal build'e her zaman dahil
  edilir ve shader'ını yanında taşır. Runtime
  `Shader.Find("Universal Render Pipeline/Lit")` materyal üretimi artık
  cihaz build'lerinde pembe materyal riskine karşı sabitlenmiş.
  (SPRINT_07 risk maddesi kapandı.)

### 4. Performans temeli ✅
- `Application.targetFrameRate = 60` (iOS default 30'dur; açık hedef).

### 5. Xcode imzalama + cihazda çalıştırma ⏳ (kullanıcı adımı)
Headless yapılamaz — Apple Developer hesabı/girişi gerekir:
1. `Builds/iOS/Unity-iPhone.xcodeproj` dosyasını Xcode'da aç.
2. Signing & Capabilities → Team seç (ücretsiz kişisel takım yeterli,
   7 günlük provisioning).
3. USB ile iPhone bağla → cihazı seç → Run.
4. Cihazda Ayarlar → Genel → VPN ve Cihaz Yönetimi → geliştirici
   sertifikasına güven.
5. Demo modu: Play → login (demo/demo) → karakter seç → joystick + SALDIR.

Not: uygulama ilk açılışta lokal ağ/socket izni isteyebilir (iOS 14+
local network privacy) — onaylanmalı.

## Doğrulama

- Unity batch derleme: temiz (bkz. commit; `-executeMethod
  UrpMigration.Migrate` exit 0).
- `Metin2URPLit.mat` oluşturuldu (Resources pin).
- Headless test paketi etkilenmedi (Frontend katmanı headless grafiğinde
  yok); Sprint 7'deki 685/685 sonucu geçerli.

## Kalan riskler

- Cihazda ilk koşu: `Shader.Find` pin'i (yukarıda) + URP asset'lerinin
  iOS IL2CPP build'inde davranışı — cihazda görsel doğrulama bekleniyor.
- On-device performans profili henüz çıkarılmadı (referans cihaz kararı
  ürün kararı — guide §9.3).
