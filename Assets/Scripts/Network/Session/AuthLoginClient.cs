using System;
using System.Security.Cryptography;
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
    /// Auth-core login result: either success (server-issued login key for the
    /// channel-core hop) or failure (server status string shown on the login UI).
    /// </summary>
    public readonly struct AuthLoginResult
    {
        public bool Succeeded { get; }
        public uint LoginKey { get; }
        public string Status { get; }

        private AuthLoginResult(bool succeeded, uint loginKey, string status)
        {
            Succeeded = succeeded;
            LoginKey = loginKey;
            Status = status ?? string.Empty;
        }

        public static AuthLoginResult Success(uint loginKey)
        {
            return new AuthLoginResult(true, loginKey, string.Empty);
        }

        public static AuthLoginResult Failure(string status)
        {
            return new AuthLoginResult(false, 0, status);
        }

        public override string ToString()
        {
            return Succeeded
                ? $"AuthLoginResult(Success, LoginKey={LoginKey})"
                : $"AuthLoginResult(Failure, Status='{Status}')";
        }
    }

    /// <summary>
    /// Auth-core login over an already-handshaked secure channel.
    /// Wire sequence (docs/protocol/connection-flow.md §3):
    ///   client sends CG_LOGIN3 (111, 65B, encrypted) after PHASE_AUTH, then
    ///   server replies GC_AUTH_SUCCESS (150, 6B) or GC_LOGIN_FAILURE (7, 10B).
    ///
    /// Source anchors:
    /// - Client sends on GC_PHASE(PHASE_AUTH): `AccountConnector.cpp:166-212`
    ///   (login3 carries `g_adwEncryptKey[4]` as adwClientKey).
    /// - Server: `input_auth.cpp:102-200` (Login → DB query), reply
    ///   `input_db.cpp:1679-1712` (150, key + bResult) or `input.cpp:177-188`
    ///   (7, status string).
    /// - Client receives: `AccountConnector.cpp:312-352` (150 with bResult==0
    ///   maps to "BESAMEKEY"; 7 carries the status string).
    ///
    /// Phase guard: replies are accepted only when the auth registry allows
    /// them in <see cref="PhaseType.Auth"/> (guide §5.3). Passwords are never
    /// included in exceptions or logs (guide §5.3/§6.1; SecretRedactor).
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class AuthLoginClient
    {
        private readonly HandshakeClient _handshake;
        private readonly PacketRegistry _registry;

        public AuthLoginClient(HandshakeClient handshake, PacketRegistry registry = null)
        {
            if (handshake == null)
            {
                throw new ArgumentNullException(nameof(handshake));
            }

            if (!handshake.Completed || handshake.Session == null)
            {
                throw new HandshakeFailedException("Handshake must be completed before auth login.");
            }

            _handshake = handshake;
            _registry = registry ?? PacketRegistry.CreateAuthRegistry();
        }

        /// <summary>
        /// Sends CG_LOGIN3 (encrypted). adwClientKey must hold exactly 4 words
        /// (see <see cref="GenerateClientKeys"/>); they are later XORed into the
        /// PanamaKey, so the caller must retain them until login completes.
        /// </summary>
        public async Task SendLoginAsync(
            string login, string password, uint[] clientKeys, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(login))
            {
                throw new ArgumentException("Login must not be null or empty.", nameof(login));
            }

            if (string.IsNullOrEmpty(password))
            {
                // Value never echoed: the message carries no credential material.
                throw new ArgumentException("Password must not be null or empty.", nameof(password));
            }

            if (clientKeys == null || clientKeys.Length != PacketCGLogin3.KeyCount)
            {
                throw new ArgumentException(
                    $"Client keys must hold exactly {PacketCGLogin3.KeyCount} words.", nameof(clientKeys));
            }

            var packet = new PacketCGLogin3(login, password, clientKeys);
            byte[] wire = PacketCGLogin3Codec.Serialize(packet);
            try
            {
                await _handshake.SendSecureAsync(wire, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                throw new HandshakeFailedException("Failed to send CG_LOGIN3 packet.", ex);
            }
            finally
            {
                Array.Clear(wire, 0, wire.Length);
            }
        }

        /// <summary>
        /// Receives one auth reply: 150 → success (or "BESAMEKEY" when bResult
        /// is zero, mirroring AccountConnector.cpp:318-322), 7 → failure with
        /// the server status string. Anything else fails closed.
        /// </summary>
        public async Task<AuthLoginResult> ReceiveResultAsync(CancellationToken cancellationToken = default)
        {
            byte[] frame;
            try
            {
                frame = await _handshake.ReceiveSecureFrameAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                throw new HandshakeFailedException("Failed to receive auth reply.", ex);
            }

            if (frame == null || frame.Length == 0)
            {
                throw new HandshakeFailedException("Empty auth reply frame.");
            }

            byte header = frame[0];
            if (!_registry.IsAllowed(header, PhaseType.Auth))
            {
                throw new HandshakeFailedException(
                    $"Auth reply header 0x{header:X2} is not valid in the Auth phase.");
            }

            if (header == PacketGCAuthSuccess.PacketHeader)
            {
                if (!PacketGCAuthSuccessCodec.TryDeserialize(frame, out PacketGCAuthSuccess success, out string error))
                {
                    throw new HandshakeFailedException($"Invalid GC_AUTH_SUCCESS packet: {error}");
                }

                if (!success.Succeeded)
                {
                    return AuthLoginResult.Failure("BESAMEKEY");
                }

                return AuthLoginResult.Success(success.LoginKey);
            }

            if (header == PacketGCLoginFailure.PacketHeader)
            {
                if (!PacketGCLoginFailureCodec.TryDeserialize(frame, out PacketGCLoginFailure failure, out string failureError))
                {
                    throw new HandshakeFailedException($"Invalid GC_LOGIN_FAILURE packet: {failureError}");
                }

                return AuthLoginResult.Failure(failure.Status);
            }

            throw new HandshakeFailedException($"Unexpected auth reply header 0x{header:X2}.");
        }

        /// <summary>
        /// PanamaKey derivation, identical on both sides:
        /// server `input_auth.cpp:151`, client `AccountConnector.cpp:325`.
        /// key ^ clientKeys[0..3].
        /// </summary>
        public static uint ComputePanamaKey(uint loginKey, uint[] clientKeys)
        {
            if (clientKeys == null || clientKeys.Length != PacketCGLogin3.KeyCount)
            {
                throw new ArgumentException(
                    $"Client keys must hold exactly {PacketCGLogin3.KeyCount} words.", nameof(clientKeys));
            }

            return loginKey ^ clientKeys[0] ^ clientKeys[1] ^ clientKeys[2] ^ clientKeys[3];
        }

        /// <summary>
        /// Fresh per-login client keys (the C++ client sends its
        /// `g_adwEncryptKey[4]`; freshness gives a fresh PanamaKey).
        /// </summary>
        public static uint[] GenerateClientKeys()
        {
            uint[] keys = new uint[PacketCGLogin3.KeyCount];
            byte[] raw = new byte[PacketCGLogin3.KeyCount * 4];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(raw);
            }

            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = (uint)(raw[i * 4]
                    | (raw[i * 4 + 1] << 8)
                    | (raw[i * 4 + 2] << 16)
                    | (raw[i * 4 + 3] << 24));
            }

            Array.Clear(raw, 0, raw.Length);
            return keys;
        }
    }
}
