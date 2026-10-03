# Sprint 10 — İçerik Pipeline'ı (fulldosya asset'leri)

**Durum:** 🟡 Devam ediyor (SP10-1 ✅, SP10-8 ✅; SP10-2 devam ediyor).
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

## SP10-2 — Proto Converter (devam)

Sıradaki adım; girdiler hazır: `Extracted/locale_tr/{item_proto,mob_proto,
item_list.txt,itemdesc.txt}`. Format: MIPX/MMPT + LZO+TEA blob (anahtarlar
DumpProto kaynağında), struct'lar TMobTable/TClientItemTable.
