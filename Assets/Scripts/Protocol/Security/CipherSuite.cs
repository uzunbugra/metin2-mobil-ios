namespace Metin2.Protocol.Security
{
    /// <summary>
    /// Block-cipher selector enum, mirroring BlockCipherAlgorithm
    /// (Server/game/src/cipher.cpp:37-60). kDefault/kMaxAlgorithms included
    /// so Pick() ports hint % kMaxAlgorithms exactly.
    /// </summary>
    public enum CipherSuite
    {
        Default = 0,
        RC6 = 1,
        MARS = 2,
        Twofish = 3,
        Serpent = 4,
        CAST256 = 5,
        IDEA = 6,
        TripleDES = 7,
        Camellia = 8,
        SEED = 9,
        RC5 = 10,
        Blowfish = 11,
        TEA = 12,
        SHACAL2 = 13
    }

    /// <summary>
    /// Source-verified suite table: selector mapping (cipher.cpp:244-299) plus
    /// DEFAULT_KEYLENGTH / BLOCKSIZE per suite from the vendored CryptoPP
    /// headers (see docs/protocol/cipher-spec.md §3). IV length equals the
    /// block size — SetUp uses GetBlockSize() for IVs (cipher.cpp:199/204).
    /// </summary>
    public static class CipherSuiteTable
    {
        /// <summary>kMaxAlgorithms sentinel (cipher.cpp:58).</summary>
        public const int SuiteCount = 14;

        /// <summary>
        /// Exact port of BlockCipherAlgorithm::Pick: selector = hint % 14,
        /// unknown selectors fall through to the Twofish default
        /// (cipher.cpp:293-296). Hint is always non-negative on the wire
        /// (shared-secret bytes); negatives map to the default as well.
        /// </summary>
        public static CipherSuite Pick(int hint)
        {
            int selector = hint % SuiteCount;
            switch (selector)
            {
                case (int)CipherSuite.RC6: return CipherSuite.RC6;
                case (int)CipherSuite.MARS: return CipherSuite.MARS;
                case (int)CipherSuite.Twofish: return CipherSuite.Twofish;
                case (int)CipherSuite.Serpent: return CipherSuite.Serpent;
                case (int)CipherSuite.CAST256: return CipherSuite.CAST256;
                case (int)CipherSuite.IDEA: return CipherSuite.IDEA;
                case (int)CipherSuite.TripleDES: return CipherSuite.TripleDES;
                case (int)CipherSuite.Camellia: return CipherSuite.Camellia;
                case (int)CipherSuite.SEED: return CipherSuite.SEED;
                case (int)CipherSuite.RC5: return CipherSuite.RC5;
                case (int)CipherSuite.Blowfish: return CipherSuite.Blowfish;
                case (int)CipherSuite.TEA: return CipherSuite.TEA;
                case (int)CipherSuite.SHACAL2: return CipherSuite.SHACAL2;
                case (int)CipherSuite.Default:
                default: return CipherSuite.Twofish;
            }
        }

        /// <summary>
        /// GetDefaultKeyLength() for every selectable suite is 16
        /// (all FixedKeyLength&lt;16&gt; / VariableKeyLength&lt;16,...&gt;).
        /// </summary>
        public static int GetDefaultKeyLength(CipherSuite suite)
        {
            return 16;
        }

        /// <summary>GetBlockSize() per suite; also the CTR IV length.</summary>
        public static int GetBlockSize(CipherSuite suite)
        {
            switch (suite)
            {
                case CipherSuite.IDEA:
                case CipherSuite.TripleDES:
                case CipherSuite.RC5:
                case CipherSuite.Blowfish:
                case CipherSuite.TEA:
                    return 8;
                case CipherSuite.SHACAL2:
                    return 32;
                default:
                    return 16;
            }
        }

        public static string GetName(CipherSuite suite)
        {
            switch (suite)
            {
                case CipherSuite.Default: return "Twofish(default)";
                case CipherSuite.RC6: return "RC6";
                case CipherSuite.MARS: return "MARS";
                case CipherSuite.Twofish: return "Twofish";
                case CipherSuite.Serpent: return "Serpent";
                case CipherSuite.CAST256: return "CAST-256";
                case CipherSuite.IDEA: return "IDEA";
                case CipherSuite.TripleDES: return "DES-EDE2";
                case CipherSuite.Camellia: return "Camellia";
                case CipherSuite.SEED: return "SEED";
                case CipherSuite.RC5: return "RC5";
                case CipherSuite.Blowfish: return "Blowfish";
                case CipherSuite.TEA: return "TEA";
                case CipherSuite.SHACAL2: return "SHACAL-2";
                default: return "Unknown";
            }
        }
    }
}
