using System;
using System.Threading;
using System.Threading.Tasks;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;
using Metin2.Protocol.Registry;

namespace Metin2.Network.Session
{
    /// <summary>
    /// Movement client over an already-handshaked secure channel (Game phase).
    /// Wire exchanges (docs/protocol/connection-flow.md §6):
    /// - C→S CG_MOVE (7, 16B): intent with quantized rotation, cm coords and
    ///   server-synced timestamp (`PhaseGame.cpp:1107-1145`,
    ///   `input_main.cpp:1514-1688` — server-authoritative checks, rebroadcast
    ///   to viewers only).
    /// - C→S CG_SYNC_POSITION (8, dynamic): visible-actor positions, at most 16
    ///   entries (`PhaseGame.cpp:2697-2714`, `input_main.cpp:1782-1818`).
    /// - S→C GC_MOVE (3, 24B): another actor's movement.
    /// - S→C GC_SYNC_POSITION (5, dynamic): position batch.
    ///
    /// The server never echoes our own move back (`PacketAround` excludes self),
    /// so sent intents and received broadcasts are separate streams.
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class MovementClient
    {
        private readonly HandshakeClient _handshake;
        private readonly PacketRegistry _registry;

        public MovementClient(HandshakeClient handshake, PacketRegistry registry = null)
        {
            if (handshake == null)
            {
                throw new ArgumentNullException(nameof(handshake));
            }

            if (!handshake.Completed || handshake.Session == null)
            {
                throw new HandshakeFailedException("Handshake must be completed before movement.");
            }

            _handshake = handshake;
            _registry = registry ?? PacketRegistry.CreateGameRegistry();
        }

        /// <summary>
        /// Rotation quantizer, mirroring `SendCharacterStatePacket`
        /// (`PhaseGame.cpp:1113-1123`): wraps to [0,360) then divides by 5
        /// (server restores `bRot*5`, `input_main.cpp:1604`).
        /// </summary>
        public static byte QuantizeRotation(float degrees)
        {
            float wrapped = degrees % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            return (byte)(wrapped / 5f);
        }

        /// <summary>
        /// Sends a movement intent (cm coords, server-synced timestamp).
        /// </summary>
        public async Task SendMoveAsync(
            byte func, byte arg, float rotationDegrees, int x, int y, uint serverTimeMs,
            CancellationToken cancellationToken = default)
        {
            if (func >= MoveFunc.MaxNum && (func & MoveFunc.Skill) == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(func), "Unknown move func (server ignores it and logs an error).");
            }

            if (x < 0 || y < 0)
            {
                throw new ArgumentOutOfRangeException("Coordinates must be non-negative (client assert range).");
            }

            var packet = new PacketCGMove(
                func, arg, QuantizeRotation(rotationDegrees), x, y, serverTimeMs);
            byte[] wire = PacketCGMoveCodec.Serialize(packet);
            try
            {
                await _handshake.SendSecureAsync(wire, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                throw new HandshakeFailedException("Failed to send CG_MOVE packet.", ex);
            }
            finally
            {
                Array.Clear(wire, 0, wire.Length);
            }
        }

        /// <summary>
        /// Sends a sync-position batch (at most 16 entries — server clamp).
        /// </summary>
        public async Task SendSyncAsync(
            SyncPositionElement[] elements, CancellationToken cancellationToken = default)
        {
            if (elements == null || elements.Length == 0)
            {
                throw new ArgumentException("Sync batch must hold at least one element.", nameof(elements));
            }

            if (elements.Length > PacketCGSyncPosition.MaxElements)
            {
                throw new ArgumentException(
                    $"Too many sync elements: {elements.Length} exceeds {PacketCGSyncPosition.MaxElements}.",
                    nameof(elements));
            }

            byte[] wire = PacketCGSyncPositionCodec.Serialize(new PacketCGSyncPosition(elements));
            try
            {
                await _handshake.SendSecureAsync(wire, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                throw new HandshakeFailedException("Failed to send CG_SYNC_POSITION packet.", ex);
            }
            finally
            {
                Array.Clear(wire, 0, wire.Length);
            }
        }

        /// <summary>
        /// Receives another actor's movement broadcast.
        /// </summary>
        public async Task<PacketGCMove> ReceiveMoveAsync(CancellationToken cancellationToken = default)
        {
            byte[] frame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            if (frame[0] != PacketGCMove.PacketHeader
                || !_registry.IsAllowed(frame[0], PhaseType.Game))
            {
                throw new HandshakeFailedException(
                    $"Expected GC_MOVE (0x{PacketGCMove.PacketHeader:X2}) in Game phase, got 0x{frame[0]:X2}.");
            }

            if (!PacketGCMoveCodec.TryDeserialize(frame, out PacketGCMove move, out string error))
            {
                throw new HandshakeFailedException($"Invalid GC_MOVE packet: {error}");
            }

            return move;
        }

        /// <summary>
        /// Receives a position batch.
        /// </summary>
        public async Task<PacketGCSyncPosition> ReceiveSyncAsync(CancellationToken cancellationToken = default)
        {
            byte[] frame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            if (frame[0] != PacketGCSyncPosition.PacketHeader
                || !_registry.IsAllowed(frame[0], PhaseType.Game))
            {
                throw new HandshakeFailedException(
                    $"Expected GC_SYNC_POSITION (0x{PacketGCSyncPosition.PacketHeader:X2}) in Game phase, got 0x{frame[0]:X2}.");
            }

            if (!PacketGCSyncPositionCodec.TryDeserialize(frame, out PacketGCSyncPosition sync, out string error))
            {
                throw new HandshakeFailedException($"Invalid GC_SYNC_POSITION packet: {error}");
            }

            return sync;
        }

        private async Task<byte[]> ReceiveFrameAsync(CancellationToken cancellationToken)
        {
            byte[] frame;
            try
            {
                frame = await _handshake.ReceiveSecureFrameAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                throw new HandshakeFailedException("Failed to receive movement packet.", ex);
            }

            if (frame == null || frame.Length == 0)
            {
                throw new HandshakeFailedException("Empty movement frame.");
            }

            return frame;
        }
    }
}
