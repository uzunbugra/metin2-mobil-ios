using System;
using System.Numerics;
using System.Security.Cryptography;

namespace Metin2.Protocol.Security
{
    /// <summary>
    /// C# port of DH2KeyAgreement (Server/game/src/cipher.cpp:301-398):
    /// Unified Diffie-Hellman with a static and an ephemeral key pair over
    /// <see cref="DiffieHellmanGroup"/>. Wire behavior mirrors Prepare/Agree:
    /// public data is spub(128) || epub(128); the shared secret is
    /// static-agreed(128) || ephemeral-agreed(128).
    ///
    /// Agreed-half order is PARTIALLY VERIFIED (see docs/protocol/cipher-spec.md §2):
    /// header-implied static-first; final proof is a live encrypted-packet decode.
    /// No UnityEngine dependency. Dispose to wipe private keys.
    /// </summary>
    public class Dh2KeyAgreement : IDisposable
    {
        private byte[] _staticPrivate;
        private byte[] _ephemeralPrivate;
        private bool _disposed;

        private Dh2KeyAgreement(byte[] staticPrivate, byte[] ephemeralPrivate)
        {
            _staticPrivate = staticPrivate;
            _ephemeralPrivate = ephemeralPrivate;
        }

        /// <summary>
        /// Generates a fresh static + ephemeral private key pair
        /// (mirrors GenerateStaticKeyPair/GenerateEphemeralKeyPair).
        /// </summary>
        public static Dh2KeyAgreement Generate()
        {
            return new Dh2KeyAgreement(RandomExponent(), RandomExponent());
        }

        /// <summary>
        /// Exports spub(128) || epub(128), the 256-byte key data carried by
        /// TPacketKeyAgreement (mirrors the Prepare output buffer).
        /// </summary>
        public byte[] ExportPublicData()
        {
            ThrowIfDisposed();
            byte[] result = new byte[DiffieHellmanGroup.KeyDataLength];
            byte[] spub = PublicKey(PrivateScalar(_staticPrivate));
            byte[] epub = PublicKey(PrivateScalar(_ephemeralPrivate));
            System.Buffer.BlockCopy(spub, 0, result, 0, spub.Length);
            System.Buffer.BlockCopy(epub, 0, result, spub.Length, epub.Length);
            return result;
        }

        /// <summary>
        /// Derives the 256-byte shared secret from the peer's public data.
        /// Fails closed (returns false) on length mismatch or invalid peer keys,
        /// mirroring DH2KeyAgreement::Agree (cipher.cpp:380-398) and the server's
        /// agreed_length/data_length checks (input.cpp:571-582).
        /// </summary>
        public bool TryAgree(ushort agreedLength, byte[] peerData, out byte[] shared)
        {
            shared = null;
            if (_disposed)
            {
                return false;
            }

            if (agreedLength != DiffieHellmanGroup.AgreedValueLength)
            {
                return false;
            }

            if (peerData == null || peerData.Length != DiffieHellmanGroup.KeyDataLength)
            {
                return false;
            }

            BigInteger staticPeer = DiffieHellmanGroup.FromBigEndian(peerData, 0, DiffieHellmanGroup.ModulusByteLength);
            BigInteger ephemeralPeer = DiffieHellmanGroup.FromBigEndian(peerData, DiffieHellmanGroup.ModulusByteLength, DiffieHellmanGroup.ModulusByteLength);

            if (!IsValidPeerKey(staticPeer) || !IsValidPeerKey(ephemeralPeer))
            {
                return false;
            }

            byte[] staticAgreed = DiffieHellmanGroup.ToFixedBigEndian(
                BigInteger.ModPow(staticPeer, PrivateScalar(_staticPrivate), DiffieHellmanGroup.P),
                DiffieHellmanGroup.ModulusByteLength);
            byte[] ephemeralAgreed = DiffieHellmanGroup.ToFixedBigEndian(
                BigInteger.ModPow(ephemeralPeer, PrivateScalar(_ephemeralPrivate), DiffieHellmanGroup.P),
                DiffieHellmanGroup.ModulusByteLength);

            shared = new byte[DiffieHellmanGroup.AgreedValueLength];
            System.Buffer.BlockCopy(staticAgreed, 0, shared, 0, staticAgreed.Length);
            System.Buffer.BlockCopy(ephemeralAgreed, 0, shared, staticAgreed.Length, ephemeralAgreed.Length);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Wipe(_staticPrivate);
            Wipe(_ephemeralPrivate);
            _staticPrivate = null;
            _ephemeralPrivate = null;
        }

        private static byte[] PublicKey(BigInteger privateScalar)
        {
            return DiffieHellmanGroup.ToFixedBigEndian(
                BigInteger.ModPow(DiffieHellmanGroup.G, privateScalar, DiffieHellmanGroup.P),
                DiffieHellmanGroup.ModulusByteLength);
        }

        private static BigInteger PrivateScalar(byte[] privateKey)
        {
            return DiffieHellmanGroup.FromBigEndian(privateKey, 0, privateKey.Length);
        }

        /// <summary>
        /// Random exponent in [2, q-2]. Any value in [1, q-1] interoperates;
        /// the exact CryptoPP sampling distribution is irrelevant on the wire.
        /// </summary>
        private static byte[] RandomExponent()
        {
            BigInteger range = DiffieHellmanGroup.Q - 3;
            byte[] candidate = new byte[DiffieHellmanGroup.SubgroupByteLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                while (true)
                {
                    rng.GetBytes(candidate);
                    BigInteger value = DiffieHellmanGroup.FromBigEndian(candidate, 0, candidate.Length);
                    if (value < range)
                    {
                        return DiffieHellmanGroup.ToFixedBigEndian(
                            value + 2, DiffieHellmanGroup.SubgroupByteLength);
                    }
                }
            }
        }

        /// <summary>
        /// Fail-closed peer key check: subgroup element in [2, p-2] with y^q == 1 (mod p),
        /// matching the subgroup-order verification pattern of Prepare (cipher.cpp:348-352).
        /// </summary>
        private static bool IsValidPeerKey(BigInteger y)
        {
            if (y < 2 || y > DiffieHellmanGroup.P - 2)
            {
                return false;
            }

            return BigInteger.ModPow(y, DiffieHellmanGroup.Q, DiffieHellmanGroup.P) == BigInteger.One;
        }

        private static void Wipe(byte[] key)
        {
            if (key != null)
            {
                Array.Clear(key, 0, key.Length);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Dh2KeyAgreement));
            }
        }
    }
}
