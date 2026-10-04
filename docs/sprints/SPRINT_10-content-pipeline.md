# Sprint 10 — İçerik Pipeline'ı (fulldosya asset'leri)

**Durum:** 🟡 Devam ediyor (SP10-1 ✅, SP10-2 ✅, SP10-3 kısmi ✅, SP10-8 ✅;
sırada SP10-4 ikon pipeline + Unity proto import).
**Tarih:** 2026-10-03 başlangıç · **Teslim:** `uzunbugra/metin2-mobil-ios`
**Lisans çerçevesi:** ADR-0003 — KOD commit edilir, DATA commit edilmez;
extract çıktıları (`Extracted/`) local-only, gitignore'da.

## SP10-1 — EIX/EPK Extractor CLI ✅

`Tools/PackExtractor/` (net10.0, sıfır dış bağımlılık — tüm formatlar
kaynak portu):

| Bileşen | Kaynak referansı |
|---|---|
| `Tea.cs` | EterBase/tea.cpp (Wheeler-Needham 32 round, LE DWORD; `unchecked` ile 4307 pragması karşılığı) |
| `Lzo1x.cs` | Linux kernel lib/lzo/lzo1x_decompress_safe.c portu (bitstream_version=0 → klasik minilzo semantiği; C# scope kuralları için döngü-öncesi blok inline, bounds try/catch) |
| `Crc32.cs` | EterBase/CRC32.cpp (zlib tablosu, init/xor FFFFFFFF) |
| `Mcoz.cs` | EterBase/lzo.cpp CLZObject (16B header + [iç fourcc + LZO + pad]; anahtar doğrulaması iç fourcc ile) |
| `EterPackReader.cs` | EterPack.cpp __BuildIndex:407-465 + Get dispatch:528-628 |
| `Program.cs` | list/get/extract/report/dumpindex; path sanitization (guide §7.1) |

**Doğrulama (gerçek veriyle, locale_tr pack):**
- MCOZ index decrypt (IndexKey) + LZO1X → EPKD v2, 242 slot ✓
- **Ampirik format düzeltmesi:** entry = **192 B** (188 değil — pack(4)
  hizalaması filename[161] sonrası 3 pad; kanıt: 46476 = 12 + 242×192).
- `item_proto` (tip 0) → MIPX v1, stride 156, count 5929 + iç MCOZ ✓
- `atlasinfo.txt` (tip 2 SECURITY) → CRC32 + TEA(SecurityKey) + LZO →
  gerçek metin içeriği ✓
- Yan bulgu: index `real_data_size` tip≥1'de güvenilmez — blob dwRealSize
  esas (client parity).
- Tip 3/4/5 (PANAMA/HybridCrypt) fail-closed NotSupportedException.

**Kullanım:**
```bash
dotnet run --project Tools/PackExtractor -- list   fulldosya/fullbinary/pack/locale_tr.eix
dotnet run --project Tools/PackExtractor -- get    fulldosya/fullbinary/pack/locale_tr.eix locale/tr/item_proto Extracted/locale_tr/item_proto
dotnet run --project Tools/PackExtractor -- extract fulldosya/fullbinary/pack/icon.eix Extracted/icon
dotnet run --project Tools/PackExtractor -- report fulldosya/fullbinary/pack/PC.eix Extracted/PC.report.json
```

## SP10-8 — Lisans ADR ✅

`docs/decisions/ADR-0003-asset-licensing-and-usage.md` (subagent): dahili
kullanım sınırı, store dağıtım DMCA riski, "KOD commit / DATA commit etme"
politikası, extract çıktı yollarının .gitignore'a eklenmesi (uygulandı:
`Extracted/`, `Assets/Art/`, `Assets/**/*.{dds,tga,gr2,fbx}`).

## SP10-2 — Proto Converter ✅

`Tools/ProtoConverter/` (net10.0; TEA/LZO1X/MCOZ/CRC32 PackExtractor'tan
link-compile):

- `MobProto.cs` — MMPT + MCOZ(mob key) → TMobTable[255] parse → JSON
  (alan ofsetleri PythonNonPlayer.h:57-114'ten; struct boyu ampirik
  doğrulandı: 343.485 = 1.347 × 255).
- `ItemProto.cs` — MIPX v1 + stride(156) + MCOZ(item key) →
  TClientItemTable[156] parse → JSON; **item_list.txt** (icon/model
  yolları) ve **itemdesc.txt** (CP1254) vnum-key merge.
- `Cp1254.cs` — Latin-1 + 5 Türkçe override (bağımlılıksız CP1254).
- Deterministik kaynak SHA-256 + count/stride doğrulaması (fail-closed).

**Doğrulama (locale_tr, gerçek veri):**
- mob_proto: **1.347 mob** → JSON (1,0 MB); MOB 101 "Yabani Köpek"
  (CP1254 ✓), folder `stray_dog` (model klasör alanı ✓), Lykos ✓.
- item_proto: **5.929 item** (6.234 ikon eşlemesi, 2.406 açıklama) →
  JSON (3,5 MB); ITEM 19 "Kılıç+9" + `icon/item/00010.tga` ✓.
- Not: `szName` iç isimler **CP949 (Korece)** — localeName (CP1254)
  görüntü alanıdır; Unity tarafında localeName kullanılacak. CP949
  decode gerekiyorsa sonradan eklenebilir.

**Kullanım:**
```bash
dotnet run --project Tools/ProtoConverter -- mob  Extracted/locale_tr/mob_proto  Extracted/locale_tr/mob_proto.json
dotnet run --project Tools/ProtoConverter -- item Extracted/locale_tr/item_proto Extracted/locale_tr/item_proto.json \
  --item-list Extracted/locale_tr/item_list.txt --itemdesc Extracted/locale_tr/itemdesc.txt
```

> JSON çıktıları telifli isim stringleri içerir → **local-only**
> (Extracted/, ADR-0003). Unity ScriptableObject import'u (numeric
> alanlar + sprite referansları) sıradaki adım — `ItemDef`/`MobDef`
> üreten editor script.

## SP10-3 — Pack Envanter Raporları ✅ (kısmi)

`report` komutuyla index-seviyesi envanter (extract değil — hızlı):

| Pack | Dosya | Boyut | İçerik |
|---|---|---|---|
| icon | 1.952 | 5 MB | 1.577 TGA ikon + 220 .sub + 153 DDS |
| PC | 2.704 | 70 MB | 690 .gr2 + 738 .msa + 528 .mse + 520 .dds |
| Monster | 3.092 | 67 MB | 1.483 .gr2 + 1.302 .msa + 100 .msm + 124 .dds |

Bulgu: PC/Monster pack'lerindeki dosyaların tamamına yakını tip 1
(şifresiz MCOZ/LZO) — SECURITY tip 2 yalnızca birkaç dosyada. Yani model/
animasyon extraction'ı anahtar gerektirmeden çalışacak.

## SP10-7 — .sub Sprite Slicer ✅

`Tools/SubSlicer/` (net10.0, sıfır bağımlılık; subagent üretimi, gözden
geçirildi):

- `SubImage.cs` — GrpSubImage.cpp:65-133 + FileLoader.cpp tokenizer portu;
  v1.0 ("D:/Ymir Work/UI/" öneki) + v2.0 (göreli yol) dalları; tırnak
  içinde boşluklu değerler; token≠2 → dosya reddi (client parity); koordinat
  atoi() semantiği (eksik değer 0).
- CLI: `metin2-subslicer parse <inputDir> <out.json> [--recursive]`.
- **Gerçek veri doğrulaması:** ETC pack'ten 784/784 .sub parse, 0 atlanan;
  48 farklı atlas (windows.dds 136 ref, public.dds 118, taskbar.tga 89…);
  dikdörtgen boyutları 3–495 × 4–357 px.
- Çıktı: `Extracted/subs.json` — Unity tarafı atlas DDS + rect'ten Sprite
  üretecek (sonraki adım: UI mock demo).

## SP10-6 — Motion Metadata Parser ✅

`Tools/MotionParser/` (net10.0, sıfır bağımlılık; subagent üretimi, gözden
geçirildi — 949 satır, 4 dosya):

- `TextScript.cs` — CTextFileLoader portu (Group/List blokları, quoted
  string, `SetChildNode(name, index)` prefix-eşlemeli indeksli gruplar).
- `MotListParser.cs` — RaceManager.cpp:180-326 birebir port: tip tablosu +
  2-karakter truncation fallback (WAIT4→NAME_WAIT); bilinmeyen tipler
  `canonical: null` + uyarı (client sessizce atlıyor — veri kaybı olmasın).
- `MsaParser.cs` — RaceMotionData.cpp:309-472 + RaceMotionDataEvent.h +
  GameType.cpp: attacking/hitPositions/event blokları.
- CLI: `metin2-motionparser parse <raceRootDir> <out.json>`.

**Gerçek veri doğrulaması:**
- **Monster pack'teki 83 ırğın tamamı** (83/83 başarı, 0 hata); wolf
  motlist + .msa tam detay (attacking + hitPositions + event'ler).
- Warrior: 36 motion, 21 event (sentetik motlist + gerçek .msa — aşağıya bak).
- Doğrulanan event tipleri: EFFECT, SCREEN_WAVING, SPECIAL_ATTACKING, FLY,
  EFFECT_TO_TARGET.

**Önemli bulgu — PC pack'tinde motlist.txt yok:** tüm pack'ler tarandı;
PC ırklarının motlist'leri bu dump'da pack dışından geliyor (loose dosya/
patcher). İstemci varsayılanı `motlist.txt` (RaceData.cpp:549,581). GR2
pilot adımında (SP10-5) warrior motlist'inin başka kaynaktan bulunması
gerekecek — ya da Monster ırklarıyla (motlist'leri pack içinde) devam
edilmeli.

**Format notları (kaynak referanslı):** event tipi 4 iki farklı varyantla
geliyor (sade attack skalerleri vs tam AttackingData bloğu); `HitPosition`
satırları 7 float; mode token'ı client tarafından yok sayılıyor.

**Filtre notu:** subagent raporundaki "filtre tüm pack'i çıkarıyor"
gözlemi doğrulanamadı — `--filter "monster/wolf/"` listelemede 42 kayıt
döndürüyor (beklenen); bug yok.

## Kalan adımlar (sırayla)

1. ~~**SP10-5 GR2 pilot dönüşüm**~~ ✅ (aşağıda)
2. **Unity UI sprite üretimi**: atlas DDS + `subs.json` rect'lerinden
   Sprite üreten editor script (SP10-7 çıktısı üzerine).
3. **Motion parser → Unity**: animasyon import otomasyonu (SP10-6 çıktısı
   üzerine; GR2 dönüşümünden sonra anlamlı).
4. **GR2 toplu dönüşüm**: wolf pilotunu üretim zincirine genişletme
   (Monster pack 83 ırk; animasyon GR2'leri motlist eşlemesiyle).

## SP10-5 — GR2 Model Pilot Dönüşümü (wolf) ✅

**Hedef:** Metin2 `wolf.gr2` → Unity'de gerçek model. Planlanan zincir
LSLib(divine)→DAE→Blender→FBX idi; **Blender 5.x Collada desteğini
kaldırdığı için** zincir sadeleşti: **GR2 → divine → GLB → GLTFast**.

### Araç zinciri (macOS ARM64, kaynak derleme)

Upstream LSLib release binary'si x64-only (Rosetta yok) → kaynaktan
derlendi: `/Users/bugra/Desktop/projects/lslib/lslib-src`
(depth-1 clone + unshallow, local çalışma kopyası, upstream'e push yok).

Derleme düzeltmeleri (hepsi macOS/ARM64 derlemesi için; GR2 dönüşüm
yolunu etkilemeyen story-parser düzeltmeleri dahil):

- 2018 üretilmiş story parser dosyaları git geçmişinden restore
  (25ba95c~1): `Goal.lex.cs`/`Goal.yy.cs`/`StoryHeader.lex.cs`/
  `StoryHeader.yy.cs` — Windows-only GPPG/GPLEX pre-build adımını atlar.
- `#line` direktifleri temizlendi, `using QUT.Gppg;` eklendi.
- Parser generic'leri el yazımı ScanBase'lerle hizalandı:
  Goal → `ShiftReduceParser<object, CodeLocation>`,
  Header → `ShiftReduceParser<ASTNode, LexLocation>`.
- `GoalParser.LegacyCompat.cs`: 2018 grammar tablolarının çağırdığı
  eski ariteli `Make*` overload'ları (grammar evriminde parametre
  eklenmiş; story decompile dışında kullanılmıyor).
- `FastLZCompressor` stub'ı (LSLib.Native; VTex-only, fail-closed).
- `LSLibNative.vcxproj` referansı kaldırıldı (C++/CLI, macOS'ta yok);
  `PackageReader.cs` yönetilen K4os LZ4'e çevrildi.
- `PlatformTarget` x64 → AnyCPU (ARM64 süreçte çalışması için).
- Divine CLI Unix yol doğrulaması düzeltildi (`Uri.IsFile` →
  `Path.IsPathRooted`; `/Users/...` relative URI sayılıyordu).
- .NET 8 hedefli DLL, portable .NET 10 SDK ile
  `DOTNET_ROLL_FORWARD=Major` ile koşuyor.

### Granny Oodle-1 decompression (codec 2) — saf C# port

Metin2 GR2 dosyaları **sıkıştırılmış bölümler** kullanıyor (granny
codec 2 = `GrannyOodle1Compression`; bkz. client granny.h). granny2.dll
Windows x86 — macOS'ta yüklenemez. Çözüm:
**[arves100/opengr2](https://github.com/arves100/opengr2)** (MPL-2.0)
`oodle1.c`'nin saf C# portu: `LSLib/Granny/GR2/Oodle1.cs`
(3×12B TParameter başlığı, 3 sözlüklü aritmetik kod çözücü, stop
offset'leri section header'ın first16bit/first8bit alanlarından).
`Granny2Compressor.Decompress` codec 2'de artık yönetilen yolu kullanıyor.

### LSLib düzeltmeleri (Metin2 dosyaları için)

1. **`MemberDefinition.LookupFieldInfo`** (Format.cs): FieldInfo cache'i
   aynı isimli alanlarda (`UserDefinedProperties` hem Bone hem Mesh
   ExtendedData'da) yanlış tipe bağlanıyordu → declaring-type uyumsuzsa
   yeniden çözümleme.
2. **`GLTFVertex` vertex remap**: SharpGLTF `UseVertex` eşit vertex'leri
   deduple edip gerçek indeks döndürüyor; LSLib üçgenleri orijinal GR2
   indeksleriyle yazıyordu → 762 vertex / 98 tekrar → aksesör taşması.
   Şimdi `UseVertex` dönüş değerinden remap tablosu kuruluyor.
3. **Z-up tespiti** (Exporter.cs): GR2 yüklerken `Root.ZUp` hiç
   set edilmiyordu (yalnız Collada import'ta). ArtToolInfo
   `UpVector == (0,0,1)` (3ds Max) ise `ZUp = true` →
   `ConvertToYUp` uygulanıyor. Wolf: 44.8 × 95.8(Y) × 220 (cm).
4. **Normal sanitizasyonu** (GLTFVertex.cs): eski mesh'lerde
   normalize edilmemiş/NaN normaller → birim uzunluk + sıfır/NaN
   fallback (+Z). glTF şartı 0.99–1.01.
5. **`ExportMeshExtensions` null-guard**: Divinity mesh-properties
   bloğu Metin2'de yok → atlanıyor.

### Pipeline (tekrarlanabilir)

```bash
# 0. LSLib derle (yukarıdaki düzeltmelerle; local: ~/Desktop/projects/lslib/lslib-src)
DOTNET_ROOT=<sdk> dotnet build Divine/Divine.csproj -c Release -p:PreBuildEvent=

# 1. wolf'u Monster pack'ten çıkar (SP10-1 aracı)
dotnet run --project Tools/PackExtractor -- extract \
  fulldosya/fullbinary/pack/Monster.eix Extracted/Monster --filter "monster/wolf/"

# 2. GR2 → GLB (Oodle-1 + Z-up dönüşümü divine içinde)
DOTNET_ROLL_FORWARD=Major dotnet <divine>/Divine.dll -a convert-model -g dos2 \
  -s Extracted/gr2-pilot/wolf.gr2 -d Extracted/gr2-pilot/wolf.glb

# 3. Doku: DDS → PNG (Unity DDS importçusu Metin2 DDS'ini reddediyor;
#    Blender headless ile dönüştür)
blender --background --python-expr "<load dds, save png>"

# 4. Unity: GLB + PNG'yı Assets/Art/Characters/wolf/'a kopyala, sonra
Unity -batchmode -executeMethod Metin2.Frontend.EditorTools.WolfPilot.BuildPrefab
#    → Assets/Resources/GameData/Characters/wolf.prefab
#    (0.01 ölçek cm→m, Metin2URPLit materyal + wolf.png doku)

# 5. Görsel doğrulama (opsiyonel)
Unity -batchmode -executeMethod Metin2.Frontend.EditorTools.WolfPilot.Render
#    → Extracted/gr2-pilot/wolf-preview.png
```

### Doğrulama

- GLB: 663 pozisyon (762'den dedup), 966 üçgen, 40 eklem, tam vertex
  atributları (POSITION/NORMAL/TEXCOORD_0/JOINTS_0/WEIGHTS_0), Y-up,
  ayaklar y≈0. SharpGLTF save-time validasyonundan geçiyor.
- Unity import (GLTFast `com.unity.cloud.gltfast` 6.20.0):
  SkinnedMeshRenderer 'Object02' 663 vert / 966 tri / 40 bone.
- Render analizi: 3/4 açıdan dört ayaklı siluet (kulaklar üstte, gövde
  ortada, bacaklar altta, kuyruk sola süpürüyor), koyu gri kürk dokusu
  (wolf.png), magenta yok (shader sağlam).
- `DemoWorldBehaviour.SpawnEntity`: SpawnedMobVid artık
  `Resources/GameData/Characters/wolf` prefab'ını spawn ediyor
  (pipeline asset'i yoksa küp fallback — ADR-0003 gereği DATA commit
  edilmiyor, taze klonlarda fallback çalışıyor).
- Headless testler: 683/685 (2 ağ testi TIME_WAIT flakiness — bilinen
  çevresel durum, protokol kodu değişmedi).

### Buluntular

- Metin2 GR2'leri Oodle-1 (codec 2) sıkıştırılmış — granny2.dll
  gereksinimi saf C# portla aşıldı (codec 1/Oodle-0 desteklenmiyor;
  Metin2 dosyalarında görülmedi).
- Blender 5.x'te Collada import YOK (`io_scene_dae` kaldırıldı) →
  DAE ara adımı atıldı, GLB doğrudan Unity'ye (GLTFast). Blender
  yalnızca DDS→PNG dönüşümünde kullanıldı.
- Unity'nin DDS importçusu standart DXT1 DDS'i reddediyor
  ("Unsupported") → PNG'ye çevriliyor.
- URP Lit materyal `Camera.Render()` (built-in yol) altında magenta —
  yalnızca editör önizlemesi için geçici materyal kullanılıyor; oyun
  içinde (URP aktif) sorun yok.

## SP10-4 + Unity Proto Import ✅

**Runtime** (`Assets/Scripts/Frontend/Proto/ProtoDatabases.cs`):
- `ItemDef`/`MobDef` — sunum verisi (guide §7.4: otorite server'da);
  vnum/type/flags/gold/values/sockets/iconPath + `Sprite` ikon linki;
  MobDef `Folder` alanı gelecek GR2 pipeline'ının girdisi.
- `ItemDatabase`/`MobDatabase` ScriptableObject + lazy vnum lookup.

**Editor** (`Assets/Scripts/Frontend/Editor/ProtoImporter.cs`):
- Menu: Metin2 → Import Proto Data; headless `-executeMethod
  …ProtoImporter.ImportAll`.
- ProtoConverter JSON → database asset'leri (`Assets/Resources/GameData/`,
  local-only — telifli isim/ikon içerir, ADR-0003; .gitignore'a eklendi).
- İkon TGA'ları `Extracted/icon` → `Assets/Resources/GameData/Icons`
  (sprite, mipmap kapalı, ASTC 6x6 / 64px iOS+Android) + `ItemDef.Icon`
  linki; eksik ikon sayılır ve raporlanır.
- JsonUtility DTO'ları converter'ın camelCase JSON'una birebir.

**Demo entegrasyonu** (`DemoWorldBehaviour`):
- `ItemChanged` eventleri artık gerçek envanter durumunu tutuyor
  (Set/Updated/Cleared — server-authoritative).
- OnGUI'de alt-orta **ENVANTER şeridi**: hücre başına gerçek ikon
  (yoksa vnum) + adet overlay'i; event logunda gerçek item adı
  (ör. "Kılıç+9") — DemoServer'ın verdiği vnum 19 + 11243 üzerinden.
- Database import edilmemişse zarif fallback (vnum metni).

**Doğrulama (headless import koşusu):**
- `ItemDatabase.asset`: **5.929 item, 5.011'i sprite'a bağlı** (918 ikonsuz:
  221 kaynağı patch pack'lerde + item_list'te ikon tanımsız olanlar);
  Türkçe localeName'ler düzgün serileşti ("Yang", "Türkçe Sürüm").
- `MobDatabase.asset`: 1.347 mob.
- 1.411 TGA → sprite (32x64 RGBA, ASTC 6x6/64px iOS+Android, mipmap off).
- DemoServer'ın verdiği vnum 19 → "Kılıç+9" → `Icons/item/00010.tga` bağlı.

**Tam pipeline (tekrarlanabilir):**
```bash
# 1. extract (locale_tr + icon)
dotnet run --project Tools/PackExtractor -- extract fulldosya/fullbinary/pack/locale_tr.eix Extracted/locale_tr
dotnet run --project Tools/PackExtractor -- extract fulldosya/fullbinary/pack/icon.eix       Extracted/icon
# 2. convert
dotnet run --project Tools/ProtoConverter -- mob  Extracted/locale_tr/mob_proto  Extracted/locale_tr/mob_proto.json
dotnet run --project Tools/ProtoConverter -- item Extracted/locale_tr/item_proto Extracted/locale_tr/item_proto.json \
  --item-list Extracted/locale_tr/item_list.txt --itemdesc Extracted/locale_tr/itemdesc.txt
# 3. Unity import (headless) → Assets/Resources/GameData
Unity -batchmode -quit -executeMethod Metin2.Frontend.EditorTools.ProtoImporter.ImportAll
```


