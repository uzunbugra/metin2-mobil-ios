# Metin2 Mobile — Unity 6 LTS Client

Mevcut Metin2 (Razuning-V5 / 40k tabanlı) C++ sunucusuna bağlanan, Unity/C# ile geliştirilen Android mobil istemcisi.

## Proje Durumu

| Sprint | Durum | Açıklama |
|--------|-------|----------|
| Sprint 0 — Kaynak Audit | ✅ Tamamlandı | Kaynak kod haritası, protokol keşfi, dokümanlar |
| Sprint 1 — Unity Altyapı | ✅ Tamamlandı | Proje iskeleti, golden byte test'leri, codec'ler |
| Sprint 2 — Protocol Core | ✅ Tamamlandı | Transport, framer, registry, DH2 + derivation + session/CTR |
| Sprint 3 — Cipher Engine'leri | 🚧 Devam ediyor | 13 engine'den TEA tamam; factory + KAT altyapısı hazır |

## Ne Yapıldı

### Sprint 0 — Kaynak Kod Audit'i ve Protokol Keşfi
- Workspace doğrulaması: tüm kaynak dizinler (Server C++, Client C++, DumpProto, game configs, MySQL datadir, PC client binary) mevcut
- Server ve client source içinde auth/login/game bağlantı akışı takip edildi
- Packet header, framing, cipher/sequence ve phase geçişleri kaynak dosya+satır referanslarıyla çıkarıldı
- `docs/architecture.md` — Workspace haritası, process topolojisi, build toolchain'leri
- `docs/protocol/connection-flow.md` — Handshake → Key Agreement → Auth → Login → Select → Game tam akışı
- `docs/protocol/protocol-inventory.md` — 60+ CG header, 80+ GC header, GD/DG ve GG header'ları, framing kuralları
- `docs/protocol/packet-catalog.json` — 13 bağlantı yolu paketinin makine-okunur kataloğu

### Sprint 1 — Unity Proje Altyapısı ve Golden Byte Test'leri
- **Unity 6 LTS (6000.0.23f1)** projesi oluşturuldu, Android build target
- **4 Assembly Definition** ile modüler yapı:
  - `Metin2.Core` — Logging, config, secret redaction
  - `Metin2.Protocol` — Saf C#, **UnityEngine bağımsız** (`noEngineReferences: true`), paket codec'leri
  - `Metin2.Network` — TCP transport, session state
  - `Metin2.Tests` — EditMode golden byte test suite
- **Golden Byte Test'leri** (173 test, hepsi geçiyor):
  - Yukarıdaki 74 test (Sprint 1) +
  - `PacketFramer` — fragmented/coalesced TCP, 0x00 padding, unknown-header drop, max-length guard (10 test)
  - `PacketRegistry` — phase-aware dispatch, handshake-path izinleri, duplicate/validation (7 test)
  - `TcpConnection` — loopback connect/send/receive/disconnect/graceful-close (5 test)
  - `Dh2KeyAgreement` — RFC 5114 sabitleri, subgroup order, A↔B simetrisi, fail-closed (11 test)
  - `CipherSuite` — 14 selector eşleşmesi, block/key uzunlukları (33 test)
  - `CipherKeyDerivation` — el-hesaplı vektörler, fail-closed (6 test)
  - `CipherSession` + `CtrStream` — polarity aynası, round-trip, big-endian counter (10 test)
  - `TeaEngine` — 7 KAT vektörü + decrypt + round-trip (14 test)
  - `BlockCipherEngineFactory` — suite yönlendirme + TEA session round-trip (4 test)
  - `TPacketGCHandshake` (0xff, 13 byte) — serialize/deserialize round-trip + edge cases
  - `TPacketKeyAgreement` (0xfb, 261 byte) — serialize/deserialize + zero-padding
  - `TPacketCGLogin3` (111, 65 byte) — serialize/deserialize + credential truncation safety
  - `TPacketGCPhase` (0xfd) — tüm phase enum değerleri için golden test
  - `PacketReader`/`PacketWriter` — Little-Endian I/O, fixed string, bounds checking
  - `SecretRedactor` — credential maskeleme testleri
- **Codec'ler**: Her paket için `Serialize`/`TryDeserialize`/`Deserialize` + hata yönetimi
- **Security**: Secret redaction, credential'lar test fixture'larda dummy değer

## Mimari

```
Unity Client (Android)
    |
    | TCP (Little-Endian, #pragma pack(1))
    v
Auth Core (handshake → key agreement → PHASE_AUTH)
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
│   ├── Exceptions/ — PacketException, PacketUnderflowException, InvalidPacketHeaderException
│   ├── Framing/    — PacketLengthTable, PacketFramer (TCP stream → frame)
│   ├── Registry/   — PacketRegistry, PacketDescriptor (phase-aware dispatch)
│   ├── Security/   — DiffieHellmanGroup, Dh2KeyAgreement, CipherSuite, CipherKeyDerivation, CipherSession, CtrStream
│   └── Packets/    — PacketGCHandshake, PacketKeyAgreement, PacketCGLogin3, PacketGCPhase, IPacket
├── Network/        (Metin2.Network.asmdef)
│   ├── Session/    — NetworkSessionState
│   └── Transport/  — ITcpConnection, TcpConnection, SimpleTcpProbe
Assets/Tests/EditMode/ (Metin2.Tests.asmdef)
    ├── Core/       — SecretRedactorTests
    └── Protocol/   — PacketGCHandshakeTests, PacketKeyAgreementTests, PacketCGLogin3Tests, PacketGCPhaseTests, PacketReaderWriterTests
```

## Test

```bash
# .NET CLI ile
dotnet test Metin2.Tests.csproj

# Unity Editor ile
# Window > General > Test Runner > EditMode > Run All
```

**Son test sonucu: 173/173 başarılı ✅**

## Dokümanlar

- [`AGENT_DEVELOPMENT_GUIDE.md`](AGENT_DEVELOPMENT_GUIDE.md) — AI agent geliştirme rehberi ve kurallar
- [`docs/architecture.md`](docs/architecture.md) — Workspace haritası ve build toolchain'leri
- [`docs/protocol/connection-flow.md`](docs/protocol/connection-flow.md) — Tam bağlantı akışı (kaynak referanslı)
- [`docs/protocol/protocol-inventory.md`](docs/protocol/protocol-inventory.md) — Paket envanteri ve framing kuralları
- [`docs/protocol/packet-catalog.json`](docs/protocol/packet-catalog.json) — Makine-okunur paket kataloğu

## Sonraki Adım

**Sprint 3 — Cipher Engine'leri** (devam ediyor): TEA portu + KAT testleri + factory tamam (`docs/sprints/SPRINT_03-cipher-engines.md`). Kalan 12 engine aynı pattern'le (her biri resmi KAT ile). Sonraki hedef: engine'ler bitince canlı sunucuya bağlanıp handshake + key agreement tamamlamak.

## Kurallar

- Kaynak kodda doğrulanmayan bilgiler UNVERIFIED olarak işaretlenir
- Credential ve secret değerleri asla çıktıya, teste veya commit'e yazılmaz
- Sunucu otoritesi korunur — client hasar/item/para kararı alamaz
- PC istemcisiyle uyumluluk bozulmaz
