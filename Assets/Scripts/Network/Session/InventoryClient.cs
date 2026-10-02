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

        /// <summary>
        /// Sends CG_ITEM_USE (11, 4B, encrypted): use the item at the cell.
        /// The server re-validates everything (observer mode, item state); the
        /// outcome arrives via the item/point update packets — there is no
        /// direct use-reply packet.
        /// </summary>
        public async Task SendUseItemAsync(
            byte window, ushort cell, CancellationToken cancellationToken = default)
        {
            ValidateWindow(window, nameof(window));

            var packet = new PacketCGItemUse(window, cell);
            await SendPacketAsync(PacketCGItemUseCodec.Serialize(packet), "CG_ITEM_USE", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends CG_ITEM_USE_TO_ITEM (60, 7B, encrypted): apply the item at
        /// Cell onto the item at TargetCell (e.g. upgrade stone on equipment).
        /// </summary>
        public async Task SendUseItemToItemAsync(
            byte window, ushort cell, byte targetWindow, ushort targetCell,
            CancellationToken cancellationToken = default)
        {
            ValidateWindow(window, nameof(window));
            ValidateWindow(targetWindow, nameof(targetWindow));

            var packet = new PacketCGItemUseToItem(window, cell, targetWindow, targetCell);
            await SendPacketAsync(PacketCGItemUseToItemCodec.Serialize(packet), "CG_ITEM_USE_TO_ITEM", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends CG_ITEM_MOVE (13, 8B, encrypted): move / stack / equip.
        /// count 0 moves the whole stack; an equipment destination triggers
        /// the server-side equip path (char_item.cpp:5557+ MoveItem).
        /// </summary>
        public async Task SendMoveItemAsync(
            byte window, ushort cell, byte windowTo, ushort cellTo, byte count = 0,
            CancellationToken cancellationToken = default)
        {
            ValidateWindow(window, nameof(window));
            ValidateWindow(windowTo, nameof(windowTo));

            var packet = new PacketCGItemMove(window, cell, windowTo, cellTo, count);
            await SendPacketAsync(PacketCGItemMoveCodec.Serialize(packet), "CG_ITEM_MOVE", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends CG_ITEM_DROP (12, 8B, encrypted) with gold 0: drop the whole
        /// item stack at the cell (input_main.cpp:842-854).
        /// </summary>
        public async Task SendDropItemAsync(
            byte window, ushort cell, CancellationToken cancellationToken = default)
        {
            ValidateWindow(window, nameof(window));

            var packet = new PacketCGItemDrop(window, cell, gold: 0);
            await SendPacketAsync(PacketCGItemDropCodec.Serialize(packet), "CG_ITEM_DROP", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends CG_ITEM_DROP (12, 8B, encrypted) with gold &gt; 0: drop gold
        /// (the cell is ignored by the server on this path).
        /// </summary>
        public async Task SendDropGoldAsync(
            uint gold, CancellationToken cancellationToken = default)
        {
            if (gold == 0)
            {
                throw new ArgumentException("Gold amount must be positive to drop gold.", nameof(gold));
            }

            var packet = new PacketCGItemDrop(ItemWindow.Reserved, ushort.MaxValue, gold);
            await SendPacketAsync(PacketCGItemDropCodec.Serialize(packet), "CG_ITEM_DROP", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends CG_ITEM_DROP2 (20, 9B, encrypted): drop a partial stack
        /// (input_main.cpp:856-869; client SendItemDropPacketNew).
        /// </summary>
        public async Task SendDropItemPartialAsync(
            byte window, ushort cell, byte count, CancellationToken cancellationToken = default)
        {
            ValidateWindow(window, nameof(window));

            if (count == 0)
            {
                throw new ArgumentException("Count must be positive for a partial drop.", nameof(count));
            }

            var packet = new PacketCGItemDrop2(window, cell, gold: 0, count);
            await SendPacketAsync(PacketCGItemDrop2Codec.Serialize(packet), "CG_ITEM_DROP2", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends CG_ITEM_PICKUP (15, 5B, encrypted): pick up a ground item by
        /// VID. Distance/ownership are validated server-side; the item arrives
        /// as GC_ITEM_SET.
        /// </summary>
        public async Task SendPickupAsync(
            uint vid, CancellationToken cancellationToken = default)
        {
            if (vid == 0)
            {
                throw new ArgumentException("Ground item VID must not be zero.", nameof(vid));
            }

            var packet = new PacketCGItemPickup(vid);
            await SendPacketAsync(PacketCGItemPickupCodec.Serialize(packet), "CG_ITEM_PICKUP", cancellationToken).ConfigureAwait(false);
        }

        private async Task SendPacketAsync(byte[] wire, string name, CancellationToken cancellationToken)
        {
            try
            {
                await _handshake.SendSecureAsync(wire, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                throw new HandshakeFailedException($"Failed to send {name} packet.", ex);
            }
        }

        /// <summary>
        /// Fail-closed obvious-garbage guard only: the reserved window is the
        /// NPOS marker and is rejected by the server's IsValidItemPosition
        /// (length.h:701-720). All real validation stays server-side.
        /// </summary>
        private static void ValidateWindow(byte window, string paramName)
        {
            if (window == ItemWindow.Reserved)
            {
                throw new ArgumentException("Window must not be the reserved NPOS window.", paramName);
            }
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
