# Sprint Planı — Mevcut Durum ve Yol Haritası (iOS)

> Bu doküman projenin şu ana kadar tamamlanan kısımlarının özeti ve iOS odaklı
> yol haritasıdır. Sprint kayıtları `docs/sprints/`, değişiklik günlüğü
> `CHANGELOG.md` içinde izlenir. Hedef repo: `uzunbugra/metin2-mobil-ios`
> (upstream: `YusuffEren/metin2-android`).

## 1. Mevcut Durum (Sprint 0–6, 58 commit)

Protokol ve ağ katmanı **kaynak-kanıtla** (server + eski client C++ source,
dosya+satır referanslı) inşa edildi; önyüz temeli DemoServer üzerinden
çalışır durumda.

| Katman | Durum | Özet |
|---|---|---|
| Sprint 0 — Kaynak audit | ✅ | Workspace haritası, bağlantı akışı, 49 paketlik katalog (42 VERIFIED) |
| Sprint 1 — Unity altyapı | ✅ | 5 assembly (Core/Protocol/Network/Gameplay/Frontend), golden byte test'leri |
| Sprint 2 — Protocol core | ✅ | TCP transport, framer (dinamik header 5 dahil), phase-aware registry, DH2 + key derivation + CTR |
| Sprint 3 — Cipher engine'leri | ✅ | 13/13 engine KAT'li (TEA, RC6, IDEA, RC5, SHACAL-2, Blowfish, 3DES, Twofish, Serpent, MARS, CAST-256, Camellia, SEED) + `HandshakeClient` |
| Sprint 4 — Login zinciri | ✅ | `AuthLoginClient` (CG_LOGIN3 + PanamaKey) → `ChannelLoginClient` → `CharacterSelectClient` → `WorldEntryClient` |
| Sprint 5 — Dünya temeli | ✅ | `GameWorldClient` (stats/spawn), `InventoryClient` (okuma+yazma), `MovementClient`, `CombatClient`, keepalive ping/pong |
| Sprint 6 — Önyüz (devam) | 🟡 | `GameFlow` + `DemoServer` (uçtan uca), `DemoWorldBehaviour` (server-authoritative görsel), `DemoAppBehaviour` (Login→Karakter Seç, UGUI kod-tarzı), dokunmatik joystick + SALDIR bölgesi, Android build script, Unity 6000.6.4f1 migrasyonu |

**Test: 685/685** (headless; Unity EditMode Test Runner ile aynı testler).

**Ortam notları (bu makine):**
- Unity 6000.6.4f1 kurulu (macOS, `/Applications/Unity/Hub/Editor/6000.6.4f1`).
- Platform modülleri (Android/iOS Build Support) **kurulu değil** — Unity Hub'dan
  eklenmeli (iOS Build Support: Xcode + iOS platform modülü).
- Headless test csproj'ları gitignore'da (README'de bilinen eksik); taze
  clone'da testler Unity EditMode Test Runner ile ya da csproj yeniden
  üretilerek koşulmalı.

## 2. Yol Haritası

### Sprint 7 — Render Pipeline Migration + iOS Hazırlığı *(bu sprint)*
**İşler**
1. **BIRP → URP migration** (Unity 6.5+ Built-in Render Pipeline deprecated):
   URP 17.6.0 paketi, `Assets/Settings/Metin2URP(.asset|Renderer.asset)`,
   Graphics + tüm Quality seviyeleri, Linear renk uzayı. `UrpMigration.cs`
   (menü: Metin2 → Migrate to URP; batch-mode çalıştırılabilir, idempotent).
2. `AndroidBuildScript.cs` Unity 6.6 API düzeltmesi
   (`SwitchActiveBuildTargetStatus` kaldırıldı → `bool`; `NamedBuildTarget`).
3. Unity Hub'dan iOS Build Support kurulumu.
4. `IosBuildScript.cs` (AndroidBuildScript aynısı): landscape, Xcode projesi
   üretimi (`-buildTarget iOS` batch arg'ı ile headless).

**Çıkış kriterleri**
- Proje Unity 6.6'da deprecated uyarısı olmadan açılıyor; Demo dünyası URP
  altında Play modunda görsel olarak doğru (pembe materyal yok).
- EditMode testleri geçiyor (685/685).
- iOS Xcode projesi üretilebiliyor (imzalama sonraki sprint).

### Sprint 8 — iOS Cihaz Entegrasyonu
**İşler**
- Xcode imzalama + gerçek cihazda çalıştırma (demo modu).
- Safe area (notch/Dynamic Island), ekran yönelimi doğrulaması.
- Yaşam döngüsü (guide §9.2): background'a geçme, ekran kilidi, bağlantı
  kopması → reconnect state machine; `GameFlow` dispose/re-login yolu.
- İlk performans profili (frame time, RAM, batarya — referans cihaz belirlenip).

**Çıkış kriterleri**
- Demo dünyası iOS cihazda oynanabilir (joystick + SALDIR çalışıyor).
- Background→foreground dönüşünde oturum güvenli şekilde kapanıyor veya
  yeniden bağlanıyor (sunucu otoritesi korunuyor).

### Sprint 9 — Canlı Sunucu Bağlantısı (Staging)
**İşler**
- `NetworkConfig` ile staging auth/channel endpoint'leri (gerçek IP/port).
- Canlı handshake + login denemesi — README'deki bilinen eksik: "canlı sunucuya
  karşı uçtan-uca handshake decode" (offline kanıt tamam).
- PC istemcisiyle paralel regresyon (aynı hesap, ayrı oturum).
- UNVERIFIED/PARTIALLY kalan 7 paketin canlı trafikle kapatılması.

**Çıkış kriterleri**
- Gerçek sunucuda auth → channel → karakter listesi görüntüleniyor.
- PC istemcisi login'i bozulmuyor; DB'ye yazma yok (staging izolasyonu).

### Sprint 10 — İçerik Pipeline'ı
**İşler**
- `item_proto`/`mob_proto` dönüştürücü (DumpProto kaynak; deterministik,
  input hash raporlu — guide §7.4).
- Item ikonları + envanter UI (slot bazlı, server event'leriyle senkron).
- Tek harita parçası: Metin2 map formatı araştırması + koordinat dönüşümü
  (`ToWorld` genelleyecek), ilk gerçek harita üzerinde spawn.

**Çıkış kriterleri**
- En az bir item gerçek proto verisinden render ediliyor; use/move sunucu
  onaylı çalışıyor.
- Karakter gerçek harita parçasında spawn oluyor.

### Sprint 11 — Gameplay Derinliği
**İşler**
- Hedefleme (tap-to-target), skill saldırıları (CG_ATTACK type>0 + skill
  cooldown UI), HP/SP bar + HUD (UGUI, OnGUI'den göç).
- Envanter UI etkileşimleri: use/equip/unequip/drop/pickup.
- Combat feedback: hasar sayıları, motion/animasyon tetikleme.

**Çıkış kriterleri**
- Combat sonucu tamamen server-authoritative; client manipülasyonu etkisiz.

### Sprint 12 — Sosyal Sistemler
**İşler**
- Chat (CG_CHAT + GC chat paketleri, kaynak iz sürme ile).
- NPC etkileşim + shop (envanter aksiyon altyapısı üzerine).
- Party temelleri.

**Çıkış kriterleri**
- İki client (mümkünse PC + mobil) aynı kanalda birbirini görüyor ve
  etkileşiyor.

## 3. Teslim ve Push Kuralı

- Her sprint sonunda: `git push ios main` → `uzunbugra/metin2-mobil-ios`.
- Sprint kaydı `docs/sprints/SPRINT_XX-*.md` + `CHANGELOG.md` + README
  durum tablosu aynı commit'te güncellenir (repo kuralı).
- Upstream (`YusuffEren/metin2-android`) fetch kaynağı olarak kalır;
  senkron gerekirse `git pull origin main` + conflict çözümü.

## 4. Riskler / Açık Sorular

- `Shader.Find("Universal Render Pipeline/Lit")` ile runtime materyal
  üretimi cihaz build'lerinde shader stripping'e takılabilir — ilk cihaz
  build'inde doğrulanmalı; gerekirse Always Included Shaders'a eklenir
  (Sprint 8).
- iOS'ta raw TCP soket izni gerekmez ancak App Store incelemesi için
  lokal ağ / sunucu adresi beyanı değerlendirilmeli.
- Canlı sunucu testi DB'ye karakter yazımı yapar — yalnızca staging ve test
  hesabıyla (guide §10.4).
