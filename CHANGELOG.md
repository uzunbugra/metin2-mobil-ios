# Changelog

Tüm önemli değişiklikler bu dosyada izlenir. Format: `Keep a Changelog` uyarlaması (Türkçe).
Tarihler UTC. Her madde ilgili sprint kaydına ve commit'e bağlanır.

## [Unreleased]

## [2026-09-29] — Sprint 3 başladı: cipher engine'leri
### Eklenen
- `docs/sprints/SPRINT_03-cipher-engines.md`: 15 engine için kontrol listesi, KAT kaynakları, DoD.
- `docs/decisions/ADR-0002-cipher-engine-strategy.md`: port-vs-bağımlılık kararı (port).
- TEA engine (`CipherSuite.TEA`): Wheeler–Needham + bağımsız KAT setiyle doğrulandı (7 vektör).
- `BlockCipherEngineFactory`: implemente suite'ler için üretici; diğerleri `CipherEngineNotImplementedException`.
- Test: 155 → 173 (+18: TEA KAT/decrypt/round-trip 14, factory 4).

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
