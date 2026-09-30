# Metin2 Mobile — Unity 6 LTS Client

Mevcut Metin2 (Razuning-V5 / 40k tabanlı) C++ sunucusuna bağlanan, Unity/C# ile geliştirilen Android mobil istemcisi.

> **Bu README'yi nasıl okumalı:** Proje adım adım, her adımı test'li ve commit'li ilerler.
> Hikayenin özeti aşağıda; detay için `CHANGELOG.md`, tam kayıtlar için `docs/sprints/`,
> teknik spec'ler için `docs/protocol/` dosyalarına bak. `git log --oneline` her adımı
> ayrı commit olarak gösterir.

## Proje Durumu

| Sprint | Durum | Açıklama |
|--------|-------|----------|
| Sprint 0 — Kaynak Audit | ✅ Tamamlandı | Kaynak kod haritası, protokol keşfi, dokümanlar |
| Sprint 1 — Unity Altyapı | ✅ Tamamlandı | Proje iskeleti, golden byte test'leri, codec'ler |
| Sprint 2 — Protocol Core | ✅ Tamamlandı | Transport, framer, registry, DH2 + derivation + session/CTR |
| Sprint 3 — Cipher Engine'leri | ✅ Tamamlandı | 13 engine'in 13'ü tamam + factory + KAT altyapısı |

**Test: 318/318 ✅** (`dotnet test Metin2.Tests.csproj`)

## Gelişim Hikayesi (Adım Adım)

### Adım 0 — Kaynak Kod Audit'i ve Protokol Keşfi (Sprint 0)
- Workspace doğrulandı: Server C++ source, Client C++ source, DumpProto, game config'leri, MySQL datadir, PC client binary — hepsi mevcut.
- Auth/login/game bağlantı akışı server + eski client source içinde dosya+satır referanslarıyla takip edildi.
- Çıktılar:
  - `docs/architecture.md` — Workspace haritası, process topolojisi, build toolchain'leri
  - `docs/protocol/connection-flow.md` — Handshake → Key Agreement → Auth → Login → Select → Game
  - `docs/protocol/protocol-inventory.md` — 60+ CG, 80+ GC header, GD/DG/GG header'ları, framing kuralları
  - `docs/protocol/packet-catalog.json` — 13 bağlantı paketi, makine-okunur katalog
- Commit: `dac18f6`

### Adım 1 — Unity Proje Altyapısı (Sprint 1)
- **Unity 6 LTS (6000.0.23f1)**, Android target, 4 assembly (`Metin2.Core/Protocol/Network/Tests`).
- `Metin2.Protocol` bilerek **UnityEngine bağımsız** (`noEngineReferences: true`) — saf C#, `dotnet test` ile editor'süz test.
- İlk 4 paketin codec'i (`Serialize`/`TryDeserialize`/`Deserialize`):
  `TPacketGCHandshake` (0xff, 13B), `TPacketKeyAgreement` (0xfb, 261B),
  `TPacketCGLogin3` (111, 65B), `TPacketGCPhase` (0xfd, 2B) + `PacketReader`/`PacketWriter` (LE) + `SecretRedactor`.
- Test: **74/74**. Commit: `dac18f6` içinde.

### Adım 2 — TCP Transport, Framer, Registry (Sprint 2, bölüm 1)
Neden: cipher'a geçmeden önce TCP'nin byte-stream doğası (fragmented/coalesced) çözülmeliydi.
- `TcpConnection` (`Network/Transport`): `ITcpConnection` implementasyonu — send semaphore
  (byte interleaving yok), netstandard2.1 uyumlu cancellation, idempotent disconnect/dispose.
- `PacketLengthTable` + `PacketFramer` (`Protocol/Framing`): kaynak-doğrulanmış S2C uzunlukları
  (0xff=13, 0xfb=261, 0xfa=4, 0xfd=2), 0x00 padding skip, unknown-header drop+sayacı,
  65536 byte guard (`MAX_INPUT_LEN`).
- `PacketRegistry` (`Protocol/Registry`): phase-aware dispatch — handshake paketleri yalnızca
  HANDSHAKE phase'inde, `GC_PHASE` her phase'de geçerli.
- Yolda bulunan hata: `AsReadOnly()` → `List` cast'i (`InvalidCastException`) — iterasyona çevrildi.
- Test: 74 → **96/96** (+22). Commit: `0fecf40`.

### Adım 3 — Cipher Çekirdek, Engine'ler Hariç (Sprint 2, bölüm 2)
Neden: key agreement olmadan canlı sunucuyla handshake tamamlanamaz; önce kaynak iz sürüldü.
- İz sürme (`docs/protocol/cipher-spec.md`): `cipher.cpp/h`, `desc.cpp:704-741` (server polarity **false**),
  `input.cpp:556-583` (önce 0xfa + flush, sonra cipher), `NetStream.cpp:921-928` (client polarity **true**),
  `PhaseHandShake.cpp:202-258`, vendored CryptoPP header'ları (DH2, 13 cipher'ın block/key uzunlukları).
- Implementasyon (`Protocol/Security`): `DiffieHellmanGroup` (RFC 5114 sabitleri),
  `Dh2KeyAgreement` (fail-closed `TryAgree`), `CipherSuite` (14 selector), `CipherKeyDerivation`
  (`SetUp` portu — el-hesaplı vektörlerle testli), `CipherSession` (polarity aynası),
  `CtrStream` (big-endian CTR; CryptoPP karşılaştırması UNVERIFIED).
- Test: 96 → **155/155** (+59). Commit: `a3157b5`.

### Adım 4 — İzlenebilirlik Altyapısı
Neden: "yarın bakınca anlaşılsın" — hikaye git log + dosyalarda izlenebilir olmalı.
- `CHANGELOG.md` (her değişiklik sprint'e bağlı), `docs/sprints/SPRINT_02/03` kayıtları,
  `docs/decisions/ADR-0002` (engine stratejisi: port-vs-bağımlılık → port).
- Commit: `61c4e54`.

### Adım 5 — TEA Engine (Sprint 3, ilk engine)
- Orijinal TEA (XTEA değil), 32 cycle; 7 KAT (Wheeler–Needham + bağımsız set) + decrypt + round-trip.
- `BlockCipherEngineFactory` + `CipherEngineNotImplementedException` (desteklenmeyen suite açık hata verir).
- Yolda bulunan hata: `(1,1)` word çifti `00000001_00000001` okunmalıydı, testte `…0001` yazılmıştı — test düzeltildi, engine doğruydu.
- Test: 155 → **173/173** (+18). Commit: `cdc83a0`.

### Adım 6 — RC6 + IDEA Engine'leri (Sprint 3, devam)
- **RC6-32/20/16**: 5 KAT (RC6 paper + IETF draft; 16/24-byte key). Kritik bulgu: RC6 word'leri
  **little-endian** paketler (paper §2) — TEA/IDEA big-endian. Değişken anahtar boyu (16/24/32) eklendi.
- **IDEA** (8 round): 4 KAT + 52 subkey schedule testi (HAC Tablo 7.12). Üç gerçek bug avlandı:
  1. `Mul` uint taşması (`0x10000×0x10000`) → `ulong`.
  2. IDEA swap'i round'larda değil **output transform**'dadır — 8 round'un tamamı HAC ile el/Python iziyle kanıtlandı; decrypt explicit inverse'a çevrildi.
  3. Testin beklenen-schedule dizisi 54 eleman yazılmıştı (52 olmalı) + indeks kayması — test düzeltildi, engine doğruydu.
- Strateji notları `SPRINT_03`'e işlendi: PowerShell'den ağ YOK (tablo machine-transfer iptal),
  RC5 CryptoPP default'u 16 round (Rivest vektörleri /12/16 — uymaz), kalan 10 engine'in tablo ihtiyaçları.
- Test: 173 → **191/191** (+18). Commitler: `862fea5` (feat) + `b03187c` (docs).

### Adım 7 — RC5 Engine (Sprint 3, devam)
- **RC5-32 round-parametrik** (default 16 = CryptoPP `VariableRounds<16>`; KAT'lar r=12 ile).
  5 Rivest zincir vektörü (her ciphertext bir sonrakinin plaintext'i) + decrypt + r=16 round-trip.
- İki gerçek transkripsiyon tuzağı avlandı (engine doğruydu, test düzeltildi):
  1. Rivest word basar, wire little-endian'dır (RFC 2040 §6.1/§6.3) — beklenen stringler LE'ye çevrildi.
  2. V3 word'ünde son-iki-byte takası (`…6992FC` → `…69FC92`).
- r=16/r=12 çıktı farkı assert'lenir (round parametresinin canlı olduğu kanıtı).
- Test: 191 → **201/201** (+10: RC5 9, factory 1).

### Adım 8 — SHACAL-2 Engine (Sprint 3, devam)
- CryptoPP `shacal2.cpp` birebir port: big-endian, SHA-256 round fonksiyonu (feedforward yok),
  key schedule = SHA-256 message expansion + round-sabiti (fused, `UncheckedSetKey` sırasıyla).
  `GetUserKey` zero-padding (`misc.h`) doğrulandı → key 16..64 byte desteklenir (wire'da hep 16).
- KAT: 3 NESSIE 512-bit vektörü (`TestVectors/shacal2.txt`) + 3× 16-byte Python çapraz-kontrol
  (yayınlanmış kısa-key vektörü yok; Python portu önce NESSIE'de doğrulandı — IDEA presedenti).
- Yan bulgu: `Buffer.BlockCopy` proje `Buffer` namespace'iyle çakışır → `System.Buffer` net yazıldı;
  factory negatif-test örneği `SHACAL2` → `Serpent` olarak değişti.
- Test: 201 → **214/214** (+13: SHACAL-2 12, factory 1).

### Adım 9 — Blowfish Engine (Sprint 3, devam; ilk tablolu engine)
- CryptoPP `blowfish.cpp` portu + pi-digit tabloları (P[18]+S[1024], OpenSSL `bf_pi.h`;
  baş word'ler bağımsız mirror'la çapraz kontrol edildi, tamamı KAT ile kanıtlandı).
- KAT: 6 resmi ECB + 4 resmi set_key vektörü (key 4/8/16/24 byte — değişken-key yolu dahil;
  k[16] wire-size resmi vektördür). Key 4..56 byte desteklenir.
- Transkripsiyon sırasında bozulan bir satır derleme hatası olarak yakalandı (KAT öncesi).
  Factory negatif-test örneği `Blowfish` → `MARS` olarak değişti.
- Test: 214 → **230/230** (+16: Blowfish 15, factory 1).

### Adım 10 — TripleDES Engine (Sprint 3, devam)
- FIPS 46-3 textbook DES core (IP/FP/E/P/PC1/PC2/rot/S-box) + EDE2 wiring (CryptoPP
  `DES_EDE2` aynası: enc = DESenc(K1)→DESdec(K2)→DESenc(K1); key sabit 16 byte).
- KAT stratejisi (yayınlanmış 2-key ECB dosyası yok): çekirdek degeneracy ile
  (EDE2(K‖K)==DES(K) → NIST SP 800-17 A + B.1) + Rivest Destest recurrence (X16) +
  PyCryptodome 2-key vektörü + 2 Python çapraz-kontrol (Python önce SP 800-17'de doğrulandı).
- Gerçek bug avı: PC-1'de "21" girişi düşmüştü (55 entry) — degenerate B.1 vektörü
  (subkey'ler sıfır!) yakalayamadı, A vektörü yakaladı. Ders SPRINT_03'e işlendi:
  non-degenerate key şart + yapısal kontrol (IP∘FP=id, PC-1 parity-drop).
- Test: 230 → **241/241** (+11: TripleDES 10, factory 1).

### Adım 11 — Twofish Engine (Sprint 3, devam)
- CryptoPP `twofish.cpp` birebir port (h0 fallthrough + RS + m_s + PHT/rotasyon, LE);
  q[2][256] + mds[4][256] tabloları `tftables.cpp`'den (CryptoPP repo'da ayrı dosya!).
- KAT: 5 Botan zincir vektörü (128-bit; her biri fresh schedule — schedule coverage ücretsiz).
  Key 16/24/32 desteklenir (24/32 yolu round-trip ile kaplı).
- Not: hafızadaki E_0(0) varyantı (B7220BDC…) yanlış çıktı; literal port + KAT kanıtı
  hafızayı yendi (doğrusu 9F589F5CF6122C32…).
- Test: 241 → **253/253** (+12: Twofish 11, factory 1).

### Adım 12 — Serpent Engine (Sprint 3, devam; en zorlu debug)
- Submission-spec classic yapı (LE prekey recurrence + 33 subkey grubu + LT/ILT) ile
  Osvik bitslice S-box'lar (nibble-paralel, 8 fwd + 8 inv). Key 16/24/32.
- KAT: 5 Botan (128-bit, key=0) + 3 LTC single-bit 16-byte + 1 Botan 192-bit +
  1 LTC 24-bit + 1 Botan 256-bit + 1 LTC 32-bit + decrypt + round-trip (16/24/32).
- Üç bug, üç teknik: (1) S7/I3 output-mapping slip'leri — Python'a aktarılan 16 gövde
  Botan tablolarıyla karşılaştırıldı, doğru mapping'ler brute-force ile kanıtlandı;
  (2) schedule off-by-8 (recurrence çıktısı w[8+j]'ye paketlenir; round-trip geçtiği halde
  KAT patladı — round-trip ≠ doğruluk!); (3) test hex'lerinde düşen trailing char'lar.
- Test: 253 → **272/272** (+19: Serpent 18, factory 1).

### Adım 13 — MARS Engine (Sprint 3, devam)
- CryptoPP `mars.cpp` birebir port (IBM Aug-1999 tweak'li schedule + E-fonksiyonlu core,
  LE); S-box `marss.cpp`'den (512 word, hatasız çıktı). Key 16..56 (8'in katları).
- KAT: 6 CryptoPP `mars.txt` vektörü (5×128-bit zincirli + 1×192-bit) + decrypt + round-trip.
- Gerçek bug: backward mixing'de `t = ROTL24(a)` yerine `ROTR24` yazılmıştı (forward'daki
  ile karıştı) — round-trip dahil her şey patladı. C#+Python aynı yanlış okumayı paylaştığı
  için ikisi de aynı yanlış cevabı verdi; KAT hakemliği + `t`'ye odaklanma çözdü.
- Test: 272 → **285/285** (+13: MARS 12, factory 1).

### Adım 14 — CAST-256 Engine (Sprint 3, devam)
- BouncyCastle `Cast6Engine` portu (RFC 2612 W-iterasyonu + 6 ileri + 6 geri quad-round,
  BE); S1..S4 BouncyCastle `Cast5Engine.cs`'den machine-transfer
  (== RFC 2144 Ek A == RFC 2612 §2.1.1; S5–S8 CAST-256'da kullanılmaz).
  CryptoPP `cast.cpp`'deki statik `t_m/t_r` tabloları yerine dinamik Tm/Tr
  (`Cm/Mm/Cr/Mr`'den) — tablo riski sıfır.
- KAT: 3 RFC 2612 Ek A vektörü (128/192/256-bit, PT=sıfır) **ilk denemede geçti**
  + decrypt + round-trip (16/20/24/28/32). Key 16..32 byte (4'ün katları).
- Factory negatif-test örneği `CAST256` → `Camellia` olarak değişti.
  Test-key notu: 28-byte round-trip key'i önce 27 hex-char yazılmıştı
  (`ArgumentException` olarak yakalandı) — RFC 256-bit key'in ilk 56 char'ı ile düzeltildi.
- Test: 285 → **297/297** (+12: CAST-256 11, factory 1).

### Adım 15 — Camellia Engine (Sprint 3, devam)
- BouncyCastle `CamelliaEngine` C#→C# portu (RFC 3713: F + FL/FLINV, 18 round
  / 24 round, KA/KB schedule, BE); SBOX1..4 machine-transfer
  (BouncyCastle == CryptoPP `camellia.cpp` SP, head word'ler çapraz kontrollü).
  Enc+dec schedule'ları ayrı kurulur (reverse-derivasyon yok).
- KAT: 3 RFC 3713 Ek A (128/192/256-bit) + NESSIE zero-key + decrypt +
  round-trip (16/24/32). Key 16/24/32 (`VariableKeyLength<16,16,32,8>`).
- Gerçek transkripsiyon tuzağı: 192/256-bit beklenen CT'ler ezberden yazılmıştı
  (31 char kalmış) — 128-bit + NESSIE geçtiği için engine doğruydu, test düzeltildi
  (RFC metnindeki gerçek değerler). Ders SPRINT_03'e işlendi: beklenen hex DAİMA
  kaynaktan, uzunluk gözle.
- Factory negatif-test örneği `Camellia` → `SEED` (son kalan).
- Test: 297 → **308/308** (+11: Camellia 10, factory 1).

### Adım 16 — SEED Engine (Sprint 3, son engine — 13/13 ✅)
- CryptoPP `seed.cpp` birebir port (SS0..SS3 maskeli G + iç-içe G katmanlı Feistel,
  BE); s0/s1/kc machine-transfer. Key sabit 16 byte (`FixedKeyLength<16>`).
  Decrypt pre-reversed schedule (CryptoPP DECRYPTION aynası).
- KAT: 4 RFC 4269 vektörü (`seed.txt`) **ilk denemede geçti** + decrypt + round-trip.
- Tüm suite'ler portlu olduğu için factory negatif-testi out-of-range selector'a
  çevrildi (`(CipherSuite)99` → "Unknown", fail-closed kanıtı).
- Test: 308 → **318/318** (+10: SEED 9, factory 1). **Sprint 3 epic tamam.**

## Test Tablosu (Komut: `dotnet test Metin2.Tests.csproj`)

| Alan | Test | Kapsam |
|---|---|---|
| Handshake codec | `PacketGCHandshakeTests` | Golden byte, round-trip, truncation, header check |
| KeyAgreement codec | `PacketKeyAgreementTests` | 261B serialize, zero-padding |
| Login3 codec | `PacketCGLogin3Tests` | Credential truncation safety |
| Phase codec | `PacketGCPhaseTests` | Tüm phase enum değerleri |
| Buffer | `PacketReaderWriterTests` | LE I/O, fixed string, bounds |
| Secret | `SecretRedactorTests` | Credential maskeleme |
| Framer (10) | `PacketFramerTests` | Fragmented/coalesced, padding, unknown drop, max-length |
| Registry (7) | `PacketRegistryTests` | Phase izinleri, duplicate/validation |
| Transport (5) | `TcpConnectionTests` | Loopback connect/send/receive/disconnect/close |
| DH2 (11) | `Dh2KeyAgreementTests` | RFC 5114, `g^q==1`, A↔B simetrisi, fail-closed |
| Suite (32) | `CipherSuiteTests` | 14 selector, block/key uzunlukları |
| Derivation (6) | `CipherKeyDerivationTests` | El-hesaplı vektörler, fail-closed |
| Session/CTR (10) | `CipherSessionTests` | Polarity aynası, round-trip, counter |
| TEA (14) | `TeaEngineTests` | 7 KAT + decrypt + round-trip |
| RC6 (9) | `Rc6EngineTests` | 5 KAT + decrypt + round-trip |
| IDEA (9) | `IdeaEngineTests` | 4 KAT + schedule + decrypt + round-trip |
| RC5 (9) | `Rc5EngineTests` | 5 KAT (r=12) + decrypt + r=16 round-trip |
| SHACAL-2 (12) | `Shacal2EngineTests` | 3 NESSIE KAT + 3 Python çapraz-kontrol + decrypt + round-trip |
| Blowfish (15) | `BlowfishEngineTests` | 6 ECB + 4 set_key KAT + decrypt + round-trip |
| TripleDES (10) | `TripleDesEngineTests` | NIST degeneracy + Destest + 2-key + Python + decrypt |
| Twofish (11) | `TwofishEngineTests` | 5 zincir KAT + decrypt + round-trip (16/24/32) |
| Serpent (18) | `SerpentEngineTests` | 5 Botan + 3 LTC-16 + 2×192 + 2×256 + decrypt + round-trip |
| MARS (12) | `MarsEngineTests` | 6 CryptoPP KAT + decrypt + round-trip (16/24/56) |
| CAST-256 (11) | `Cast256EngineTests` | 3 RFC 2612 KAT (128/192/256) + decrypt + round-trip (16/20/24/28/32) |
| Camellia (10) | `CamelliaEngineTests` | 3 RFC 3713 KAT + NESSIE zero-key + decrypt + round-trip (16/24/32) |
| SEED (9) | `SeedEngineTests` | 4 RFC 4269 KAT + decrypt + round-trip |
| Factory (14) | `BlockCipherEngineFactoryTests` | Suite yönlendirme, session round-trip, unknown-selector fail-closed |

## Mimari

```
Unity Client (Android)
    |
    | TCP (Little-Endian, #pragma pack(1)) → PacketFramer → PacketRegistry
    v
Auth Core (0xff handshake → 0xfb key agreement → DH2 → CTR → PHASE_AUTH)
    |
    v
Channel/Game Core (PHASE_LOGIN → PHASE_SELECT → PHASE_GAME)
    |
    v
DB Core ←→ MySQL (sunucu tarafında kalır)
```

## Assembly Yapısı

```
Assets/Scripts/
├── Core/           (Metin2.Core.asmdef)
│   ├── Config/     — EnvironmentType, NetworkConfig
│   └── Logging/    — ILogger, SecretRedactor, UnityLogger
├── Protocol/       (Metin2.Protocol.asmdef — noEngineReferences: true)
│   ├── Buffer/     — PacketReader, PacketWriter (LE binary I/O)
│   ├── Codecs/     — PacketGCHandshakeCodec, PacketKeyAgreementCodec, PacketCGLogin3Codec, PacketGCPhaseCodec
│   ├── Constants/  — PacketHeaders, PhaseType
│   ├── Exceptions/ — PacketException, PacketUnderflowException, InvalidPacketHeaderException,
│   │                  CipherEngineNotImplementedException
│   ├── Framing/    — PacketLengthTable, PacketFramer (TCP stream → frame)
│   ├── Registry/   — PacketRegistry, PacketDescriptor (phase-aware dispatch)
│   ├── Security/   — DiffieHellmanGroup, Dh2KeyAgreement, CipherSuite, CipherKeyDerivation,
│   │                  CipherSession, CtrStream
│   │   └── Engines/— TeaEngine, Rc6Engine, IdeaEngine, Rc5Engine, Shacal2Engine, BlowfishEngine, TripleDesEngine, TwofishEngine, SerpentEngine, MarsEngine, Cast256Engine, CamelliaEngine, SeedEngine, BlockCipherEngineFactory (13/13 KAT'li ✅)
│   └── Packets/    — PacketGCHandshake, PacketKeyAgreement, PacketCGLogin3, PacketGCPhase, IPacket
├── Network/        (Metin2.Network.asmdef)
│   ├── Session/    — NetworkSessionState
│   └── Transport/  — ITcpConnection, TcpConnection, SimpleTcpProbe
Assets/Tests/EditMode/ (Metin2.Tests.asmdef)
    ├── Core/       — SecretRedactorTests
    ├── Network/    — TcpConnectionTests
    └── Protocol/   — PacketGCHandshakeTests, PacketKeyAgreementTests, PacketCGLogin3Tests,
                       PacketGCPhaseTests, PacketReaderWriterTests, PacketFramerTests,
                       PacketRegistryTests, Dh2KeyAgreementTests, CipherSuiteTests,
                       CipherKeyDerivationTests, CipherSessionTests, TeaEngineTests,
                        Rc6EngineTests, IdeaEngineTests, Rc5EngineTests, Shacal2EngineTests, BlowfishEngineTests, TripleDesEngineTests, TwofishEngineTests, SerpentEngineTests, MarsEngineTests, Cast256EngineTests, CamelliaEngineTests, SeedEngineTests, BlockCipherEngineFactoryTests
```

## Test

```bash
# .NET CLI ile
dotnet test Metin2.Tests.csproj

# Unity Editor ile
# Window > General > Test Runner > EditMode > Run All
```

**Son test sonucu: 318/318 başarılı ✅**

## Dokümanlar

- [`CHANGELOG.md`](CHANGELOG.md) — Değişiklik günlüğü (sprint'e bağlı)
- [`docs/sprints/SPRINT_02-protocol-core.md`](docs/sprints/SPRINT_02-protocol-core.md) — Sprint 2 kaydı
- [`docs/sprints/SPRINT_03-cipher-engines.md`](docs/sprints/SPRINT_03-cipher-engines.md) — Engine kontrol listesi (13/13 ✅ tamamlandı)
- [`docs/decisions/ADR-0002-cipher-engine-strategy.md`](docs/decisions/ADR-0002-cipher-engine-strategy.md) — Port kararı
- [`docs/protocol/cipher-spec.md`](docs/protocol/cipher-spec.md) — Cipher iz sürme (kaynak referanslı)
- [`AGENT_DEVELOPMENT_GUIDE.md`](AGENT_DEVELOPMENT_GUIDE.md) — AI agent geliştirme rehberi ve kurallar
- [`docs/architecture.md`](docs/architecture.md) — Workspace haritası ve build toolchain'leri
- [`docs/protocol/connection-flow.md`](docs/protocol/connection-flow.md) — Tam bağlantı akışı
- [`docs/protocol/protocol-inventory.md`](docs/protocol/protocol-inventory.md) — Paket envanteri ve framing kuralları
- [`docs/protocol/packet-catalog.json`](docs/protocol/packet-catalog.json) — Makine-okunur paket kataloğu

## Bilinen Eksikler (UNVERIFIED)

- DH2 agreed yarı sırası (header-imaalı; ilk canlı şifreli paketle kanıtlanacak)
- CTR counter byte order (CryptoPP `modes.cpp` vendored değil; ilk canlı paketle kanıtlanacak)
- GC ping header çelişkisi: `PacketHeaders.cs` 0xfe vs `packet-catalog.json` 44 (çözülmedi, framer'a alınmadı)

## Sonraki Adım

**Canlı handshake**: 13 engine tamam — sırada canlı sunucuya handshake → key agreement → ilk şifreli paket decode
(bu adım yukarıdaki UNVERIFIED'ları da kapatır).

## Kurallar

- Kaynak kodda doğrulanmayan bilgiler UNVERIFIED olarak işaretlenir
- Credential ve secret değerleri asla çıktıya, teste veya commit'e yazılmaz
- Sunucu otoritesi korunur — client hasar/item/para kararı alamaz
- PC istemcisiyle uyumluluk bozulmaz
