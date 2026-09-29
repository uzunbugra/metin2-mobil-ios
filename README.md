# Metin2 Mobile — Unity 6 LTS Client

Mevcut Metin2 (Razuning-V5 / 40k tabanlı) C++ sunucusuna bağlanan, Unity/C# ile geliştirilen Android mobil istemcisi.

## Proje Durumu

| Sprint | Durum | Açıklama |
|--------|-------|----------|
| Sprint 0 — Kaynak Audit | ✅ Tamamlandı | Kaynak kod haritası, protokol keşfi, dokümanlar |
| Sprint 1 — Unity Altyapı | ✅ Tamamlandı | Proje iskeleti, golden byte test'leri, codec'ler |
| Sprint 2 — Protocol Core | 🔜 Sırada | TCP transport, framer, handshake/cipher |

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
- **Golden Byte Test'leri** (74 test, hepsi geçiyor):
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
│   └── Packets/    — PacketGCHandshake, PacketKeyAgreement, PacketCGLogin3, PacketGCPhase, IPacket
├── Network/        (Metin2.Network.asmdef)
│   ├── Session/    — NetworkSessionState
│   └── Transport/  — ITcpConnection, SimpleTcpProbe
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

**Son test sonucu: 74/74 başarılı ✅**

## Dokümanlar

- [`AGENT_DEVELOPMENT_GUIDE.md`](AGENT_DEVELOPMENT_GUIDE.md) — AI agent geliştirme rehberi ve kurallar
- [`docs/architecture.md`](docs/architecture.md) — Workspace haritası ve build toolchain'leri
- [`docs/protocol/connection-flow.md`](docs/protocol/connection-flow.md) — Tam bağlantı akışı (kaynak referanslı)
- [`docs/protocol/protocol-inventory.md`](docs/protocol/protocol-inventory.md) — Paket envanteri ve framing kuralları
- [`docs/protocol/packet-catalog.json`](docs/protocol/packet-catalog.json) — Makine-okunur paket kataloğu

## Sonraki Adım

**Sprint 2 — Protocol Core**: TCP transport, packet framer (fragmented/coalesced TCP handling), cipher/key agreement implementasyonu, phase-aware packet registry. İlk hedef: canlı sunucuya bağlanıp handshake + key agreement tamamlamak.

## Kurallar

- Kaynak kodda doğrulanmayan bilgiler UNVERIFIED olarak işaretlenir
- Credential ve secret değerleri asla çıktıya, teste veya commit'e yazılmaz
- Sunucu otoritesi korunur — client hasar/item/para kararı alamaz
- PC istemcisiyle uyumluluk bozulmaz
