using System;

namespace Metin2.Protocol.Security
{
    /// <summary>
    /// C# port of the Cipher session object (cipher.h / NetStream polarity wiring):
    /// holds derived key material, assigns encoder/decoder directions by polarity,
    /// and gates Encrypt/Decrypt on activation.
    ///
    /// Polarity rule (cipher.cpp:232-238): client (polarity true, NetStream.cpp:923)
    /// encodes with direction-1 and decodes with direction-0; server
    /// (polarity false, desc.cpp:735) mirrors it.
    /// Activation rule: traffic passes through the transform only after
    /// ActivateCipher (client, on 0xfa) / Activate (server, before phase switch);
    /// before that Encrypt/Decrypt are silent no-ops, mirroring cipher.h.
    ///
    /// Block-cipher engines are not yet implemented: with a null engine factory
    /// the session derives everything but throws InvalidOperationException on
    /// first activated use, with a message pointing at cipher-spec.md §6.
    /// No UnityEngine dependency.
    /// </summary>
    public class CipherSession : IDisposable
    {
        private readonly Func<CipherDirectionMaterial, IBlockCipherEngine> _engineFactory;
        private CtrStream _encoder;
        private CtrStream _decoder;
        private bool _disposed;

        /// <summary>Client polarity is true (NetStream.cpp:923); server false (desc.cpp:735).</summary>
        public bool IsClientPolarity { get; }

        public bool Activated { get; private set; }

        public CipherDirectionMaterial EncoderMaterial { get; }

        public CipherDirectionMaterial DecoderMaterial { get; }

        public CipherSession(bool isClientPolarity, CipherKeyMaterial material, Func<CipherDirectionMaterial, IBlockCipherEngine> engineFactory = null)
        {
            IsClientPolarity = isClientPolarity;
            _engineFactory = engineFactory;

            // Mirrors SetUp polarity assignment (cipher.cpp:232-238).
            EncoderMaterial = isClientPolarity ? material.Direction1 : material.Direction0;
            DecoderMaterial = isClientPolarity ? material.Direction0 : material.Direction1;
        }

        /// <summary>Mirrors ActivateCipher()/Activate: arms the transform.</summary>
        public void SetActivated(bool value)
        {
            ThrowIfDisposed();
            EnsureStreams();
            Activated = value;
        }

        /// <summary>Mirrors Cipher::Encrypt: in-place, no padding, no-op unless activated.</summary>
        public void Encrypt(byte[] buffer, int offset, int count)
        {
            if (!Activated)
            {
                return;
            }

            ThrowIfDisposed();
            EnsureStreams();
            _encoder.ProcessData(buffer, offset, count);
        }

        /// <summary>Mirrors Cipher::Decrypt: in-place, no padding, no-op unless activated.</summary>
        public void Decrypt(byte[] buffer, int offset, int count)
        {
            if (!Activated)
            {
                return;
            }

            ThrowIfDisposed();
            EnsureStreams();
            _decoder.ProcessData(buffer, offset, count);
        }

        /// <summary>Mirrors Cipher::CleanUp: drops stream state and deactivates.</summary>
        public void CleanUp()
        {
            _encoder = null;
            _decoder = null;
            Activated = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Wipe(EncoderMaterial.Key);
            Wipe(EncoderMaterial.IV);
            Wipe(DecoderMaterial.Key);
            Wipe(DecoderMaterial.IV);
            CleanUp();
        }

        private void EnsureStreams()
        {
            if (_encoder != null)
            {
                return;
            }

            if (_engineFactory == null)
            {
                throw new InvalidOperationException(
                    "No block-cipher engine registered. The 15 CryptoPP engines are not yet ported " +
                    "(docs/protocol/cipher-spec.md §6); pass an engine factory to enable traffic.");
            }

            IBlockCipherEngine encoderEngine = _engineFactory(EncoderMaterial);
            IBlockCipherEngine decoderEngine = _engineFactory(DecoderMaterial);
            if (encoderEngine == null || decoderEngine == null)
            {
                throw new InvalidOperationException(
                    $"Engine factory returned null for suite {CipherSuiteTable.GetName(EncoderMaterial.Suite)}/{CipherSuiteTable.GetName(DecoderMaterial.Suite)}.");
            }

            _encoder = new CtrStream(encoderEngine, EncoderMaterial.IV);
            _decoder = new CtrStream(decoderEngine, DecoderMaterial.IV);
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
                throw new ObjectDisposedException(nameof(CipherSession));
            }
        }
    }
}
