# Connection flow (source-verified)

No guesses. UNVERIFIED = not proven from source + byte test. No secrets.

## 0. Phase enum

Server `game/src/packet.h:797-813`: CLOSE, HANDSHAKE, LOGIN, SELECT,
LOADING, GAME, DEAD, CLIENT_CONNECTING, DBCLIENT, P2P, AUTH, TEEN.
Client mirror `UserInterface/Packet.h:1042-1062` (+DBCLIENT_CONNECTING).

Server pushes phase via `DESC::SetPhase` (`game/src/desc.cpp:518`):
sends `HEADER_GC_PHASE` then swaps `m_pInputProcessor`.
Client applies in `PythonNetworkStream.cpp:RecvPhasePacket`.

## 1. TCP accept -> handshake

1. `DESC::Setup` (`desc.cpp:205`): output `DEFAULT_PACKET_BUFFER_SIZE*2`
(`protocol.h:42`), input `MAX_INPUT_LEN` (`desc.h:10-12`, 65536),
`SetPhase(PHASE_HANDSHAKE)`, `StartHandshake(handshake)`.
2. `StartHandshake/SendHandshake` (`desc.cpp:615-640`):
`TPacketGCHandshake{bHeader=0xff,dwHandshake,dwTime,lDelta}`
(`packet.h:789-795`). Retry `HANDSHAKE_RETRY_LIMIT 32` (`desc.h:14`).
3. Client `HandShakePhase` (`PhaseHandShake.cpp:10-91`): handles
GC_PHASE, GC_BINDUDP, GC_HANDSHAKE, GC_PING, HYBRIDCRYPT_KEYS/SDB,
GC_KEY_AGREEMENT / _COMPLETED.
4. Legacy time sync `RecvHandshakePacket/OK` (`PhaseHandShake.cpp:120-158`).
UNVERIFIED: used after key agreement in this build, or key agreement only.

## 2. Key agreement (mandatory here)

`common/service.h:6` defines `_IMPROVED_PACKET_ENCRYPTION_`.

Server send `DESC::SendKeyAgreement` (`desc.cpp:704-722`):
`TPacketKeyAgreement{bHeader=0xfb,wAgreedLength,wDataLength,data[256]}`
(`packet.h:2226-2233`, `MAX_DATA_LEN=256` at `packet.h:2228`),
via `cipher_.Prepare`.

Server recv `CInputHandshake::Analyze` (`input.cpp:556-583`):
`SendKeyAgreementCompleted (0xfa)` + `ProcessOutput()` flush,
then `FinishHandshake(agreed,data)` -> `g_bAuthServer?PHASE_AUTH:PHASE_LOGIN`
else `PHASE_CLOSE`.

Client `RecvKeyAgreementPacket/Completed` (`PhaseHandShake.cpp:201-260`):
`Prepare` -> `Activate(agreed,peer)` -> send `HEADER_CG_KEY_AGREEMENT(0xfb)`;
on `0xfa` call `ActivateCipher()`.

Cipher: `game/src/cipher.h:12-56` + `EterBase/cipher.h`, impl
`game/src/cipher.cpp:119-308` (DH2 + CTR chooser).
Client polarity true (`EterLib/NetStream.cpp:921-924`),
server false (`desc.cpp:733-736`).
UNVERIFIED: suite id, IV derivation, test vectors, agreed-length values.

Sequence/anti-replay: no explicit sequence counter or nonce field was found in
the framing path (`input.cpp:59-130`, `CheckPacket`, `NetStream.cpp` encrypt
calls operate on raw buffer slices). Whether the chosen CTR cipher derives an
implicit counter/IV from the DH2 shared secret is UNVERIFIED until
`cipher.cpp:119-308` `SetUp()` is fully traced in the golden-test task.

## 3. Auth-core login

Handshake/key agreement done -> server `PHASE_AUTH` (`input.cpp:574`),
client `SetLoginPhase` (`PhaseLogin.cpp:73-122`).

Client sends `HEADER_CG_LOGIN3=111` (`game/src/packet.h:86`):
`TPacketCGLogin3{header,login[31],passwd[17],adwClientKey[4]}`
(`packet.h:515-523`; lengths `common/length.h:9-10`).

Server `CInputAuth::Login` (`input_auth.cpp`): require `g_bAuthServer`,
validate string, reject duplicate, `CreateLoginKey`, `dwPanamaKey = key XOR ckeys`,
`DBManager::ReturnQuery(QID_AUTH_LOGIN, SELECT ... FROM account ...)`.
Reply path via `input_db.cpp`. UNVERIFIED: exact GC reply ids for auth path.

Client login RX (`PhaseLogin.cpp:11-71`): GC_LOGIN_SUCCESS3/4,
GC_LOGIN_FAILURE, GC_EMPIRE, GC_LOGIN_KEY, GC_PING, HYBRIDCRYPT.

## 4. Channel-core login

New TCP to channel core -> server `PHASE_LOGIN` (`input.cpp:576`).

A. `SendLoginPacket` (`PhaseLogin.cpp:234-252`): `HEADER_CG_LOGIN=1`,
`TPacketCGLogin{header,name,pwd}` (`packet.h:501-506`) ->
`CInputLogin::Login` (`input_login.cpp:86`) -> `HEADER_GD_LOGIN` to DB.

B. `SendLoginPacketNew` (`PhaseLogin.cpp:254-280`): `HEADER_CG_LOGIN2=109`,
`TPacketCGLogin2{header,name,dwLoginKey,adwClientKey[4]}` (`packet.h:508-514`)
-> `CInputLogin::LoginByKey` (`input_login.cpp:138`) -> `HEADER_GD_LOGIN_BY_KEY`.

Legacy TEA re-arm in `SetSelectPhase` only when improved encryption is OFF
(`PhaseSelect.cpp:20-22`); here it is ON (`service.h:6`). Verified.

## 5. Select -> loading -> game

Dispatch `CInputLogin::Analyze` (`input_login.cpp:994-1096`):
SELECT/CREATE/DELETE, ENTERGAME, EMPIRE, marks, CLIENT_VERSION, XTRAP_ACK.

- `CharacterSelect` (`input_login.cpp:222`): needs `TAccountTable`,
checks `PLAYER_PER_ACCOUNT`, sends `HEADER_GD_PLAYER_LOAD`.
- `Entergame` (`input_login.cpp:546`): needs character, `Show()`,
`SetPhase(PHASE_GAME)`, then `HEADER_GC_TIME`, `HEADER_GC_CHANNEL`, greet.
- Client `ConnectGameServer(slot)` (`PythonNetworkStream.cpp`):
uses `m_akSimplePlayerInfo[slot].lAddr/wPort`. UNVERIFIED: byte order/offsets.
- Loading (`PhaseLoading.cpp`): `GC_MAIN_CHARACTER*`, points/item/quickslot,
default -> `GamePhase()`; `SendEnterGame` sends `HEADER_CG_ENTERGAME=10`
with client struct `TPacketCGEnterFrontGame` (`client Packet.h:564-567`),
server counterpart `TPacketCGEnterGame` (`server packet.h:627-630`).

## 6. Framing

Server RX `CInputProcessor::Process` (`input.cpp:59-130`): 1-byte header,
`0x00` padding consumed as len 1, else `CPacketInfoCG::Get`, unknown ->
`PHASE_CLOSE`, short buffer waits, `Analyze()` may add extra bytes,
phase change re-loops (returns false -> `ProcessInput` loops).

Server TX `DESC::Packet` (`desc.cpp:409`) via `packet_encode`
(`protocol.h:45-59`); ping `ping_event`; phase `SetPhase`.

Client RX `CheckPacket` (`PythonNetworkStream.cpp`): Peek(1), skip 0x00,
`CMainPacketHeaderMap` lookup, static=`Peek(fixed)`,
dynamic=`Peek(TDynamicSizePacketHeader)` then `Peek(size)`
(`Packet.h:1087-1094`). Unknown -> clear buffer + quit.

Socket `EterLib/NetStream.{h,cpp}`: non-blocking TCP, cipher only when
`m_cipher.activated()`. UNVERIFIED: keepalive, ping period, 0x00 after crypt.
