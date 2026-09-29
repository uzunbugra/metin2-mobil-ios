# Sprint 2 — Protocol Core (kayıt)

Durum: çekirdek tamam, cipher engine'leri Sprint 3'e devredildi.
İlgili doküman: `docs/protocol/cipher-spec.md`, `docs/protocol/connection-flow.md`.

## Bölüm 1 — Transport / Framer / Registry

Hedef: canlı handshake öncesi TCP altyapısı (cipher hariç).

| İş | Dosya | Test |
|---|---|---|
| TCP transport | `Assets/Scripts/Network/Transport/TcpConnection.cs` | `TcpConnectionTests` (5, loopback) |
| Framing | `Assets/Scripts/Protocol/Framing/PacketLengthTable.cs`, `PacketFramer.cs` | `PacketFramerTests` (10) |
| Phase-aware registry | `Assets/Scripts/Protocol/Registry/PacketRegistry.cs` | `PacketRegistryTests` (7) |

Doğrulama: `dotnet test Metin2.Tests.csproj` → 96/96.
Not: 1 ara hata (`AsReadOnly` cast) bulunup düzeltildi.

## Bölüm 2 — Cipher çekirdek (engine'ler hariç)

Hedef: DH2 + suite seçimi + key derivation + session/polarity + CTR altyapısı.
Kaynak iz sürme: `cipher.cpp/h`, `desc.cpp:704-741`, `input.cpp:556-583`,
`NetStream.cpp` (polarity true), `PhaseHandShake.cpp:202-258`, vendored CryptoPP header'ları.

| İş | Dosya | Test |
|---|---|---|
| RFC 5114 sabitleri | `Security/DiffieHellmanGroup.cs` | subgroup order `g^q==1` check |
| DH2 anlaşması | `Security/Dh2KeyAgreement.cs` | `Dh2KeyAgreementTests` (11: simetri, fail-closed) |
| Suite tablosu | `Security/CipherSuite.cs` | `CipherSuiteTests` (33: 14 selector + uzunluklar) |
| Key/IV türetme | `Security/CipherKeyDerivation.cs` | `CipherKeyDerivationTests` (6: el-hesaplı vektör) |
| Session + CTR | `Security/CipherSession.cs`, `CtrStream.cs` | `CipherSessionTests` (10: polarity aynası, counter) |

Doğrulama: `dotnet test Metin2.Tests.csproj` → 155/155.

## Sprint 3'e devredilenler

- 15 block-cipher engine portu (`SPRINT_03-cipher-engines.md`).
- UNVERIFIED: DH2 yarı sırası, CTR counter byte order → ilk canlı şifreli paketle kanıtlanacak.
- Bilinen çelişki (çözülmedi): GC ping header'ı — `PacketHeaders.cs` 0xfe vs `packet-catalog.json` 44.
