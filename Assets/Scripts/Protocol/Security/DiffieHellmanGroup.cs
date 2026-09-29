using System.Numerics;

namespace Metin2.Protocol.Security
{
    /// <summary>
    /// RFC 5114 1024-bit MODP group with 160-bit prime-order subgroup, quoted
    /// verbatim from Server/game/src/cipher.cpp:310-324
    /// (DH2KeyAgreement::Prepare). Public parameters, no secrets here.
    /// </summary>
    public static class DiffieHellmanGroup
    {
        // cipher.cpp:310-315
        private const string P_Hex =
            "B10B8F96A080E01DDE92DE5EAE5D54EC52C99FBCFB06A3C6" +
            "9A6A9DCA52D23B616073E28675A23D189838EF1E2EE652C0" +
            "13ECB4AEA906112324975C3CD49B83BFACCBDD7D90C4BD70" +
            "98488E9C219A73724EFFD6FAE5644738FAA31A4FF55BCCC0" +
            "A151AF5F0DC8B4BD45BF37DF365C1A65E68CFDA76D4DA708" +
            "DF1FB2BC2E4A4371";

        // cipher.cpp:317-322
        private const string G_Hex =
            "A4D1CBD5C3FD34126765A442EFB99905F8104DD258AC507F" +
            "D6406CFF14266D31266FEA1E5C41564B777E690F5504F213" +
            "160217B4B01B886A5E91547F9E2749F4D7FBD7D3B9A92EE1" +
            "909D0D2263F80A76A6A24C087A091F531DBF0A0169B6A28A" +
            "D662A4D18E73AFA32D779D5918D08BC8858F4DCEF97C2A24" +
            "855E6EEB22B3B2E5";

        // cipher.cpp:324
        private const string Q_Hex = "F518AA8781A8DF278ABA4E7D64B7CB9D49462353";

        /// <summary>Prime modulus p (1024-bit).</summary>
        public static readonly BigInteger P = ParseHex(P_Hex);

        /// <summary>Generator g.</summary>
        public static readonly BigInteger G = ParseHex(G_Hex);

        /// <summary>Prime subgroup order q (160-bit).</summary>
        public static readonly BigInteger Q = ParseHex(Q_Hex);

        /// <summary>Modulus length: 1024 bits = 128 bytes (one DH public key / agreed half).</summary>
        public const int ModulusByteLength = 128;

        /// <summary>Subgroup order length: 160 bits = 20 bytes (private key size).</summary>
        public const int SubgroupByteLength = 20;

        /// <summary>DH2 public data length: spub(128) || epub(128) = 256 = MAX_DATA_LEN (packet.h:2228).</summary>
        public const int KeyDataLength = 256;

        /// <summary>DH2 agreed value length: static(128) || ephemeral(128) = 256.</summary>
        public const int AgreedValueLength = 256;

        public static BigInteger ParseHex(string hex)
        {
            BigInteger value = BigInteger.Zero;
            for (int i = 0; i < hex.Length; i++)
            {
                char c = hex[i];
                int nibble = c >= '0' && c <= '9' ? c - '0'
                    : c >= 'A' && c <= 'F' ? c - 'A' + 10
                    : c >= 'a' && c <= 'f' ? c - 'a' + 10
                    : throw new System.ArgumentException($"Invalid hex character '{c}'.", nameof(hex));
                value = (value << 4) | nibble;
            }
            return value;
        }

        /// <summary>Encodes a non-negative value as fixed-length big-endian (CryptoPP Integer encoding).</summary>
        public static byte[] ToFixedBigEndian(BigInteger value, int length)
        {
            if (value.Sign < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), "Value must be non-negative.");
            }

            byte[] result = new byte[length];
            BigInteger rest = value;
            for (int i = length - 1; i >= 0 && rest > BigInteger.Zero; i--)
            {
                result[i] = (byte)(rest & 0xFF);
                rest >>= 8;
            }

            if (rest > BigInteger.Zero)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), "Value does not fit in the requested length.");
            }

            return result;
        }

        /// <summary>Decodes fixed-length big-endian bytes to a non-negative value.</summary>
        public static BigInteger FromBigEndian(byte[] data, int offset, int length)
        {
            BigInteger value = BigInteger.Zero;
            for (int i = 0; i < length; i++)
            {
                value = (value << 8) | data[offset + i];
            }
            return value;
        }
    }
}
