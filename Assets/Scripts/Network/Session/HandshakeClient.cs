using System;
using System.Threading;
using System.Threading.Tasks;
using Metin2.Network.Transport;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Framing;
using Metin2.Protocol.Packets;
using Metin2.Protocol.Security;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Network.Session
{
    /// <summary>
    /// Client-side handshake orchestrator (auth/channel core entry).
    /// Wire sequence (docs/protocol/connection-flow.md §1-2, cipher-spec.md §4):
    ///   GC 0xff (13B, plaintext) → GC 0xfb (261B, plaintext) →
    ///   derive (DH2 agree + SetUp port) → send CG 0xfb (261B, plaintext) →
    ///   await GC 0xfa (4B, plaintext) → activate cipher (client polarity true).
    ///
    /// Source anchors:
    /// - Server sends: desc.cpp:615-640 (0xff), desc.cpp:704-722 (0xfb),
    ///   input.cpp:556-583 (0xfa first + flush, then Activate(false)).
    /// - Client drives: PhaseHandShake.cpp:202-258, NetStream.cpp:921-928
    ///   (Activate polarity true on GC 0xfb, set_activated(true) on 0xfa).
    /// - Crypto: cipher.cpp:301-398 (DH2), cipher.cpp:180-242 (SetUp).
    ///
    /// Fail-closed: any length/key/order/close violation throws
    /// <see cref="HandshakeFailedException"/>; no partial session is exposed.
    /// Post-handshake traffic uses <see cref="SendSecureAsync"/> /
    /// <see cref="ReceiveSecureFrameAsync"/> (decrypt-before-frame).
    /// Single-threaded per instance; the socket thread never touches Unity APIs.
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class HandshakeClient : IDisposable
    {
        private const int ReceiveChunkSize = 4096;

        private readonly ITcpConnection _connection;
        private readonly PacketFramer _plainFramer = new PacketFramer();
        private readonly PacketFramer _secureFramer = new PacketFramer();
        private Dh2KeyAgreement _agreement;
        private bool _disposed;

        /// <summary>GC 0xff payload from the server (time-sync source).</summary>
        public PacketGCHandshake ServerHandshake { get; private set; }

        public bool HasServerHandshake { get; private set; }

        /// <summary>Activated client session (polarity true). Null until <see cref="RunAsync"/> succeeds.</summary>
        public CipherSession Session { get; private set; }

        public bool Completed { get; private set; }

        /// <summary>
        /// Raised on every server phase transition (GC_PHASE 0xfd, pushed by
        /// DESC::SetPhase on every phase change, desc.cpp:518). Raised from the
        /// receive thread — Unity subscribers must marshal to the main thread.
        /// </summary>
        public event Action<PhaseType> PhaseChanged;

        public HandshakeClient(ITcpConnection connection, Dh2KeyAgreement agreement = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _agreement = agreement ?? Dh2KeyAgreement.Generate();
        }

        /// <summary>
        /// Runs the full handshake on an already-connected <see cref="ITcpConnection"/>.
        /// Returns the activated <see cref="CipherSession"/> (also exposed via <see cref="Session"/>).
        /// </summary>
        public async Task<CipherSession> RunAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (Completed)
            {
                throw new InvalidOperationException("Handshake already completed on this instance.");
            }

            if (!_connection.IsConnected)
            {
                throw new HandshakeFailedException("Connection is not established; connect before handshake.");
            }

            try
            {
                byte[] handshakeFrame = await ReceiveFrameAsync(
                    PacketHeaders.HEADER_GC_HANDSHAKE, _plainFramer, cancellationToken).ConfigureAwait(false);
                ServerHandshake = PacketGCHandshakeCodec.Deserialize(handshakeFrame);
                HasServerHandshake = true;

                byte[] keyAgreementFrame = await ReceiveFrameAsync(
                    PacketHeaders.HEADER_GC_KEY_AGREEMENT, _plainFramer, cancellationToken).ConfigureAwait(false);
                if (!PacketKeyAgreementCodec.TryDeserialize(
                    keyAgreementFrame, out PacketKeyAgreement serverAgreement, out string parseError))
                {
                    throw new HandshakeFailedException(
                        $"Invalid GC key-agreement packet: {parseError}");
                }

                if (serverAgreement.AgreedLength != DiffieHellmanGroup.AgreedValueLength)
                {
                    throw new HandshakeFailedException(
                        $"Bad agreed length: expected {DiffieHellmanGroup.AgreedValueLength}, got {serverAgreement.AgreedLength}.");
                }

                if (serverAgreement.DataLength != DiffieHellmanGroup.KeyDataLength)
                {
                    throw new HandshakeFailedException(
                        $"Bad key-data length: expected {DiffieHellmanGroup.KeyDataLength}, got {serverAgreement.DataLength}.");
                }

                if (!_agreement.TryAgree(serverAgreement.AgreedLength, serverAgreement.Data, out byte[] shared))
                {
                    throw new HandshakeFailedException(
                        "DH2 agreement failed (length mismatch or invalid peer keys).");
                }

                CipherKeyMaterial material;
                try
                {
                    if (!CipherKeyDerivation.TryDerive(shared, out material))
                    {
                        throw new HandshakeFailedException("Cipher key derivation failed (short shared secret).");
                    }
                }
                finally
                {
                    Array.Clear(shared, 0, shared.Length);
                }

                byte[] ownPublicData = _agreement.ExportPublicData();
                _agreement.Dispose();
                _agreement = null;

                var session = new CipherSession(
                    isClientPolarity: true,
                    material: material,
                    engineFactory: BlockCipherEngineFactory.ForSession());

                var reply = new PacketKeyAgreement(
                    agreedLength: DiffieHellmanGroup.AgreedValueLength,
                    dataLength: DiffieHellmanGroup.KeyDataLength,
                    data: ownPublicData);
                byte[] replyBytes = PacketKeyAgreementCodec.Serialize(reply);
                Array.Clear(ownPublicData, 0, ownPublicData.Length);
                try
                {
                    await _connection.SendAsync(replyBytes, 0, replyBytes.Length, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!(ex is HandshakeFailedException))
                {
                    session.Dispose();
                    throw new HandshakeFailedException("Failed to send CG key-agreement packet.", ex);
                }

                await ReceiveFrameAsync(
                    PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED, _plainFramer, cancellationToken).ConfigureAwait(false);

                try
                {
                    session.SetActivated(true);
                }
                catch (Exception ex)
                {
                    session.Dispose();
                    throw new HandshakeFailedException("Failed to activate cipher session.", ex);
                }

                Session = session;
                Completed = true;
                return session;
            }
            catch (HandshakeFailedException)
            {
                DisposeAgreement();
                throw;
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                DisposeAgreement();
                throw new HandshakeFailedException("Handshake aborted by transport or cancellation.", ex);
            }
        }

        /// <summary>
        /// Sends one post-handshake frame (plaintext in, encrypted on the wire).
        /// The caller's buffer is never mutated; encryption applies to a copy.
        /// </summary>
        public async Task SendSecureAsync(byte[] frame, CancellationToken cancellationToken = default)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            await SendSecureAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a slice of a post-handshake frame (plaintext in, encrypted on the wire).
        /// </summary>
        public async Task SendSecureAsync(byte[] frame, int offset, int count, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CipherSession session = RequireCompletedSession();

            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            if (offset < 0 || count < 0 || offset + count > frame.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count == 0)
            {
                throw new HandshakeFailedException("Refusing to send an empty secure frame.");
            }

            if (count > PacketFramer.MaxFrameLength)
            {
                throw new HandshakeFailedException(
                    $"Secure frame too large: {count} bytes exceeds MaxFrameLength {PacketFramer.MaxFrameLength}.");
            }

            byte[] wire = new byte[count];
            Buffer.BlockCopy(frame, offset, wire, 0, count);
            session.Encrypt(wire, 0, wire.Length);
            try
            {
                await _connection.SendAsync(wire, 0, wire.Length, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Array.Clear(wire, 0, wire.Length);
            }
        }

        /// <summary>
        /// Receives one post-handshake frame (encrypted on the wire, plaintext out).
        /// Decrypts the raw stream BEFORE framing (server encrypts the whole stream,
        /// desc.cpp:460-461; client decrypts on recv, NetStream.cpp:109).
        /// Server keepalive pings (GC_PING, every phase) are answered with an
        /// encrypted CG_PONG and skipped, mirroring RecvPingPacket
        /// (PythonNetworkStream.cpp:636-656); callers never see them.
        /// </summary>
        public async Task<byte[]> ReceiveSecureFrameAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CipherSession session = RequireCompletedSession();

            byte[] chunk = new byte[ReceiveChunkSize];
            while (true)
            {
                if (_secureFramer.TryDequeue(out byte[] frame) && frame != null)
                {
                    if (frame.Length > 0 && frame[0] == PacketHeaders.HEADER_GC_PING)
                    {
                        await SendPongAsync(encrypted: true, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (frame.Length > 0 && frame[0] == PacketHeaders.HEADER_GC_PHASE)
                    {
                        // Server pushes GC_PHASE on every SetPhase (desc.cpp:518),
                        // interleaved between the step replies (e.g. the channel
                        // hop wires [90 empire][GC_PHASE(SELECT)][32 slots]).
                        // Consume it transparently so step clients never see it;
                        // surface it via PhaseChanged for the session/UI layer.
                        PacketGCPhase phase = PacketGCPhaseCodec.Deserialize(frame);
                        PhaseChanged?.Invoke(phase.Phase);
                        continue;
                    }

                    return frame;
                }

                int received = await _connection.ReceiveAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false);
                if (received == 0)
                {
                    throw new HandshakeFailedException("Connection closed during secure receive.");
                }

                session.Decrypt(chunk, 0, received);
                _secureFramer.Append(new ReadOnlySpan<byte>(chunk, 0, received));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeAgreement();
            if (!Completed)
            {
                Session?.Dispose();
                Session = null;
            }
        }

        private async Task<byte[]> ReceiveFrameAsync(
            byte expectedHeader, PacketFramer framer, CancellationToken cancellationToken)
        {
            byte[] chunk = new byte[ReceiveChunkSize];
            while (true)
            {
                if (framer.TryDequeue(out byte[] frame) && frame != null)
                {
                    if (frame.Length == 0)
                    {
                        continue;
                    }

                    if (frame[0] == PacketHeaders.HEADER_GC_PING)
                    {
                        // The ping event starts in the DESC constructor
                        // (desc.cpp:227-233), so a keepalive can arrive while
                        // the handshake is still plaintext; answer in plaintext.
                        await SendPongAsync(encrypted: false, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (frame[0] != expectedHeader)
                    {
                        throw new HandshakeFailedException(
                            $"Unexpected packet header 0x{frame[0]:X2} while waiting for 0x{expectedHeader:X2}.");
                    }

                    return frame;
                }

                int received;
                try
                {
                    received = await _connection.ReceiveAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!(ex is HandshakeFailedException))
                {
                    throw new HandshakeFailedException("Transport receive failed during handshake.", ex);
                }

                if (received == 0)
                {
                    throw new HandshakeFailedException("Connection closed by the server during handshake.");
                }

                framer.Append(new ReadOnlySpan<byte>(chunk, 0, received));
            }
        }

        /// <summary>
        /// Sends one CG_PONG (1 byte) in answer to a server keepalive ping.
        /// Plaintext while the handshake is unfinished, encrypted afterwards —
        /// the server accepts PONG in every phase (input.cpp:132-135, 551-552)
        /// and closes the session on the next ping cycle without it
        /// (desc.cpp:174-180).
        /// </summary>
        private async Task SendPongAsync(bool encrypted, CancellationToken cancellationToken)
        {
            byte[] pong = PacketCGPongCodec.Serialize(new PacketCGPong());
            if (encrypted)
            {
                await SendSecureAsync(pong, 0, pong.Length, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _connection.SendAsync(pong, 0, pong.Length, cancellationToken).ConfigureAwait(false);
            }
        }

        private void DisposeAgreement()
        {
            Dh2KeyAgreement agreement = _agreement;
            _agreement = null;
            if (agreement != null)
            {
                agreement.Dispose();
            }
        }

        private CipherSession RequireCompletedSession()
        {
            if (!Completed || Session == null)
            {
                throw new HandshakeFailedException("Handshake is not completed; secure traffic is not allowed yet.");
            }

            return Session;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HandshakeClient));
            }
        }
    }
}
