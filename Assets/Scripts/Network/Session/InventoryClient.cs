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
    /// One inventory cell event: full assignment, clear, or in-place mutation.
    /// </summary>
    public readonly struct ItemEvent
    {
        public enum Kind
        {
            Set,
            Cleared,
            Updated
        }

        public Kind EventKind { get; }
        public byte Window { get; }
        public ushort Cell { get; }
        public PacketGCItemSet Set { get; }
        public PacketGCItemUpdate Update { get; }

        private ItemEvent(Kind kind, byte window, ushort cell, PacketGCItemSet set, PacketGCItemUpdate update)
        {
            EventKind = kind;
            Window = window;
            Cell = cell;
            Set = set;
            Update = update;
        }

        public static ItemEvent Assigned(PacketGCItemSet set)
        {
            return new ItemEvent(Kind.Set, set.Window, set.Cell, set, default);
        }

        public static ItemEvent Cleared(byte window, ushort cell)
        {
            return new ItemEvent(Kind.Cleared, window, cell, default, default);
        }

        public static ItemEvent Mutated(PacketGCItemUpdate update)
        {
            return new ItemEvent(Kind.Updated, update.Window, update.Cell, default, update);
        }
    }

    /// <summary>
    /// Inventory stream client over an already-handshaked secure channel.
    /// Wire frames (docs/protocol/connection-flow.md §6):
    /// - GC_ITEM_SET (21, 51B): cell gains an item — full data
    ///   (`CHARACTER::SetItem`, `char_item.cpp:408-425`).
    /// - GC_ITEM_DEL (20, 42B): cell cleared — zeroed variant of the same shape
    ///   (`char_item.cpp:426-437`; NOT the 2-byte struct, which nothing sends).
    /// - GC_ITEM_UPDATE (25, 38B): count/socket/attr mutation
    ///   (`CItem::UpdatePacket`, `item.cpp:207-228`).
    ///
    /// Frames arrive both in Loading (initial `ItemLoad`) and Game (live play),
    /// so all three are accepted in both phases (guide §5.3).
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class InventoryClient
    {
        private readonly HandshakeClient _handshake;
        private readonly PacketRegistry _registry;

        public InventoryClient(HandshakeClient handshake, PacketRegistry registry = null)
        {
            if (handshake == null)
            {
                throw new ArgumentNullException(nameof(handshake));
            }

            if (!handshake.Completed || handshake.Session == null)
            {
                throw new HandshakeFailedException("Handshake must be completed before inventory streaming.");
            }

            _handshake = handshake;
            _registry = registry ?? PacketRegistry.CreateGameRegistry();
        }

        /// <summary>
        /// Receives one inventory event in the given phase (Loading or Game).
        /// </summary>
        public async Task<ItemEvent> ReceiveItemAsync(
            PhaseType phase, CancellationToken cancellationToken = default)
        {
            if (phase != PhaseType.Loading && phase != PhaseType.Game)
            {
                throw new ArgumentException("Item frames arrive in Loading or Game phase.", nameof(phase));
            }

            byte[] frame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            byte header = frame[0];

            if (!_registry.IsAllowed(header, phase))
            {
                throw new HandshakeFailedException(
                    $"Item header 0x{header:X2} is not valid in the {phase} phase.");
            }

            if (header == PacketGCItemSet.PacketHeader)
            {
                if (!PacketGCItemSetCodec.TryDeserialize(frame, out PacketGCItemSet set, out string setError))
                {
                    throw new HandshakeFailedException($"Invalid item-set packet: {setError}");
                }

                return ItemEvent.Assigned(set);
            }

            if (header == PacketGCItemDel.PacketHeader)
            {
                if (!PacketGCItemDelCodec.TryDeserialize(frame, out PacketGCItemDel del, out string delError))
                {
                    throw new HandshakeFailedException($"Invalid item-del packet: {delError}");
                }

                return ItemEvent.Cleared(del.Window, del.Cell);
            }

            if (header == PacketGCItemUpdate.PacketHeader)
            {
                if (!PacketGCItemUpdateCodec.TryDeserialize(frame, out PacketGCItemUpdate update, out string updateError))
                {
                    throw new HandshakeFailedException($"Invalid item-update packet: {updateError}");
                }

                return ItemEvent.Mutated(update);
            }

            throw new HandshakeFailedException($"Unexpected item header 0x{header:X2}.");
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
                throw new HandshakeFailedException("Failed to receive item packet.", ex);
            }

            if (frame == null || frame.Length == 0)
            {
                throw new HandshakeFailedException("Empty item frame.");
            }

            return frame;
        }
    }
}
