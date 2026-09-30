# Changelog

Tüm önemli değişiklikler bu dosyada izlenir. Format: `Keep a Changelog` uyarlaması (Türkçe).
Tarihler UTC. Her madde ilgili sprint kaydına ve commit'e bağlanır.

## [Unreleased]

## [2026-09-29] — Sprint 3 başladı: cipher engine'leri
### Eklenen
- `docs/sprints/SPRINT_03-cipher-engines.md`: 15 engine için kontrol listesi, KAT kaynakları, DoD.
- `docs/decisions/ADR-0002-cipher-engine-strategy.md`: port-vs-bağımlılık kararı (port).
- TEA engine (`CipherSuite.TEA`): Wheeler–Needham + bağımsız KAT setiyle doğrulandı (7 vektör).
- RC6 engine (`CipherSuite.RC6`): RC6 paper + IETF KAT'leri (5 vektör, 16/24-byte key; little-endian).
- IDEA engine (`CipherSuite.IDEA`): HAC Tablo 7.12 (4 vektör + 52 subkey schedule testi).
- RC5 engine (`CipherSuite.RC5`): Rivest zincir vektörleri, r=12 (5 vektör, word→LE çevrimli);
  round-parametrik (default 16 = CryptoPP `VariableRounds<16>`).
- SHACAL-2 engine (`CipherSuite.SHACAL2`): CryptoPP birebir port (big-endian, K=sHA-256
  sabitleri); NESSIE KAT'leri (3×512-bit) + Python çapraz-kontrol (3×128-bit, kısa-key
  padding yolu); key 16..64 byte (CryptoPP `VariableKeyLength<16,16,64>`).
- Blowfish engine (`CipherSuite.Blowfish`): CryptoPP port + pi-digit tabloları (P+S,
  OpenSSL `bf_pi.h`); Schneier resmi vektörleri (6 ECB + 4 set_key, key 4/8/16/24 byte;
  k[16] wire-size resmi vektör); key 4..56 byte (CryptoPP `VariableKeyLength<16,4,56>`).
- TripleDES engine (`CipherSuite.TripleDES`): FIPS 46-3 textbook DES core + EDE2 wiring
  (CryptoPP `DES_EDE2` aynası); NIST SP 800-17 degeneracy KAT'leri + Rivest Destest (X16)
  + PyCryptodome 2-key vektörü + Python çapraz-kontrol; key sabit 16 byte (`FixedKeyLength<16>`).
- Twofish engine (`CipherSuite.Twofish`): CryptoPP birebir port (RS schedule, key-dependent
  S-box, LE); q+MDS tabloları (`tftables.cpp`); Botan zincir KAT'leri (5×128-bit, her biri
  fresh schedule); key 16/24/32 byte (CryptoPP `VariableKeyLength<16,16,32,8>`).
- Serpent engine (`CipherSuite.Serpent`): submission-spec classic yapı + Osvik bitslice
  S-box'ları (nibble-paralel); Botan KAT'leri (5×128-bit) + LTC single-bit KAT'leri
  (3×16 + 1×24 + 1×32-byte); key 16/24/32 byte (CryptoPP `VariableKeyLength<16,16,32,8>`).
- MARS engine (`CipherSuite.MARS`): CryptoPP birebir port (IBM Aug-1999 tweak'li schedule,
  E-fonksiyon, LE); S-box (`marss.cpp`); CryptoPP `mars.txt` KAT'leri (5×128 + 1×192-bit,
  zincirli vektör dahil); key 16..56 byte (8'in katları).
- CAST-256 engine (`CipherSuite.CAST256`): BouncyCastle `Cast6Engine` portu (RFC 2612
  W-iterasyonu + 6 ileri + 6 geri quad-round, BE); S1..S4 machine-transfer
  (BouncyCastle `Cast5Engine.cs` == RFC 2144 Ek A == RFC 2612 §2.1.1); Tm/Tr dinamik
  (`Cm/Mm/Cr/Mr`'den, statik `t_m/t_r` tablosuz); RFC 2612 Ek A KAT'leri (128/192/256-bit);
  key 16..32 byte (4'ün katları, `VariableKeyLength<16,16,32,4>`).
- Camellia engine (`CipherSuite.Camellia`): BouncyCastle portu (RFC 3713 F + FL/FLINV,
  18 round / 24 round, KA/KB schedule, BE); SBOX1..4 machine-transfer
  (BouncyCastle `CamelliaEngine.cs` == CryptoPP `camellia.cpp` SP); enc+dec schedule
  ayrı kurulum; RFC 3713 Ek A KAT'leri (128/192/256-bit) + NESSIE zero-key;
  key 16/24/32 byte (`VariableKeyLength<16,16,32,8>`).
- SEED engine (`CipherSuite.SEED`): CryptoPP `seed.cpp` birebir port (SS0..SS3 maskeli
  G-fonksiyonu, iç-içe G katmanlı Feistel, BE); s0/s1/kc machine-transfer;
  RFC 4269 KAT'leri via `seed.txt` (4 vektör, ilk denemede geçti); key sabit 16 byte
  (`FixedKeyLength<16>`); decrypt pre-reversed schedule.
- `BlockCipherEngineFactory`: 13 suite (tümü portlu); bilinmeyen selector `CipherEngineNotImplementedException` (fail-closed).
- Test: 155 → 318 (+163: TEA 14, RC6 9, IDEA 9, RC5 9, SHACAL-2 12, Blowfish 15, TripleDES 10, Twofish 11, Serpent 18, MARS 12, CAST-256 11, Camellia 10, SEED 9, factory 14).

## [2026-09-29] — Sprint 2: cipher çekirdek (bölüm 2)
### Eklenen
- Cipher kaynak iz sürme: `docs/protocol/cipher-spec.md` (DH2, suite tablosu, türetme, polarity, wire sırası; dosya+satır referanslı).
- `Metin2.Protocol.Security`: `DiffieHellmanGroup` (RFC 5114 sabitleri), `Dh2KeyAgreement`,
  `CipherSuite` (14 selector), `CipherKeyDerivation` (SetUp portu), `CipherSession` (polarity),
  `CtrStream` (big-endian CTR; CryptoPP karşılaştırması UNVERIFIED).
- Test: 96 → 155 (+59).
### Bilinen eksikler (UNVERIFIED)
- DH2 agreed yarı sırası, CTR counter byte order, 15 block-cipher engine → Sprint 3.

## [2026-09-29] — Sprint 2: transport/framer/registry (bölüm 1)
### Eklenen
- `TcpConnection` (`ITcpConnection` implementasyonu: send semaphore, cancellation, idempotent shutdown).
- `PacketLengthTable` + `PacketFramer` (fragmented/coalesced, 0x00 padding, unknown-header drop, 65536 guard).
- `PacketRegistry` + `PacketDescriptor` (phase-aware dispatch, handshake registry).
- Test: 74 → 96 (+22: framer 10, registry 7, loopback transport 5).

## [2026-09-29] — Sprint 0+1: audit + Unity altyapısı
### Eklenen
- Kaynak audit dokümanları (`docs/architecture.md`, `docs/protocol/connection-flow.md`,
  `protocol-inventory.md`, `packet-catalog.json`).
- Unity 6 LTS iskeleti, 4 assembly, golden byte codec'ler (handshake/key-agreement/login3/phase).
- Test: 74/74.
