# Cipher / key-agreement spec (source-verified)

No guesses. Anything not proven below is marked **UNVERIFIED**.
No secrets. All group parameters are public RFC 5114 constants quoted from source.

## 0. Switch

`Server/common/service.h:6` defines `_IMPROVED_PACKET_ENCRYPTION_`.
When OFF, the legacy TEA path in `EterLib/NetStream.cpp` is used instead
(this spec covers only the ON path, which is active in this build).

## 1. Cipher class (both sides)

- Server: `Server/game/src/cipher.h:12-56`
- Client: `Client Source/source/EterBase/cipher.h:15-58` (same shape)
- API: `Prepare(buffer, length)` → agreed length or 0;
  `Activate(polarity, agreed_length, buffer, length)` → bool;
  `Encrypt`/`Decrypt` in-place, no padding, silent no-op unless activated;
  `activated()` / `set_activated(bool)`.
- Key agreement object is always `DH2KeyAgreement` (`cipher.cpp:155`).

## 2. DH2 key agreement

Impl: `Server/game/src/cipher.cpp:301-398`
(`DH2KeyAgreement::Prepare` 307-378, `Agree` 380-398).

- Group: **RFC 5114, 1024-bit MODP with 160-bit prime-order subgroup**
  (`cipher.cpp:308-309`), p/q/g literals at `cipher.cpp:310-324`.
- `Prepare` validates the group (`ValidateGroup`, subgroup order `g^q == 1`,
  `cipher.cpp:338-352`), generates a static and an ephemeral key pair,
  writes `spub || epub` into the caller's buffer, returns
  `dh2_.AgreedValueLength()`.
- Sizes (1024-bit p, corroborated by `MAX_DATA_LEN = 256` exact fit,
  `packet.h:2228`): static/ephemeral public halves **128 bytes** each,
  key data **256 bytes** total, agreed shared secret **256 bytes**
  (`DH2::AgreedValueLength() = d1 + d2`, `cryptopp/dh2.h:34-35`).
- `Agree` fails closed: `agreed_length != AgreedValueLength()` → false;
  `length != spub + epub` → false (`cipher.cpp:381-389`).
- Agreed layout **static-agreed || ephemeral-agreed** — PARTIALLY VERIFIED:
  strongly implied by `dh2.h` (d1=static composed first, `Agree` takes static
  keys first), but `dh2.cpp` is not vendored in this tree; final proof is a
  live-server encrypted-packet decode.

## 3. Suite selection + key/IV derivation (`Cipher::SetUp`, cipher.cpp:180-242)

- `shared.size() < 2` → fail.
- `hint_0 = shared[shared[0] % size]`, `hint_1 = shared[shared[1] % size]`
  (`cipher.cpp:189-190`).
- `Pick(hint % kMaxAlgorithms)` with `kMaxAlgorithms = 14`
  (`cipher.cpp:244-299`). Selector → cipher (all VERIFIED from vendored
  CryptoPP headers, `DEFAULT_KEYLENGTH` / `BLOCKSIZE`):

| Selector | Enum        | Cipher   | Key len | Block/IV len | Source header     |
|----------|-------------|----------|---------|--------------|-------------------|
| 0        | kDefault    | Twofish  | 16      | 16           | twofish.h         |
| 1        | kRC6        | RC6      | 16      | 16           | rc6.h             |
| 2        | kMARS       | MARS     | 16      | 16           | mars.h            |
| 3        | kTwofish    | Twofish  | 16      | 16           | twofish.h         |
| 4        | kSerpent    | Serpent  | 16      | 16           | serpent.h         |
| 5        | kCAST256    | CAST-256 | 16      | 16           | cast.h            |
| 6        | kIDEA       | IDEA     | 16      | 8            | idea.h            |
| 7        | k3DES       | DES-EDE2 | 16      | 8            | des.h             |
| 8        | kCamellia   | Camellia | 16      | 16           | camellia.h        |
| 9        | kSEED       | SEED     | 16      | 16           | seed.h            |
| 10       | kRC5        | RC5      | 16      | 8            | rc5.h             |
| 11       | kBlowfish   | Blowfish | 16      | 8            | blowfish.h        |
| 12       | kTEA        | TEA      | 16      | 8            | tea.h (not XTEA)  |
| 13       | kSHACAL2    | SHACAL-2 | 16      | 32           | shacal2.h         |

  (`VariableKeyLength<D,...>` → DEFAULT=D per `seckey.h`; every suite above
  has D=16. `switch` default also maps to Twofish, `cipher.cpp:293-296`.)
- Key/IV slicing (`cipher.cpp:214-228`, note non-Windows `min()` branch):
  `key_0 = shared[0..16)`; `key_1 = shared[min(16, size-16)..+16)`;
  `iv_0` = last `iv_length_0` bytes; `iv_1` ends `iv_length_1` bytes before
  `iv_0` starts (or at 0 if overlapping).
- Polarity (`cipher.cpp:232-238`): `polarity == true` →
  encoder=(suite_1/key_1/iv_1), decoder=(suite_0/key_0/iv_0);
  `false` → mirrored.
- Both directions run CTR mode (`CTR_Mode<T>::Encryption/Decryption`,
  `cipher.cpp:86-93`), so encryption and decryption are the same keystream-XOR
  operation; only the (suite,key,iv) assignment differs per direction.

## 4. Wire sequence

Server (`desc.cpp`, `input.cpp`):
- `SendKeyAgreement` (`desc.cpp:704-722`): `Prepare` → GC 0xfb
  (`wAgreedLength`, `wDataLength`, `data[256]`; `packet.h:2226-2233`).
  Prepare failure → `PHASE_CLOSE`.
- On CG 0xfb (`input.cpp:556-583`): send `0xfa` **first**, flush output
  (`ProcessOutput`), THEN `FinishHandshake` = `Activate(false, ...)`
  (`desc.cpp:733-736`, **server polarity false**). Cipher required to be
  prepared, else disconnect. Success → `PHASE_AUTH` (auth core) or
  `PHASE_LOGIN` (channel core); failure → `PHASE_CLOSE`.
- Post-handshake RX/TX passes through cipher only when activated
  (`desc.cpp:284-285` decrypt, `desc.cpp:460-461` encrypt).

Client (`PhaseHandShake.cpp`, `NetStream.cpp`):
- On GC 0xfb (`PhaseHandShake.cpp:202-244`): `Prepare` own keys →
  `Activate(wAgreedLength, data, wDataLength)` = `Activate(polarity=true, ...)`
  (`NetStream.cpp:921-924`, **client polarity true**) → send CG 0xfb.
  Failure → disconnect.
- On `0xfa` (`PhaseHandShake.cpp:246-258`): `ActivateCipher()` =
  `set_activated(true)` (`NetStream.cpp:926-928`).
- Post-activation: recv decrypts (`NetStream.cpp:109`), send encrypts
  (`NetStream.cpp:211`).

## 5. Packet layouts

- `TPacketKeyAgreement`: `BYTE bHeader` (0xfb), `WORD wAgreedLength`,
  `WORD wDataLength`, `BYTE data[256]` (`packet.h:2226-2233`).
- `TPacketKeyAgreementCompleted`: `BYTE bHeader` (0xfa) + 3 dummy bytes
  (`packet.h:2235-2239`).

## 6. UNVERIFIED / deferred

- DH2 agreed-half order (see §2): header-implied, needs live confirmation.
- CTR counter increment byte order (`modes.cpp` not vendored; `modes.h:224-247`
  declares the policy only). C# port uses standard big-endian CTR
  (NIST SP 800-38A); confirm against first live encrypted packet.
- The 15 block-cipher engines are NOT yet implemented. Each is independently
  verifiable against official algorithm KATs (all are AES-candidate / standard
  ciphers). Options evaluated: (a) port from CryptoPP reference — large but
  exact; (b) BouncyCastle C# dependency — incomplete (no SHACAL-2/RC5) and
  needs license/platform/size review per guide §16, so NOT adopted without
  approval; (c) native CryptoPP P/Invoke — platform packaging cost for
  Android/iOS. Decision deferred to engine epic.
- Sequence/anti-replay: no explicit counter found in the framing path
  (see connection-flow.md §2); CTR keystream position is the implicit state.
