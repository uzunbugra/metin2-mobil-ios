# Sprint 5 — Game World (kayıt)

Hedef: loading sonrası oyun-dünyası paketleri (stats, spawn, item, hareket) —
oynanabilir vertical slice'a giden protokol katmanı.

Önkoşul: Sprint 4 zinciri (auth → channel → select → world entry) tamam.

## Adım 1 — Loading stats + spawn (tamamlandı ✅)

İz sürme: `PlayerLoad` devamı (`input_db.cpp:452-473`: quickslot store (wire'a
çıkmaz), `PointsPacket` + `SkillLevelPacket`, safebox sorgusu) → item'lar ayrı
`DG_ITEM_LOAD` yolundan parça-parça (`ItemLoad`, `input_db.cpp:1444+` — sonraki adım).
Spawn: `CHARACTER::Show` → 1/35B (`char.cpp:812`), despawn 2/5B.

| İş | Dosya | Test |
|---|---|---|
| GC_POINTS (16, 1021B) + codec | `Packets/PacketGCPoints.cs`, `Codecs/PacketGCPointsCodec.cs` | `PacketGCPointsTests` (6) |
| GC_SKILL_LEVEL (76, 1531B) + skill entry codec | `Packets/PacketGCSkillLevel.cs` + `PlayerSkill.cs`, `Codecs` (2 dosya) | `PacketGCSkillLevelTests` (7) |
| GC_ADD (1, 35B) + GC_DEL (2, 5B) + codec'ler | 2 paket + 2 codec (float dahil) | Add (6), Del (5) |
| Buffer float | `PacketReader.ReadFloat` / `PacketWriter.WriteFloat` (netstandard2.1 uyumlu) | ReaderWriter +2 |
| Framer + registry | `PacketLengthTable` (+16→1021, +76→1531, +1→35, +2→5), `CreateGameRegistry` (16/76 Loading, 1/2 Game) | framer +1, registry +3 |
| Dünya istemcisi | `Network/Session/GameWorldClient.cs` (stats sırası + spawn event) | `GameWorldClientTests` (5 loopback) |

Dersler:
- **netstandard2.1'de float BinaryPrimitives yok**: `Read/WriteSingleLittleEndian`
  eksik → BitConverter + LE normalizasyon. (Unity netstandard2.1 hedefi bunu dayatır.)
- **`TPlayerSkill.time_t` yine 4B**: GC_TIME'daki ABI notu burada da geçerli.
- **Quickslot wire'a çıkmaz**: `SetQuickslot` yalnızca server-state yazar;
  istemciye ayrı quickslot paketi bu yolda yok — implemente edilmedi.
- Test: 476 → 511 (+35).

## Adım 2 — Item sistemi (tamamlandı ✅)

İz sürme: `CHARACTER::SetItem` (`char_item.cpp:405-437`: item varsa 51B SET,
yoksa 42B sıfırlanmış DEL) + `CItem::UpdatePacket` (`item.cpp:207-228`: 38B).
Loading'daki ilk envanter bu yoldan parça-parça gelir (`ItemLoad` → AddToCharacter).

| İş | Dosya | Test |
|---|---|---|
| GC_ITEM_SET (21, 51B) + codec | `Packets/PacketGCItemSet.cs`, `Codecs/PacketGCItemSetCodec.cs` (+`ItemFieldCodec`) | `PacketGCItemSetTests` (6) |
| GC_ITEM_DEL (20, 42B DelDeprecated) + codec | `Packets/PacketGCItemDel.cs`, `Codecs/PacketGCItemDelCodec.cs` | `PacketGCItemDelTests` (6) |
| GC_ITEM_UPDATE (25, 38B) + codec | `Packets/PacketGCItemUpdate.cs`, `Codecs/PacketGCItemUpdateCodec.cs` | `PacketGCItemUpdateTests` (6) |
| Envanter istemcisi | `Network/Session/InventoryClient.cs` (Set/Cleared/Updated event) | `InventoryClientTests` (5 loopback) |
| Framer + registry | `PacketLengthTable` (+21→51, +20→42, +25→38), game registry +3 (Loading+Game) | framer +1, registry +1 |

Dersler:
- **İsim tuzağı (SET/SET2)**: sunucu 21'e ITEM_SET der, istemci ITEM_SET2 —
  51B layout aynıdır, wire kazanır. Katalogda notlu.
- **20 asla 2-byte değildir**: `packet_item_del` (2B) bu yolda gönderilmez;
  header 20 = 42B sıfırlanmış varyant. Boyut struct adından değil çağrı
  yerinden (`char_item.cpp:428-436`) okunur.
- **ref struct kopyalanır**: `PacketReader/Writer` helper'lara `ref` ile verilir,
  yoksa pozisyon ilerlemez (derleme hatası değil, sessiz bug olurdu).
- Test: 511 → 536 (+25).

## Adım 3 — Hareket (tamamlandı ✅)

İz sürme: istemci niyeti `SendCharacterStatePacket` (`PhaseGame.cpp:1107-1145`,
rot=degrees/5, cm, server-ms) → `CInputMain::Move` (`input_main.cpp:1514-1688`:
teleport/speed/combo kontrolleri, izleyicilere rebroadcast — gönderene HARİÇ) →
sync batch'leri (`input_main.cpp:1782+`: wSize doğrulama, 16 clamp, sync-owner +
3500cm anti-hack).

| İş | Dosya | Test |
|---|---|---|
| CG_MOVE (7, 16B) + GC_MOVE (3, 24B) + codec'ler | 2 paket + 2 codec (+`MoveFunc`) | 6 + 6 |
| Sync element + CG/GC dynamic codec'ler | `SyncPositionElement.cs` + 3 codec | `SyncPositionCodecTests` (8) |
| Dinamik framer (header 5, inline wSize) | `PacketLengthTable` sync sabitleri + `PacketFramer` wSize dalı | framer +3 |
| Registry | game registry +2 (3/5 Game) | registry +1 |
| Hareket istemcisi | `Network/Session/MovementClient.cs` (quantize + fail-closed) | `MovementClientTests` (6 loopback) |

Dersler:
- **24B'yi 16B okumak**: GC_MOVE ilk yazımda 16B yazılmıştı (CG ile karıştı);
  istemci struct'ı (24B + duration) hakem oldu. Yön başına boyut ayrı doğrulanır.
- **Framer test verisi gerçek header'la çakışabilir**: bozuk-wSize testinde
  `0x0A` artığı geçerli bir 10-frame kurdu — test verisi registry'den kaçırıldı.
- **Struct clamp'ı codec'i kör eder**: CG struct'taki Min(...,16) serialize'ın
  overflow'u görmesini engelliyordu; clamp kaldırıldı, hata fail-closed'a döndü.
- Test: 536 → 566 (+30).

## Sıradaki (Sprint 5 devam)

- Headless canlı giriş: staging sunucuya handshake → world entry denemesi
  (test hesabı, staging izolasyonu).
