# Sprint 3 — Cipher Engine'leri (kayıt)

Hedef: 15 block-cipher engine'ini CryptoPP-birebir port + resmi KAT ile doğrulamak.
Strateji kararı: `docs/decisions/ADR-0002-cipher-engine-strategy.md`.
Altyapı hazır: `IBlockCipherEngine` seam, `CtrStream`, `CipherSession`, `BlockCipherEngineFactory`.

## Engine başına DoD

- [ ] Port, seçili suite'in `CipherSuiteTable` uzunluklarıyla tutarlı (key 16, block tabloya uygun).
- [ ] En az 2 bağımsız KAT vektörü (encrypt + decrypt) testte, kaynak belirtilmiş.
- [ ] `BlockCipherEngineFactory`'de suite eşleşmesi + factory testi.
- [ ] CTR üzerinden 1 round-trip testi (session veya stream seviyesi).

## Kontrol listesi

| # | Suite | Cipher | Block | KAT kaynağı | Durum |
|---|---|---|---|---|---|
| 12 | TEA | TEA (orijinal, XTEA değil) | 8 | Wheeler–Needham + bağımsız vektör seti (7 vektör) | ✅ Tamamlandı |
| 1 | RC6 | RC6-32/20/16 (little-endian packing!) | 16 | RC6 paper appendix + IETF draft (5 vektör, 16/24-bit anahtar) | ✅ Tamamlandı |
| 6 | IDEA | IDEA 8 round (swap output transform'da!) | 8 | HAC Tablo 7.12 (4 vektör + 52 subkey) | ✅ Tamamlandı |
| 11 | Blowfish | Blowfish (big-endian, 16 round) | 8 | Schneier/Eric Young resmi vektörleri (6 ECB + 4 set_key, değişken key dahil) | ✅ Tamamlandı |
| 7 | TripleDES | DES-EDE2 (FIPS 46-3 textbook core) | 8 | NIST SP 800-17 degeneracy + Rivest Destest + PyCryptodome 2-key + Python | ✅ Tamamlandı |
| 3 | Twofish | Twofish | 16 | AES aday KAT'leri (NIST) | ⬜ Sırada (q-box tabloları gerekli) |
| 4 | Serpent | Serpent | 16 | AES aday KAT'leri (NIST) | ⬜ Sırada (S-box tabloları gerekli) |
| 2 | MARS | MARS | 16 | AES aday KAT'leri (NIST) | ⬜ Sırada (S-box tablosu gerekli) |
| 5 | CAST256 | CAST-256 | 16 | RFC 2612 | ⬜ Sırada (S-box tabloları gerekli) |
| 8 | Camellia | Camellia | 16 | RFC 3713 / NESSIE | ⬜ Sırada (S-box GF(2^8) matematiğinden türetilebilir) |
| 9 | SEED | SEED | 16 | RFC 4269 / KISA | ⬜ Sırada (S-box tabloları gerekli) |
| 10 | RC5 | RC5-32/16/16 (CryptoPP default 16 round; little-endian!) | 8 | Rivest zincir vektörleri, r=12 (5 vektör, word→LE çevrimli) | ✅ Tamamlandı |
| 13 | SHACAL2 | SHACAL-2 (big-endian, SHA-256 round fn, feedforward yok) | 32 | NESSIE submission via CryptoPP TestVectors (3×512-bit) + Python çapraz-kontrol (3×128-bit) | ✅ Tamamlandı |

Not: `kDefault` (0) Twofish'e eşlenir — ayrı engine gerekmez.

## Uygulama dersleri (TEA/RC6/IDEA'dan)

- **Byte order engine'e özeldir**: TEA/IDEA big-endian, RC6 little-endian. Asla varsayma — KAT kanıtlar.
- **IDEA swap'i output transform'dadır**: round'lar `((11),(12),(13),(14))` zincirlenir
  (HAC Tablo 7.12'nin 8 round'u da bunu kanıtlar); iç kelimeler OT'da çaprazlanır.
  Decrypt için staggered schedule yerine explicit inverse kullanıldı (gerekçeli, KAT'li).
- **RC5 uyarısı (doğrulandı)**: `VariableRounds<16>` → CryptoPP default 16 round; Rivest'in yayınlanmış
  vektörleri /12/16 içindir. RC5 engine'i round-parametrik yazıldı (default 16),
  algoritma r=12 KAT ile kanıtlandı (5 zincir vektörü). r=16/r=12 çıktı farkı testte
  assert'lenir (parametrenin gerçekten canlı olduğu kanıtı).
- **Word-sıralı vektör tuzağı (RC5)**: Rivest word basar (`EEDBA521 6D8F4B15`), wire
  little-endian'dır (RFC 2040 §6.1/§6.3 — RC6 ile aynı konvansiyon). Teste LE byte
  karşılığı yazılır (`21A5DBEE154B8F6D`). İlk denemede BE string kullanıldı, 6 test
  patladı; düzeltme testte yapıldı, engine doğruydu. Transkripsiyon çift gözle kontrol
  edilir (V3 word'ünde son-iki-byte takası da yakalandı).
- **SHACAL-2 dersleri**: CryptoPP portu birebir alındı (`shacal2.cpp` + `misc.h`
  `GetUserKey` zero-padding + `TestVectors/shacal2.txt` — hepsi webfetch ile çekildi).
  K[64] = SHA-256 sabitleri (FIPS 180-4 §4.2.2 ile çift kaynaklı). Kısa key için yayınlanmış
  vektör YOK (tüm NESSIE vektörleri 512-bit) → 16-byte yolu bağımsız Python referans
  portuyla çapraz-kontrol edildi (önce Python NESSIE'de doğrulandı, sonra C# Python'la;
  IDEA'daki "Python izi" presedenti). C# rotation-form, Python shift-form — farklı kod
  yolları, aynı spec. Yan bulgu: `Buffer.BlockCopy` → `Metin2.Protocol.Buffer` ile
  çakışır, `System.Buffer` net yazılır.
- **Blowfish dersleri (ilk tablolu engine)**: P[18]+S[1024] OpenSSL `bf_pi.h`'den alındı
  (Eric Young tabloları = Schneier pi digitleri; baş word'ler bağımsız mirror'la çapraz
  kontrol edildi, tamamı 10 KAT ile kanıtlandı). Transkripsiyon sırasında bir satır
  bozuldu (`0xEBCDAF0C, 0x7B3E89A0` tek satırda birleşmişti) — derleme hatası olarak
  yakalandı, KAT öncesi düzeltildi. set_key vektörleri (key 1..24 byte) değişken-key
  yolunu da kanıtlar; k[16] wire-size resmi vektördür. Factory negatif-test örneği
  `Blowfish` → `MARS` olarak değişti (tekrarlayan pattern: portlanan suite negatif
  örnekten çıkarılır).
- **TripleDES dersleri**: yayınlanmış 2-key ECB vektör dosyası YOK (CryptoPP TestVectors'ta
  DES yok — validat.cpp hardcoded; SP 800-67 B.1 üç-key). Strateji: çekirdek degeneracy
  ile kanıtlandı (EDE2(K‖K)==DES(K) → NIST SP 800-17 A + B.1 resmi vektörleri) + Rivest
  Destest recurrence (X16; enc+dec+schedule hepsi) + PyCryptodome 2-key vektörü + Python
  çapraz-kontrol (Python önce SP 800-17'de doğrulandı). PC-1 transkripsiyonunda "21" girişi
  düşmüştü (55 entry) — degenerate B.1 vektörü (tüm subkey'ler sıfır!) bunu yakalayamadı,
  A vektörü yakaladı. Ders: her KAT setinde en az bir non-degenerate key şart; yapısal
  kontrol (IP∘FP=id, PC-1 parity-drop) ucuz ve etkili.
- **Tablo stratejisi**: PowerShell'den ağ erişimi YOK (doğrulandı). Tablo-tabanlı
  engine'ler için webfetch ile yetkili kaynaktan çekip dosyaya yazma (machine-transfer,
  transcription yok) + SHA256 provenance kaydı + KAT doğrulaması. KAT geçmeden tablo
  güvenilmez sayılır.

## Tamamlanma kriteri (epic)

- 13 engine + factory + her biri için KAT testleri yeşil.
- Canlı sunucuya handshake → key agreement → ilk şifreli paket decode (bu adım DH2 yarı
  sırası + CTR byte order UNVERIFIED'larını da kapatır).
- GC ping header çelişkisi (0xfe vs 44) ayrıca çözülecek.
