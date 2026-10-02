using System;
using System.Collections.Generic;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Framing;

namespace Metin2.Protocol.Registry
{
    /// <summary>Packet direction on the wire.</summary>
    public enum PacketDirection
    {
        ClientToServer,
        ServerToClient
    }

    /// <summary>
    /// Static metadata for one packet header: wire length plus the phases it may
    /// legally appear in. Unknown header/phase combinations must be rejected by the
    /// session layer (per AGENT_DEVELOPMENT_GUIDE.md §5.3, only phase-valid packets
    /// are accepted).
    /// </summary>
    public sealed class PacketDescriptor
    {
        public byte Header { get; }
        public string Name { get; }
        public PacketDirection Direction { get; }
        public int Length { get; }
        public bool AllowedInAllPhases { get; }
        public IReadOnlyCollection<PhaseType> AllowedPhases { get; }

        public PacketDescriptor(
            byte header,
            string name,
            PacketDirection direction,
            int length,
            bool allowedInAllPhases = false,
            ICollection<PhaseType> allowedPhases = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Packet name must not be null or empty.", nameof(name));
            }

            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Packet length must be positive.");
            }

            if (!allowedInAllPhases && (allowedPhases == null || allowedPhases.Count == 0))
            {
                throw new ArgumentException(
                    "Either allowedInAllPhases or a non-empty allowedPhases set is required.",
                    nameof(allowedPhases));
            }

            Header = header;
            Name = name;
            Direction = direction;
            Length = length;
            AllowedInAllPhases = allowedInAllPhases;
            AllowedPhases = allowedInAllPhases
                ? (IReadOnlyCollection<PhaseType>)Array.Empty<PhaseType>()
                : new List<PhaseType>(allowedPhases).AsReadOnly();
        }

        public bool IsAllowedIn(PhaseType phase)
        {
            if (AllowedInAllPhases)
            {
                return true;
            }

            foreach (PhaseType allowed in AllowedPhases)
            {
                if (allowed == phase)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Phase-aware packet registry. Replaces ad-hoc switch dispatch with data driven
    /// from the C++ source (docs/protocol/packet-catalog.json).
    /// No UnityEngine dependency.
    /// </summary>
    public class PacketRegistry
    {
        private readonly Dictionary<byte, PacketDescriptor> _entries = new Dictionary<byte, PacketDescriptor>();

        public int Count => _entries.Count;

        public void Register(PacketDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (_entries.ContainsKey(descriptor.Header))
            {
                throw new InvalidOperationException(
                    $"Duplicate packet header registration: 0x{descriptor.Header:X2} ({descriptor.Name}).");
            }

            _entries.Add(descriptor.Header, descriptor);
        }

        public bool TryGet(byte header, out PacketDescriptor descriptor)
        {
            return _entries.TryGetValue(header, out descriptor);
        }

        /// <summary>
        /// True only when the header is known AND valid in the given phase.
        /// Unknown headers and wrong-phase packets both return false.
        /// </summary>
        public bool IsAllowed(byte header, PhaseType phase)
        {
            return _entries.TryGetValue(header, out PacketDescriptor descriptor)
                && descriptor.IsAllowedIn(phase);
        }

        /// <summary>
        /// Registry for the source-verified S2C handshake path
        /// (docs/protocol/connection-flow.md §1-2):
        /// GC_HANDSHAKE (0xff) -> GC_KEY_AGREEMENT (0xfb) ->
        /// GC_KEY_AGREEMENT_COMPLETED (0xfa), plus GC_PHASE (0xfd, all phases).
        /// </summary>
        public static PacketRegistry CreateHandshakeRegistry()
        {
            var registry = new PacketRegistry();

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_HANDSHAKE,
                "HEADER_GC_HANDSHAKE",
                PacketDirection.ServerToClient,
                13,
                allowedPhases: new[] { PhaseType.Handshake }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_KEY_AGREEMENT,
                "HEADER_GC_KEY_AGREEMENT",
                PacketDirection.ServerToClient,
                261,
                allowedPhases: new[] { PhaseType.Handshake }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED,
                "HEADER_GC_KEY_AGREEMENT_COMPLETED",
                PacketDirection.ServerToClient,
                4,
                allowedPhases: new[] { PhaseType.Handshake }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_PHASE,
                "HEADER_GC_PHASE",
                PacketDirection.ServerToClient,
                2,
                allowedInAllPhases: true));

            // Server keepalive: the ping event is created in the DESC constructor
            // (desc.cpp:227-233) and fires in EVERY phase, including plaintext
            // handshake; the C++ client answers it in all five client phases
            // (PhaseHandShake.cpp:63, PhaseLogin.cpp:50, PhaseSelect.cpp:137,
            // PhaseLoading.cpp:140, PhaseGame.cpp:381).
            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_PING,
                "HEADER_GC_PING",
                PacketDirection.ServerToClient,
                1,
                allowedInAllPhases: true));

            return registry;
        }

        /// <summary>
        /// Handshake path plus the source-verified auth-login replies
        /// (docs/protocol/connection-flow.md §3):
        /// GC_AUTH_SUCCESS (150, 6B, Auth only — input_db.cpp:1686-1710) and
        /// GC_LOGIN_FAILURE (7, 10B, Auth + Login — the shared LoginFailure()
        /// helper in input.cpp:177-188 is called from both CInputAuth and
        /// CInputLogin paths).
        /// </summary>
        public static PacketRegistry CreateAuthRegistry()
        {
            var registry = CreateHandshakeRegistry();

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_AUTH_SUCCESS,
                "HEADER_GC_AUTH_SUCCESS",
                PacketDirection.ServerToClient,
                6,
                allowedPhases: new[] { PhaseType.Auth }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_LOGIN_FAILURE,
                "HEADER_GC_LOGIN_FAILURE",
                PacketDirection.ServerToClient,
                10,
                allowedPhases: new[] { PhaseType.Auth, PhaseType.Login }));

            return registry;
        }

        /// <summary>
        /// Auth registry plus the source-verified channel-login replies
        /// (docs/protocol/connection-flow.md §4):
        /// GC_EMPIRE (90, 2B) arrives in the Login phase — server sends it
        /// BEFORE SetPhase(SELECT) (input_db.cpp:157-172);
        /// GC_LOGIN_SUCCESS_NEWSLOT (32, 329B) arrives in the Select phase
        /// (desc.cpp:892-919 SendLoginSuccessPacket).
        /// </summary>
        public static PacketRegistry CreateChannelRegistry()
        {
            var registry = CreateAuthRegistry();

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_EMPIRE,
                "HEADER_GC_EMPIRE",
                PacketDirection.ServerToClient,
                2,
                allowedPhases: new[] { PhaseType.Login }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_LOGIN_SUCCESS_NEWSLOT,
                "HEADER_GC_LOGIN_SUCCESS_NEWSLOT",
                PacketDirection.ServerToClient,
                329,
                allowedPhases: new[] { PhaseType.Select }));

            return registry;
        }

        /// <summary>
        /// Channel registry plus the source-verified select-phase replies
        /// (SELECT and LOGIN share m_inputLogin — desc.cpp:539-547):
        /// create success (8, 65B — input_db.cpp:221-227),
        /// create failure (9, 2B — input_login.cpp:427-482),
        /// delete success (10, 2B — input_db.cpp:285-286),
        /// delete failure (11, 1B — input_db.cpp:296).
        /// Client dispatch mirror: PhaseSelect.cpp:71-89.
        /// </summary>
        public static PacketRegistry CreateSelectRegistry()
        {
            var registry = CreateChannelRegistry();

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_CHARACTER_CREATE_SUCCESS,
                "HEADER_GC_CHARACTER_CREATE_SUCCESS",
                PacketDirection.ServerToClient,
                65,
                allowedPhases: new[] { PhaseType.Select }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_CHARACTER_CREATE_FAILURE,
                "HEADER_GC_CHARACTER_CREATE_FAILURE",
                PacketDirection.ServerToClient,
                2,
                allowedPhases: new[] { PhaseType.Select }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_CHARACTER_DELETE_SUCCESS,
                "HEADER_GC_CHARACTER_DELETE_SUCCESS",
                PacketDirection.ServerToClient,
                2,
                allowedPhases: new[] { PhaseType.Select }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_CHARACTER_DELETE_WRONG_SOCIAL_ID,
                "HEADER_GC_CHARACTER_DELETE_WRONG_SOCIAL_ID",
                PacketDirection.ServerToClient,
                1,
                allowedPhases: new[] { PhaseType.Select }));

            return registry;
        }

        /// <summary>
        /// Select registry plus the source-verified world-entry packets:
        /// GC_MAIN_CHARACTER2 (113, 46B) arrives in the Loading phase
        /// (`PlayerLoad`, input_db.cpp:427-428);
        /// GC_TIME (106, 5B) and GC_CHANNEL (121, 2B) arrive in the Game phase
        /// right after PHASE_GAME (`Entergame`, input_login.cpp:579-620).
        /// Client dispatch mirror: PhaseLoading.cpp:96-113, PhaseGame.cpp:496-497.
        /// </summary>
        public static PacketRegistry CreateWorldEntryRegistry()
        {
            var registry = CreateSelectRegistry();

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_MAIN_CHARACTER2_EMPIRE,
                "HEADER_GC_MAIN_CHARACTER2_EMPIRE",
                PacketDirection.ServerToClient,
                46,
                allowedPhases: new[] { PhaseType.Loading }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_TIME,
                "HEADER_GC_TIME",
                PacketDirection.ServerToClient,
                5,
                allowedPhases: new[] { PhaseType.Game }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_CHANNEL,
                "HEADER_GC_CHANNEL",
                PacketDirection.ServerToClient,
                2,
                allowedPhases: new[] { PhaseType.Game }));

            return registry;
        }

        /// <summary>
        /// World-entry registry plus the loading stats and game-phase spawn packets:
        /// GC_POINTS (16, 1021B) and GC_SKILL_LEVEL (76, 1531B) arrive in the
        /// Loading phase (`PlayerLoad`, input_db.cpp:457-458);
        /// GC_CHARACTER_ADD (1, 35B) and GC_CHARACTER_DEL (2, 5B) arrive in the
        /// Game phase (view/Show packets, `char.cpp:812`).
        /// Client dispatch mirror: PhaseLoading (points/skill) and
        /// PhaseGame.cpp:253-274 (spawn).
        /// </summary>
        public static PacketRegistry CreateGameRegistry()
        {
            var registry = CreateWorldEntryRegistry();

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_CHARACTER_POINTS,
                "HEADER_GC_CHARACTER_POINTS",
                PacketDirection.ServerToClient,
                1021,
                allowedPhases: new[] { PhaseType.Loading }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_SKILL_LEVEL,
                "HEADER_GC_SKILL_LEVEL",
                PacketDirection.ServerToClient,
                1531,
                allowedPhases: new[] { PhaseType.Loading }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_CHARACTER_ADD,
                "HEADER_GC_CHARACTER_ADD",
                PacketDirection.ServerToClient,
                35,
                allowedPhases: new[] { PhaseType.Game }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_CHARACTER_DEL,
                "HEADER_GC_CHARACTER_DEL",
                PacketDirection.ServerToClient,
                5,
                allowedPhases: new[] { PhaseType.Game }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_ITEM_SET,
                "HEADER_GC_ITEM_SET",
                PacketDirection.ServerToClient,
                51,
                allowedPhases: new[] { PhaseType.Game, PhaseType.Loading }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_ITEM_DEL,
                "HEADER_GC_ITEM_DEL",
                PacketDirection.ServerToClient,
                42,
                allowedPhases: new[] { PhaseType.Game, PhaseType.Loading }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_ITEM_UPDATE,
                "HEADER_GC_ITEM_UPDATE",
                PacketDirection.ServerToClient,
                38,
                allowedPhases: new[] { PhaseType.Game, PhaseType.Loading }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_MOVE,
                "HEADER_GC_MOVE",
                PacketDirection.ServerToClient,
                24,
                allowedPhases: new[] { PhaseType.Game }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_SYNC_POSITION,
                "HEADER_GC_SYNC_POSITION",
                PacketDirection.ServerToClient,
                PacketLengthTable.MaxSyncPacketSize,
                allowedPhases: new[] { PhaseType.Game }));

            // Combat events: point deltas (GC_POINT_CHANGE, 17, 17B) are handled
            // by the C++ client in Select, Loading AND Game phases
            // (PhaseSelect.cpp:129, PhaseLoading.cpp:129, PhaseGame.cpp:311);
            // stun/dead/motion/damage-info are Game-only dispatches
            // (PhaseGame.cpp:303/307/356/396).
            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_POINT_CHANGE,
                "HEADER_GC_POINT_CHANGE",
                PacketDirection.ServerToClient,
                17,
                allowedPhases: new[] { PhaseType.Select, PhaseType.Loading, PhaseType.Game }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_STUN,
                "HEADER_GC_STUN",
                PacketDirection.ServerToClient,
                5,
                allowedPhases: new[] { PhaseType.Game }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_DEAD,
                "HEADER_GC_DEAD",
                PacketDirection.ServerToClient,
                5,
                allowedPhases: new[] { PhaseType.Game }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_MOTION,
                "HEADER_GC_MOTION",
                PacketDirection.ServerToClient,
                11,
                allowedPhases: new[] { PhaseType.Game }));

            registry.Register(new PacketDescriptor(
                PacketHeaders.HEADER_GC_DAMAGE_INFO,
                "HEADER_GC_DAMAGE_INFO",
                PacketDirection.ServerToClient,
                10,
                allowedPhases: new[] { PhaseType.Game }));

            return registry;
        }
    }
}
