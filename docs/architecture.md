# Architecture (source-verified)

> Sources under `fulldosya/source`, `fulldosya/game`, `fulldosya/mysql`, `fulldosya/fullbinary`.
> Rules: `AGENT_DEVELOPMENT_GUIDE.md` 0.1. Unverified = UNVERIFIED. No secrets here.

## 1. Workspace (verified)

- `source/Razuning-V5/Server`: FreeBSD C++ server (`game`, `db`, `common`, libs, `Extern`, `Makefile`)
- `source/Client Source/source`: legacy Windows client (`UserInterface`, `EterLib`, `EterBase`, etc.)
- `source/DumpProto/dump_proto`: proto CSV tool
- `game/game`: runnable FreeBSD tree (`share`, `cores/auth,channel1-4,db,game99`, `allconfig`, `*.sh`)
- `mysql/mysql`: MySQL datadir backup (`account`, `player`, `common`, `log`, ...)
- `fullbinary/fullbinary`: Windows player client (`Metin2Release.exe`, `pack/*.eix/*.epk`)
- `docs/` created by this audit. `AGENT_DEVELOPMENT_GUIDE.md` at workspace root verified.

## 2. Process topology

Client (TCP) -> Auth core, Channel1-4, Game99, DB core (:15000 internal).

- `Server/game/src/desc_client.h:6-43`: `CLIENT_DESC : DESC`, globals `db_clientdesc`, `g_pkAuthMasterDesc`.
- `Server/game/src/desc_client.cpp:116-234`: `PHASE_DBCLIENT` sends `HEADER_GD_BOOT` + `HEADER_GD_SETUP`; `PHASE_P2P` binds `m_inputP2P`.
- `Server/game/src/main.cpp:647-681`: TCP/UDP/P2P bind; `g_bAuthServer` branching.
- `Server/game/src/config.cpp:875-899`: `AUTH_SERVER` token sets master mode.

Game/Auth cores never let Unity talk to MySQL directly; they use DB core via `HEADER_GD_*`.
See `Server/game/src/input_login.cpp:135,189`.

## 3. Build toolchains (verified from build files)

- Server: `source/Razuning-V5/Server/Makefile` uses `CC=gcc9`, `CXX=g++9`,
  target list `libthecore libpoly libgame liblua libsql game db`;
  `game/src/Makefile` uses `-m32`, `-std=c++11`, `SVN_VERSION=40000`,
  links static DevIL/mysqlclient/cryptopp from `Extern/`.
- Client: `source/Client Source/client.sln` = VS2017 (Format 12.00),
  projects Win32/x64 (x64 mostly maps to Win32 configs in the sln),
  includes `discord_rpc`, `CWebBrowser`.
- No unit-test or CI infrastructure exists in the repo; `UNVERIFIED` notes
  can only be closed with local packet captures / byte-level golden tests.

## 4. Sprint 0 durumu (guide 11)

Yapıldı:
- Workspace yolları doğrulandı (guide 1.2 envanteri birebir mevcut).
- Server/client source map'i çıkarıldı (`docs/architecture.md`).
- `docs/protocol/connection-flow.md` yazıldı.
- Paket envanterinin ilk doğrulanmış bölümü: `docs/protocol/protocol-inventory.md`
  + `docs/protocol/packet-catalog.json` (13 bağlantı-yolu paketi,
  PARTIALLY VERIFIED).

Henüz yapılmadı (bu audit kapsamı dışı bırakıldı, bilinçli):
- Test ortamı ve DB/server rollback prosedürü (guide 11 Sprint 0).
- Gizli bilgilerin repodan ayrılması ve rotate planı; config ve `mysql/`
  altında gerçek sırlar mevcut (docs'a kopyalanmadı).
- PC client'ın canlı sunucuya bağlanma doğrulaması (sunucu çalıştırma izni
  gerektirir).
