using System;

namespace Metin2.Protocol.Security
{
    /// <summary>
    /// Derived (suite, key, IV) material for one direction.
    /// </summary>
    public struct CipherDirectionMaterial
    {
        public CipherSuite Suite;
        public byte[] Key;
        public byte[] IV;
    }

    /// <summary>
    /// Derived material for both directions (mirrors SetUp locals).
    /// </summary>
    public struct CipherKeyMaterial
    {
        public CipherDirectionMaterial Direction0;
        public CipherDirectionMaterial Direction1;
    }

    /// <summary>
    /// Exact port of Cipher::SetUp (Server/game/src/cipher.cpp:180-242):
    /// suite hints from the shared secret, suite picks, then key/IV slicing
    /// with the same min()/overlap rules. Fails closed (false) on short input,
    /// mirroring the size guards. Direction assignment (polarity) is applied by
    /// <see cref="CipherSession"/>, not here.
    /// No UnityEngine dependency.
    /// </summary>
    public static class CipherKeyDerivation
    {
        public static bool TryDerive(byte[] shared, out CipherKeyMaterial material)
        {
            material = default;

            if (shared == null || shared.Length < 2)
            {
                return false;
            }

            int size = shared.Length;
            int hint0 = shared[shared[0] % size];
            int hint1 = shared[shared[1] % size];

            CipherSuite suite0 = CipherSuiteTable.Pick(hint0);
            CipherSuite suite1 = CipherSuiteTable.Pick(hint1);

            int keyLength0 = CipherSuiteTable.GetDefaultKeyLength(suite0);
            int ivLength0 = CipherSuiteTable.GetBlockSize(suite0);
            if (size < keyLength0 || size < ivLength0)
            {
                return false;
            }

            int keyLength1 = CipherSuiteTable.GetDefaultKeyLength(suite1);
            int ivLength1 = CipherSuiteTable.GetBlockSize(suite1);
            if (size < keyLength1 || size < ivLength1)
            {
                return false;
            }

            byte[] key0 = new byte[keyLength0];
            System.Buffer.BlockCopy(shared, 0, key0, 0, keyLength0);

            int offset = Math.Min(keyLength0, size - keyLength1);
            byte[] key1 = new byte[keyLength1];
            System.Buffer.BlockCopy(shared, offset, key1, 0, keyLength1);

            offset = size - ivLength0;
            byte[] iv0 = new byte[ivLength0];
            System.Buffer.BlockCopy(shared, offset, iv0, 0, ivLength0);

            offset = offset < ivLength1 ? 0 : offset - ivLength1;
            byte[] iv1 = new byte[ivLength1];
            System.Buffer.BlockCopy(shared, offset, iv1, 0, ivLength1);

            material = new CipherKeyMaterial
            {
                Direction0 = new CipherDirectionMaterial { Suite = suite0, Key = key0, IV = iv0 },
                Direction1 = new CipherDirectionMaterial { Suite = suite1, Key = key1, IV = iv1 }
            };
            return true;
        }
    }
}
