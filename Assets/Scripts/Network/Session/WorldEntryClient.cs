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
    /// Own-character world-entry snapshot: loading-phase spawn plus the
    /// game-phase clock and channel that follow ENTERGAME.
    /// </summary>
    public struct WorldEntryData
    {
        public uint Vid;
        public ushort Race;
        public string Name;
        public int X;
        public int Y;
        public int Z;
        public byte Empire;
        public byte SkillGroup;
        public uint ServerTime;
        public byte Channel;
    }

    /// <summary>
    /// World-entry client over an already-handshaked secure channel.
    /// Wire sequence (docs/protocol/connection-flow.md §5):
    ///   server sends GC_MAIN_CHARACTER2 (113, 46B) in the Loading phase
    ///   (from `PlayerLoad`, `input_db.cpp:427-428`); the client answers
    ///   CG_ENTERGAME (10, via <see cref="CharacterSelectClient"/>); then
    ///   server sends GC_TIME (106, 5B) and GC_CHANNEL (121, 2B) in the
    ///   Game phase (`Entergame`, `input_login.cpp:579-620`).
    ///
    /// Source anchors:
    /// - Server: `char.cpp:1495-1555` (packet choice), `input_login.cpp:546-620`.
    /// - Client: `PhaseLoading.cpp:96-113,218-242`, `PhaseGame.cpp:496-497,3907-3914`.
    ///
    /// Phase guard: 113 accepted in Loading only, 106/121 in Game only (guide §5.3).
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class WorldEntryClient
    {
        private readonly HandshakeClient _handshake;
        private readonly PacketRegistry _registry;

        public WorldEntryClient(HandshakeClient handshake, PacketRegistry registry = null)
        {
            if (handshake == null)
            {
                throw new ArgumentNullException(nameof(handshake));
            }

            if (!handshake.Completed || handshake.Session == null)
            {
                throw new HandshakeFailedException("Handshake must be completed before world entry.");
            }

            _handshake = handshake;
            _registry = registry ?? PacketRegistry.CreateWorldEntryRegistry();
        }

        /// <summary>
        /// Receives the own-character spawn (Loading phase).
        /// </summary>
        public async Task<PacketGCMainCharacter> ReceiveMainCharacterAsync(
            CancellationToken cancellationToken = default)
        {
            byte[] frame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            byte header = frame[0];

            if (!_registry.IsAllowed(header, PhaseType.Loading))
            {
                throw new HandshakeFailedException(
                    $"Main-character header 0x{header:X2} is not valid in the Loading phase.");
            }

            if (header != PacketGCMainCharacter.PacketHeader)
            {
                throw new HandshakeFailedException($"Unexpected main-character header 0x{header:X2}.");
            }

            if (!PacketGCMainCharacterCodec.TryDeserialize(frame, out PacketGCMainCharacter main, out string error))
            {
                throw new HandshakeFailedException($"Invalid main-character packet: {error}");
            }

            return main;
        }

        /// <summary>
        /// Receives the game-phase entry pair in server order: TIME then CHANNEL.
        /// </summary>
        public async Task<WorldEntryData> ReceiveGameEntryAsync(
            PacketGCMainCharacter mainCharacter, CancellationToken cancellationToken = default)
        {
            byte[] timeFrame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            if (!ExpectGameHeader(timeFrame, PacketGCTime.PacketHeader))
            {
                throw new HandshakeFailedException(
                    $"Expected GC_TIME (0x{PacketGCTime.PacketHeader:X2}) first, got 0x{timeFrame[0]:X2}.");
            }

            if (!PacketGCTimeCodec.TryDeserialize(timeFrame, out PacketGCTime time, out string timeError))
            {
                throw new HandshakeFailedException($"Invalid GC_TIME packet: {timeError}");
            }

            byte[] channelFrame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            if (!ExpectGameHeader(channelFrame, PacketGCChannel.PacketHeader))
            {
                throw new HandshakeFailedException(
                    $"Expected GC_CHANNEL (0x{PacketGCChannel.PacketHeader:X2}) second, got 0x{channelFrame[0]:X2}.");
            }

            if (!PacketGCChannelCodec.TryDeserialize(channelFrame, out PacketGCChannel channel, out string channelError))
            {
                throw new HandshakeFailedException($"Invalid GC_CHANNEL packet: {channelError}");
            }

            return new WorldEntryData
            {
                Vid = mainCharacter.Vid,
                Race = mainCharacter.Race,
                Name = mainCharacter.Name,
                X = mainCharacter.X,
                Y = mainCharacter.Y,
                Z = mainCharacter.Z,
                Empire = mainCharacter.Empire,
                SkillGroup = mainCharacter.SkillGroup,
                ServerTime = time.Time,
                Channel = channel.Channel
            };
        }

        private bool ExpectGameHeader(byte[] frame, byte expected)
        {
            return frame[0] == expected && _registry.IsAllowed(frame[0], PhaseType.Game);
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
                throw new HandshakeFailedException("Failed to receive world-entry packet.", ex);
            }

            if (frame == null || frame.Length == 0)
            {
                throw new HandshakeFailedException("Empty world-entry reply frame.");
            }

            return frame;
        }
    }
}
