# Metin2 Unity Mobile Client — AI Agent Development Guide

> **Amaç:** Mevcut Metin2 (Razuning-V5 / 40k tabanlı) C++ sunucusuna bağlanan, Unity/C# ile geliştirilen Android, iOS ve PC istemcisi oluşturmak. Mevcut hesaplar, karakterler, item'lar, harita ilerlemesi ve oyun dünyası mümkün olduğunca aynı sunucu/veritabanı üzerinde paylaşılacaktır.
>
> **Bu belge AI coding agent'ları içindir.** Agent bu dosyayı, görev açıklamasını ve gerçek kaynak kodunu birlikte kullanmalıdır. Kaynakta doğrulanmayan protokol ayrıntılarını tahmin etmemelidir.

---

## 0. Agent için zorunlu başlangıç talimatları

Her yeni oturumda önce bu dosyayı ve depo yapısını oku. Ardından istenen görevi küçük, doğrulanabilir adımlara böl.

### 0.1 Kesin kurallar

1. **Önce incele, sonra değiştir.** İlgili C++ sunucu ve eski istemci kodunu bulmadan protokol, paket başlığı, paket uzunluğu, şifreleme, handshake veya oyun davranışı yazma.
2. **Kaynak kod gerçek doğruluk kaynağıdır.** Bu dokümandaki mimari öneriler, kaynak kodda doğrulanması gereken tasarım hedefleridir; gerçek paket ID'leri veya wire format tanımı değildir.
3. **Paket numarası/struct boyutu uydurma.** `HEADER_CG_*`, `HEADER_GC_*`, `TPacket*`, `sizeof`, değişken uzunluk formülleri, byte order, cipher ve sequence davranışını gerçek kaynakta doğrula.
4. **Var olan sunucuyu yeniden yazma.** İlk tercih mevcut FreeBSD C++ sunucusunu korumak ve Unity istemcisini onun protokolüne uyarlamaktır. Sunucu değişikliği ancak zorunlu ve küçük, geriye uyumlu bir yama olarak önerilebilir.
5. **DB'ye istemciden doğrudan bağlanma.** Unity uygulamasına MySQL kullanıcı adı, parola, admin parolası, DB portu veya sunucu içi sır koyma. İstemci yalnızca oyun sunucusuyla konuşur.
6. **Güvenlik sırlarını loglama veya commit etme.** Repoda bulunan credential'ları çıktı, doküman, test fixture veya örnek config içine kopyalama. Daha önce paylaşılmış gerçek sırlar varsa açığa çıkmış kabul edilmeli ve döndürülmelidir.
7. **Tehlikeli bakım scriptlerini çalıştırma.** Özellikle `temizle.sh`, `Fulltemizleme` veya DB dosyalarını silen/yeniden oluşturan scriptleri kullanıcı açıkça onaylamadan çalıştırma. `ibdata*`, `ib_logfile*`, MySQL datadir, oyuncu verisi ve logları silme.
8. **Yedeksiz veri migrasyonu yok.** Şema değişikliği gerekiyorsa önce şema ve kullanım noktalarını incele; yedek, rollback ve staging planı olmadan canlı DB'de işlem yapma.
9. **Her görevde test yaz veya test eksikliğini belirt.** Projede build/test altyapısı yoksa önce mevcut durumu raporla; uygun en küçük test altyapısını öner.
10. **Küçük ve gözden geçirilebilir değişiklikler.** Alakasız refactor, tüm dosyaları yeniden biçimlendirme, bağımlılıkları topluca güncelleme veya protokolü aynı anda yeniden tasarlama yapma.
11. **Platform farklarını açık tut.** Unity ana thread'i, socket thread'i, Android/iOS yaşam döngüsü, arka plana geçme, bağlantı kesilmesi ve uygulama kapatılması ayrı ele alınmalıdır.
12. **Kaynak dosyalar eksikse açıkça bildir.** Tahmin ederek sahte implementasyon yapma; hangi dosya/sınıf/symbol gerektiğini ve neden gerektiğini söyle.

### 0.2 Her görev için çalışma akışı

1. İstenen davranışı ve kabul kriterlerini netleştir.
2. İlgili dosyaları `rg`, IDE araması veya eşdeğer araçlarla bul.
3. Veri akışını takip et: eski istemci → paket oluşturma → sunucu input handler → cevap paketi → eski istemci handler.
4. Değişiklik planını 3–8 maddede yaz.
5. En küçük uygulanabilir değişikliği yap.
6. Build, unit test, protokol golden test veya statik kontrol çalıştır.
7. Değişen dosyaları, doğrulama sonuçlarını, kalan riskleri ve sonraki adımı raporla.
8. Gerçek kaynakta doğrulanmayan noktaları **UNVERIFIED** olarak işaretle.

### 0.3 Agent'ın görev sonunda rapor formatı

```text
## Yapılanlar
- ...

## Değişen dosyalar
- path — neden değişti

## Doğrulama
- Komut/test:
- Sonuç:
- Çalıştırılamayan test ve nedeni:

## Protokol / uyumluluk notları
- Kaynakta doğrulananlar:
- Henüz doğrulanmayanlar:

## Riskler ve sonraki adım
- ...
```

---

## 1. Proje tanımı ve hedef

### 1.1 Ürün hedefi

Mevcut Metin2 oyun dünyasına bağlanan yeni Unity istemcisi geliştirmek:

- Android ve iOS mobil istemcileri.
- İhtiyaç halinde Windows/macOS/PC Unity istemcisi.
- Var olan C++ auth, db ve game servisleriyle iletişim.
- Mevcut hesap/karakter/veri modelini kullanma.
- PC istemcisiyle aynı oyun dünyasında bulunma hedefi.
- Mevcut oyun kuralları ve sunucu otoritesini koruma.
- İlk aşamada küçük bir oynanabilir vertical slice; sonra sistemleri aşamalı taşıma.

### 1.2 Bilinen kaynak envanteri

Aşağıdaki konumlar kullanıcının verdiği envanterden gelir. Agent çalışma ortamında bu dizinlerin gerçekten mevcut olduğunu ayrıca kontrol etmelidir.

| Kaynak | Kullanıcının bildirdiği konum | Rol |
|---|---|---|
| FreeBSD server files | `C:\Users\yusuf\Downloads\fulldosya\game\` | Sunucu çalıştırma dosyaları, config, locale, map ve quest verileri |
| MySQL datadir kopyası | `C:\Users\yusuf\Downloads\fulldosya\mysql\mysql\` | DB dosyaları; doğrudan elle düzenlenmemeli |
| Windows PC client | `C:\Users\yusuf\Downloads\fulldosya\fullbinary\fullbinary\` | Çalışan eski istemci, pack arşivleri ve referans davranış |
| Server C++ source | `C:\Users\yusuf\Downloads\fulldosya\source\Razuning-V5\Server\` | Auth/DB/game/common ve yardımcı kütüphaneler |
| Client C++ source | `C:\Users\yusuf\Downloads\fulldosya\source\Client Source\` | Eski Windows istemcisi, UserInterface ve network kodu |
| DumpProto | `C:\Users\yusuf\Downloads\fulldosya\source\DumpProto\` | Item/mob proto dönüştürme yardımcıları |

Önemli: Bu Windows yolları agent'ın kendi sandbox'ında bulunmayabilir. `Test-Path` / `ls` ile kontrol et; yoksa kullanıcıdan depo/workspace konumunu iste. Dosyaların içeriğini görmeden okumuş gibi davranma.

### 1.3 Kaynak envanterinden bildirilen teknik özellikler

Bunlar kullanıcı tarafından bildirilen envanter bilgileri olup, gerçek dosyada doğrulanmadan wire-protocol implementasyonu için yeterli değildir:

- Sunucu C++ ve FreeBSD tabanlı; 32-bit eski binary'ler mevcut.
- Kaynak ağacı 40k / Marty tarzı Metin2 tabanı olarak tanımlanmış.
- Oyun seviyesi envanterde 105 maksimum olarak bildirilmiş.
- Eski istemci Visual Studio 2017, Python 2.7, DirectX 8 ve Granny/SpeedTree/Miles gibi eski bağımlılıklar içeriyor.
- İstemci pack dosyaları EIX/EPK formatında.
- Dragon Soul, pet/mount, auction ve çeşitli özel sistemler mevcut olabilir.
- Auth, DB ve channel servisleri farklı portlarda çalışıyor.
- Kullanıcının belirttiği config/port bilgileri ortamdan ortama değişebilir; config dosyaları çalışma anındaki gerçek kaynaktır.

---

## 2. Mimari kararlar

### 2.1 Hedef mimari

```text
Unity Client (Android / iOS / PC)
    |
    | TCP (gerçek protokol ve şifreleme kaynakta doğrulanacak)
    v
Auth / Login service
    |
    v
DB service <----> MySQL (sunucu tarafında kalır)
    |
    v
Game channels / game cores
    |
    +---- map, quest, item, character, guild, shop, etc.
```

Bu şema kavramsaldır. Gerçek bağlantı sırası, redirect, handshake, auth token ve game server bağlantı akışı eski istemci ve server source içinden çıkarılmalıdır.

### 2.2 Korunacak sınırlar

- C++ sunucu, oyun kurallarının ve kalıcı verinin otoritesidir.
- Unity istemcisi görüntüleme, input, ses, UI, animasyon, yerel tahmin ve sunucu mesajlarını sunma işini yapar.
- Unity istemcisi hasar, item sahipliği, para, EXP, drop, ticaret veya yetki kararlarını nihai olarak belirleyemez.
- MySQL yalnızca güvenilir sunucu servislerinden erişilebilir olmalıdır.
- Auth ve game bağlantıları arasında mevcut sunucu tasarımının gerektirdiği token/handshake korunmalıdır.
- Var olan PC istemcisi davranış ve paket akışı için referans istemcidir; yeni client PC istemcisini bozacak sunucu değişikliği yapmamalıdır.

### 2.3 Geliştirme sırası

Önce bağlantı ve en küçük oynanabilir akış:

1. Kaynak/build baseline.
2. Paket sözlüğü ve gerçek wire format.
3. Transport, framing, handshake/cipher.
4. Login → server list → channel → character select/create.
5. Game bağlantısı ve karakterin dünyaya girişi.
6. Görünür entity spawn/despawn ve hareket.
7. Kamera, input, mobil kontrol.
8. Temel combat, HP/MP, hedefleme.
9. Inventory/equipment ve item ikonları.
10. NPC, shop, chat ve temel sosyal sistemler.
11. Haritalar, terrain, efekt, ses ve animasyon kalitesi.
12. İleri sistemler: guild, party, trade, pet/mount, Dragon Soul, dungeon, auction vb.

Bu sıralama, kaynak incelemesinden sonra yeniden planlanabilir. İleri sistemler ilk vertical slice'a eklenmemelidir.

---

## 3. Kaynak kod keşif rehberi

Agent ilk olarak aşağıdaki sembol ve dosyaları aramalıdır. İsimler fork'a göre farklılık gösterebilir; yoksa benzer görevli sınıfları bul.

### 3.1 Server source aranacak yerler

Muhtemel klasörler:

- `Server/common/`
- `Server/game/`
- `Server/db/`
- `Server/libthecore/`
- `Server/libgame/`

Aranacak semboller/konular:

- `HEADER_CG_`, `HEADER_GC_`, `HEADER_GD_`, `HEADER_DG_`
- `TPacketCG`, `TPacketGC`, `TPacketGD`, `TPacketDG`
- `length.h`, `packet.h`, `tables.h`, `typedef.h`
- `input.cpp`, `input_login.cpp`, `input_main.cpp`, `input_auth.cpp`
- `desc.cpp`, `desc.h`, `desc_manager`
- `char.cpp`, `char.h`, `char_manager`
- `ClientManager`, `DBManager`
- `SetPhase`, `PHASE_*`, `STATE_*`
- ` handshake`, `crypt`, `cipher`, `sequence`, `CRC`, `XTEA`, `TEA`, `RSA` (gerçekte hangisi kullanılıyorsa)
- `CInput*`, `CDesc`, `DESC`
- `P2P`, `login`, `select`, `create`, `enter game`, `character`

Önerilen aramalar (ripgrep varsa):

```bash
rg -n "HEADER_(CG|GC|GD|DG)_" source/Razuning-V5/Server
rg -n "TPacket(CG|GC|GD|DG)" source/Razuning-V5/Server
rg -n "SetPhase|PHASE_|STATE_" source/Razuning-V5/Server
rg -n "crypt|cipher|sequence|handshake|CRC|XTEA|TEA" source/Razuning-V5/Server
rg -n "input_login|input_main|ClientManager|desc.cpp" source/Razuning-V5/Server
```

### 3.2 Client source aranacak yerler

Muhtemel konum:

- `source/Client Source/UserInterface/`
- `EterPack`, `EterLib`, `EterBase`, `GameLib`, `PRTerrainLib`

Aranacak semboller:

- `CPythonNetworkStream`
- `PythonNetworkStream`
- `SendLogin`, `SendSelect`, `SendCharacter`, `SendEnterGame`
- `Recv`, `RecvPhase`, `SetPhase`
- `HEADER_CG_`, `HEADER_GC_`
- `TPacketCG`, `TPacketGC`
- `SetSecurityMode`, `SetCipher`, `SendSequence`, `RecvSequence` veya fork'taki eşdeğerler
- `CEterPackManager`, `EterPack`, pack index/decrypt
- `CPythonPlayer`, `CPythonCharacterManager`, `CPythonBackground`

Aramalar:

```bash
rg -n "CPythonNetworkStream|PythonNetworkStream" "source/Client Source"
rg -n "HEADER_(CG|GC)_" "source/Client Source"
rg -n "SendLogin|SendSelect|EnterGame|RecvPhase" "source/Client Source"
rg -n "Cipher|Sequence|Security|Handshake|CRC" "source/Client Source"
```

### 3.3 Her protokol özelliği için iz sürme

Her paket veya akış için bir protokol notu hazırla:

| Alan | Kaydedilecek bilgi |
|---|---|
| Packet name | Kaynaktaki tam isim |
| Header value | Macro/enum değeri ve tanımlandığı dosya |
| Direction | Client→Server veya Server→Client |
| Phase | Login/select/game/loading vb. |
| Fixed/variable | Sabit mi, değişken mi? |
| Size rule | Kaynaktaki gerçek formül |
| Fields | Alan sırası, türü, packing/alignment |
| Encoding | String encoding, null terminator, length prefix |
| Endianness | Kaynaktan doğrulanan byte order |
| Security | Cipher/sequence öncesi/sonrası davranış |
| Handler | Server ve client handler dosya/satırları |
| Golden sample | PC client veya test fixture'dan gerçek byte dizisi |
| Status | VERIFIED / PARTIALLY VERIFIED / UNVERIFIED |

Paket başlıklarını veya struct layout'u yalnızca isimlerinden çıkarsama. C++ compiler packing, `#pragma pack`, platform ABI ve `sizeof` farklılıklarını kontrol et.

---

## 4. Unity repository yapısı

Yeni Unity projesinde aşağıdaki yapıyı başlangıç önerisi olarak kullan. Mevcut proje farklıysa önce mevcut düzeni incele; büyük çaplı taşıma yapma.

```text
Metin2Unity/
├── AGENT_DEVELOPMENT_GUIDE.md
├── README.md
├── docs/
│   ├── architecture.md
│   ├── protocol/
│   │   ├── protocol-inventory.md
│   │   ├── packet-catalog.json
│   │   ├── connection-flow.md
│   │   └── golden-tests.md
│   ├── assets/
│   │   ├── asset-pipeline.md
│   │   └── licensing.md
│   ├── decisions/
│   │   └── ADR-0001-client-server-boundary.md
│   └── testing/
│       └── test-plan.md
├── Assets/
│   ├── Art/
│   │   ├── Characters/
│   │   ├── Monsters/
│   │   ├── NPC/
│   │   ├── Items/
│   │   ├── Maps/
│   │   ├── Effects/
│   │   └── UI/
│   ├── Audio/
│   ├── Prefabs/
│   ├── Scenes/
│   │   ├── Bootstrap.unity
│   │   ├── Login.unity
│   │   ├── CharacterSelect.unity
│   │   └── GameWorld.unity
│   ├── Scripts/
│   │   ├── Bootstrap/
│   │   ├── Core/
│   │   ├── Network/
│   │   │   ├── Transport/
│   │   │   ├── Protocol/
│   │   │   ├── Security/
│   │   │   ├── Session/
│   │   │   ├── Packets/
│   │   │   └── Handlers/
│   │   ├── Gameplay/
│   │   │   ├── Characters/
│   │   │   ├── Combat/
│   │   │   ├── Inventory/
│   │   │   ├── Skills/
│   │   │   └── Social/
│   │   ├── World/
│   │   │   ├── Entities/
│   │   │   ├── Maps/
│   │   │   ├── Terrain/
│   │   │   └── Streaming/
│   │   ├── UI/
│   │   ├── Mobile/
│   │   └── Utilities/
│   ├── ScriptableObjects/
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
├── Packages/
├── ProjectSettings/
└── Tools/
    ├── PackInspector/
    ├── ProtoConverter/
    └── AssetConverter/
```

### 4.1 Assembly definition önerisi

Proje büyüdüğünde assembly definition kullan:

- `Metin2.Core`: Unity bağımsız temel tipler ve yardımcılar.
- `Metin2.Protocol`: byte reader/writer, packet definitions, framing; mümkün olduğunca UnityEngine bağımsız.
- `Metin2.Network`: socket transport, session, dispatcher, reconnect.
- `Metin2.Gameplay`: oyun modelleri ve sunucu event uygulama katmanı.
- `Metin2.World`: Unity entity, map, terrain ve rendering.
- `Metin2.UI`: login, select, HUD, inventory ve UI.
- `Metin2.Mobile`: touch, safe area, lifecycle, platform özellikleri.
- `Metin2.Tests`: unit ve integration testleri.

`Metin2.Protocol` içine `UnityEngine` bağımlılığı ekleme. Protocol assembly mümkün olduğunca saf C# ve test edilebilir olmalıdır.

---

## 5. Network katmanı tasarımı

### 5.1 Katmanlar

```text
Gameplay / UI
      |
GameSession (state machine + public API)
      |
Packet handlers / packet registry
      |
Packet framer (stream -> complete packet)
      |
Security / cipher / sequence (kaynak doğrulandıktan sonra)
      |
Transport (TCP socket)
```

Sorumlulukları birbirine karıştırma:

- **Transport:** byte gönder/al; TCP'nin paket sınırlarını koruduğunu varsayma.
- **Framer:** TCP byte stream'inden tam protokol mesajlarını çıkar.
- **Security:** kaynakta tanımlı handshake, cipher, sequence, checksum vb.
- **Protocol:** paket alanlarını gerçek wire formatına göre serialize/deserialize et.
- **Session:** phase/state, bağlantı ve sunucu yönlendirmesi.
- **Handlers:** gelen paketleri gameplay/UI event'lerine dönüştür.
- **Gameplay:** sunucu state değişikliklerini uygula.

### 5.2 TCP ile ilgili zorunluluklar

TCP mesaj tabanlı değil, byte stream tabanlıdır. Bir `ReadAsync` çağrısı:

- Tek paketin bir kısmını,
- Bir paketi,
- Birden çok paketi,
- Önceki paketin sonu ve sonraki paketin başını

döndürebilir.

Bu nedenle tek socket read = tek packet kabulü yapma. Framer, sunucunun gerçek protokolündeki header/length/phase kurallarına göre eksik ve birleşik verileri tamponlamalıdır.

Ek gereksinimler:

- Maksimum frame uzunluğu doğrulaması (kaynak protokolün gerçek sınırına göre).
- Hatalı/negatif/taşan length alanlarını reddetme.
- Gereksiz allocation ve buffer kopyalarını azaltma.
- Socket thread'inden Unity API çağırmama.
- Gelen paketleri ana thread'e güvenli dispatcher/queue ile aktarma.
- Gönderimlerde birden çok thread'in byte'ları iç içe geçirmesini engelleyen send queue/semaphore.
- İptal token'ı ve düzgün shutdown.
- Socket exception, timeout, app pause/resume ve bağlantı kopmasını ayrı state olarak ele alma.
- Reconnect sırasında eski session paketlerinin yeni session'a sızmasını engelleme.

### 5.3 Phase/state machine

Örnek kavramsal durumlar; gerçek sunucu akışına göre isimleri değiştir:

```text
Disconnected
  -> ConnectingAuth
  -> AuthHandshake
  -> Login
  -> ServerSelection
  -> ChannelSelection
  -> CharacterSelection
  -> ConnectingGame
  -> GameHandshake
  -> LoadingWorld
  -> InGame
  -> Disconnecting
  -> Disconnected
```

Kurallar:

- Her phase'te yalnızca o phase için geçerli paketleri kabul et.
- Paket registry'si phase-aware olmalıdır.
- Geçersiz phase'ten gelen paketlerde güvenli şekilde bağlantıyı kapat veya kaynak davranışına uygun hata uygula.
- Login sunucusundan game sunucusuna geçişte endpoint/token/handshake bilgisini eski istemci ve server source'tan çıkar.
- Şifreleri loglama; auth loglarında maskeleme uygula.

### 5.4 Packet registry

Paket dispatch'i büyük `switch` bloklarına gömmek yerine kaynakla eşlenebilir bir registry tasarla. Ancak kod üretimi veya JSON manifest otomasyonu, gerçek C++ header değerleri ve layout doğrulanmadan yapılmamalıdır.

Önerilen metadata alanları:

```json
{
  "packetName": "SOURCE_VERIFIED_NAME",
  "header": null,
  "direction": "ServerToClient",
  "phase": "SOURCE_VERIFIED_PHASE",
  "lengthKind": "Unknown",
  "length": null,
  "sourceFiles": [],
  "status": "UNVERIFIED"
}
```

Bu yalnızca doküman şablonudur; gerçek packet ID'leriyle doldurulmuş kabul edilmemelidir. JSON içinde bilinmeyen alanları `null`/`Unknown` bırak.

### 5.5 C# binary reader/writer

Gereksinimler:

- Bounds check her read işleminde yapılmalı.
- `ReadByte`, `ReadInt16`, `ReadUInt16`, `ReadInt32`, `ReadUInt32`, `ReadFloat` gibi tipler yalnızca wire format doğrulandıktan sonra kullanılmalı.
- String'lerde kaynakta kullanılan encoding ve uzunluk/null terminator kuralı uygulanmalı.
- C++ `long`, `DWORD`, `BYTE`, `WORD`, `bool`, enum ve `#pragma pack` karşılıkları tek tek doğrulanmalı.
- `BitConverter` platform endianness'ine körü körüne güvenme; protokol byte order'ını açık uygula.
- Untrusted length değerleriyle sınırsız `new byte[length]` yapma.
- Serialization için golden byte tests zorunlu.

---

## 6. Güvenlik ve güvenilirlik

### 6.1 Güvenlik ilkeleri

- Client build içine DB credential, server admin credential, private key veya shared secret koyma.
- Login parola akışını mevcut istemciyle aynı biçimde anlamadan yeni bir hash/encryption yöntemi icat etme.
- TLS eklemek mevcut legacy protokolü otomatik olarak uyumlu yapmaz; server desteği ve geçiş tasarımı gerekir.
- Client-side obfuscation güvenlik sınırı değildir.
- Server-side validation zorunludur: hareket hızı/mesafe, saldırı hızı, item/yang, trade, skill cooldown, target ve yetki kontrolleri.
- Packet rate/size limits ve timeout sunucu tarafında kontrol edilmelidir.
- Mobil uygulamanın kapanması/arka plana alınması bağlantı kaybı olarak ele alınmalı; karakter güvenliğinin server-side davranışı doğrulanmalıdır.

### 6.2 Ortam yönetimi

Config dosyalarını ayır:

- `local` — geliştirici bilgisayarı / test VM.
- `development` — izole test sunucusu.
- `staging` — üretime benzer test.
- `production` — ayrı, erişimi kısıtlı.

Sunucu IP/portları config üzerinden verilebilir; sırlar istemciye gömülmemelidir. İstemciye açık endpoint bilgisi sır değildir, fakat admin/DB endpoint'i kesinlikle client'a konmaz.

### 6.3 Sunucu erişimi

- DB portunu internete açma.
- Admin paneli ve SSH erişimini IP allowlist/VPN ile sınırla.
- Geliştirme sunucusunu üretim DB'sinden ayır.
- Kullanıcının paylaştığı credential'ları yeniden kullanma; gerçekse rotate et.
- DB yedeğini doğrula ve restore testi yapmadan güvenli kabul etme.

---

## 7. Asset ve içerik pipeline'ı

Mevcut PC client asset'leri referans olarak kullanılabilir; ancak teknik, lisans ve dağıtım hakları kontrol edilmelidir. Asset'leri mobil uygulamaya paketlemek, mevcut PC pack dosyalarını olduğu gibi kopyalamaktan farklı bir pipeline gerektirir.

### 7.1 EIX/EPK arşivleri

- Pack formatı, index yapısı, sıkıştırma ve olası şifreleme kaynak araçlardan doğrulanmalı.
- Orijinal arşivleri read-only sakla; dönüştürme çıktısını ayrı klasöre yaz.
- Araçların kaynağını, lisansını ve güvenilirliğini kontrol et.
- Çıkarılan dosyalarda path traversal (`../`), beklenmeyen boyut ve bozuk arşiv kontrolü yap.
- Kullanıcıya ait dosyaları veya üçüncü taraf asset'leri izinsiz yayımlama.

### 7.2 Modeller ve animasyonlar

Eski Granny modelleri Unity tarafından doğrudan desteklenmeyebilir. Önerilen aşama:

1. Tek karakter, tek iskelet ve tek animasyon seti seç.
2. Kaynak formatı ve export araçlarını belirle.
3. Lisanslı/izinli dönüştürme yolunu doğrula.
4. Skeleton, bone hierarchy, bind pose, scale, coordinate system ve animation events kontrolü yap.
5. Unity humanoid/Generic rig kararını model bazında ver.
6. Mobilde polygon, material, texture, draw call ve bone count profili çıkar.

Tüm karakterleri tek seferde dönüştürmeye çalışma.

### 7.3 Harita ve terrain

- Kaynak map formatı, terrain height, tile/texture, object placement, collision ve coordinate transform incelenmeli.
- İlk test için bir küçük map parçası seç.
- Unity world units ile Metin2 koordinatlarının ölçeği ve eksen yönü için açık bir dönüşüm fonksiyonu tanımla.
- Client'ın yerel navmesh'i server movement otoritesini geçersiz kılamaz.
- Collision ve pathfinding davranışını PC client/server ile karşılaştır.

### 7.4 Item/mob proto

DumpProto araçları ve locale tabloları incelenerek bir dönüştürme pipeline'ı kurulabilir.

- Kaynak proto formatı ve encoding doğrulanmalı.
- `item_proto` / `mob_proto` alanlarının anlamı server source'tan çıkarılmalı.
- Unity için JSON/CSV/ScriptableObject çıktısı üretilebilir; bu çıktı yalnızca istemci sunum verisi olmalıdır.
- Item fiyatı, stat, bonus, upgrade ve combat hesaplarının otoritesi server'da kalır.
- Proto converter deterministik olmalı ve input hash/version raporlamalı.

---

## 8. Gameplay ve dünya senkronizasyonu

### 8.1 Entity modeli

En azından kavramsal olarak şu türleri ayır:

- Player entity
- NPC
- Monster
- Ground item
- Pet/mount (daha sonraki aşama)
- Effect/projectile (sunucu mesajlarına göre)

Her network entity için:

- Server VID/VID benzeri kimlik alanını kaynakta bul.
- Client-side Unity instance ID ile server entity ID'yi karıştırma.
- Spawn/despawn, map change, death, revive ve reconnect davranışlarını ayrı ele al.
- Entity referansları despawn sonrasında geçersiz kılınmalı.
- Paketlerden gelen koordinat ve hız değerlerini doğrula.

### 8.2 Hareket

- Server authoritative movement korunmalı.
- Client input'u kaynak protokoldeki hareket paketiyle gönderilir.
- Görsel yumuşatma/interpolation, server state'i değiştirmemeli.
- Prediction/reconciliation ancak gerçek movement akışı ve server correction davranışı anlaşıldıktan sonra eklenmeli.
- Unity NavMesh yalnızca client-side görsel/yardımcı amaçlarla kullanılabilir; server'ın path/anti-cheat kontrollerini atlamamalıdır.
- Teleport, knockback, stun, mount ve map transition gibi durumlar normal hareketten ayrı ele alınmalıdır.

### 8.3 Combat

- Skill ID, target VID, attack packet, combo ve hit feedback kaynak koddan çıkarılmalı.
- Client animasyon ve efekt oynatabilir; hasar/crit/drop/EXP kararını veremez.
- Sunucu onayı gelmeden kalıcı HP veya item sonucunu kesinleştirme.
- Gecikme durumunda UI tahmini ile server-confirmed state ayrılmalı.
- Skill/attack rate limit server tarafında doğrulanmalıdır.

### 8.4 Inventory ve ekonomi

- Slot, window, cell, item ID, count, sockets/attributes, yang ve currency alanları gerçek server tablolarından doğrulanmalı.
- Client UI'daki item state yalnızca server event'leriyle güncellenmeli.
- Move, split, use, equip, unequip, drop, shop, trade ve upgrade işlemlerinde server yanıtı beklenmeli.
- Tekrarlanan istekler ve reconnect sonrası duplicate action riskleri değerlendirilmelidir.
- Client tarafında fiyat veya item stat'ı hesaplanması yalnızca önizleme içindir; server sonucu esas alınır.

---

## 9. Mobil UX ve performans

### 9.1 Mobil kontroller

- Sol tarafta sanal joystick veya kaynak oyunun hareket modeline uygun kontrol.
- Sağ tarafta attack/skill/target/interact butonları.
- Buton yerleşimleri farklı aspect ratio ve safe area'larda test edilmeli.
- UI ölçeklendirmesi çözünürlük ve DPI farklarını ele almalı.
- Touch input ile UI click-through/oyun input çakışması engellenmeli.
- Otomatik hedefleme veya auto-potion gibi özellikler sunucu kurallarına ve oyun tasarımına uygun olmalı; server doğrulaması olmadan avantaj sağlayan otomasyon ekleme.

### 9.2 Unity lifecycle

Aşağıdaki durumları test et:

- Uygulama background'a geçer.
- Ekran kilitlenir.
- Ağ Wi-Fi'dan mobile data'ya geçer.
- Bağlantı kesilir ve geri gelir.
- Kullanıcı uygulamayı zorla kapatır.
- İşletim sistemi uygulamayı bellek nedeniyle kapatır.
- Cihaz uyku modundan döner.

Bu olaylarda oyun state'i yerel olarak uydurulmamalı; sunucu reconnect/relogin politikasına göre senkronize edilmelidir.

### 9.3 Performans hedefleri

İlk hedef cihazlar ve ölçümler ürün kararıyla belirlenmelidir. Agent rastgele “60 FPS tüm cihazlarda” gibi garanti vermemeli.

Ölçülecek metrikler:

- CPU/GPU frame time
- FPS ve frame-time spike
- RAM ve native memory
- Draw calls / batches
- Texture/model/scene memory
- Network bytes/sec, packet rate, RTT
- Loading time ve asset decompression time
- Isınma ve uzun süreli throttling
- Batarya tüketimi (cihaz üzerinde ölçüm varsa)

Önce referans Android cihaz belirle; ardından düşük/orta/yüksek sınıf cihazlarda profil çıkar.

---

## 10. Test stratejisi

### 10.1 Protocol unit tests

Her doğrulanmış packet için:

- C# serialize → beklenen golden bytes.
- Golden bytes → C# deserialize → beklenen alanlar.
- Eksik byte dizisi.
- Hatalı length.
- Bilinmeyen header.
- Maksimum boyut sınırı.
- Birden fazla paketin tek TCP buffer'ında gelmesi.
- Tek paketin birkaç read'e bölünmesi.
- Cipher/sequence varsa gerçek kaynakla uyumlu test vektörleri.

Golden bytes eski C++ istemci/server kodundan veya güvenilir, gizli veri içermeyen test capture'larından alınmalı. Gerçek kullanıcı parolası/token'ı test fixture'a koyma.

### 10.2 Integration tests

Test sunucusunda:

1. Auth connect ve handshake.
2. Geçerli/geçersiz login.
3. Server/channel list.
4. Character list/create/select.
5. Game server redirect/connection.
6. World entry ve spawn.
7. Movement send/receive.
8. Disconnect/reconnect.
9. Inventory read ve server-confirmed action.

Her adımda eski PC istemcisiyle regresyon testi yap.

### 10.3 Cross-platform tests

- Unity Editor / Windows standalone.
- Android gerçek cihaz.
- iOS gerçek cihaz (build imzalama ve cihaz testleri uygun ortamda).
- Farklı ağlar ve yüksek latency/packet loss simülasyonu.
- Background/resume.
- Düşük bellek ve uygulama kapatılması.

### 10.4 Test verisi

- Üretim karakterlerini test amacıyla değiştirme.
- Ayrı test hesabı ve test karakterleri kullan.
- Test item/yang işlemlerini staging ortamında yap.
- Test reset script'i varsa içeriğini ve DB etkisini önce incele; veri silen scripti açık onay olmadan çalıştırma.

---

## 11. Sprint planı ve teslimatlar

Bu plan tahminidir; kaynak incelemesi, mevcut build durumu ve ekip kapasitesine göre güncellenmelidir.

### Sprint 0 — Kaynak ve risk audit'i

**İşler**
- Repo/workspace yollarını doğrula.
- Server ve client source build talimatlarını çıkar.
- PC client'ın mevcut sunucuya bağlandığını doğrula.
- Protocol/security akışını iz sürerek dokümante et.
- DB ve server yedekleme/restore prosedürünü belirle.
- Gizli bilgileri repodan ayır ve açığa çıkmış sırlar için rotate planı oluştur.

**Çıkış kriterleri**
- Server/client source map.
- `docs/protocol/connection-flow.md`.
- Paket envanterinin ilk doğrulanmış bölümü.
- Test ortamı ve rollback planı.

### Sprint 1 — Unity foundation

**İşler**
- Unity LTS sürümünü proje gereksinimi ve platformlarla doğrula; sürümü sabitle.
- Git repo, `.gitignore`, asmdef ve test altyapısı.
- Bootstrap ve scene navigation.
- Config/environment katmanı.
- Logging abstraction (secret redaction ile).

**Çıkış kriterleri**
- Unity projesi temiz checkout sonrası açılıyor/build ediliyor.
- EditMode testleri çalışıyor.
- Secret veya yerel credential commit edilmiyor.

### Sprint 2 — Protocol core

**İşler**
- TCP transport.
- Cancellation/shutdown.
- Packet framer.
- Byte reader/writer.
- Phase-aware registry.
- Kaynakta doğrulanmış ilk handshake/security akışı.
- Golden testler.

**Çıkış kriterleri**
- Fragmented/coalesced TCP verisi testleri geçiyor.
- En az bir gerçek protokol paketi byte-for-byte doğrulanmış.
- Bilinmeyen paket/phase davranışı güvenli.

### Sprint 3 — Login ve karakter seçimi

**İşler**
- Auth session.
- Login ekranı.
- Server/channel list.
- Character list/select/create (sunucu destekliyorsa).
- Game endpoint redirect ve ikinci bağlantı.
- Hata ve reconnect UI.

**Çıkış kriterleri**
- Test hesabıyla eski sunucuya bağlanıp karakter seçimine kadar ulaşma.
- Eski PC client login'i bozulmuyor.
- Şifre/token loglanmıyor.

### Sprint 4 — İlk dünya vertical slice

**İşler**
- Tek harita parçası.
- Player spawn.
- Başka oyuncu/NPC/monster görünürlüğü.
- Movement send/receive.
- Kamera ve mobil joystick.
- Entity spawn/despawn lifecycle.

**Çıkış kriterleri**
- Mobil cihazda karakter dünyaya girer ve sunucu hareket state'iyle senkron kalır.
- En az iki client aynı test alanında birbirini görür (mümkünse PC + Unity).

### Sprint 5 — Temel combat ve HUD

**İşler**
- Targeting.
- HP/MP ve temel HUD.
- Normal attack / tek test skill.
- Server-confirmed damage/death.
- Animasyon ve efektin ilk entegrasyonu.

**Çıkış kriterleri**
- Combat sonucu server-authoritative.
- Client-side değer manipülasyonu server state'ini değiştirmiyor.

### Sprint 6 — Inventory ve içerik pipeline'ı

**İşler**
- Item/mob proto dönüştürücü.
- Inventory read/render.
- Equipment görünümü.
- Tek bir item use/equip işlemi.
- Pack/model/texture dönüşüm pipeline'ı.

**Çıkış kriterleri**
- Kaynak proto'dan deterministik client data üretimi.
- En az bir item server ile tutarlı şekilde takılıp çıkarılabiliyor.

### Sonraki sprintler

- NPC/shop, chat, party, guild, trade.
- Quest, dungeon, pet/mount, Dragon Soul, auction.
- Harita/asset kapsamı, kalite ve performans.
- Anti-abuse, telemetry/privacy, crash reporting.
- Store release, signing, privacy disclosures ve platform compliance.

---

## 12. Definition of Done

Bir iş “tamamlandı” sayılabilmesi için:

- [ ] İstenen davranış gerçek kaynak/protokolle eşleştirildi.
- [ ] Değişiklik küçük ve ilgili kapsamda.
- [ ] Hata ve bağlantı kopması durumları ele alındı.
- [ ] Uygun unit/integration testleri eklendi veya test eksikliği açıklandı.
- [ ] Unity main-thread kuralları ihlal edilmiyor.
- [ ] Secret, credential veya kişisel veri loglanmıyor.
- [ ] Android/iOS etkileri değerlendirildi.
- [ ] PC istemci ve sunucu regresyon riski değerlendirildi.
- [ ] Doküman/packet catalog güncellendi.
- [ ] Agent görev sonu raporunu verdi.

---

## 13. Bilinen riskler ve açık sorular

Aşağıdaki maddeler kaynak incelemesi sırasında cevaplanmalıdır. Agent cevabı tahmin etmemeli.

### Protocol
- [ ] Login/auth handshake tam olarak nasıl çalışıyor?
- [ ] Auth ve game bağlantıları ayrı mı; redirect formatı nedir?
- [ ] Packet header/length framing kuralları nelerdir?
- [ ] Şifreleme, sequence, checksum veya key exchange var mı?
- [ ] String encoding ve null terminator/length-prefix kuralları nedir?
- [ ] `#pragma pack` veya compiler-specific struct layout kullanılıyor mu?
- [ ] Client version/CRC kontrolü var mı?
- [ ] PC client ve server aynı kaynak revizyonu mu?

### Server / operations
- [ ] Server kaynak kodu mevcut toolchain ile derlenebiliyor mu?
- [ ] FreeBSD ve MySQL sürümleri kesin olarak nedir?
- [ ] DB backup/restore pratikte test edildi mi?
- [ ] Test/staging sunucusu üretimden ayrılmış mı?
- [ ] Firewall, DB binding ve admin erişimleri güvenli mi?

### Unity / assets
- [ ] Hedef Unity LTS sürümü ve minimum Android/iOS sürümleri nedir?
- [ ] Granny/SpeedTree/Miles ve diğer asset lisansları nedir?
- [ ] EIX/EPK formatı ve extractor lisansı doğrulandı mı?
- [ ] İlk vertical slice için hangi map, karakter sınıfı, mob ve item seçilecek?
- [ ] Hedef düşük/orta seviye cihaz modeli nedir?

### Product
- [ ] İlk release yalnızca Android mi, yoksa Android+iOS+PC mi?
- [ ] Mobilde cross-play ve aynı kanala giriş zorunlu mu?
- [ ] UI dili ve çözünürlük hedefleri nelerdir?
- [ ] Otomatik hedefleme, joystick, kamera ve skill bar tasarımı nasıl olacak?

---

## 14. Agent'a verilecek ilk görev prompt'u

Aşağıdaki prompt'u agent'a bu dosyayla birlikte ver:

```text
Önce AGENT_DEVELOPMENT_GUIDE.md dosyasını baştan sona oku ve kurallarına uy.

Bu ilk görevde henüz büyük implementasyon yapma. Kaynak kod audit'i ve protokol keşfi yap.

1. Workspace içindeki mevcut klasörleri doğrula; dosya yolları yoksa raporla.
2. Server C++ source ve eski Windows client source içinde auth/login/game bağlantı akışını takip et.
3. Packet header, packet length/framing, cipher/sequence ve phase geçişlerini kaynak dosya ve satır referanslarıyla çıkar.
4. Aşağıdaki dokümanları oluştur veya güncelle:
   - docs/architecture.md
   - docs/protocol/connection-flow.md
   - docs/protocol/protocol-inventory.md
5. Paket ID'si, struct size, cipher algoritması veya login akışı hakkında tahmin yürütme. Doğrulanmayan her alanı UNVERIFIED olarak işaretle.
6. Henüz sunucuya veya veritabanına yazma işlemi yapma; temizleme/reset script'i çalıştırma.
7. Sonuçta bulduğun gerçek dosyaları, kritik sınıfları, protokol akışını, belirsizlikleri ve bir sonraki en küçük implementasyon görevini raporla.

Kod değişikliği yaparsan yalnızca dokümantasyon ve güvenli, geri alınabilir audit yardımcılarıyla sınırla. Credential veya secret değerlerini çıktıya yazma.
```

---

## 15. Kaynak dosyalar geldikten sonra ilk protokol uygulaması

Protokol implementasyonuna başlamadan önce aşağıdaki kaynak dosyaların gerçek içerikleri incelenmelidir (dosya adları fork'ta farklı olabilir):

- Server `common/length.h` veya packet header/length tanımlarını içeren dosya.
- Server `common/tables.h` veya packet struct tanımlarını içeren dosya.
- Server `game` içindeki login/main input handler'ları ve descriptor/network kodu.
- Client `UserInterface` içindeki `PythonNetworkStream` / `CPythonNetworkStream` ve packet send/receive tanımları.
- Client security/cipher/handshake kodu.
- Build ayarları ve kullanılan compiler packing/defines.

İlk uygulama paketi, rastgele bir gameplay paketi değil; kaynak koddan en baştan doğrulanmış bağlantı/handshake mesajı olmalıdır. Her implementasyonda C++ tanımı, C# DTO/codec ve golden test birbirine bağlanmalıdır.

---

## 16. Değişiklik yönetimi

- Her özellik için ayrı branch veya küçük commit kullan.
- Packet catalog değişikliklerini kodla aynı commit'te güncelle.
- Protocol wire format değişikliği gerekiyorsa ADR yaz ve eski PC client uyumluluğunu değerlendir.
- Server patch'lerini Unity client değişikliklerinden ayrı commit/patch olarak tut.
- Üretim server config ve credential dosyalarını kaynak kontrolüne ekleme.
- Binary asset ve büyük dosyalar için Git LFS veya uygun asset storage kararını bilinçli ver.
- Her dependency eklemesinde lisans, platform desteği, bakım durumu ve boyut etkisini not et.

---

**Son not:** Bu proje, yalnızca Unity'de bir karakter kontrolü yazmaktan ibaret değildir. Başarı için en kritik ve riskli iş, eski istemci ile C++ sunucu arasındaki gerçek protokolü ve var olan asset formatlarını doğru anlamaktır. Agent'ın ilk görevi kod üretmek değil, kaynak koddan doğrulanmış bir bağlantı ve paket haritası çıkarmak olmalıdır.
