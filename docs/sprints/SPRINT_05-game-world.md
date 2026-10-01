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

## Sıradaki (Sprint 5 devam)

- Hareket: SYNC_POSITION/MOVE paketleri (oynanabilir vertical slice).
