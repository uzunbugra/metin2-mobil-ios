# Protocol inventory (source-verified)

Sources: `source/Razuning-V5/Server/game/src/packet.h`,
`Server/common/tables.h`, `Server/game/src/packet_info.cpp`,
`source/Client Source/source/UserInterface/Packet.h`,
`PythonNetworkStream.cpp` (CMainPacketHeaderMap), `EterLib/NetStream.cpp`.

Rules: no invented IDs/sizes. Everything not proven here = UNVERIFIED.
All IDs below are quoted from the C++ enums with file+line references.

## 1. Framing rules

- 1 byte header at offset 0; `0x00` = padding, consumed 1 byte
  (`input.cpp:78-79`; client mirrors in `CheckPacket`).
- Server knows lengths from `CPacketInfoCG` (`packet_info.cpp`, ctor).
  Unknown header -> `PHASE_CLOSE` (`input.cpp:80-87`).
- Client knows lengths from `CMainPacketHeaderMap` (`PythonNetworkStream.cpp`):
  static = fixed size; dynamic = `{header, WORD size}` then payload
  (`TDynamicSizePacketHeader`, client `Packet.h:1087-1094`).
- Server->client dynamic packets use explicit `size` fields inside structs
  (e.g. `TPacketGCChat.size`, `TPacketGCDuelStart.wSize`), NOT the client
  `TDynamicSizePacketHeader`; client peeks the struct's own size field.
  UNVERIFIED: exact byte offsets of each `size` field until golden tests.
- Server max input buffer `MAX_INPUT_LEN 65536` (`desc.h:10-12`);
  output `DEFAULT_PACKET_BUFFER_SIZE*2 = 65536` (`protocol.h:42`).

## 2. CG headers (client -> server), from `Server/game/src/packet.h:6-108`

| Value | Name | Note |
|---|---|---|
| 0xff | HEADER_CG_HANDSHAKE | `TPacketCGHandshake` |
| 0xfe | HEADER_CG_PONG | size `sizeof(BYTE)` (packet_info.cpp) |
| 0xfc | HEADER_CG_TIME_SYNC | `TPacketCGHandshake` struct ==`sizeof` |
| 0xfb | HEADER_CG_KEY_AGREEMENT | `TPacketKeyAgreement` |
| 0xfd | HEADER_CG_CLIENT_VERSION | `TPacketCGClientVersion` |
| 0xf1 | HEADER_CG_CLIENT_VERSION2 | `TPacketCGClientVersion2` |
| 1 | HEADER_CG_LOGIN | `TPacketCGLogin` |
| 2 | HEADER_CG_ATTACK | `TPacketCGAttack` |
| 3 | HEADER_CG_CHAT | `TPacketCGChat` |
| 4 | HEADER_CG_CHARACTER_CREATE | `TPacketCGPlayerCreate` |
| 5 | HEADER_CG_CHARACTER_DELETE | `TPacketCGPlayerDelete` |
| 6 | HEADER_CG_CHARACTER_SELECT | `TPacketCGPlayerSelect` |
| 7 | HEADER_CG_MOVE | `TPacketCGMove` |
| 8 | HEADER_CG_SYNC_POSITION | `TPacketCGSyncPosition` |
| 10 | HEADER_CG_ENTERGAME | `TPacketCGEnterGame` |
| 11 | HEADER_CG_ITEM_USE | `TPacketCGItemUse` |
| 12 | HEADER_CG_ITEM_DROP | `TPacketCGItemDrop` |
| 13 | HEADER_CG_ITEM_MOVE | `TPacketCGItemMove` |
| 15 | HEADER_CG_ITEM_PICKUP | `TPacketCGItemPickup` |
| 16/17/18 | QUICKSLOT_ADD/DEL/SWAP | `TPacketCGQuickslot*` |
| 19 | HEADER_CG_WHISPER | `TPacketCGWhisper` |
| 20 | HEADER_CG_ITEM_DROP2 | `TPacketCGItemDrop2` |
| 26 | HEADER_CG_ON_CLICK | `TPacketCGOnClick` |
| 27 | HEADER_CG_EXCHANGE | `TPacketCGExchange` |
| 28 | HEADER_CG_CHARACTER_POSITION | `TPacketCGPosition` |
| 29 | HEADER_CG_SCRIPT_ANSWER | `TPacketCGScriptAnswer` |
| 30 | HEADER_CG_QUEST_INPUT_STRING | `TPacketCGQuestInputString` |
| 31 | HEADER_CG_QUEST_CONFIRM | `TPacketCGQuestConfirm` |
| 50 | HEADER_CG_SHOP | `TPacketCGShop` |
| 51 | HEADER_CG_FLY_TARGETING | `TPacketCGFlyTargeting` |
| 52 | HEADER_CG_USE_SKILL | `TPacketCGUseSkill` |
| 53 | HEADER_CG_ADD_FLY_TARGETING | `TPacketCGFlyTargeting` (same struct) |
| 54 | HEADER_CG_SHOOT | `TPacketCGShoot` |
| 55 | HEADER_CG_MYSHOP | `TPacketCGMyShop` |
| 60 | HEADER_CG_ITEM_USE_TO_ITEM | `TPacketCGItemUseToItem` |
| 61 | HEADER_CG_TARGET | `TPacketCGTarget` |
| 64 | HEADER_CG_TEXT | `TPacketCGText` (socket text/admin) |
| 65 | HEADER_CG_WARP | `TPacketCGWarp` |
| 66 | HEADER_CG_SCRIPT_BUTTON | `TPacketCGScriptButton` |
| 67 | HEADER_CG_MESSENGER | `TPacketCGMessenger` |
| 69-71 | MALL_CHECKOUT / SAFEBOX_CHECKIN+OUT | `TPacketCGSafeboxCheckout` used twice |
| 72-78 | PARTY_* | `TPacketCGParty*` |
| 77 | HEADER_CG_SAFEBOX_ITEM_MOVE | `TPacketCGItemMove` |
| 80 | HEADER_CG_GUILD | `TPacketCGGuild{header,subheader}` |
| 81 | HEADER_CG_ANSWER_MAKE_GUILD | `TPacketCGAnswerMakeGuild` |
| 82 | HEADER_CG_FISHING | `TPacketCGFishing{header,dir}` |
| 83 | HEADER_CG_ITEM_GIVE | `TPacketCGGiveItem` |
| 90 | HEADER_CG_EMPIRE | `TPacketCGEmpire` |
| 96 | HEADER_CG_REFINE | `TPacketCGRefine` |
| 100-104 | MARK_LOGIN/CRCLIST/UPLOAD/IDXLIST | `TPacketCGMark*` |
| 105 | HEADER_CG_HACK | `TPacketCGHack` |
| 106 | HEADER_CG_CHANGE_NAME | `TPacketCGChangeName` |
| 109 | HEADER_CG_LOGIN2 | `TPacketCGLogin2` |
| 110 | HEADER_CG_DUNGEON | (dispatch: input_login) |
| 111 | HEADER_CG_LOGIN3 | `TPacketCGLogin3` (auth core) |
| 112/113 | GUILD_SYMBOL_UPLOAD / SYMBOL_CRC | var-len upload |
| 114 | HEADER_CG_SCRIPT_SELECT_ITEM | `TPacketCGScriptSelectItem` |
| 204 | HEADER_CG_XTRAP_ACK | `TPacketXTrapCSVerify` |
| 205 | HEADER_CG_DRAGON_SOUL_REFINE | `TPacketCGDragonSoulRefine` |
| 206 | HEADER_CG_STATE_CHECKER | size `sizeof(BYTE)` |

UNVERIFIED: numeric IDs listed as ranges (69-78, 72-78) not individually
re-read; verify per header before implementing those exact packets.

## 3. GC headers (server -> client), from `Server/game/src/packet.h:111-284`

| Value | Name |
|---|---|
| 0xfa | HEADER_GC_KEY_AGREEMENT_COMPLETED |
| 0xfb | HEADER_GC_KEY_AGREEMENT |
| 0xfc | HEADER_GC_TIME_SYNC |
| 0xfd | HEADER_GC_PHASE |
| 0xfe | HEADER_GC_BINDUDP |
| 0xff | HEADER_GC_HANDSHAKE |
| 1 | HEADER_GC_CHARACTER_ADD |
| 2 | HEADER_GC_CHARACTER_DEL |
| 3 | HEADER_GC_MOVE |
| 4 | HEADER_GC_CHAT |
| 5 | HEADER_GC_SYNC_POSITION |
| 6 | HEADER_GC_LOGIN_SUCCESS (= client LOGIN_SUCCESS3) |
| 7 | HEADER_GC_LOGIN_FAILURE |
| 8 | HEADER_GC_CHARACTER_CREATE_SUCCESS (= client PLAYER_CREATE_SUCCESS) |
| 9 | HEADER_GC_CHARACTER_CREATE_FAILURE |
| 10 | HEADER_GC_CHARACTER_DELETE_SUCCESS |
| 11 | HEADER_GC_CHARACTER_DELETE_WRONG_SOCIAL_ID |
| 12 | HEADER_GC_ATTACK |
| 13 | HEADER_GC_STUN |
| 14 | HEADER_GC_DEAD |
| 15 | HEADER_GC_MAIN_CHARACTER_OLD |
| 16 | HEADER_GC_CHARACTER_POINTS |
| 17 | HEADER_GC_CHARACTER_POINT_CHANGE |
| 18 | HEADER_GC_CHANGE_SPEED |
| 19 | HEADER_GC_CHARACTER_UPDATE |
| 20 | HEADER_GC_ITEM_DEL |
| 21 | HEADER_GC_ITEM_SET |
| 22 | HEADER_GC_ITEM_USE |
| 23 | HEADER_GC_ITEM_DROP |
| 24 | HEADER_GC_CHARACTER_UPDATE_NEW |
| 25 | HEADER_GC_ITEM_UPDATE |
| 26 | HEADER_GC_ITEM_GROUND_ADD |
| 27 | HEADER_GC_ITEM_GROUND_DEL |
| 28-30 | QUICKSLOT_ADD / DEL / SWAP |
| 31 | HEADER_GC_ITEM_OWNERSHIP |
| 32 | HEADER_GC_LOGIN_SUCCESS_NEWSLOT (= client LOGIN_SUCCESS4) |
| 34 | HEADER_GC_WHISPER |
| 35 | HEADER_GC_ALERT (client only name, same value) |
| 36 | HEADER_GC_MOTION |
| 37 | HEADER_GC_PARTS |
| 38 | HEADER_GC_SHOP |
| 39 | HEADER_GC_SHOP_SIGN |
| 40 | HEADER_GC_DUEL_START |
| 41 | HEADER_GC_PVP |
| 42 | HEADER_GC_EXCHANGE |
| 43 | HEADER_GC_CHARACTER_POSITION |
| 44 | HEADER_GC_PING |
| 45 | HEADER_GC_SCRIPT |
| 46 | HEADER_GC_QUEST_CONFIRM |
| 61 | HEADER_GC_MOUNT |
| 62 | HEADER_GC_OWNERSHIP |
| 63 | HEADER_GC_TARGET |
| 65 | HEADER_GC_WARP |
| 69-71 | ADD_FLY_TARGETING / CREATE_FLY / FLY_TARGETING |
| 72/76 | SKILL_LEVEL_OLD / SKILL_LEVEL |
| 74 | HEADER_GC_MESSENGER |
| 75 | HEADER_GC_GUILD |
| 77-80 | PARTY_INVITE / ADD / UPDATE / REMOVE |
| 81 | HEADER_GC_QUEST_INFO |
| 82 | HEADER_GC_REQUEST_MAKE_GUILD |
| 83 | HEADER_GC_PARTY_PARAMETER |
| 85-88 | SAFEBOX_SET / DEL / WRONG_PASSWORD / SIZE |
| 89 | HEADER_GC_FISHING |
| 90 | HEADER_GC_EMPIRE |
| 91/92 | PARTY_LINK / UNLINK |
| 95 | HEADER_GC_REFINE_INFORMATION_OLD |
| 96-98 | OBSERVER_ADD/REMOVE/MOVE (client names, same values) |
| 99 | HEADER_GC_VIEW_EQUIP |
| 100 | HEADER_GC_MARK_BLOCK |
| 102 | HEADER_GC_MARK_IDXLIST |
| 106 | HEADER_GC_TIME |
| 107 | HEADER_GC_CHANGE_NAME |
| 110 | HEADER_GC_DUNGEON |
| 111 | HEADER_GC_WALK_MODE |
| 112 | HEADER_GC_SKILL_GROUP |
| 113 | HEADER_GC_MAIN_CHARACTER |
| 114 | HEADER_GC_SEPCIAL_EFFECT (typo in source) |
| 115 | HEADER_GC_NPC_POSITION |
| 118 | HEADER_GC_LOGIN_KEY |
| 119 | HEADER_GC_REFINE_INFORMATION |
| 121 | HEADER_GC_CHANNEL |
| 122 | HEADER_GC_MALL_OPEN |
| 123-125 | TARGET_UPDATE / DELETE / CREATE |
| 126/127 | AFFECT_ADD / REMOVE |
| 128/129 | MALL_SET / DEL |
| 130 | HEADER_GC_LAND_LIST |
| 131/132 | LOVER_INFO / LOVE_POINT_UPDATE |
| 133 | HEADER_GC_SYMBOL_DATA |
| 134 | HEADER_GC_DIG_MOTION |
| 135 | HEADER_GC_DAMAGE_INFO |
| 136 | HEADER_GC_CHAR_ADDITIONAL_INFO |
| 137/138 | MAIN_CHARACTER3_BGM / MAIN_CHARACTER4_BGM_VOL |
| 150 | HEADER_GC_AUTH_SUCCESS |
| 151 | HEADER_GC_PANAMA_PACK |
| 152/153 | HYBRIDCRYPT_KEYS / HYBRIDCRYPT_SDB |
| 200 | HEADER_GC_ROULETTE |
| 205 | HEADER_GC_XTRAP_CS1_REQUEST |
| 208 | HEADER_GC_SPECIFIC_EFFECT |
| 209 | HEADER_GC_DRAGON_SOUL_REFINE |
| 210 | HEADER_GC_RESPOND_CHANNELSTATUS |

Client-side naming differences (verified in `UserInterface/Packet.h:143-327`):
client `HEADER_GC_CHARACTER_MOVE=3`, client `HEADER_GC_PLAYER_CREATE_SUCCESS=8`,
client `HEADER_GC_PHASE=0xfd` but no separate `GC_TIME_SYNC` constant
(it uses `HEADER_GC_HANDSHAKE_OK=0xfc` for the time-sync reply).
Unity port must reuse the server-side names/IDs above.

## 4. Dynamic-size GC packets (client map, verified)

`UserInterface/PythonNetworkStream.cpp` `CMainPacketHeaderMap` marks these
`DYNAMIC_SIZE_PACKET` (line refs): QUEST_INFO:34, DUEL_START:37, CHAT:45,
SYNC_POSITION:47, SHOP:91, SCRIPT:97, MESSENGER:121, GUILD:122, DUNGEON:139,
NPC_POSITION:147, LAND_LIST:154, HYBRIDCRYPT_KEYS:176, HYBRIDCRYPT_SDB:177.
All others in that map are `STATIC_SIZE_PACKET` with `sizeof(struct)`.
UNVERIFIED: exact `size` field offset inside each of these structs.

## 5. GD/DG headers (game core <-> DB core)

`Server/common/tables.h`: `HEADER_GD_*` start at line 14 (`GDPOGOUT`) and
end at line 137 (`HEADER_GD_SETUP=0xff`); `HEADER_DG_*` from line 147
(`HEADER_DG_PLAYER_LOAD_SUCCESS=35`) to line 250 (`HEADER_DG_P2P=0xff`).
Verified highlights: `GD_LOGIN=1, GD_LOGOUT=2, GD_PLAYER_LOAD=3,
GD_PLAYER_CREATE=5, GD_PLAYER_DELETE=6, GD_BOOT=9, GD_LOGIN_KEY=7,
GD_AUTH_LOGIN=100, GD_LOGIN_BY_KEY=101, GD_MALL_LOAD=107,
GD_UPDATE_CHANNELSTATUS=139, GD_REQUEST_CHANNELSTATUS=140, GD_SETUP=0xff`.
Wire format (game->db): `bHeader(BYTE) + dwHandle(DWORD) + dwSize(DWORD) +
payload` — `desc_client.cpp:236-255 DBPacketHeader/DBPacket`.
This is internal (server<->server); Unity does not speak it.
UNVERIFIED: full list not enumerated here; read tables.h before any DB patch.

## 6. GG headers (game core <-> game core, P2P)

`Server/game/src/packet.h:289-315`: LOGIN=1, LOGOUT=2, RELAY=3, NOTICE=4,
SHUTDOWN=5, GUILD=6, DISCONNECT=7, SHOUT=8, SETUP=9, MESSENGER_ADD=10,
MESSENGER_REMOVE=11, FIND_POSITION=12, WARP_CHARACTER=13,
GUILD_WAR_ZONE_MAP_INDEX=15, TRANSFER=16, XMAS_WARP_SANTA=17,
XMAS_WARP_SANTA_REPLY=18, RELOAD_CRC_LIST=19, LOGIN_PING=20,
CHECK_CLIENT_VERSION=21, BLOCK_CHAT=22, SIEGE=25, PCBANG_UPDATE=28,
CHECK_AWAKENESS=29.
Sizes in `packet_info.cpp` `CPacketInfoGG`. Unity does not speak GG directly.

## 7. Sizes / structs policy

Per-packet sizes are canonical in `Server/game/src/packet_info.cpp`
(`CPacketInfoCG` ctor, lines ~90-200) and the client map above.
Packing: `#pragma pack(1)` in `game/src/packet.h:318-2307`,
`common/tables.h:260-1311`, client `Packet.h:388-2676`.
UNVERIFIED until a golden byte test exists:
- sizeof each `TPacket*` under Win32 (MSVC) vs FreeBSD (gcc9 32-bit)
- endianness (little-endian assumed, NOT proven by test)
- `WORD size`/`wSize` fields counted inclusive/exclusive of header per packet
- `TPacketGCLoginSuccess` layout (`packet.h:838-847`: bHeader,
  players[4], guild_id[4], guild_name[4][13], handle, random_key)
- `TPacketGCHandshake` 1+4+4+4 bytes under `#pragma pack(1)` = 13 bytes
  claimed, not yet byte-verified
- `TPacketKeyAgreement` total size (1 + 2 + 2 + 256 = 261 under pack(1))
  claimed, not yet byte-verified

## 8. Next smallest implementation task

Build `docs/protocol/golden-tests/handshake.md`: a byte-level golden test
that (1) starts from `TPacketGCHandshake` fields in `desc.cpp:625-640`,
(2) states expected 13-byte layout `{0xff,dwHandshake,dwTime,lDelta}`,
(3) validates against a captured PCAP from the live server (loopback),
(4) only then repeats for `TPacketKeyAgreement` (0xfb) sizes and the
`HEADER_CG_LOGIN3 (111)` payload.
Do not write C# DTOs before this golden test passes.

## 9. Packet catalog & status scheme (guide 3.3 / 5.4)

Machine-readable catalog for the connection path:
`docs/protocol/packet-catalog.json` (13 packets: GC_PHASE, GC_HANDSHAKE,
CG_TIME_SYNC, GC/CG_KEY_AGREEMENT, GC_KEY_AGREEMENT_COMPLETED, CG_LOGIN3,
CG_LOGIN, CG_LOGIN2, GC_LOGIN_SUCCESS_NEWSLOT, GC_PING, CG_PONG, CG_ENTERGAME).

Each entry carries: name, header (+source ref), direction, phase, lengthKind,
length, sizeRule, fields, encoding, endianness, security, server+client
handler refs, sourceFiles, goldenSample, status.

Status scheme (from the guide):
- `VERIFIED` = id + layout + size proven from source AND byte-confirmed with a
  golden sample. **Currently zero packets are VERIFIED.**
- `PARTIALLY VERIFIED` = id/struct/size rule read from C++ source, no golden
  sample yet. **All 13 catalog entries are in this state.**
- `UNVERIFIED` = not yet proven (see 7. and the notes below).

Catalog JSON is hand-written; do not auto-generate code or manifests from it
until at least the handshake golden test passes (guide 5.4).


