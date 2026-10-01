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
    /// Loading-phase stat snapshot (points + skills, server send order).
    /// </summary>
    public struct LoadingStatsData
    {
        public int[] Points;
        public PlayerSkill[] Skills;
    }

    /// <summary>
    /// One game-phase spawn event: entity appeared or disappeared.
    /// </summary>
    public readonly struct SpawnEvent
    {
        public bool Added { get; }
        public PacketGCCharacterAdd Add { get; }
        public uint RemovedVid { get; }

        private SpawnEvent(bool added, PacketGCCharacterAdd add, uint removedVid)
        {
            Added = added;
            Add = add;
            RemovedVid = removedVid;
        }

        public static SpawnEvent EntityAdded(PacketGCCharacterAdd add)
        {
            return new SpawnEvent(true, add, 0);
        }

        public static SpawnEvent EntityRemoved(uint vid)
        {
            return new SpawnEvent(false, default, vid);
        }
    }

    /// <summary>
    /// Loading-stats + spawn client over an already-handshaked secure channel.
    /// Wire sequences (docs/protocol/connection-flow.md §5-6):
    /// - Loading: GC_POINTS (16, 1021B) then GC_SKILL_LEVEL (76, 1531B)
    ///   (`PlayerLoad`, `input_db.cpp:457-458`).
    /// - Game: GC_CHARACTER_ADD (1, 35B) / GC_CHARACTER_DEL (2, 5B) view packets
    ///   (`char.cpp:812`, `PhaseGame.cpp:253-274`).
    ///
    /// Phase guard: 16/76 Loading-only, 1/2 Game-only (guide §5.3).
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class GameWorldClient
    {
        private readonly HandshakeClient _handshake;
        private readonly PacketRegistry _registry;

        public GameWorldClient(HandshakeClient handshake, PacketRegistry registry = null)
        {
            if (handshake == null)
            {
                throw new ArgumentNullException(nameof(handshake));
            }

            if (!handshake.Completed || handshake.Session == null)
            {
                throw new HandshakeFailedException("Handshake must be completed before game world streaming.");
            }

            _handshake = handshake;
            _registry = registry ?? PacketRegistry.CreateGameRegistry();
        }

        /// <summary>
        /// Receives the loading stat bundle in server order: points then skills.
        /// </summary>
        public async Task<LoadingStatsData> ReceiveLoadingStatsAsync(
            CancellationToken cancellationToken = default)
        {
            byte[] pointsFrame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            if (pointsFrame[0] != PacketGCPoints.PacketHeader
                || !_registry.IsAllowed(pointsFrame[0], PhaseType.Loading))
            {
                throw new HandshakeFailedException(
                    $"Expected GC_POINTS (0x{PacketGCPoints.PacketHeader:X2}) first, got 0x{pointsFrame[0]:X2}.");
            }

            if (!PacketGCPointsCodec.TryDeserialize(pointsFrame, out PacketGCPoints points, out string pointsError))
            {
                throw new HandshakeFailedException($"Invalid GC_POINTS packet: {pointsError}");
            }

            byte[] skillsFrame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            if (skillsFrame[0] != PacketGCSkillLevel.PacketHeader
                || !_registry.IsAllowed(skillsFrame[0], PhaseType.Loading))
            {
                throw new HandshakeFailedException(
                    $"Expected GC_SKILL_LEVEL (0x{PacketGCSkillLevel.PacketHeader:X2}) second, got 0x{skillsFrame[0]:X2}.");
            }

            if (!PacketGCSkillLevelCodec.TryDeserialize(skillsFrame, out PacketGCSkillLevel skills, out string skillsError))
            {
                throw new HandshakeFailedException($"Invalid GC_SKILL_LEVEL packet: {skillsError}");
            }

            return new LoadingStatsData { Points = points.Points, Skills = skills.Skills };
        }

        /// <summary>
        /// Receives one spawn event (add or remove).
        /// </summary>
        public async Task<SpawnEvent> ReceiveSpawnAsync(CancellationToken cancellationToken = default)
        {
            byte[] frame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            byte header = frame[0];

            if (!_registry.IsAllowed(header, PhaseType.Game))
            {
                throw new HandshakeFailedException(
                    $"Spawn header 0x{header:X2} is not valid in the Game phase.");
            }

            if (header == PacketGCCharacterAdd.PacketHeader)
            {
                if (!PacketGCCharacterAddCodec.TryDeserialize(frame, out PacketGCCharacterAdd add, out string addError))
                {
                    throw new HandshakeFailedException($"Invalid spawn packet: {addError}");
                }

                return SpawnEvent.EntityAdded(add);
            }

            if (header == PacketGCCharacterDelete.PacketHeader)
            {
                if (!PacketGCCharacterDeleteCodec.TryDeserialize(frame, out PacketGCCharacterDelete del, out string delError))
                {
                    throw new HandshakeFailedException($"Invalid despawn packet: {delError}");
                }

                return SpawnEvent.EntityRemoved(del.Vid);
            }

            throw new HandshakeFailedException($"Unexpected spawn header 0x{header:X2}.");
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
                throw new HandshakeFailedException("Failed to receive game world packet.", ex);
            }

            if (frame == null || frame.Length == 0)
            {
                throw new HandshakeFailedException("Empty game world frame.");
            }

            return frame;
        }
    }
}
