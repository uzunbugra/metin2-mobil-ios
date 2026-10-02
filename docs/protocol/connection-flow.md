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

Client sends `HEADER_CG_LOGIN3=111` (`game/src/packet.h:86`) on
`GC_PHASE(PHASE_AUTH)` (`AccountConnector.cpp:166-212`, adwClientKey =
`g_adwEncryptKey[4]`):
`TPacketCGLogin3{header,login[31],passwd[17],adwClientKey[4]}`
(`packet.h:515-523`; lengths `common/length.h:9-10`; client mirror
`UserInterface/Packet.h:511-517`, `ID_MAX_NUM=30`/`PASS_MAX_NUM=16`).
C# codec + 65B golden tests: `PacketCGLogin3Codec`, `PacketCGLogin3Tests`.

Server `CInputAuth::Login` (`input_auth.cpp:102-200`): require `g_bAuthServer`,
trim+lower login, validate string (`NOID` on bad chars), reject `SHUTDOWN`
/ duplicate `ALREADY`, then `CreateLoginKey`, `dwPanamaKey = key XOR ckeys[4]`
(`input_auth.cpp:151`), `DBManager::ReturnQuery(QID_AUTH_LOGIN, SELECT ... FROM account ...)`.
Reply path via `input_db.cpp:1679-1712` — VERIFIED, no longer UNVERIFIED:
- Success: `HEADER_GC_AUTH_SUCCESS=150` (`packet.h:266`),
  `TPacketGCAuthSuccess{bHeader,dwLoginKey,bResult}` (`packet.h:849-854`,
  1+4+1 = 6B; client mirror `UserInterface/Packet.h:2370-2375`).
  Client `AccountConnector.cpp:312-337`: bResult==0 → `OnLoginFailure("BESAMEKEY")`;
  else PanamaKey = key ^ clientKeys[4] (same formula as server),
  `DecryptPackIV`, `SetLoginKey`, connect to channel, disconnect auth.
- Failure: `HEADER_GC_LOGIN_FAILURE=7` (`packet.h:126`),
  `TPacketGCLoginFailure{header,szStatus[9]}` (`packet.h:856-860`,
  `ACCOUNT_STATUS_MAX_LEN=8` in `common/length.h:12`; client mirror
  `LOGIN_STATUS_MAX_LEN=8`, `UserInterface/Packet.h:1136-1141`).
  Shared helper `LoginFailure()` (`input.cpp:177-188`, `strlcpy` truncation-safe);
  observed statuses: `NOID`, `ALREADY`, `WRONGPWD`, `SHUTDOWN` (+ DB status
  passthrough `input_db.cpp:142`). Client `PhaseLogin.cpp:204-212` and
  `AccountConnector.cpp:340-352` surface `szStatus` to the login UI.
- Wire rule: both replies travel ENCRYPTED (post-activation stream cipher,
  `desc.cpp:460-461` / `NetStream.cpp:109`); max status is 8 chars + null
  (a 9-char status can never arrive — proven by a failing-then-fixed test).

Client login RX (`PhaseLogin.cpp:11-71`): GC_LOGIN_SUCCESS3/4,
GC_LOGIN_FAILURE, GC_EMPIRE, GC_LOGIN_KEY, GC_PING, HYBRIDCRYPT.

## 4. Channel-core login

New TCP to channel core -> server `PHASE_LOGIN` (`input.cpp:576`).

A. `SendLoginPacket` (`PhaseLogin.cpp:234-252`): `HEADER_CG_LOGIN=1`,
`TPacketCGLogin{header,name,pwd}` (`packet.h:501-506`) ->
`CInputLogin::Login` (`input_login.cpp:86`) -> `HEADER_GD_LOGIN` to DB.

B. `SendLoginPacketNew` (`PhaseLogin.cpp:254-280`): `HEADER_CG_LOGIN2=109`,
`TPacketCGLogin2{header,name[31],dwLoginKey,adwClientKey[4]}` (`packet.h:508-514`,
1+31+4+16 = 52B; client mirror `Packet.h:503-509`)
-> `CInputLogin::LoginByKey` (`input_login.cpp:138-190`: trim+lower, SHUTDOWN/FULL
guards, `SetLoginKey`, `HEADER_GD_LOGIN_BY_KEY` to DB).
C# codec + 52B golden tests: `PacketCGLogin2Codec`, `PacketCGLogin2Tests`.

DB `HEADER_DG_LOGIN_SUCCESS` -> `CInputDB::LoginSuccess` (`input_db.cpp:106-175`)
— VERIFIED: status column must be "OK" else `LoginFailure(d, status)` (7);
else `GC_EMPIRE` (`TPacketGCEmpire{bHeader=90,bEmpire}`, `packet.h:1638-1642`, 2B;
client mirror `Packet.h:2083-2087`, handled `PhaseLogin.cpp:124-132`), then
`SetPhase(PHASE_SELECT)` + `SendLoginSuccessPacket` (`desc.cpp:892-919`):
`HEADER_GC_LOGIN_SUCCESS_NEWSLOT=32` (`packet.h:125`),
`TPacketGCLoginSuccess{bHeader,players[4],guild_id[4],guild_name[4][13],handle,random_key}`
(`packet.h:838-847`, 1+63*4+16+52+4+4 = 329B; client `TPacketGCLoginSuccess4`
`Packet.h:1125-1133`, handled `PhaseLogin.cpp:162-188`).
`TSimplePlayer` (63B, pack(1)) is field-identical both sides
(`tables.h:275-291` vs `Packet.h:1096-1113`; name len 24 both: server
`length.h:13`, client `StdAfx.h:43`).
Slot endpoint — VERIFIED: `lAddr` holds `inet_addr()` output
(`map_location.cpp:47`, network order) and the client passes it verbatim to
`Connect()` (`PythonNetworkStream.cpp:471-472`); `wPort` travels host order.
NOTE: `HEADER_GC_LOGIN_KEY` (118) has NO sender in game src — dead in this
build (client still handles it, `PhaseLogin.cpp:282-289`); not implemented.

Legacy TEA re-arm in `SetSelectPhase` only when improved encryption is OFF
(`PhaseSelect.cpp:20-22`); here it is ON (`service.h:6`). Verified.

## 5. Select -> loading -> game

Dispatch `CInputLogin::Analyze` (`input_login.cpp:994-1096`, SELECT and LOGIN
share `m_inputLogin`, `desc.cpp:539-547`); client dispatch mirror
`PhaseSelect.cpp:41-140`.

- Select: C→S `HEADER_CG_CHARACTER_SELECT=6` (`packet.h:16`),
  `{header,index}` 2B (`packet.h:530-534`; client `Packet.h:529-533`, sent
  `PhaseSelect.cpp:161-175`) → `CharacterSelect` (`input_login.cpp:222-255`:
  account/index guards, then `HEADER_GD_PLAYER_LOAD` to DB). NO direct reply.
- Empire (empire-less accounts): C→S `HEADER_CG_EMPIRE=90`
  (`TPacketCGEmpire{bHeader,bEmpire}`, client `Packet.h:2075-2081`) →
  `Empire()` (`input_login.cpp:792-822`: ≥ EMPIRE_MAX_NUM=4 (`length.h:19`) →
  CLOSE; else `GD_EMPIRE_SELECT` to DB).
- Create: C→S `HEADER_CG_CHARACTER_CREATE=4` (`packet.h:14`),
  `{header,index,name[25],job u16,shape,con,int,str,dex}` 34B
  (`packet.h:543-554`; client `Packet.h:1143-1154`, sent `PhaseSelect.cpp:194-215`)
  → `CharacterCreate` (`input_login.cpp:416-499`: name strlen > 12 → 9/t0;
  bad name/shape → 9/t1 Canada else 9/t0; bad job → 9/t0; else DB).
  Replies: 8 `{header,slot,TSimplePlayer}` 65B (`packet.h:556-561`, sent
  `input_db.cpp:221-227`; client `Packet.h:1156-1161`, `PhaseSelect.cpp:233-249`)
  or 9 `{header,bType}` 2B (`packet.h:862-866`; client `Packet.h:1163-1167`,
  `PhaseSelect.cpp:252-262`). QUIRK: `input_db.cpp:199` sends a bare 1-byte 9
  on one path — our framer and the C++ client stall on it identically.
- Delete: C→S `HEADER_CG_CHARACTER_DELETE=5` (`packet.h:15`),
  `{header,index,private_code[8]}` 10B (`packet.h:536-541`; client
  `Packet.h:1169-1174` with `PRIVATE_CODE_LENGTH=8` (`Packet.h:378`), sent
  `PhaseSelect.cpp:177-192`) → `CharacterDelete` (`input_login.cpp:501-535`:
  no-account/overflow → silent; empty slot → bare 1-byte 11; else DB).
  Replies: 10 + index 2B (`input_db.cpp:285-286`; client `Packet.h:1176-1180`,
  `PhaseSelect.cpp:264-276`) or 11 bare 1B (`input_db.cpp:296`; client 1-byte
  `TPacketGCBlank`, `PhaseSelect.cpp:278-286`).
- `CharacterSelect` (`input_login.cpp:222`): needs `TAccountTable`,
  checks `PLAYER_PER_ACCOUNT`, sends `HEADER_GD_PLAYER_LOAD`.
- Select → DB `PlayerLoad` (`input_db.cpp:385-459`): binds character,
  `SetPhase(PHASE_LOADING)`, sends `GC_MAIN_CHARACTER2` (113, 46B empire layout
  — `char.cpp:1543-1553`; BGM maps use 137/138 variants instead, out of scope),
  then points/skill/quickslot/item bundle (loading bundle — next step), and the
  client answers ENTERGAME from `PhaseLoading`.
- `Entergame` (`input_login.cpp:546`): needs character, `Show()`,
  `SetPhase(PHASE_GAME)`, then `HEADER_GC_TIME` (106, 5B: `time_t` is 4B BOTH
  sides — 32-bit server, client `_USE_32BIT_TIME_T` in `StdAfx.h:14`),
  `HEADER_GC_CHANNEL` (121, 2B, `g_bChannel`), greet.
  C# mirror: `WorldEntryClient` (113 Loading → 10 → 106+121 Game).
- Client `ConnectGameServer(slot)` (`PythonNetworkStream.cpp:462-472`):
  uses `m_akSimplePlayerInfo[slot].lAddr/wPort` — VERIFIED (see §4 lAddr note).
  C# mirror: `ChannelLoginClient.GetSlotEndpoint` (slot 0..3, empty-slot guard).
- Loading (`PhaseLoading.cpp`): `GC_MAIN_CHARACTER*`, points/item/quickslot,
  default -> `GamePhase()`; `SendEnterGame` sends `HEADER_CG_ENTERGAME=10`
  with client struct `TPacketCGEnterFrontGame` (`client Packet.h:564-567`),
  server counterpart `TPacketCGEnterGame` (`server packet.h:627-630`, 1-byte
  header; no loaded character → PHASE_CLOSE, `input_login.cpp:550-554`).

## 6. Movement (Game phase, server-authoritative)

Client intent: `SendCharacterStatePacket` (`PhaseGame.cpp:1107-1145`):
`HEADER_CG_MOVE=7` (`packet.h:17`; client name `HEADER_CG_CHARACTER_MOVE`,
`Packet.h:18`), `{bFunc,bArg,bRot=degrees/5,lX,lY cm,dwTime server-ms}`
(`packet.h:586-595`, 16B; client `Packet.h:695-704`).
Server `CInputMain::Move` (`input_main.cpp:1514-1688`): func validity,
teleport check (>25m walk / 40m ride → HackLog + reshow + stop), speedhack
timing (30s slow / negative-delta disconnect), combo-hack; FUNC_MOVE → Goto
with rotation `bRot*5`; then `GC_MOVE` rebroadcast to viewers ONLY
(`PacketAround` excludes self, `input_main.cpp:1651-1663`).

`TPacketGCMove` (`packet.h:1288-1299`, 24B; client `Packet.h:1888-1899`):
`{bFunc,bArg,bRot,dwVID,lX,lY,dwTime,dwDuration}` (duration = travel time on
FUNC_MOVE, else 0).

Sync positions: client batches visible actors per frame
(`PhaseGame.cpp:2697-2714`, from `PlayerEventHandler.cpp:214`):
`HEADER_CG_SYNC_POSITION=8` (`packet.h:18`), `{wSize + N×{vid,x,y}}`
(`packet.h:597-609`; client `Packet.h:706-717`).
Server `SyncPosition` (`input_main.cpp:1782-1900+`): wSize short → CLOSE,
misaligned → error, count clamped to 16, per-victim sync-owner + 3500cm
distance rules (repeated violation → CLOSE); rebroadcasts GC batch
(`packet.h:1310-1322`, header 5).
C# mirror: `MovementClient` (quantized rotation, fail-closed func/coords/
count) + dynamic wSize framing in `PacketFramer` (same guards as server).

## 7. Framing

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
`m_cipher.activated()`. Keepalive VERIFIED: `ping_event` is created in the DESC
constructor (`desc.cpp:227-233`) and fires in every phase (plaintext handshake
included); the client answers each 1-byte `TPacketGCPing` (44) with a 1-byte
`TPacketCGPong` (0xfe) via `RecvPingPacket` (`PythonNetworkStream.cpp:636-656`,
called in all five phase loops), and the server closes the session on the next
cycle without a pong (`desc.cpp:174-180`). Cycle default 60 s
(`ping_event_second_cycle = passes_per_sec * 60`, `config.cpp:32`; TOKEN
override `config.cpp:764-768`). UNVERIFIED: 0x00 after crypt.
