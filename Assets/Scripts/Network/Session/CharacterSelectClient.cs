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
    /// Character create result: success carries the slot + fresh server
    /// TSimplePlayer, failure carries the server bType (0 blocked/overlong/
    /// bad-job, 1 bad-name).
    /// </summary>
    public readonly struct CharacterCreateResult
    {
        public bool Succeeded { get; }
        public byte Slot { get; }
        public SimplePlayer Player { get; }
        public byte FailureType { get; }

        private CharacterCreateResult(bool succeeded, byte slot, SimplePlayer player, byte failureType)
        {
            Succeeded = succeeded;
            Slot = slot;
            Player = player;
            FailureType = failureType;
        }

        public static CharacterCreateResult Success(byte slot, SimplePlayer player)
        {
            return new CharacterCreateResult(true, slot, player, 0);
        }

        public static CharacterCreateResult Failure(byte type)
        {
            return new CharacterCreateResult(false, 0, default, type);
        }
    }

    /// <summary>
    /// Character delete result: success carries the cleared slot index.
    /// </summary>
    public readonly struct CharacterDeleteResult
    {
        public bool Succeeded { get; }
        public byte Index { get; }

        private CharacterDeleteResult(bool succeeded, byte index)
        {
            Succeeded = succeeded;
            Index = index;
        }

        public static CharacterDeleteResult Success(byte index)
        {
            return new CharacterDeleteResult(true, index);
        }

        public static CharacterDeleteResult Failure()
        {
            return new CharacterDeleteResult(false, 0);
        }
    }

    /// <summary>
    /// Select-phase client over an already-handshaked secure channel.
    /// Wire sequences (docs/protocol/connection-flow.md §5):
    /// - Select: CG_CHARACTER_SELECT (6, 2B) → NO direct reply (server only
    ///   forwards GD_PLAYER_LOAD to DB, `input_login.cpp:222-255`); the world
    ///   entry continues with CG_ENTERGAME (10, 1B).
    /// - Create: CG_CHARACTER_CREATE (4, 34B) → 8 (65B, slot + TSimplePlayer)
    ///   or 9 (2B, bType).
    /// - Delete: CG_CHARACTER_DELETE (5, 10B) → 10 (2B, slot) or 11 (1B).
    /// - Empire (empire-less accounts): CG_EMPIRE (90, 2B) → GD_EMPIRE_SELECT.
    ///
    /// Source anchors:
    /// - Client sends: `PhaseSelect.cpp:145-231`.
    /// - Server: `input_login.cpp:222-255,416-535,546-579,792-822`,
    ///   `input_db.cpp:190-300`, dispatch `input_login.cpp:994-1096`
    ///   (SELECT and LOGIN share m_inputLogin, `desc.cpp:539-547`).
    /// - Client receives: `PhaseSelect.cpp:233-286` (create/delete results).
    ///
    /// Phase guard: 8/9/10/11 accepted in Select only (guide §5.3).
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class CharacterSelectClient
    {
        private readonly HandshakeClient _handshake;
        private readonly PacketRegistry _registry;

        public CharacterSelectClient(HandshakeClient handshake, PacketRegistry registry = null)
        {
            if (handshake == null)
            {
                throw new ArgumentNullException(nameof(handshake));
            }

            if (!handshake.Completed || handshake.Session == null)
            {
                throw new HandshakeFailedException("Handshake must be completed before character select.");
            }

            _handshake = handshake;
            _registry = registry ?? PacketRegistry.CreateSelectRegistry();
        }

        /// <summary>
        /// Sends CG_CHARACTER_SELECT (server replies nothing directly).
        /// </summary>
        public async Task SendSelectAsync(byte index, CancellationToken cancellationToken = default)
        {
            RequireSlot(index, nameof(index));
            await SendSecureAsync(
                PacketCGCharacterSelectCodec.Serialize(new PacketCGCharacterSelect(index)),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends CG_EMPIRE for empire-less accounts (server closes on
        /// empire &gt;= EMPIRE_MAX_NUM=4, `input_login.cpp:796-800`).
        /// </summary>
        public async Task SendSelectEmpireAsync(byte empire, CancellationToken cancellationToken = default)
        {
            if (empire > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(empire), "Empire must be 0..3 (server closes the connection otherwise).");
            }

            // Wire-identical to the S2C empire packet (header 90 both ways).
            await SendSecureAsync(
                PacketGCEmpireCodec.Serialize(new PacketGCEmpire(empire)),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends CG_CHARACTER_CREATE. Name is server-limited to 12 chars
        /// (`input_login.cpp:437`); longer names fail closed here.
        /// </summary>
        public async Task SendCreateAsync(
            byte index, string name, ushort job, byte shape,
            byte con, byte @int, byte str, byte dex,
            CancellationToken cancellationToken = default)
        {
            RequireSlot(index, nameof(index));
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Character name must not be null or empty.", nameof(name));
            }

            if (name.Length > PacketCGCharacterCreate.NameMaxChars)
            {
                throw new ArgumentException(
                    $"Character name must be at most {PacketCGCharacterCreate.NameMaxChars} characters (server rejects longer).",
                    nameof(name));
            }

            await SendSecureAsync(
                PacketCGCharacterCreateCodec.Serialize(
                    new PacketCGCharacterCreate(index, name, job, shape, con, @int, str, dex)),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Awaits one create reply: 8 → success, 9 → typed failure.
        /// </summary>
        public async Task<CharacterCreateResult> AwaitCreateResultAsync(CancellationToken cancellationToken = default)
        {
            byte[] frame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            byte header = frame[0];

            if (!_registry.IsAllowed(header, PhaseType.Select))
            {
                throw new HandshakeFailedException(
                    $"Create-reply header 0x{header:X2} is not valid in the Select phase.");
            }

            if (header == PacketGCCreateSuccess.PacketHeader)
            {
                if (!PacketGCCreateSuccessCodec.TryDeserialize(frame, out PacketGCCreateSuccess success, out string error))
                {
                    throw new HandshakeFailedException($"Invalid create-success packet: {error}");
                }

                if (success.Slot >= PacketGCLoginSuccess.SlotCount)
                {
                    throw new HandshakeFailedException($"Create-success slot out of range: {success.Slot}.");
                }

                return CharacterCreateResult.Success(success.Slot, success.Player);
            }

            if (header == PacketGCCreateFailure.PacketHeader)
            {
                if (!PacketGCCreateFailureCodec.TryDeserialize(frame, out PacketGCCreateFailure failure, out string failureError))
                {
                    throw new HandshakeFailedException($"Invalid create-failure packet: {failureError}");
                }

                return CharacterCreateResult.Failure(failure.Type);
            }

            throw new HandshakeFailedException($"Unexpected create-reply header 0x{header:X2}.");
        }

        /// <summary>
        /// Sends CG_CHARACTER_DELETE (server: silent on no-account/overflow,
        /// 1-byte 11 on empty slot, else DB round-trip).
        /// </summary>
        public async Task SendDeleteAsync(byte index, string privateCode, CancellationToken cancellationToken = default)
        {
            RequireSlot(index, nameof(index));
            if (privateCode == null)
            {
                throw new ArgumentNullException(nameof(privateCode));
            }

            if (privateCode.Length > PacketCGCharacterDelete.PrivateCodeBufferLen)
            {
                throw new ArgumentException(
                    $"Private code must be at most {PacketCGCharacterDelete.PrivateCodeBufferLen} characters.",
                    nameof(privateCode));
            }

            await SendSecureAsync(
                PacketCGCharacterDeleteCodec.Serialize(new PacketCGCharacterDelete(index, privateCode)),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Awaits one delete reply: 10 → cleared slot, 11 → failure.
        /// </summary>
        public async Task<CharacterDeleteResult> AwaitDeleteResultAsync(CancellationToken cancellationToken = default)
        {
            byte[] frame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            byte header = frame[0];

            if (!_registry.IsAllowed(header, PhaseType.Select))
            {
                throw new HandshakeFailedException(
                    $"Delete-reply header 0x{header:X2} is not valid in the Select phase.");
            }

            if (header == PacketGCDeleteSuccess.PacketHeader)
            {
                if (!PacketGCDeleteSuccessCodec.TryDeserialize(frame, out PacketGCDeleteSuccess success, out string error))
                {
                    throw new HandshakeFailedException($"Invalid delete-success packet: {error}");
                }

                return CharacterDeleteResult.Success(success.Index);
            }

            if (header == PacketGCDeleteFailure.PacketHeader)
            {
                return CharacterDeleteResult.Failure();
            }

            throw new HandshakeFailedException($"Unexpected delete-reply header 0x{header:X2}.");
        }

        /// <summary>
        /// Sends CG_ENTERGAME (server needs a DB-loaded character, else it
        /// closes the connection — `input_login.cpp:546-554`).
        /// </summary>
        public async Task SendEnterGameAsync(CancellationToken cancellationToken = default)
        {
            await SendSecureAsync(
                PacketCGEnterGameCodec.Serialize(new PacketCGEnterGame()),
                cancellationToken).ConfigureAwait(false);
        }

        private async Task SendSecureAsync(byte[] plain, CancellationToken cancellationToken)
        {
            try
            {
                await _handshake.SendSecureAsync(plain, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                throw new HandshakeFailedException("Failed to send select-phase packet.", ex);
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
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
                throw new HandshakeFailedException("Failed to receive select-phase reply.", ex);
            }

            if (frame == null || frame.Length == 0)
            {
                throw new HandshakeFailedException("Empty select-phase reply frame.");
            }

            return frame;
        }

        private static void RequireSlot(byte index, string paramName)
        {
            if (index >= PacketGCLoginSuccess.SlotCount)
            {
                throw new ArgumentOutOfRangeException(
                    paramName, $"Slot must be 0..{PacketGCLoginSuccess.SlotCount - 1} (server ignores wider values silently).");
            }
        }
    }
}
