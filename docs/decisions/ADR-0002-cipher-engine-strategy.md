# ADR-0002: Cipher engine stratejisi

Tarih: 2026-09-29
Durum: Kabul edildi (TEA ile uygulamada)

## Bağlam

`Cipher::SetUp` 14 selector'dan birini seçer (`cipher.cpp:244-299`); bağlantı başına
hangi cipher'ın kullanılacağı paylaşılan sırra bağlıdır, yani gerçek sunucuyla
birlikte çalışmak için 13 engine'in tamamı gerekir. Altyapı (`IBlockCipherEngine`,
`CtrStream`, `CipherSession`, factory) hazır; engine'ler eksik.

## Değerlendirilen seçenekler

1. **CryptoPP referansından C# port** — her engine bağımsız resmi KAT ile doğrulanabilir
   (AES adayları NIST, CAST-256 RFC 2612, Camellia RFC 3713, SEED RFC 4269...).
   Maliyet: engine başına ~100-200 satır + test.
2. **BouncyCastle C# bağımlılığı** — reddedildi: SHACAL-2 ve RC5 yok (bağlantıların
   ~2/14'ü yine çalışmazdı); lisans/platform/boyut incelemesi de gerekir (guide §16).
3. **Native CryptoPP P/Invoke** — reddedildi: Android/iOS için native paketleme maliyeti,
   Unity uyumluluğu belirsiz; gerekirse son çare.

## Karar

Seçenek 1: sırayla port, her engine KAT'li. Sıra: TEA → Blowfish → 3DES → AES adayları →
diğerleri (kolaydan zora, `SPRINT_03-cipher-engines.md`).

## Sonuçlar

- Wire-compat riski iki UNVERIFIED noktasında toplanır (DH2 yarı sırası, CTR byte order);
  ikisi de ilk canlı şifreli paketle tek seferde kanıtlanır.
- Engine KAT'leri algoritma doğruluğunu kaynaktan bağımsız garanti eder.
