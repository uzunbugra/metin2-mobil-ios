# ADR-0003: Fulldosya asset lisansı ve kullanım stratejisi

Tarih: 2026-10-03
Durum: Kabul edildi (Sprint 10 — SP10-8)
Sprint referansı: `SPRINT_PLAN.md` Sprint 10 / SP10-8

## Bağlam

Sprint 10'da orijinal Metin2 PC istemci asset'leri (karakter modelleri `.gr2`,
UI atlas DDS/TGA, ikonlar, `item_proto`/`mob_proto` verileri) fulldosya
workspace'inden (`fullbinary/pack/` — 97 pack çifti) extract edilip Unity 6 mobil
istemcisine taşınacak (pipeline: `docs/assets/asset-pipeline-research.md` §3).
Proje, kullanıcının kendi Metin2 sunucusuna (Razuning-V5/40k tabanlı C++ fork)
bağlanan kişisel bir istemcidir; sunucu ve istemci kaynaklarının tamamı
kullanıcının elindedir.

Bu ADR, bu asset'lerin hangi kapsamda kullanılabileceğinin ve hangi dağıtım
kanallarının açıkça dışlandığının yazılı tescilidir (SP10-8). Repo kuralı
açısından bağlayıcı çerçeve `AGENT_DEVELOPMENT_GUIDE.md` §7/§7.1'dir
("kullanıcıya ait dosyaları veya üçüncü taraf asset'leri izinsiz yayımlama").

## Durum tespiti

- **Telif sahibi:** Asset'ler Ymir Entertainment / Webzen'e ait orijinal
  Metin2 içeriğidir. Kullanıcının elinde bulunması bu hakları devralmaz;
  asset'ler **lisanssız kopya** niteliğindedir.
- **Kullanıcının konumu:** Asset'ler kullanıcının kendi sunucusu ve kendi
  kopyası üzerinden ele geçmiştir (fulldosya workspace, 4.2 GB — zaten
  `.gitignore`'da local-only tutuluyor). Kullanım, telif sahibinden alınmış
  herhangi bir lisans veya yazılı izinle desteklenmemektedir.
- **Sonuç:** Bu içerikle yapılacak her çalışma, varsayılan olarak yalnızca
  kişisel/dahili kullanımla sınırlıdır. Araştırma raporu da aynı teşhisi
  en yüksek risk kalemi olarak işaretlemiştir
  (`asset-pipeline-research.md` §4/1).

## Karar

### KABUL EDİLEN kullanım

1. **Kişisel/dahili kullanım:** İstemci yalnızca kullanıcının kendi sunucusunda
   oynanmak üzere lokal derlemelerde bu asset'leri kullanabilir (geliştirme,
   test, pilot dönüşüm). Herkese açık dağıtım yok.
2. **Geliştirme aşamasında local extract:** Extract çıktıları geliştirici
   makinesinde, orijinaller read-only kalarak ayrı klasöre yazılır
   (guide §7.1: "Orijinal arşivleri read-only sakla; dönüştürme çıktısını
   ayrı klasöre yaz").
3. **Pipeline kodu repo'da:** Extractor/converter/parser araçlarının kodu
   (SP10-1/2/3/6/7 çıktısı C#/Python kaynakları) telifli veri içermediği
   sürece commit edilir.

### REDDEDİLEN kullanım

1. **Herkese açık dağıtım:** App Store / Google Play (veya herhangi bir
   public kanal) üzerinden bu asset'leri içeren build yayımlamak — ciddi
   DMCA/takedown riski ve hesap yaptırımı riski taşır.
2. **Asset'lerin repoya/public kanala commit'i:** Telifli asset'ler GitHub
   (public veya private-fork riski) üzerinden yayımlanamaz (guide §7.1:
   "üçüncü taraf asset'leri izinsiz yayımlama").
3. **Extract çıktılarının repo'da uzun süreli tutulması:** `.dds`/`.tga`/`.gr2`/
   proto binary çıktıları ve bunların Unity'ye dönüştürülmüş türevleri
   (FBX, Sprite dokuları, paketlenmiş ASTC dokular) repoda tutulmaz;
   deterministik pipeline ile her zaman yeniden üretilebilir oldukları için
   local-only kalırlar.

## Repo politikası ve .gitignore önerisi

Prensip: **KOD commit edilir, DATA commit edilmez.** Extract çıktısı yolları
`.gitignore`'a eklenmelidir (ana agent uygular):

```gitignore
# Metin2 extract outputs — Ymir/Webzen copyrighted, local-only (ADR-0003)
Extracted/
Assets/Art/
Assets/**/*.dds
Assets/**/*.tga
Assets/**/*.gr2
Assets/**/*.fbx
Assets/StreamingAssets/Extracted/
```

- `fulldosya/` zaten ignore'dadır (SPRINT_07 kararındaki mevcut satır korunur).
- ScriptableObject **tanım sınıfları** (`ItemDef`, `MobDef`, motion map)
  commit edilir; üretilen **asset dosyaları** (`.asset` instance'ları)
  ignore kapsamındadır.
- **Türev metadata ayrımı:** Koddan yeniden üretilebilen, telifli ifade
  içermeyen veri (ör. sprite rect koordinatları, `vnum → sprite` eşlemesi,
   protodan türetilen sayısal alanlar — özünde non-copyrightable fakt/veri)
  repo'da tutulabilir; ancak item isimleri (CP1254 stringler) ve görsel
  içerik telifli ifade sayılır, dahil edilmez. Şüpheli durumda ADR sahibine
  (kullanıcıya) sorulur; yorum bu ADR'nin lehine değil telif sahibi lehine
  yapılır.

## Dönüşüm araçları — lisans durumu

| Araç | Lisans | Kullanım |
|---|---|---|
| **LSLib** (`divine.exe`, GR2→DAE) | **MIT** — doğrulandı: `github.com/Norbyte/lslib` LICENSE, "Copyright (c) 2015 Norbyte" | Uyumlu; CLI araç olarak kullanımı sorunsuz |
| **Blender** (DAE→FBX) | **GPL** — headless/CLI dönüşüm için çağrılması, çıktının (FBX) GPL kapsamına girmesine yol açmaz; araç kopyası dağıtılmıyor | Sorun değil |
| **LZO / minilzo** (pack decompress) | Markus F. J. Oberhumer lisansı — özel mülk kullanımda ticari lisans koşulu içerir, ancak burada **araç yalnızca dahili pipeline'da** kullanılıyor ve dağıtılmıyor | İç araç kullanımı OK; istemci binary'sine gömülmesi gerekirse yeniden değerlendirilir |
| Granny SDK | Ticari (RAD Game Tools) — pratik değil | Kullanılmıyor (araştırma §3/D) |

Pipeline kodu (extractor/converter) bu araçları çağıran bağımsız çalışmadır;
araç binary'leri repo'ya commit edilmez, kurulumları dokümante edilir.

## Gelecek adım

Store dağıtımı (App Store/Google Play) düşünülürse, telifli asset'lerin
**tamamının** değiştirilmesi (özgün/komisyonla üretilmiş içerik) veya
Ymir/Webzen'den lisans alınması gerekir. Bu ADR o kararı **ÖNLER**: mevcut
kapsam yalnızca kişisel/dahili kullanımdır ve herkese açık dağıtım bu ADR
kapsamında açıkça reddedilmiştir. Dağıtım kararı verilirse yeni bir ADR ile
ele alınır (asset değiştirme maliyeti, alternatif içerik kaynakları).

## Sonuçlar

- Fulldosya asset'leri yalnızca kişisel/dahili kullanımda geçerlidir; herkese
  açık dağıtım ve repo/public commit yasaklanmıştır.
- Repo, pipeline kodunu içerir; extract çıktısı ve türev görsel içerik
  `.gitignore` ile local-only tutulur (önerilen satırlar yukarıda; ana agent
  tarafından uygulanacak).
- Bu kısıt sayesinde DMCA riski repoya ve dağıtım kanallarına sızmaz; risk
  yalnızca kullanıcının lokal makinesinde kalır.
- Non-copyrightable türev metadata (sprite koordinatları, vnum eşlemeleri)
  repo'da tutulabilir; telifli ifade (isim stringleri, görseller) tutulmaz.
