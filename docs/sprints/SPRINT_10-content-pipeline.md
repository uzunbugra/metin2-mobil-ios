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

## Sıradaki adımlar (sırayla)

1. **SP10-4 İkon pipeline**: icon pack extract → ASTC Sprite +
   `ItemDef.iconSprite` bağlama (item_proto.json'daki iconPath eşlemesiyle).
2. **Unity proto import**: item_proto.json/mob_proto.json → ScriptableObject
   editor script (numeric alanlar; isimler local-only kalır).
3. **SP10-6 Motion metadata parser**: motlist.txt + .msa → motion kayıt
   tablosu (PC/Monster pack raporları .msa sayılarını doğruladı).
4. **SP10-5 GR2 pilot dönüşüm**: LSLib(divine)→DAE→Blender→FBX zinciri,
   1 karakter + 4 animasyon (araç kurulumu kullanıcı onaylı).

