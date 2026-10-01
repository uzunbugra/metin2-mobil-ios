# Sprint 4 — Auth Login (kayıt)

Hedef: handshake sonrası auth-core login akışının istemci tarafı
(CG_LOGIN3 gönderimi → 150/7 karşılama), kaynak-doğrulanmış + loopback kanıtlı.

## Kaynak iz sürme (tamamlandı)

| Yön | Paket | Kaynak |
|---|---|---|
| C→S 111 (65B) | `TPacketCGLogin3` | Server `packet.h:516-522` + `length.h:9-10`; client `Packet.h:511-517` + `AccountConnector.cpp:166-212` (PHASE_AUTH'ta gönderir, key=`g_adwEncryptKey[4]`) |
| S→C 150 (6B) | `TPacketGCAuthSuccess` | Server `packet.h:849-854` + `input_db.cpp:1679-1712`; client `Packet.h:2370-2375` + `AccountConnector.cpp:312-337` (bResult==0 → "BESAMEKEY", yoksa PanamaKey + channel connect) |
| S→C 7 (10B) | `TPacketGCLoginFailure` | Server `packet.h:856-860` + `input.cpp:177-188` + `length.h:12`; client `Packet.h:1136-1141` + `PhaseLogin.cpp:204-212` + `AccountConnector.cpp:340-352` |
| PanamaKey | `key ^ k0^k1^k2^k3` | Server `input_auth.cpp:151` == client `AccountConnector.cpp:325` |

## Implementasyon

| İş | Dosya | Test |
|---|---|---|
| 150 paketi + codec (6B) | `Protocol/Packets/PacketGCAuthSuccess.cs`, `Codecs/PacketGCAuthSuccessCodec.cs` | `PacketGCAuthSuccessTests` (9: golden, zero-result, truncation, header) |
| 7 paketi + codec (10B) | `Protocol/Packets/PacketGCLoginFailure.cs`, `Codecs/PacketGCLoginFailureCodec.cs` | `PacketGCLoginFailureTests` (13: 4 golden status, round-trip, trunc-safe, header) |
| Framer uzunlukları | `Framing/PacketLengthTable.cs` (+150→6, +7→10) | `PacketFramerTests.AuthReplies_*` |
| Phase-aware registry | `Registry/PacketRegistry.cs` (`CreateAuthRegistry`: 150 Auth-only, 7 Auth+Login) | `PacketRegistryTests` (+3) |
| Login orkestrasyonu | `Network/Session/AuthLoginClient.cs` (send/recv/PanamaKey/keygen) | `AuthLoginClientTests` (9 loopback) |

## Uygulama dersleri

- **Wire buffer 8+null'dur, 9-char status asla gelmez**: `RoundTrip("BESAMEKEY")`
  testi patladı (9 char > 8+null) — test yanlıştı, engine/codec doğruydu.
  "BESAMEKEY" yalnızca 150/bResult==0 yolunun istemci-içi sentetik status'üdür
  (`AccountConnector.cpp:321`), 10-byte paket olarak telde taşınmaz. Ders:
  status sabitleri telden değil, her iki taraftaki struct'tan okunur.
- **Client `__AnalyzePacket` boyutları aldatıcıdır**: `AccountConnector.cpp:132`
  7-header'ını `sizeof(TPacketGCAuthSuccess)` ile bind eder, ama gerçek `Recv`
  `sizeof(TPacketGCLoginFailure)` kullanır (satır 343). Wire kanıtı struct
  boyutlarıdır (6 vs 10), dispatch peek-size değil.
- **Şifre test'e yazılmaz**: `SendLogin_EmptyCredentials` testi, exception
  mesajlarının parolayı içermediğini assert'ler (guide §6.1).

## Sıradaki (Sprint 4 devam) — güncelleme: adım 2 tamamlandı

- ~~Channel-core login: CG_LOGIN (1) / CG_LOGIN2 (109) + GC_LOGIN_KEY (118) akışı.~~ ✅
  CG_LOGIN2 (109) + empire (90) + character list (32) tamam (aşağıda).
  CG_LOGIN (1, legacy parola) ve GC_LOGIN_KEY (118, bu build'de ölü — gönderen yok) açıkta.
- Select fazı: select/create/delete paketleri, ENTERGAME (10).

## Adım 2 — Channel login + character list (tamamlandı)

İz sürme: `LoginByKey` (`input_login.cpp:138-190`) → `GD_LOGIN_BY_KEY` →
`LoginSuccess` (`input_db.cpp:106-175`: status "OK" şart, empire, PHASE_SELECT) →
`SendLoginSuccessPacket` (`desc.cpp:892-919`, header 32 NEWSLOT).
İstemci: `SendLoginPacketNew` (`PhaseLogin.cpp:254-280`) → empire
(`PhaseLogin.cpp:124-132`) → slotlar (`PhaseLogin.cpp:162-188`).

| İş | Dosya | Test |
|---|---|---|
| CG_LOGIN2 (109, 52B) + codec | `Packets/PacketCGLogin2.cs`, `Codecs/PacketCGLogin2Codec.cs` | `PacketCGLogin2Tests` (6) |
| GC_EMPIRE (90, 2B) + codec | `Packets/PacketGCEmpire.cs`, `Codecs/PacketGCEmpireCodec.cs` | `PacketGCEmpireTests` (7) |
| TSimplePlayer (63B) + codec | `Packets/SimplePlayer.cs`, `Codecs/SimplePlayerCodec.cs` | `SimplePlayerCodecTests` (5) |
| LoginSuccess (32, 329B) + codec | `Packets/PacketGCLoginSuccess.cs`, `Codecs/PacketGCLoginSuccessCodec.cs` | `PacketGCLoginSuccessTests` (6) |
| Framer + registry | `PacketLengthTable` (+90→2, +32→329), `CreateChannelRegistry` (90 Login-only, 32 Select-only) | framer +1, registry +3 |
| Kanal orkestrasyonu | `Network/Session/ChannelLoginClient.cs` (login2 → empire → slotlar; `GetSlotEndpoint`) | `ChannelLoginClientTests` (7 loopback) |

Dersler:
- **IPAddress(long) bayt takası yapmaz**: `new IPAddress(0x7F000001)` =
  "1.0.0.127" verir. Wire'daki `inet_addr` çıktısı LE-okunmuş word'dür
  (127.0.0.1 → 0x0100007F); octet'ler LSB-first genişletilir. İlk denemede
  ters yazıldı, loopback testi yakaladı.
- **`GC_LOGIN_KEY` (118) bu build'de ölüdür**: game src'de gönderen yok
  (istemci handle eder ama asla gelmez) — implemente edilmedi, katalogda notlu.
- **Null vs boş slot adı**: boş slot serialize'da sıfırlanır, deserialize'da
  `""` döner; `Equals` null/"" denkliğini kabul eder.
- Test: 362 → 397 (+35).

## Adım 3 — Select fazı: select/create/delete + ENTERGAME (tamamlandı)

İz sürme: select yanıtsızdır (`CharacterSelect`, `input_login.cpp:222-255` →
sadece DB'ye `GD_PLAYER_LOAD`); empire seçimi `Empire()` (`input_login.cpp:792-822`,
≥4 → CLOSE); create kuralları `CharacterCreate` (`input_login.cpp:416-499`:
ad > 12 → 9/t0, bozuk ad/şekil → 9/t1 Canada yoksa 9/t0, bozuk job → 9/t0);
yanıtlar `PlayerCreateSuccess` (8, `input_db.cpp:221-227`),
`PlayerDeleteSuccess` (10+index, `input_db.cpp:285-286`),
`PlayerDeleteFail` (çıplak 11, `input_db.cpp:296`).
İstemci dispatch aynası: `PhaseSelect.cpp:41-140` + sends `145-231` +
sonuçlar `233-286`. SELECT/LOGIN aynı input processor'ı paylaşır
(`desc.cpp:539-547`).

| İş | Dosya | Test |
|---|---|---|
| CG select (6, 2B) / create (4, 34B) / delete (5, 10B) / entergame (10, 1B) + codec'ler | `Packets/PacketCGCharacter{Select,Create,Delete}.cs`, `PacketCGEnterGame.cs` + 4 codec | 4 fixture (22 test) |
| GC create-ok (8, 65B) / create-fail (9, 2B) / delete-ok (10, 2B) / delete-fail (11, 1B) + codec'ler | 4 paket + 4 codec | 4 fixture (22 test) |
| Framer + registry | `PacketLengthTable` (+8→65, +9→2, +10→2, +11→1), `CreateSelectRegistry` (8/9/10/11 Select-only) | framer +1, registry +2 |
| Select orkestrasyonu | `Network/Session/CharacterSelectClient.cs` | `CharacterSelectClientTests` (6 loopback) |

Dersler:
- **Select'in yanıtı yoktur**: `CharacterSelect` yalnızca DB'ye yazar; istemci
  sonraki adıma (ENTERGAME) kendi kararıyla geçer. `Await*` yalnızca
  create/delete içindir — API bunu tiplerle dayatır.
- **9-header'ının 1-byte quirk'i** (`input_db.cpp:199`): framer 9→2 bekler,
  C++ istemci de `Recv(2)`'de takılır — parite korunur, workaround yok.
- **11'in adı yanıltır**: tetikleyici boş slottur (`input_login.cpp:520-525`),
  yalnızca social-id uyuşmazlığı değil.
- Test: 397 → 450 (+53).

## Sıradaki (Sprint 4 devam)

- Loading/world entry: ENTERGAME sonrası GC_TIME/GC_CHANNEL/greet + spawn
  paketleri (`input_login.cpp:546-579` devamı), entity spawn/despawn iz sürme.
