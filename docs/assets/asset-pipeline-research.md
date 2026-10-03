# Fulldosya Asset Pipeline Araştırması — Metin2 PC → Unity 6

> **Amaç:** `fulldosya/` içindeki orijinal Metin2 PC istemci asset'lerinin
> (karakter, monster, NPC, item, UI) Unity 6 mobil istemcisinde kullanılmasının
> implementasyon yollarının kaynak-kanıtlı çıkarımı.
> **Yöntem:** Read-only inceleme — client C++ source (EterPack, EterGrnLib,
> EterPythonLib, GameLib, UserInterface, DumpProto) dosya+satır referanslı;
> arşivler (`*.rar`, `*.tgz`) açılmadı, binary'ler çalıştırılmadı.
> **Tarih:** 2026-10-03 (Sprint 8 araştırma görevi).

## 1. Envanter Özeti

| Kaynak | İçerik |
|---|---|
| `fullbinary/pack/` | **97 pack çifti** (194 `.eix/.epk`): PC, pc2, Monster, monster2, NPC, npc2, item, icon, Effect, ETC (UI atlas'ları!), locale_tr, uiscript, root, Outdoor*/indoor* haritalar, 40+ patch pack |
| `fullbinary/` | Metin2Release.exe, granny2.dll (Granny runtime), python27.dll, `locale.cfg` → **CP1254 Türkçe** (proto string decode kritik), EPack32.exe (3. taraf extract aracı) |
| `source/Client Source/` | 17 modül: EterPack (format), EterBase (LZO+TEA), EterGrnLib (Granny), EterImageLib (DDS/TGA), EterLib (.sub), EterPythonLib (UI widget), GameLib (RaceManager), UserInterface |
| `source/DumpProto/` | Proto dönüştürücü kaynağı — format + **şifreleme anahtarları** burada |
| Eksik | Server proto **txt** kaynakları (muhtemelen `game.tgz` içinde — açılmadı) |

## 2. Format Analizi (kaynak referanslı)

### 2.1 EterPack (.eix index + .epk data)

- EIX header: `'EPKD'`, version 2, indexCount; **entry başına 192 byte**
  (`EterPack.h:47-62` + SP10-1 ampirik düzeltme: `char filename[161]` 4-hizalamayı
  bozduğu için `filename_crc` 168'e hizalanır; kanıt: locale_tr index boyu
  46476 = 12 + 242×192 — ilk rapordaki 188 yanlıştı). Alan ofsetleri: id@0,
  filename@4, filename_crc@168, real_data_size@172, data_size@176, data_crc@180,
  data_position@184, compressed_type@188. Index'teki `real_data_size` tip≥1
  girdilerde güvenilmez — gerçek boy blob'un dwRealSize'ıdır (client parity).
  (`EterPack.h:47-62`): filename[161], filename_crc, real/data size, data_crc,
  data_position, compressed_type.
- EIX şifreliyse fourcc `'MCOZ'` → LZO1X-1 + TEA; anahtar
  `s_adwEterPackKey = {45129401, 92367215, 681285731, 1710201}`
  (`EterPack.cpp:325-331`; TEA portu `EterBase/lzo.cpp:66-99,171`).
- Sıkıştırma tipleri: **0 NONE / 1 LZO (en yaygın) / 2 SECURITY (LZO+TEA) /
  3 PANAMA (LZO+Crypto++ Panama) / 4-5 HYBRIDCRYPT (uzantı-bazlı
  Camellia/Twofish/XTEA — anahtarlar server handshake'inden)**.
  Bu dağıtımda çoğunluk 0/1 beklenir; 3/4/5 gerekirse server source'tan
  dökülür (`WriteHybridCryptPackInfo`, `EterPackManager.cpp:516+`).
- UI dokuları `d:/ymir work/ui/` yoluyla **ETC pack'ine** mount'lı.

### 2.2 Karakter zinciri (RaceManager.cpp:98-174)

```
mob_proto szFolder / Python RegisterRaceName
  → d:/ymir work/{pc,monster,npc}.../<race>/<race>.msm   (ırk tanımı, text)
    → basemodelfilename (.gr2) + shapedata local_model'ler (parça .gr2:
      gövde/zırh/saç — bone attach ile) + motlist.txt
      → .msa (motion: .gr2 animasyon + duration + accumulation xyz
        [root-motion karşılığı] + motioneventdata [ses/hit/efekt])
      → .dds/.tga dokular (.gr2'ye gömülü DEĞİL, dışarıda)
```
- Motion tipleri: SPAWN, WAIT/WALK/RUN(1-2), DEAD, COMBO/NORMAL_ATTACK,
  DAMAGE/KNOCKDOWN/STANDUP (F/B), SPECIAL(1-5), SKILL(1-5)
  (`RaceManager.cpp:189-238`).
- Silahlar: `item/weapon/%05d.gr2`; drop modeli `item/etc/item_bag.gr2`.
- LOD deseni: `<model>_lod_01.gr2`.

### 2.3 UI sistemi

- Mimari: Python layout (`uiscript` + `root` pack) + C++ widget çekirdeği
  (EterPythonLib). **Layout mantığı yeniden yazılır; asset'ler taşınır.**
- Atlas: `.dds` (DXT1/3/5) + `.tga` — **Unity doğrudan import eder.**
- **`.sub` = text sprite-sheet slicing tanımı** (`GrpSubImage.cpp:65-133`):
  `image <atlas> + left/top/right/bottom` — Unity Sprite üretimine birebir
  map'lenir.
- İkonlar: `icon` pack, `icon/item/%05d.tga`; `item_list.txt` =
  `vnum → icon [+ model]` eşlemesi (`ItemManager.cpp:108-173`).

### 2.4 item_proto / mob_proto (DumpProto)

- `mob_proto`: `'MMPT'` + count + LZO+TEA blob; anahtar
  `{4813894, 18955, 552631, 6822045}`. Alanlar: TMobTable
  (`dump_proto.cpp:71-130`) — vnum, isimler, level/HP/damage, **szFolder[65]
  (model klasörü)**, skill dizileri vb.
- `item_proto`: `'MIPX'` + version + stride + count + LZO+TEA blob; anahtar
  `{173217, 72619434, 408587239, 27973291}`. Alanlar: TClientItemTable
  (`dump_proto.cpp:174-203`) — vnum, isimler, type/wear/flag, limits/applies,
  values[6], sockets[3], refined vnum vb.
- Konum: `locale_tr.epk` içinde; stringler **CP1254** → UTF-8 çevirimi şart.

### 2.5 Granny (.gr2)

- SDK başlığı 2.11.8 (`extern/include/granny.h:1255`); dosyalar 2.x alt
  sürümleri karışık olabilir. Section-based container (mesh/skeleton/
  animation/RTTI); vertex+texture sekmeleri ayrı okunabilir (`Thing.cpp:141-221`).

## 3. Unity'ye Taşıma Pipeline'ı (sıralı)

| Adım | Araç | Çaba | Risk |
|---|---|---|---|
| **A. Pack extract** | Kendi C#/Python CLI (~300 satır): EIX parse + LZO1X + TEA; öncelik: locale_tr → icon → PC → Monster → ETC → item | 1-2 gün | Orta (MCOZ index, tip 3/4/5) |
| **B. Proto convert** | C# CLI: fourcc → blob → struct → JSON → ScriptableObject; CP1254→UTF-8; item_list/itemdesc birleştirme | 1 gün | Düşük (formatlar biliniyor) |
| **C. Texture convert** | DDS/TGA doğrudan Unity import; ASTC 6x6 iOS, mipmap, boyut bütçeleri; AssetPostprocessor | ½ gün | Çok düşük |
| **D. Model convert** | **LSLib (`divine.exe`) GR2→DAE** (toplulukta kanıtlanmış) → Blender → FBX/glTF → Unity. Pilot: 1 karakter + 4 animasyon. Yedek: Assimp GR2 (sınırlı). Granny SDK: lisans açısından pratik değil | Pilot 2-4 gün | **Orta-yüksek** (animasyon bağlama — lslib issue #44) |
| **E. Animasyon sistemi** | motlist+.msa parser → `(race, motionType)→clip+duration+accumulation+events` ScriptableObject; accumulation = kod ile yer değiştirme (server-otorite hareketle uyum — root motion KULLANMA) | 2-3 gün | Orta |
| **F. Karakter kompozisyon** | .msm parser → parça sistemi (SMR + bone attach: silah→el kemiği); kostümler aynı şema | 2-3 gün | Orta |
| **G. UI sprite'ları** | .sub parser → atlas'tan Unity Sprite (Sprite.Create / SpriteEditor data); ikonlar `ItemDef.iconSprite` | 1 gün (framework hariç) | Düşük |
| **H. Import preset'leri** | Rig: **Generic** (humanoid değil); mesh compression; 30 FPS animasyon (orijinal `g_fGameFPS`); ASTC; SpriteAtlas; koordinat/ölçek dönüşümü tek fonksiyon | ½ gün | Düşük |

## 4. Riskler

1. **LİSANS (en yüksek):** Asset'ler Ymir/Webzen telifli orijinal Metin2
   içeriği — **yalnızca kendi sunucumuzda dahili kullanım**; App Store/Play
   gibi herkese açık dağıtım ciddi DMCA riski taşır. Orijinaller read-only,
   extract çıktıları repoya/public kanala **konmaz** (guide §7/§7.1).
   Dönüşüm araçları (LSLib MIT-vari, Blender) temiz.
2. **Granny sürüm uyumu:** mesh genelde sorunsuz; animasyon dönüşümünde
   topluluk raporlu kayma sorunları — pilot-önce stratejisi şart.
3. **Şifreli pack:** MCOZ index çözülür (anahtar elimizde); PANAMA/HybridCrypt
   dosyalarına rastlanırsa anahtar server source'tan dökülür.
4. **Animasyon semantiği:** `accumulation` yanlış taşınırsa karakterler kayar;
   motion event zamanlaması combat sync'i bozar.
5. **CP1254:** yanlış decode → bozuk Türkçe item adları.
6. **Eksik proto txt kaynakları:** client binary proto'dan dönüşüm mümkün
   (anahtarlar elimizde); `game.tgz` açılırsa txt kaynak daha temiz olur.

## 5. Sprint 10 İş Kalemleri

SP10-1 … SP10-9 kalemleri [`SPRINT_PLAN.md`](sprints/SPRINT_PLAN.md)
Sprint 10 bölümüne işlendi.
