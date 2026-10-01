using System;
using System.Net;
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
    /// Channel-core login result: either the select-screen data (empire +
    /// character slots) or a server failure status.
    /// </summary>
    public readonly struct ChannelLoginResult
    {
        public bool Succeeded { get; }
        public string Status { get; }
        public ChannelSelectData Data { get; }

        private ChannelLoginResult(bool succeeded, string status, ChannelSelectData data)
        {
            Succeeded = succeeded;
            Status = status ?? string.Empty;
            Data = data;
        }

        public static ChannelLoginResult Success(ChannelSelectData data)
        {
            return new ChannelLoginResult(true, string.Empty, data);
        }

        public static ChannelLoginResult Failure(string status)
        {
            return new ChannelLoginResult(false, status, default);
        }
    }

    /// <summary>
    /// Select-screen data from GC_EMPIRE + GC_LOGIN_SUCCESS_NEWSLOT.
    /// </summary>
    public struct ChannelSelectData
    {
        public byte Empire;
        public SimplePlayer[] Players;
        public uint[] GuildIds;
        public string[] GuildNames;
        public uint Handle;
        public uint RandomKey;
    }

    /// <summary>
    /// Channel-core login over an already-handshaked secure channel
    /// (a fresh channel connection runs its own handshake first).
    /// Wire sequence (docs/protocol/connection-flow.md §4):
    ///   client sends CG_LOGIN2 (109, 52B, encrypted) with the auth-issued
    ///   login key, then server replies GC_EMPIRE (90, 2B, still Login phase)
    ///   and GC_LOGIN_SUCCESS_NEWSLOT (32, 329B, Select phase) — or
    ///   GC_LOGIN_FAILURE (7, 10B) on VERSION/SHUTDOWN/FULL/NOID/WRONGPWD.
    ///
    /// Source anchors:
    /// - Client sends: `PhaseLogin.cpp:254-280` (`SendLoginPacketNew`).
    /// - Server: `input_login.cpp:138-190` (`LoginByKey` → GD_LOGIN_BY_KEY),
    ///   `input_db.cpp:106-175` (`LoginSuccess`: empire, then PHASE_SELECT),
    ///   `desc.cpp:892-919` (`SendLoginSuccessPacket`).
    /// - Client receives: `PhaseLogin.cpp:124-132` (empire),
    ///   `PhaseLogin.cpp:162-188` (4 slots + guilds + mark handle/key).
    ///
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class ChannelLoginClient
    {
        private readonly HandshakeClient _handshake;
        private readonly PacketRegistry _registry;

        public ChannelLoginClient(HandshakeClient handshake, PacketRegistry registry = null)
        {
            if (handshake == null)
            {
                throw new ArgumentNullException(nameof(handshake));
            }

            if (!handshake.Completed || handshake.Session == null)
            {
                throw new HandshakeFailedException("Handshake must be completed before channel login.");
            }

            _handshake = handshake;
            _registry = registry ?? PacketRegistry.CreateChannelRegistry();
        }

        /// <summary>
        /// Sends CG_LOGIN2 (encrypted) with the auth-issued login key.
        /// The caller retains <paramref name="clientKeys"/> for PanamaKey use.
        /// </summary>
        public async Task SendChannelLoginAsync(
            string login, uint loginKey, uint[] clientKeys, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(login))
            {
                throw new ArgumentException("Login must not be null or empty.", nameof(login));
            }

            if (clientKeys == null || clientKeys.Length != PacketCGLogin2.KeyCount)
            {
                throw new ArgumentException(
                    $"Client keys must hold exactly {PacketCGLogin2.KeyCount} words.", nameof(clientKeys));
            }

            var packet = new PacketCGLogin2(login, loginKey, clientKeys);
            byte[] wire = PacketCGLogin2Codec.Serialize(packet);
            try
            {
                await _handshake.SendSecureAsync(wire, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                throw new HandshakeFailedException("Failed to send CG_LOGIN2 packet.", ex);
            }
            finally
            {
                Array.Clear(wire, 0, wire.Length);
            }
        }

        /// <summary>
        /// Receives empire + character list (or a 7 failure status).
        /// Enforces the server's phase order: empire while Login, slots in Select.
        /// A 7 failure is returned as <see cref="ChannelLoginResult.Failure"/>
        /// wherever it appears; anything else unexpected fails closed.
        /// </summary>
        public async Task<ChannelLoginResult> ReceiveSelectDataAsync(CancellationToken cancellationToken = default)
        {
            byte[] first = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            ChannelLoginResult? earlyFailure = TryParseFailure(first);
            if (earlyFailure.HasValue)
            {
                return earlyFailure.Value;
            }

            byte empire = ParseEmpireFrame(first);

            byte[] second = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            ChannelLoginResult? lateFailure = TryParseFailure(second);
            if (lateFailure.HasValue)
            {
                return lateFailure.Value;
            }

            byte header = second[0];
            if (!_registry.IsAllowed(header, PhaseType.Select))
            {
                throw new HandshakeFailedException(
                    $"Character-list header 0x{header:X2} is not valid in the Select phase.");
            }

            if (header != PacketGCLoginSuccess.PacketHeader)
            {
                throw new HandshakeFailedException($"Unexpected character-list header 0x{header:X2}.");
            }

            if (!PacketGCLoginSuccessCodec.TryDeserialize(second, out PacketGCLoginSuccess success, out string error))
            {
                throw new HandshakeFailedException($"Invalid GC_LOGIN_SUCCESS packet: {error}");
            }

            return ChannelLoginResult.Success(new ChannelSelectData
            {
                Empire = empire,
                Players = success.Players,
                GuildIds = success.GuildIds,
                GuildNames = success.GuildNames,
                Handle = success.Handle,
                RandomKey = success.RandomKey
            });
        }

        private static ChannelLoginResult? TryParseFailure(byte[] frame)
        {
            if (frame[0] != PacketGCLoginFailure.PacketHeader)
            {
                return null;
            }

            if (!PacketGCLoginFailureCodec.TryDeserialize(frame, out PacketGCLoginFailure failure, out string failureError))
            {
                throw new HandshakeFailedException($"Invalid GC_LOGIN_FAILURE packet: {failureError}");
            }

            return ChannelLoginResult.Failure(failure.Status);
        }

        private byte ParseEmpireFrame(byte[] frame)
        {
            byte header = frame[0];
            if (!_registry.IsAllowed(header, PhaseType.Login))
            {
                throw new HandshakeFailedException(
                    $"Empire header 0x{header:X2} is not valid in the Login phase.");
            }

            if (header != PacketGCEmpire.PacketHeader)
            {
                throw new HandshakeFailedException($"Unexpected empire header 0x{header:X2}.");
            }

            if (!PacketGCEmpireCodec.TryDeserialize(frame, out PacketGCEmpire empire, out string error))
            {
                throw new HandshakeFailedException($"Invalid GC_EMPIRE packet: {error}");
            }

            return empire.Empire;
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
                throw new HandshakeFailedException("Failed to receive channel-login reply.", ex);
            }

            if (frame == null || frame.Length == 0)
            {
                throw new HandshakeFailedException("Empty channel-login reply frame.");
            }

            return frame;
        }

        /// <summary>
        /// Game-core endpoint for a character slot, mirroring
        /// `CPythonNetworkStream::ConnectGameServer` (`PythonNetworkStream.cpp:462-472`):
        /// lAddr is used verbatim as the IPv4 address (it holds `inet_addr()`
        /// output, i.e. network byte order — `map_location.cpp:47`), wPort as-is.
        /// </summary>
        public static (IPAddress Address, ushort Port) GetSlotEndpoint(SimplePlayer slot)
        {
            if (slot.Id == 0)
            {
                throw new HandshakeFailedException("Character slot is empty; no game endpoint.");
            }

            // AddrNetworkOrder holds the inet_addr() s_addr read as a little-endian
            // word (e.g. 127.0.0.1 travels as bytes 7F 00 00 01, read as
            // 0x0100007F), so the dotted octets are the value's bytes taken
            // least-significant first. Expanded explicitly (no BitConverter,
            // hence platform-endian independent).
            uint raw = slot.AddrNetworkOrder;
            byte[] octets = new byte[]
            {
                (byte)raw, (byte)(raw >> 8), (byte)(raw >> 16), (byte)(raw >> 24)
            };
            return (new IPAddress(octets), slot.Port);
        }
    }
}
