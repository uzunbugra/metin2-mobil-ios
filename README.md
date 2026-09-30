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
| Sprint 3 — Cipher Engine'leri | 🚧 Devam ediyor | 13 engine'den TEA + RC6 + IDEA + RC5 tamam (4/13); factory + KAT altyapısı hazır |

**Test: 201/201 ✅** (`dotnet test Metin2.Tests.csproj`)

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
| Factory (5) | `BlockCipherEngineFactoryTests` | Suite yönlendirme, session round-trip |

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
│   │   └── Engines/— TeaEngine, Rc6Engine, IdeaEngine, Rc5Engine, BlockCipherEngineFactory (KAT'li; +9 sırada)
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
                       Rc6EngineTests, IdeaEngineTests, Rc5EngineTests, BlockCipherEngineFactoryTests
```

## Test

```bash
# .NET CLI ile
dotnet test Metin2.Tests.csproj

# Unity Editor ile
# Window > General > Test Runner > EditMode > Run All
```

**Son test sonucu: 201/201 başarılı ✅**

## Dokümanlar

- [`CHANGELOG.md`](CHANGELOG.md) — Değişiklik günlüğü (sprint'e bağlı)
- [`docs/sprints/SPRINT_02-protocol-core.md`](docs/sprints/SPRINT_02-protocol-core.md) — Sprint 2 kaydı
- [`docs/sprints/SPRINT_03-cipher-engines.md`](docs/sprints/SPRINT_03-cipher-engines.md) — Engine kontrol listesi (4/13 ✅)
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
- 9 cipher engine (tablo-tabanlı olanlar için strateji `SPRINT_03`'te)
- GC ping header çelişkisi: `PacketHeaders.cs` 0xfe vs `packet-catalog.json` 44 (çözülmedi, framer'a alınmadı)

## Sonraki Adım

**Sprint 3 — Cipher Engine'leri** (devam): SHACAL-2 (küçük tablo, 64 word K sabiti) sırada.
Engine'ler bitince: canlı sunucuya handshake → key agreement → ilk şifreli paket decode
(bu adım yukarıdaki UNVERIFIED'ları da kapatır).

## Kurallar

- Kaynak kodda doğrulanmayan bilgiler UNVERIFIED olarak işaretlenir
- Credential ve secret değerleri asla çıktıya, teste veya commit'e yazılmaz
- Sunucu otoritesi korunur — client hasar/item/para kararı alamaz
- PC istemcisiyle uyumluluk bozulmaz
