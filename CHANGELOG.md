# Changelog

Tüm önemli değişiklikler bu dosyada izlenir. Format: `Keep a Changelog` uyarlaması (Türkçe).
Tarihler UTC. Her madde ilgili sprint kaydına ve commit'e bağlanır.

## [Unreleased]
### Eklenen
- `HandshakeClient` (`Metin2.Network.Session`): GC 0xff → GC 0xfb → DH2 agree +
  `CipherKeyDerivation` → CG 0xfb → 0xfa bekleme → `SetActivated(true)` (client
  polarity true); fail-closed (`HandshakeFailedException`); şifreli trafik için
  `SendSecureAsync` / `ReceiveSecureFrameAsync` (decrypt-before-frame).
- `HandshakeClientTests` (7 loopback fake-server testi): tam handshake + şifreli
  `GC_PHASE` çift-yön round-trip, parçalı yazımlar, bozuk agreed-length/key,
  handshake-ortası kapanış, handshake-öncesi guard, çift-çalıştırma guard'ı.
- Auth-core login istemcisi (Sprint 4, adım 1): `AuthLoginClient`
  (`Metin2.Network.Session`) — handshake'li kanaldan CG_LOGIN3 (111, 65B) gönderimi,
  150/7 karşılama (`AuthLoginResult`: `Success(LoginKey)` / `Failure(Status)`;
  150+bResult==0 → "BESAMEKEY", `AccountConnector.cpp:321` aynası),
  `ComputePanamaKey` (server `input_auth.cpp:151` == client
  `AccountConnector.cpp:325`), `GenerateClientKeys` (login başına taze 4 word).
- `PacketGCAuthSuccess` (150, 6B) + codec + 9 test (golden, zero-result, truncation, header).
- `PacketGCLoginFailure` (7, 10B, status[9]) + codec + 13 test (NOID/ALREADY/WRONGPWD/SHUTDOWN
  golden, round-trip, trunc-safe, header).
- `PacketLengthTable`: 150→6, 7→10 (kaynak-doğrulanmış boyutlar); `PacketRegistry.CreateAuthRegistry`
  (150 Auth-only, 7 Auth+Login — paylaşılan `LoginFailure()` helper'ı, `input.cpp:177-188`).
- `AuthLoginClientTests` (9 loopback): success (login/password/key assertion + PanamaKey
  eşleşmesi), WRONGPWD, BESAMEKEY, parçalı yanıt, yanlış-phase header fail-closed,
  boş-credential guard (parola sızıntısı yok), ctor guard, PanamaKey formülü, key tazeliği.
- `docs/sprints/SPRINT_04-auth-login.md`; `packet-catalog.json`: 150/7 eklendi (VERIFIED),
  111 VERIFIED'a yükseltildi; `connection-flow.md` §3 auth yanıt yolları VERIFIED.
- Channel login + character list (Sprint 4, adım 2): `ChannelLoginClient`
  (`Metin2.Network.Session`) — CG_LOGIN2 (109, 52B) → empire (90) → slotlar (32, 329B).
- `PacketCGLogin2` + codec (6 test), `PacketGCEmpire` + codec (7 test),
  `SimplePlayer` TSimplePlayer 63B + codec (5 test), `PacketGCLoginSuccess` + codec (6 test).
- `PacketLengthTable`: 90→2, 32→329; `PacketRegistry.CreateChannelRegistry`
  (90 Login-only, 32 Select-only); `GetSlotEndpoint` (lAddr network-order → IP, VERIFIED).
- `ChannelLoginClientTests` (7 loopback): tam kanal login + endpoint/expectation,
  FULL status, parçalı 329B, ters-sıra fail-closed, guard'lar.
- `packet-catalog.json`: 90 eklendi, 109/32 VERIFIED'a yükseltildi (16 paket);
  `connection-flow.md` §4-§5: empire/select sırası, lAddr byte order ve ölü 118 notu.
- Select fazı (Sprint 4, adım 3): `CharacterSelectClient`
  (`Metin2.Network.Session`) — select (yanıtsız) / create → 8/9 / delete → 10/11 /
  empire seçimi / ENTERGAME gönderimi.
- CG select (6, 2B) + create (4, 34B) + delete (5, 10B) + entergame (10, 1B) paket+codec;
  GC create-ok (8, 65B) + create-fail (9, 2B) + delete-ok (10, 2B) + delete-fail (11, 1B).
- `PacketLengthTable`: 8→65, 9→2, 10→2, 11→1; `PacketRegistry.CreateSelectRegistry`
  (8/9/10/11 Select-only; SELECT/LOGIN aynı input processor, `desc.cpp:539-547`).
- `CharacterSelectClientTests` (6 loopback): select→create→delete→entergame tam tur,
  create-fail tipi, delete-fail, yanlış-tür fail-closed, validasyon guard'ları.
- `packet-catalog.json`: 7 select paketi eklendi, 10 VERIFIED (23 paket);
  `connection-flow.md` §5 select/create/delete/ENTERGAME yolları + 1-byte quirk'ler.
- World entry (Sprint 4, adım 4): `WorldEntryClient`
  (`Metin2.Network.Session`) — main-char (113, Loading) → ENTERGAME → TIME+CHANNEL (Game).
- `PacketGCMainCharacter` (113, 46B empire layout) + `PacketGCTime` (106, 5B) +
  `PacketGCChannel` (121, 2B) paket+codec (18 test).
- `PacketLengthTable`: 113→46, 106→5, 121→2; `PacketRegistry.CreateWorldEntryRegistry`
  (113 Loading-only, 106/121 Game-only).
- `WorldEntryClientTests` (4 loopback): tam giriş turu, ters-sıra ve yanlış-faz fail-closed.
- `packet-catalog.json`: 113/106/121 VERIFIED (26 paket); `connection-flow.md` §5
  PlayerLoad→main-char sırası + `time_t` ABI notu.
- Loading stats + spawn (Sprint 5, adım 1): `GameWorldClient`
  (`Metin2.Network.Session`) — points→skills (Loading) + add/del spawn event (Game).
- `PacketGCPoints` (16, 1021B) + `PacketGCSkillLevel` (76, 1531B, skill entry 6B) +
  `PacketGCCharacterAdd` (1, 35B, float angle) + `PacketGCCharacterDelete` (2, 5B).
- `PacketReader/Writer` float desteği (netstandard2.1 uyumlu BitConverter yolu).
- `PacketLengthTable`: 16→1021, 76→1531, 1→35, 2→5; `PacketRegistry.CreateGameRegistry`
  (16/76 Loading-only, 1/2 Game-only).
- `GameWorldClientTests` (5 loopback): stats sırası, add→del eventleri, ters-sıra fail-closed.
- `packet-catalog.json`: 16/76/1/2 VERIFIED (30 paket).
- Item sistemi (Sprint 5, adım 2): `InventoryClient`
  (`Metin2.Network.Session`) — set/clear/update eventleri (Loading+Game).
- `PacketGCItemSet` (21, 51B) + `PacketGCItemDel` (20, 42B DelDeprecated) +
  `PacketGCItemUpdate` (25, 38B) paket+codec (+`ItemFieldCodec`).
- `PacketLengthTable`: 21→51, 20→42, 25→38; game registry +3 (Loading+Game).
- `InventoryClientTests` (5 loopback): set→update→clear yaşam döngüsü,
  Game-fazı kabulü, yanlış-faz fail-closed.
- `packet-catalog.json`: 21/20/25 VERIFIED (33 paket).
- Hareket (Sprint 5, adım 3): `MovementClient`
  (`Metin2.Network.Session`) — move intent + sync batch gönderimi, move/sync alımı.
- `PacketCGMove` (7, 16B) + `PacketGCMove` (3, 24B) + sync element/CG/GC dynamic
  codec'ler (+`MoveFunc` sabitleri).
- Dinamik framer: header 5 inline wSize ile (server-guard paritesi:
  kısa/hizasız/aşırı boy reddi); `PacketLengthTable` sync sabitleri.
- `PacketLengthTable`: 3→24; game registry +2 (3/5 Game-only).
- `MovementClientTests` (6 loopback): niyet+yayın turu, sync turu, rot quantize,
  geçersiz func/koordinat/eleman-sayısı guard'ları.
- `packet-catalog.json`: 7/3/8/5 VERIFIED (37 paket); `connection-flow.md` §6 hareket.
- Test: 536 → 566 (+30).
- Keepalive (ping/pong): `PacketGCPing` (44, 1B) + `PacketCGPong` (0xfe, 1B)
  paket+codec'leri; `PacketRegistry` taban handshake registry'sine GC_PING
  tüm-fazlar kaydı (ping event DESC kurucusunda başlar, desc.cpp:227-233).
- `HandshakeClient`: sunucu keepalive ping'i her iki alım yolunda da (düz-metin
  handshake + şifreli faz) otomatik CG_PONG ile yanıtlanır ve çağırıcıdan
  gizlenir — C++ `RecvPingPacket` aynası (PythonNetworkStream.cpp:636-656,
  beş faz döngüsünde de çağrılır). Pong gönderimi kanal durumuna uyar:
  handshake bitmeden düz-metin, sonrasında şifreli.
- Kaynak-kanıtı: sunucu pong yoksa sonraki çevrimde oturumu kapatır
  (desc.cpp:174-180); çevrim varsayılanı 60 s (config.cpp:32).
- `packet-catalog.json`: GC_PING ve CG_PONG VERIFIED'a yükseltildi (katalog toplam
  37 paket — ping/pong zaten kayıtlıydı, 30 VERIFIED kaldı; sayım düzeltildi);
  `connection-flow.md` §7 keepalive/ping-period UNVERIDDEN kapatıldı.
- Test: 568 → 583 (+15: GCPing 6, CGPong 6, registry 1, loopback 2 —
  handshake ortası düz-metin ping + şifreli faz ping).
- Temel combat (Sprint 5 devamı): 6 paket + `CombatClient`
  (`Metin2.Network.Session`) — saldırı niyeti gönderimi + combat event akışı.
- `PacketCGAttack` (2, 8B) + codec: normal saldırı (type 0) / skill (type>0),
  magic-cube CRC alanları dahil; sunucu her şeyi yeniden doğrular
  (input_main.cpp:1690-1770 + IS_SPEED_HACK).
- `PacketGCPointChange` (17, 17B) + codec — **int-header quirk**: header wire'da
  4 bayt int'tir (11 00 00 00 ile başlar), tek paket böyle; framer atomik
  17B tükettiği için iç sıfırlar padding'e karışmaz. Faz: Select+Loading+Game
  (istemci üç fazda da işler). `PointTypes` sabitleri (char.h EPointTypes
  0..34: HP=5, SP=7, GOLD=11 ...).
- `PacketGCStun` (13, 5B) + `PacketGCDead` (14, 5B) + codec'ler — PacketAround
  yayın (char_battle.cpp:429-432 / 1468-1471).
- `PacketGCMotion` (36, 11B) + codec — gözlemcilerin saldırı animasyonu;
  HEADER_GC_ATTACK (12) bu build'de ölü sabit (gönderen yok), belgelendi.
- `PacketGCDamageInfo` (135, 10B) + codec — hasar sayısı yalnız kurban+saldıran
  descriptor'ına (char_battle.cpp:1584-1605).
- `CombatClient`: `SendAttackAsync` (fail-closed vid guard) + `ReceiveEventAsync`
  (PointChanged/Stunned/Dead/Motion/DamageInfo; stun Loading fazında reddi gibi
  faz guard'ları registry'den).
- `packet-catalog.json`: +6 paket → toplam 43 (36 VERIFIED, 7 PARTIALLY).
- Test: 583 → 636 (+53: CGAttack 5, PointChange 7, Stun 5, Dead 5, Motion 5,
  DamageInfo 5, framer 2, registry 3, Combat 5, PointTypes 1).
### Düzeltilen (kod)
- `PacketHeaders.HEADER_GC_PING`: 0xfe → **44**. Kök neden: 0xfe/`packet.h:115`
  aslında `HEADER_GC_BINDUDP` — önceki geliştirici satırı yanlış okumuş. Her iki
  tarafta kanıtlandı: server `packet.h:172` + client `Packet.h:195` = 44.
  README'deki "bilinen eksik" kapandı.
- `PacketLengthTable`: GC_PING (44 → 1B, `TPacketGCPing` server packet.h:1259-1262,
  client Packet.h:1839-1842) framer'a kaydedildi. Önceki davranışta sunucunun
  periyodik keepalive ping'i (desc.cpp `ping_event`) unknown-header sayılıp
  `DroppedBytes`'a yazılıyordu — sürekli çöp algısına yol açabilirdi.
- `PacketHeaders.cs` bayat satır referansları kaynakla eşitlendi: CG_HANDSHAKE
  (packet.h:6), GC_HANDSHAKE (116), CG_PONG (7), CG_TIME_SYNC (client Packet.h:135).
- `packet-catalog.json` GC_PING `headerSource`: client Packet.h:195 (enum sabiti;
  eski 1842 struct gövdesiydi).
- Framer testleri +2 (566 → **568**): ping tek-bayt frame çöp-sayılmez,
  coalesced akışta sırayla dequeues.
### Düzeltilen (test)
- `RoundTrip("BESAMEKEY")` kaldırıldı: wire buffer 8+null'dur, 9-char status tele
  asla gelmez (codec doğruydu, test yanlıştı).
### Doğrulanan (kaynak-kanıt, UNVERIFIED kapatıldı)
- DH2 agreed yarı sırası = static || ephemeral — VERIFIED (`dh2.h:34-35,59-62` +
  `cipher.cpp:393` çağrı sırası + loopback taraflar-arası aynı secret).
- CTR sayaç = big-endian — VERIFIED (upstream `modes.cpp`
  `OperateKeystream`/`IncrementCounterBy256` + `CtrStream` birebir port +
  loopback şifreli-faz interop).
- `CipherSession` dokümanı güncellendi (engine'ler portlu; null-factory guard halde).

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
