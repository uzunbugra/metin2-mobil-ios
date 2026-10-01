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
| 3 | Twofish | Twofish (RS schedule, key-dependent S-box, LE) | 16 | Botan chaining KAT'leri (5×128-bit, fresh schedule each) | ✅ Tamamlandı |
| 4 | Serpent | Serpent (Osvik S-box + LT, LE, 32 round) | 16 | Botan (5) + LTC single-bit (3+2+1) KAT'leri | ✅ Tamamlandı |
| 2 | MARS | MARS (IBM tweak'li schedule, E-fonksiyon, LE) | 16 | CryptoPP mars.txt KAT'leri (5×128 + 1×192-bit) | ✅ Tamamlandı |
| 5 | CAST256 | CAST-256 | 16 | RFC 2612 Appendix A (128/192/256-bit) | ✅ Tamamlandı |
| 8 | Camellia | Camellia | 16 | RFC 3713 Ek A (128/192/256-bit) + NESSIE zero-key | ✅ Tamamlandı |
| 9 | SEED | SEED | 16 | RFC 4269 via CryptoPP TestVectors/seed.txt (4 vektör) | ✅ Tamamlandı |
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
- **Twofish dersleri**: tablolar CryptoPP `tftables.cpp`'den (q[2][256] + mds[4][256];
  mantık `twofish.cpp` birebir port: h0 fallthrough + RS + m_s + PHT/rotasyon).
  KAT Botan `twofish.vec` (AES submission türevi) zincir vektörleri — her KAT fresh
  schedule demek, schedule coverage'ı ücretsiz. kDefault Twofish'e eşlenir, ayrı engine
  yok. Key 16/24/32 (CryptoPP aralığı); 24/32 yolu round-trip ile kaplı.
  Not: E_0(0)=9F589F5CF6122C32… — hafızadaki B7220BDC varyantı farklı bir teste aitmiş;
  kanıt (literal port + KAT) hafızayı yendi.
- **Serpent dersleri (en zorlu debug)**: ÜÇ ayrı bug, üç ayrı teknikle avlandı —
  1. S7/I3 output-mapping slip'leri: 16 S-box gövdesi Python'a aktarılıp Botan S-box
     tablolarıyla karşılaştırıldı (S0–S6 birebir, S7/I3 hatalı) → brute-force ile doğru
     mapping'ler kanıtlandı (S7: r2/r4/r3/r0, I3: r3/r0/r2/r1). Boolean gövdeler doğruydu.
  2. Schedule off-by-8: recurrence çıktısı seed'in ÜSTÜNE (w[8+j], sabit j) paketlenir;
     gruplar w[8+4g..] okur — w[4g..] okumak grup 0-1'e ham key sokar. Round-trip GEÇER
     (self-consistent) ama KAT'ler patlar — round-trip ≠ doğruluk kanıtı!
  3. Test string'lerinde düşen trailing char'lar (Byte[15] hatası) — hex uzunluğu gözle.
  Debug altyapısı: C# gövdelerini parse edip çalıştıran Python script'i (transkripsiyonsuz
  doğrulama) + Botan tabloları hakem. Factory throw-testi örneği `Serpent` → `MARS`.
- **MARS dersleri (rotl/rotr kafa karışması)**: backward mixing'de `t = ROTL24(a)` varken
  `ROTR24` yazılmıştı (forward'daki `ROTR24` ile karıştı) — round-trip dahil HER ŞEY patladı,
  ama 512 word S-box hatasızdı. Ders: rotl/rotr yönleri tablo gibi değil, SEMANTİK olarak
  doğrulanır (fwd/bwd mutual-inverse testi: B(F(x))==x — bu test olsaydı bug ilk anda
  yakalanırdı). Debug yolu öğreticiydi: C#+Python aynı yanlış okumayı paylaşınca ikisi de
  aynı yanlış cevabı verdi (D1D9…); jenerik-inversiyon deneyi kendi türetme hatasıyla
  (r, a'dan değil t'ten hesaplanır) vakit kaybettirdi — ama `t`'ye odaklanmak gerçek bug'a
  götürdü. İkinci kaynaktan (jsDelivr) doğrulama fetch-mangling'i eledi.
- **TripleDES dersleri**: yayınlanmış 2-key ECB vektör dosyası YOK (CryptoPP TestVectors'ta
  DES yok — validat.cpp hardcoded; SP 800-67 B.1 üç-key). Strateji: çekirdek degeneracy
  ile kanıtlandı (EDE2(K‖K)==DES(K) → NIST SP 800-17 A + B.1 resmi vektörleri) + Rivest
  Destest recurrence (X16; enc+dec+schedule hepsi) + PyCryptodome 2-key vektörü + Python
  çapraz-kontrol (Python önce SP 800-17'de doğrulandı). PC-1 transkripsiyonunda "21" girişi
  düşmüştü (55 entry) — degenerate B.1 vektörü (tüm subkey'ler sıfır!) bunu yakalayamadı,
  A vektörü yakaladı. Ders: her KAT setinde en az bir non-degenerate key şart; yapısal
  kontrol (IP∘FP=id, PC-1 parity-drop) ucuz ve etkili.
- **CAST-256 dersleri (tablosuz key schedule)**: S1–S4 BouncyCastle `Cast5Engine.cs`'den
  machine-transfer ile alındı (== RFC 2144 Ek A == RFC 2612 §2.1.1; S5–S8 CAST-256'da
  kullanılmaz — sadece CAST-128 key schedule'ına aittir). CryptoPP `cast.cpp`'deki
  statik `t_m/t_r` tabloları (192+192 word) yerine BouncyCastle yolu seçildi:
  `Cm/Mm/Cr/Mr`'den dinamik Tm/Tr türetme — tablo riski sıfır, KAT hakem (3 RFC vektörü
  ilk denemede geçti). Key 16..32 byte (4'ün katları; `VariableKeyLength<16,16,32,4>`),
  round-trip 16/20/24/28/32 ile kaplı. Factory negatif-test örneği `CAST256` →
   `Camellia` olarak değişti.
- **Camellia dersleri (ezber-transkripsiyon tuzağı)**: SBOX1..4 BouncyCastle
  `CamelliaEngine.cs`'den machine-transfer (== CryptoPP `camellia.cpp` SP tabloları,
  head word'ler çapraz kontrol edildi). Mantık BouncyCastle C#→C# port (RFC 3713:
  F + FL/FLINV, 18 round / 24 round, KA/KB schedule); enc+dec schedule'ları ayrı
  kurulur (reverse-derivasyon yok, BC `setKey` forward/reverse verbatim). KAT:
  128-bit RFC + NESSIE zero-key ilk denemede geçti; 192/256-bit patladı — neden
  engine değil TEST'ti: beklenen CT'ler ezberden yazılmıştı (31 char kalmış,
  Serpent'teki trailing-char dersi tekrarı!). RFC metnindeki gerçek değerlerle
  düzeltildi, hepsi geçti. Ders: beklenen hex'ler DAİMA kaynaktan kopyalanır,
  uzunluk gözle sayılır (32 char). Key 16/24/32 (`VariableKeyLength<16,16,32,8>`).
  Factory negatif-test örneği `Camellia` → `SEED` olarak değişti (son kalan).
- **SEED dersleri (en pürüzsüz engine)**: CryptoPP `seed.cpp` birebir port (G-fonksiyonu
  SS0..SS3 maskeleri + iç-içe G katmanlı Feistel, BE); s0/s1/kc machine-transfer.
  4 RFC 4269 KAT'ı İLK DENEMEDE geçti — tablo + mantık hatasızdı. Key sabit 16 byte
  (`FixedKeyLength<16>`, tek fixed-key engine). Decrypt ayrı pre-reversed schedule
  (CryptoPP DECRYPTION m_k'yi tersten yazar — aynı fikir). Tüm 13 suite portlu olduğu
  için factory negatif-testi out-of-range selector'a çevrildi (`(CipherSuite)99` →
  "Unknown", fail-closed kanıtı; sessiz fallback yok).
- **Tablo stratejisi**: PowerShell'den ağ erişimi YOK (doğrulandı). Tablo-tabanlı
   engine'ler için webfetch ile yetkili kaynaktan çekip dosyaya yazma (machine-transfer,
  transcription yok) + SHA256 provenance kaydı + KAT doğrulaması. KAT geçmeden tablo
  güvenilmez sayılır.

## Tamamlanma kriteri (epic)

- 13 engine + factory + her biri için KAT testleri yeşil. ✅
- Handshake orkestrasyonu (offline): `HandshakeClient` (GC 0xff → GC 0xfb →
  CG 0xfb → 0xfa → activate, client polarity true) + `SendSecureAsync` /
  `ReceiveSecureFrameAsync` + 7 loopback testi (şifreli `GC_PHASE` çift-yön
  round-trip dahil). ✅ (`HandshakeClientTests`, 325/325.)
- Kaynak-kanıtla kapatılanlar: DH2 yarı sırası (dh2.h + cipher.cpp:393 çağrı
  sırası) ve CTR big-endian sayaç (upstream modes.cpp OperateKeystream) —
  cipher-spec.md §2/§4'te VERIFIED. ✅
- Kalan: canlı sunucuya karşı uçtan-uca decode (gerçek auth/channel core ile
  final kanıt) + GC ping header çelişkisi (0xfe vs 44) ayrıca çözülecek.
