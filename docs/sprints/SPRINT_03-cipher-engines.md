# Sprint 3 — Cipher Engine'leri (kayıt)

Hedef: 15 block-cipher engine'ini CryptoPP-birebir port + resmi KAT ile doğrulamak.
Strateji kararı: `docs/decisions/ADR-0002-cipher-engine-strategy.md`.
Altyapı hazır: `IBlockCipherEngine` seam, `CtrStream`, `CipherSession`, `BlockCipherEngineFactory`.

## Engine başına DoD

- [ ] Port, seçili suite'in `CipherSuiteTable` uzunluklarıyla tutarlı (key 16, block tabloya uygun).
- [ ] En az 2 bağımsız KAT vektörü (encrypt + decrypt) testte, kaynak belirtilmiş.
- [ ] `BlockCipherEngineFactory`'de suite eşleşmesi + factory testi.
- [ ] CTR üzerinden 1 round-trip testi (session veya stream seviyesi).

## Kontrol listesi

| # | Suite | Cipher | Block | KAT kaynağı | Durum |
|---|---|---|---|---|---|
| 12 | TEA | TEA (orijinal, XTEA değil) | 8 | Wheeler–Needham + bağımsız vektör seti (7 vektör) | ✅ Tamamlandı |
| 11 | Blowfish | Blowfish | 8 | Schneier KAT'leri | ⬜ Sırada |
| 7 | TripleDES | DES-EDE2 | 8 | NIST SP 800-67 | ⬜ Sırada |
| 3 | Twofish | Twofish | 16 | AES aday KAT'leri (NIST) | ⬜ Sırada |
| 4 | Serpent | Serpent | 16 | AES aday KAT'leri (NIST) | ⬜ Sırada |
| 1 | RC6 | RC6 | 16 | AES aday KAT'leri (NIST) | ⬜ Sırada |
| 2 | MARS | MARS | 16 | AES aday KAT'leri (NIST) | ⬜ Sırada |
| 5 | CAST256 | CAST-256 | 16 | RFC 2612 | ⬜ Sırada |
| 8 | Camellia | Camellia | 16 | RFC 3713 / NESSIE | ⬜ Sırada |
| 9 | SEED | SEED | 16 | RFC 4269 / KISA | ⬜ Sırada |
| 6 | IDEA | IDEA | 8 | Lai/Massey test vektörleri | ⬜ Sırada |
| 10 | RC5 | RC5 | 8 | Rivest RC5-32/12/16 vektörleri | ⬜ Sırada |
| 13 | SHACAL2 | SHACAL-2 | 32 | NESSIE submission vektörleri | ⬜ Sırada |

Not: `kDefault` (0) Twofish'e eşlenir — ayrı engine gerekmez.

## Tamamlanma kriteri (epic)

- 13 engine + factory + her biri için KAT testleri yeşil.
- Canlı sunucuya handshake → key agreement → ilk şifreli paket decode (bu adım DH2 yarı
  sırası + CTR byte order UNVERIFIED'larını da kapatır).
- GC ping header çelişkisi (0xfe vs 44) ayrıca çözülecek.
