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
| Sprint 3 epic — Handshake (offline) | ✅ Tamamlandı | `HandshakeClient` + şifreli faz round-trip (loopback); DH2 sırası + CTR order kaynak-kanıtlı VERIFIED |
| Sprint 4 — Auth Login (adım 1) | ✅ Tamamlandı | `AuthLoginClient`: CG_LOGIN3 → 150/7 karşılama + PanamaKey (loopback kanıtlı) |
| Sprint 4 — Channel Login (adım 2) | ✅ Tamamlandı | `ChannelLoginClient`: CG_LOGIN2 → empire → 4 slot + endpoint (loopback kanıtlı) |
| Sprint 4 — Select Fazı (adım 3) | ✅ Tamamlandı | `CharacterSelectClient`: select/create/delete + ENTERGAME (loopback kanıtlı) |
| Sprint 4 — World Entry (adım 4) | ✅ Tamamlandı | `WorldEntryClient`: main-char → ENTERGAME → TIME+CHANNEL (loopback kanıtlı) |
| Sprint 5 — Loading Stats + Spawn (adım 1) | ✅ Tamamlandı | `GameWorldClient`: points/skills + add/del eventleri (loopback kanıtlı) |
| Sprint 5 — Item Sistemi (adım 2) | ✅ Tamamlandı | `InventoryClient`: set/clear/update eventleri (loopback kanıtlı) |
| Sprint 5 — Hareket (adım 3) | ✅ Tamamlandı | `MovementClient`: move/sync gönderim+alım, dinamik framer (loopback kanıtlı) |
| Sprint 5 — Temel Combat (adım 4) | ✅ Tamamlandı | `CombatClient`: saldırı + point/stun/dead/motion/damage akışı (loopback kanıtlı) |
| Sprint 5 — Envanter Aksiyonları (adım 5) | ✅ Tamamlandı | `InventoryClient` gönderim: use/move/drop/pickup/use-to-item (loopback kanıtlı) |
| Sprint 6 — Önyüz Temeli (adım 1) | ✅ Tamamlandı | `GameFlow` + `DemoServer`: uçtan uca entegrasyon (GC_PHASE şeffaf tüketim dahil) |

**Test: 685/685 ✅** (`dotnet test headless/Metin2.Tests.Headless.csproj`)

## Gelişim Hikayesi (Adım Adım)

### Adım 0 — Kaynak Kod Audit'i ve Protokol Keşfi (Sprint 0)
- Workspace doğrulandı: Server C++ source, Client C++ source, DumpProto, game config'leri, MySQL datadir, PC client binary — hepsi mevcut.
- Auth/login/game bağlantı akışı server + eski client source içinde dosya+satır referanslarıyla takip edildi.
- Çıktılar:
  - `docs/architecture.md` — Workspace haritası, process topolojisi, build toolchain'leri
  - `docs/protocol/connection-flow.md` — Handshake → Key Agreement → Auth → Login → Select → Game
  - `docs/protocol/protocol-inventory.md` — 60+ CG, 80+ GC header, GD/DG/GG header'ları, framing kuralları
  - `docs/protocol/packet-catalog.json` — 49 bağlantı paketi (42 VERIFIED + 7 PARTIALLY), makine-okunur katalog
- [`docs/sprints/SPRINT_05-game-world.md`](docs/sprints/SPRINT_05-game-world.md) — Loading stats + spawn kaydı
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

### Adım 17 — Handshake Orkestrasyonu (Sprint 3 epic, offline kısmı ✅)
- `HandshakeClient` (`Network/Session`): GC 0xff → GC 0xfb → DH2 agree + SetUp
  türetme → CG 0xfb → 0xfa bekleme → activate (client polarity **true**);
  fail-closed (`HandshakeFailedException`); `SendSecureAsync` /
  `ReceiveSecureFrameAsync` (decrypt-before-frame).
- Kaynak-kanıtla kapatılanlar: DH2 yarı sırası (dh2.h + cipher.cpp:393) ve CTR
  big-endian sayaç (upstream modes.cpp) — cipher-spec.md §2/§4 VERIFIED.
- Test: 318 → **325/325** (+7 loopback: tam handshake + şifreli `GC_PHASE`
  çift-yön round-trip, parçalı yazım, bozuk length/key, erken kapanış,
  pre-handshake guard, double-run guard).

### Adım 18 — Auth Login (Sprint 4, adım 1 ✅)
- Kaynak iz sürme: C→S 111 AccountConnector→input_auth→DB sorgusu; S→C yanıtlar
  `input_db.cpp:1679-1712` (150) ve paylaşılan `LoginFailure()` helper'ı (7).
  PanamaKey formülü iki tarafta aynı (`input_auth.cpp:151` == `AccountConnector.cpp:325`).
- `PacketGCAuthSuccess` (150, 6B) + `PacketGCLoginFailure` (7, 10B) paket+codec'leri
  (golden byte testli); `PacketLengthTable` 150→6, 7→10; `PacketRegistry.CreateAuthRegistry`
  (150 Auth-only, 7 Auth+Login).
- `AuthLoginClient` (`Network/Session`): `SendLoginAsync` + `ReceiveResultAsync`
  (`Success(LoginKey)` / `Failure(Status)`; 150+bResult==0 → "BESAMEKEY"),
  `ComputePanamaKey`, `GenerateClientKeys`. Parola asla exception/log'a girmez.
- Gerçek av: `RoundTrip("BESAMEKEY")` patladı — wire buffer 8+null, 9-char status
  tele asla gelmez (codec doğruydu, test düzeltildi).
- Test: 325 → **362/362** (+37: AuthSuccess 10, LoginFailure 14, registry 3, framer 1, AuthLogin 9).

### Adım 19 — Channel Login + Character List (Sprint 4, adım 2 ✅)
- Kaynak iz sürme: `LoginByKey` (`input_login.cpp:138-190`) → DB →
  `LoginSuccess` (`input_db.cpp:106-175`: status "OK" şart, empire, PHASE_SELECT) →
  `SendLoginSuccessPacket` (`desc.cpp:892-919`, NEWSLOT=32).
- `TSimplePlayer` 63B iki tarafta birebir aynı (`tables.h:275-291` vs
  `Packet.h:1096-1113`); slot endpoint VERIFIED (`inet_addr` → verbatim `Connect`).
  `GC_LOGIN_KEY` (118) bu build'de ölü (gönderen yok) — implemente edilmedi.
- `PacketCGLogin2` (52B) + `PacketGCEmpire` (2B) + `SimplePlayer` + `PacketGCLoginSuccess`
  (329B) paket+codec'leri; framer 90→2, 32→329; `CreateChannelRegistry`.
- `ChannelLoginClient`: `SendChannelLoginAsync` + `ReceiveSelectDataAsync`
  (empire Login-fazında, slotlar Select-fazında; 7 → Failure).
- Gerçek avlar: `IPAddress(long)` takas yapmaz (LSB-first genişletme gerekir);
  null/"" slot adı denkliği.
- Test: 362 → **397/397** (+35: Login2 6, Empire 7, SimplePlayer 5, LoginSuccess 6, registry 3, framer 1, Channel 7).

### Adım 20 — Select Fazı (Sprint 4, adım 3 ✅)
- Kaynak iz sürme: select yanıtsızdır (yalnızca DB'ye `GD_PLAYER_LOAD`);
  create kuralları (`input_login.cpp:416-499`), yanıtlar 8/9/10/11
  (`input_db.cpp:190-300`); istemci dispatch `PhaseSelect.cpp:41-140`.
- 8 yeni paket+codec: CG select (2B) / create (34B) / delete (10B) / entergame (1B),
  GC create-ok (65B) / create-fail (2B) / delete-ok (2B) / delete-fail (1B).
- `CharacterSelectClient`: select/empire/create/delete/entergame gönderimleri +
  create/delete sonuç bekleyicileri (fail-closed, slot validasyonlu).
- Quirk'ler: 9-header'ının 1-byte yolu (`input_db.cpp:199`, parite korunur);
  11'in adı yanıltır (tetikleyici boş slot).
- Test: 397 → **450/450** (+53).

### Adım 21 — World Entry (Sprint 4, adım 4 ✅)
- Kaynak iz sürme: select → DB `PlayerLoad` (PHASE_LOADING + main-char +
  loading bundle) → ENTERGAME → `Entergame` (PHASE_GAME + TIME + CHANNEL + greet).
- 113 her zaman 46B empire layout'tur (45B header-15 legacy'dir); struct sırası
  kritik: name race'ten SONRA gelir. `time_t` 4B iki tarafta (sunucu 32-bit +
  istemci `_USE_32BIT_TIME_T`).
- `PacketGCMainCharacter` (46B) + `PacketGCTime` (5B) + `PacketGCChannel` (2B);
  framer 113→46, 106→5, 121→2; `CreateWorldEntryRegistry`.
- `WorldEntryClient`: 113 (Loading) → 10 → 106+121 (Game, sıra dayatmalı).
- Test: 450 → **476/476** (+26: MainCharacter 6, Time 6, Channel 6, registry 3, framer 1, WorldEntry 4).

### Adım 22 — Loading Stats + Spawn (Sprint 5, adım 1 ✅)
- Kaynak iz sürme: `PlayerLoad` devamı (points + skills, quickslot wire'a çıkmaz) →
  item'lar ayrı `DG_ITEM_LOAD` yolundan (sonraki adım); spawn `Show` → 1/35B.
- `PacketGCPoints` (1021B) + `PacketGCSkillLevel` (1531B, entry 6B) +
  `PacketGCCharacterAdd` (35B, float angle) + `PacketGCCharacterDelete` (5B).
- Buffer'a float eklendi (netstandard2.1'de BinaryPrimitives yok).
- `GameWorldClient`: stats sırası (16→76) + spawn event (1/2, Game-fazında).
- Test: 476 → **511/511** (+35: Points 6, Skill 7, Add 6, Del 5, Buffer 2, registry 3, framer 1, World 5).

### Adım 23 — Item Sistemi (Sprint 5, adım 2 ✅)
- Kaynak iz sürme: `SetItem` (51B SET / 42B sıfırlanmış DEL) + `UpdatePacket` (38B);
  ilk envanter `ItemLoad` yolundan parça-parça gelir.
- İsim tuzakları belgelendi: sunucu-21 = istemci-SET2 (51B aynı); 20 asla 2-byte değildir.
- `PacketGCItemSet` + `PacketGCItemDel` + `PacketGCItemUpdate` paket+codec'leri
  (paylaşımlı `ItemFieldCodec`, `ref struct` notuyla).
- `InventoryClient`: Set/Cleared/Updated eventleri (Loading+Game fazlarında).
- Test: 511 → **536/536** (+25: Set 6, Del 6, Update 6, registry 1, framer 1, Inventory 5).

### Adım 24 — Hareket (Sprint 5, adım 3 ✅)
- Kaynak iz sürme: istemci niyeti (rot=degrees/5) → sunucu authoritative kontrol
  (teleport/speed/combo) → izleyicilere rebroadcast (gönderen hariç); sync batch
  (wSize doğrulama, 16 clamp, sync-owner + 3500cm anti-hack).
- `PacketCGMove` (16B) + `PacketGCMove` (24B) + sync element/CG/GC dynamic codec'ler.
- Framer artık dinamik header 5'i anlar (inline wSize, server-guard paritesi).
- `MovementClient`: quantize + fail-closed validasyon; gönderim/alım akışları.
- Test: 536 → **566/566** (+30: CGMove 6, GCMove 6, Sync 8, framer 3, registry 1, Movement 6).

### Adım 25 — Keepalive Ping/Pong (Sprint 5 sonrası sağlamlaştırma)
- Kaynak iz sürme: `ping_event` DESC kurucusunda başlar (`desc.cpp:227-233`) — yani
  handshake fazında bile düz-metin ping gelebilir; sunucu pong yoksa sonraki çevrimde
  oturumu kapatır (`desc.cpp:174-180`, çevrim varsayılanı 60s, `config.cpp:32`).
  İstemci beş faz döngüsünün hepsinde `RecvPingPacket` ile yanıtlar
  (`PythonNetworkStream.cpp:636-656`).
- `PacketGCPing` (44, 1B) + `PacketCGPong` (0xfe, 1B) paket+codec'leri; registry'ye
  GC_PING tüm-fazlar kaydı.
- `HandshakeClient` iki alım yolunda da ping'i otomatik yanıtlar (kanal durumuna uyar:
  handshake öncesi düz-metin, sonrası şifreli pong) ve çağırıcıdan gizler — tüm
  session client'ları bu davranışı ücretsiz devralır.
- Test: 568 → **583/583** (+15: codec 12, registry 1, loopback 2).

### Adım 26 — Temel Combat (Sprint 5 devamı)
- Kaynak iz sürme: `CInputMain::Attack` (input_main.cpp:1690-1770) → `CHARACTER::Attack`
  (char_battle.cpp:179-286) — sunucu menzil/hız/hedef doğrular; hasar sayısı yalnız
  kurban+saldıran'a (`SendDamagePacket`, char_battle.cpp:1584-1605), HP deltası
  `PointChange` (char.cpp:3595-3613), ölüm/bayılma `PacketAround` yayın.
- 6 paket+codec: CG_ATTACK (2, 8B), GC_POINT_CHANGE (17, 17B — **int-header quirk**:
  wire'da header 4 bayt!), GC_STUN (13, 5B), GC_DEAD (14, 5B), GC_MOTION (36, 11B),
  GC_DAMAGE_INFO (135, 10B) + `PointTypes` sabitleri (EPointTypes 0..34).
- `CombatClient`: saldırı niyeti gönderimi + 5 tip combat event akışı (faz-aware);
  HEADER_GC_ATTACK (12) bu build'de ölü sabit — belgelendi, implemente edilmedi.
- Test: 583 → **636/636** (+53).

### Adım 27 — Envanter Yazma Aksiyonları (Sprint 5 devamı)
- Kaynak iz sürme: `CInputMain` item handler'ları (input_main.cpp:830-884, observer-mode
  guard'lı dispatch 3142-3172) → `CHARACTER::MoveItem` (char_item.cpp:5557+: pozisyon
  geçerliliği, exchanging/locked/irremovable, equip/stack-merge yolları) — sonuçlar
  zaten implement edilen GC 21/20/25 + point değişimleriyle döner.
- 6 CG paket+codec: ITEM_USE (11, 4B), ITEM_MOVE (13, 8B), ITEM_DROP (12, 8B),
  ITEM_DROP2 (20, 9B), ITEM_PICKUP (15, 5B), ITEM_USE_TO_ITEM (60, 7B) — hepsi
  paylaşımlı `TItemPos` (3B) kullanır; `ItemWindow` sabitleri (EWindows).
- `InventoryClient` artık gönderir de: use/use-to-item/move (count 0 = tüm stack)/
  drop-item/drop-gold/drop-partial/pickup — fail-closed guard'lar (NPOS window,
  vid=0, gold=0, count=0), gerçek doğrulama sunucuda.
- Test: 636 → **681/681** (+45).

### Adım 28 — Unity Önyüz Temeli: GameFlow + DemoServer (Sprint 6 başlangıcı)
- **GC_PHASE interleaving gap'i kapatıldı**: gerçek sunucu faz paketlerini step-reply'lerin
  arasına iter ([90][PHASE(SELECT)][32]) — step-client'lar buna takılırdı, loopback testleri
  göndermediği için görünmemişti. `HandshakeClient` artık faz paketlerini ping gibi şeffaf
  tüketip `PhaseChanged` eventi ile açığa çıkarıyor.
- **`Metin2.Gameplay`** (5. assembly, motor-bağımsız): `GameFlow` — üst seviye bağlantı
  yaşam döngüsü (login → channel → select → dünya girişi → event pump) + tip'li event'ler;
  Unity katmanı bunlara bağlanacak.
- **`DemoServer`**: süreç-içi sahte sunucu — gerçek DH2+cipher ve gerçek frame sıraları
  (faz push'ları + keepalive dahil). Canlı sunucu olmadan uçtan uca entegrasyon kanıtı
  ve UI demo-modunun beslemesi.
- Uçtan uca test: auth → channel → karakter → dünya → spawn → saldırı/ölüm → hareket →
  item taşıma, faz ve state dizileri assert'li.
- Test: 681 → **685/685** (+4).

### Adım 29 — Unity'de İlk Görsel: Demo Dünyası (Sprint 6, adım 2)
- `Metin2.Frontend` assembly'si (Unity bağlamı; Gameplay/Network/Protocol/Core referansları)
  + `DemoWorldBehaviour`: Bootstrap scene'inde Play'e basınca DemoServer süreç içinde
  başlar, gerçek `GameFlow` tam yolculuğu koşar (auth → channel → karakter → dünya) ve
  sonuçlar görselleşir: spawn event'lerinden küpler/kapsül doğar, kapsül YALNIZ sunucu
  GC_MOVE rebroadcast'i ile hareket eder (server-authoritative görsel), SPACE ile saldırı →
  hasar/HP/ölüm paketleri HUD'a düşer.
- Thread sözleşmesi: flow arka planda, tüm Unity çağrıları Update'te boşaltılan kuyrukla
  ana thread'e (guide §5.2).
- Headless csproj'lar `headless/` altına taşındı (Unity csproj çakışması çözüldü).
- Test: 685/685 (headless grafiği Frontend'i içermez — o yalnız Unity'de derlenir).

## Test Tablosu (Komut: `dotnet test headless/Metin2.Tests.Headless.csproj`)

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
| Handshake (7) | `HandshakeClientTests` | Loopback tam handshake, şifreli faz çift-yön, fragmantasyon, fail-closed |
| AuthSuccess (10) | `PacketGCAuthSuccessTests` | Golden byte, zero-result, truncation, header check |
| LoginFailure (14) | `PacketGCLoginFailureTests` | 4 status golden byte, round-trip, trunc-safe, header check |
| AuthRegistry (3) | `PacketRegistryTests` | 150 Auth-only, 7 Auth+Login faz izinleri |
| AuthLogin (9) | `AuthLoginClientTests` | Loopback login success/failure, PanamaKey, parçalı yanıt, fail-closed, parola-sızıntısı yok |
| Login2 (6) | `PacketCGLogin2Tests` | Golden byte, LoginKey round-trip, truncation, header check |
| Empire (7) | `PacketGCEmpireTests` | 1..3 round-trip, truncation, header check |
| SimplePlayer (5) | `SimplePlayerCodecTests` | 63B layout, tüm-alan round-trip, boş slot |
| LoginSuccess (6) | `PacketGCLoginSuccessTests` | 329B layout, slot/guild/mark round-trip, header check |
| Channel (7) | `ChannelLoginClientTests` | Loopback empire+slot, endpoint, FULL, parçalı, ters-sıra fail-closed |
| SelectCG (22) | `PacketCGCharacterSelectTests`, `PacketCGCharacterCreateTests`, `PacketCGCharacterDeleteTests`, `PacketCGEnterGameTests` | Golden layout, round-trip, truncation, header check |
| SelectGC (22) | `PacketGCCreateSuccessTests`, `PacketGCCreateFailureTests`, `PacketGCDeleteSuccessTests`, `PacketGCDeleteFailureTests` | Golden layout, round-trip, truncation, header check |
| SelectFlow (6) | `CharacterSelectClientTests` | Loopback select→create→delete→entergame, fail paths, validasyon |
| WorldEntry-Codecs (18) | `PacketGCMainCharacterTests`, `PacketGCTimeTests`, `PacketGCChannelTests` | Golden layout, struct-order, legacy-15 reddi, header check |
| WorldEntry (4) | `WorldEntryClientTests` | Loopback main-char→entergame→time+channel, sıra/faz fail-closed |
| Stats (13) | `PacketGCPointsTests`, `PacketGCSkillLevelTests` | Offset spot-check, full round-trip, truncation, header check |
| Spawn (11) | `PacketGCCharacterAddTests`, `PacketGCCharacterDeleteTests` | Float golden byte, round-trip, truncation, header check |
| GameWorld (5) | `GameWorldClientTests` | Loopback stats sırası, add→del eventleri, fail-closed |
| Items (18) | `PacketGCItemSetTests`, `PacketGCItemDelTests`, `PacketGCItemUpdateTests` | Golden layout, round-trip, truncation, header check |
| Inventory (5) | `InventoryClientTests` | Loopback set→update→clear, Game-fazı kabulü, fail-closed |
| Movement-Codecs (20) | `PacketCGMoveTests`, `PacketGCMoveTests`, `SyncPositionCodecTests` | Golden layout, dynamic wSize, clamp, misalignment |
| Movement (6) | `MovementClientTests` | Loopback niyet+yayın, sync turu, quantize, guard'lar |
| Keepalive-Codecs (12) | `PacketGCPingTests`, `PacketCGPongTests` | 1B golden byte, round-trip, truncation, header (0xfe ≠ GC_PING regression) |
| Keepalive (2) | `HandshakeClientTests` | Loopback: handshake ortası düz-metin ping→pong, şifreli faz ping→pong + sonraki frame |
| Combat-Codecs (32) | `PacketCGAttackTests`, `PacketGCPointChangeTests`, `PacketGCStunTests`, `PacketGCDeadTests`, `PacketGCMotionTests`, `PacketGCDamageInfoTests` | Golden layout (int-header quirk dahil), round-trip, truncation, header |
| Combat (5) | `CombatClientTests` | Loopback saldırı turu + 5 event tipi, faz guard'ları (stun Loading'de reddi), vid guard |
| ItemAction-Codecs (34) | `PacketCGItemUseTests`, `PacketCGItemMoveTests`, `PacketCGItemDropTests`, `PacketCGItemDrop2Tests`, `PacketCGItemPickupTests`, `PacketCGItemUseToItemTests` | Golden layout (TItemPos), item/gold varyantları, round-trip, truncation, header |
| Inventory-Send (3) | `InventoryClientTests` | Loopback move/use/drop2/pickup + use-to-item/gold-drop turu, NPOS/vid/gold/count guard'ları |
| GameFlow (3) | `GameFlowTests` | Uçtan uca: GameFlow ↔ DemoServer (auth→channel→select→dünya→saldırı/hareket/item), faz+state dizileri, yanlış kimlik |

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
│   ├── Buffer/     — PacketReader, PacketWriter (LE binary I/O + float)
│   ├── Codecs/     — PacketGCHandshakeCodec, PacketKeyAgreementCodec, PacketCGLogin3Codec, PacketCGLogin2Codec, PacketGCPhaseCodec, PacketGCPingCodec, PacketCGPongCodec, PacketGCAuthSuccessCodec, PacketGCLoginFailureCodec, PacketGCEmpireCodec, SimplePlayerCodec, PacketGCLoginSuccessCodec, PacketCGCharacter{Select,Create,Delete}Codec, PacketCGEnterGameCodec, PacketGC{CreateSuccess,CreateFailure,DeleteSuccess,DeleteFailure}Codec, PacketGCMainCharacterCodec, PacketGCTimeCodec, PacketGCChannelCodec, PacketGC{Points,SkillLevel}Codec, PlayerSkillCodec, PacketGCCharacter{Add,Delete}Codec, ItemFieldCodec, PacketGCItem{Set,Del,Update}Codec, PacketCGMoveCodec, PacketGCMoveCodec, SyncPositionElementCodec, PacketCGSyncPositionCodec, PacketGCSyncPositionCodec, PacketCGAttackCodec, PacketGCPointChangeCodec, PacketGCStunCodec, PacketGCDeadCodec, PacketGCMotionCodec, PacketGCDamageInfoCodec, PacketCGItemUseCodec, PacketCGItemMoveCodec, PacketCGItemDropCodec, PacketCGItemDrop2Codec, PacketCGItemPickupCodec, PacketCGItemUseToItemCodec
│   ├── Constants/  — PacketHeaders, PhaseType, PointTypes, ItemWindow
│   ├── Exceptions/ — PacketException, PacketUnderflowException, InvalidPacketHeaderException,
│   │                  CipherEngineNotImplementedException, HandshakeFailedException
│   ├── Framing/    — PacketLengthTable, PacketFramer (TCP stream → frame)
│   ├── Registry/   — PacketRegistry, PacketDescriptor (phase-aware dispatch)
│   ├── Security/   — DiffieHellmanGroup, Dh2KeyAgreement, CipherSuite, CipherKeyDerivation,
│   │                  CipherSession, CtrStream
│   │   └── Engines/— TeaEngine, Rc6Engine, IdeaEngine, Rc5Engine, Shacal2Engine, BlowfishEngine, TripleDesEngine, TwofishEngine, SerpentEngine, MarsEngine, Cast256Engine, CamelliaEngine, SeedEngine, BlockCipherEngineFactory (13/13 KAT'li ✅)
│   └── Packets/    — PacketGCHandshake, PacketKeyAgreement, PacketCGLogin3, PacketCGLogin2, PacketGCPhase, PacketGCPing, PacketCGPong, PacketGCAuthSuccess, PacketGCLoginFailure, PacketGCEmpire, SimplePlayer, PacketGCLoginSuccess, PacketCGCharacter{Select,Create,Delete}, PacketCGEnterGame, PacketGC{CreateSuccess,CreateFailure,DeleteSuccess,DeleteFailure}, PacketGCMainCharacter, PacketGCTime, PacketGCChannel, PacketGC{Points,SkillLevel}, PlayerSkill, PacketGCCharacter{Add,Delete}, ItemAttribute, PacketGCItem{Set,Del,Update}, MoveFunc, PacketCGMove, PacketGCMove, SyncPositionElement, PacketCGSyncPosition, PacketGCSyncPosition, PacketCGAttack, PacketGCPointChange, PacketGCStun, PacketGCDead, PacketGCMotion, PacketGCDamageInfo, PacketCGItemUse, PacketCGItemMove, PacketCGItemDrop, PacketCGItemDrop2, PacketCGItemPickup, PacketCGItemUseToItem, IPacket
├── Network/        (Metin2.Network.asmdef)
│   ├── Session/    — NetworkSessionState, HandshakeClient (0xff→0xfb→0xfb→0xfa + keepalive ping→pong), AuthLoginClient (111→150/7), ChannelLoginClient (109→90→32), CharacterSelectClient (6/4/5/10→8/9/10/11), WorldEntryClient (113→10→106+121), GameWorldClient (16/76→1/2), InventoryClient (21/20/25 + C2S 11/12/13/15/20/60), MovementClient (7/8→3/5), CombatClient (2→17/13/14/36/135)
│   └── Transport/  — ITcpConnection, TcpConnection, SimpleTcpProbe
├── Gameplay/       (Metin2.Gameplay.asmdef — noEngineReferences: true)
│   ├── Flow/       — GameFlow, GameFlowState (bağlantı yaşam döngüsü + tip'li event pump)
│   └── Demo/       — DemoServer (süreç-içi sahte sunucu: gerçek DH2/cipher + frame sıraları)
Assets/Tests/EditMode/ (Metin2.Tests.asmdef)
    ├── Core/       — SecretRedactorTests
    ├── Network/    — TcpConnectionTests, HandshakeClientTests, AuthLoginClientTests, ChannelLoginClientTests, CharacterSelectClientTests, WorldEntryClientTests, GameWorldClientTests, InventoryClientTests, MovementClientTests
    └── Protocol/   — PacketGCHandshakeTests, PacketKeyAgreementTests, PacketCGLogin3Tests,
                       PacketGCPhaseTests, PacketReaderWriterTests, PacketFramerTests,
                       PacketRegistryTests, Dh2KeyAgreementTests, CipherSuiteTests,
                       CipherKeyDerivationTests, CipherSessionTests, TeaEngineTests,
                        Rc6EngineTests, IdeaEngineTests, Rc5EngineTests, Shacal2EngineTests, BlowfishEngineTests, TripleDesEngineTests, TwofishEngineTests, SerpentEngineTests, MarsEngineTests, Cast256EngineTests, CamelliaEngineTests, SeedEngineTests, BlockCipherEngineFactoryTests
```

## Test

```bash
# .NET CLI ile
dotnet test headless/Metin2.Tests.Headless.csproj

# Unity Editor ile
# Window > General > Test Runner > EditMode > Run All
```

**Son test sonucu: 685/685 başarılı ✅**

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
- [`docs/sprints/SPRINT_04-auth-login.md`](docs/sprints/SPRINT_04-auth-login.md) — Auth login iz sürme + PanamaKey + dersler
- [`docs/protocol/packet-catalog.json`](docs/protocol/packet-catalog.json) — 49 paket: 42 VERIFIED + 7 PARTIALLY (item aksiyonları +6)

## Bilinen Eksikler (UNVERIFIED)

- Canlı sunucuya karşı uçtan-uca handshake decode (offline loopback + kaynak-kanıt tamam; gerçek auth core final kanıtı)
- Headless test csproj'ları `headless/` altına taşındı (Unity'nin ürettiği csproj'larla isim
  çakışması çözüldü; `*.csproj`/`*.sln` hâlâ gitignore'da — taze clone için bu dosyaların
  repoya alınması ayrı bir karar)

## Sonraki Adım

**Unity önyüzü**: Bootstrap/Login scene'lerini `GameFlow` + `DemoServer`
demo-moduna bağlamak (Unity 6 LTS 6000.0.23f1 kurulumu + Android modülü
gerektirir; Gameplay assembly'si motor-bağımsız olduğu için scene öncesi tüm
akış headless test'li). Ertelenen canlı giriş denemesi paralel bir noktada
yapılacak (staging izolasyonu, DB'ye yazma yok).

## Kurallar

- Kaynak kodda doğrulanmayan bilgiler UNVERIFIED olarak işaretlenir
- Credential ve secret değerleri asla çıktıya, teste veya commit'e yazılmaz
- Sunucu otoritesi korunur — client hasar/item/para kararı alamaz
- PC istemcisiyle uyumluluk bozulmaz
