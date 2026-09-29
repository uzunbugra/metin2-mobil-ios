using System;
using System.Collections.Generic;
using Metin2.Protocol.Constants;

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

            return registry;
        }
    }
}
