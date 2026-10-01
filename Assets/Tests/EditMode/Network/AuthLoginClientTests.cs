using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Metin2.Network.Session;
using Metin2.Network.Transport;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;
using Metin2.Protocol.Security;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Network
{
    [TestFixture]
    public class AuthLoginClientTests
    {
        private const int TestTimeoutMs = 15000;

        private static CancellationToken TestToken(CancellationTokenSource cts)
        {
            cts.CancelAfter(TestTimeoutMs);
            return cts.Token;
        }

        /// <summary>
        /// Loopback handshake: real DH2 on both sides, real engines.
        /// Returns the client's completed HandshakeClient plus the server's
        /// activated session and stream for the login round.
        /// </summary>
        private sealed class LoginHarness : IDisposable
        {
            public TcpListener Listener;
            public TcpConnection Connection;
            public HandshakeClient Client;
            public TcpClient ServerTcp;
            public NetworkStream ServerStream;
            public CipherSession ServerSession;

            public static async Task<LoginHarness> EstablishAsync(CancellationToken token)
            {
                var harness = new LoginHarness();
                harness.Listener = new TcpListener(IPAddress.Loopback, 0);
                harness.Listener.Start();
                int port = ((IPEndPoint)harness.Listener.LocalEndpoint).Port;
                Task<TcpClient> acceptTask = harness.Listener.AcceptTcpClientAsync();

                harness.Connection = new TcpConnection();
                await harness.Connection.ConnectAsync("127.0.0.1", port, token).ConfigureAwait(false);
                harness.ServerTcp = await acceptTask.ConfigureAwait(false);
                harness.ServerStream = harness.ServerTcp.GetStream();

                Task serverHandshake = Task.Run(async () =>
                {
                    using var serverAgreement = Dh2KeyAgreement.Generate();
                    byte[] serverPub = serverAgreement.ExportPublicData();

                    byte[] hs = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(0x11223344, 555, 6));
                    await harness.ServerStream.WriteAsync(hs, 0, hs.Length, token).ConfigureAwait(false);

                    var serverKa = new PacketKeyAgreement(
                        DiffieHellmanGroup.AgreedValueLength, DiffieHellmanGroup.KeyDataLength, serverPub);
                    byte[] ka = PacketKeyAgreementCodec.Serialize(serverKa);
                    await harness.ServerStream.WriteAsync(ka, 0, ka.Length, token).ConfigureAwait(false);
                    await harness.ServerStream.FlushAsync(token).ConfigureAwait(false);

                    byte[] cgReply = await ReadExactAsync(harness.ServerStream, PacketKeyAgreement.PacketSize, token).ConfigureAwait(false);
                    Assert.IsTrue(PacketKeyAgreementCodec.TryDeserialize(cgReply, out PacketKeyAgreement cgKa, out string _));
                    Assert.IsTrue(serverAgreement.TryAgree(cgKa.AgreedLength, cgKa.Data, out byte[] serverShared));
                    Assert.IsTrue(CipherKeyDerivation.TryDerive(serverShared, out CipherKeyMaterial material));
                    Array.Clear(serverShared, 0, serverShared.Length);

                    harness.ServerSession = new CipherSession(false, material, BlockCipherEngineFactory.ForSession());

                    byte[] done = new byte[] { PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED, 0, 0, 0 };
                    await harness.ServerStream.WriteAsync(done, 0, done.Length, token).ConfigureAwait(false);
                    await harness.ServerStream.FlushAsync(token).ConfigureAwait(false);
                    harness.ServerSession.SetActivated(true);
                }, token);

                harness.Client = new HandshakeClient(harness.Connection);
                await harness.Client.RunAsync(token).ConfigureAwait(false);
                await serverHandshake.ConfigureAwait(false);

                Assert.IsTrue(harness.Client.Completed);
                return harness;
            }

            public async Task<byte[]> ReadDecryptedAsync(int length, CancellationToken token)
            {
                byte[] wire = await ReadExactAsync(ServerStream, length, token).ConfigureAwait(false);
                ServerSession.Decrypt(wire, 0, wire.Length);
                return wire;
            }

            public async Task WriteEncryptedAsync(byte[] plain, CancellationToken token)
            {
                byte[] wire = (byte[])plain.Clone();
                ServerSession.Encrypt(wire, 0, wire.Length);
                await ServerStream.WriteAsync(wire, 0, wire.Length, token).ConfigureAwait(false);
                await ServerStream.FlushAsync(token).ConfigureAwait(false);
            }

            public void Dispose()
            {
                ServerSession?.Dispose();
                ServerTcp?.Close();
                Client?.Dispose();
                Connection?.Dispose();
                Listener?.Stop();
            }
        }

        private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken token)
        {
            byte[] buffer = new byte[length];
            int total = 0;
            while (total < length)
            {
                int read = await stream.ReadAsync(buffer, total, length - total, token).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new InvalidOperationException("Peer closed before full packet arrived.");
                }

                total += read;
            }

            return buffer;
        }

        [Test]
        public async Task Login_Success_ReturnsLoginKeyAndPanamaKeyMatches()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await LoginHarness.EstablishAsync(token).ConfigureAwait(false);
            var auth = new AuthLoginClient(harness.Client);

            uint[] clientKeys = new uint[] { 0x11111111, 0x22222222, 0x33333333, 0x44444444 };

            Task serverRound = Task.Run(async () =>
            {
                byte[] loginWire = await harness.ReadDecryptedAsync(PacketCGLogin3.PacketSize, token).ConfigureAwait(false);
                Assert.IsTrue(PacketCGLogin3Codec.TryDeserialize(loginWire, out PacketCGLogin3 login3, out string _));
                Assert.AreEqual("testuser", login3.Login);
                Assert.AreEqual("s3cret!", login3.Password);
                CollectionAssert.AreEqual(clientKeys, login3.ClientKeys);

                // Server formula input_auth.cpp:151.
                uint panama = login3.ClientKeys[0] ^ login3.ClientKeys[1] ^ login3.ClientKeys[2] ^ login3.ClientKeys[3] ^ 0xAABBCCDD;
                Assert.AreEqual(
                    AuthLoginClient.ComputePanamaKey(0xAABBCCDD, clientKeys), panama);

                await harness.WriteEncryptedAsync(
                    PacketGCAuthSuccessCodec.Serialize(new PacketGCAuthSuccess(0xAABBCCDD, 1)), token).ConfigureAwait(false);
            }, token);

            await auth.SendLoginAsync("testuser", "s3cret!", clientKeys, token).ConfigureAwait(false);
            AuthLoginResult result = await auth.ReceiveResultAsync(token).ConfigureAwait(false);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(0xAABBCCDDu, result.LoginKey);
            Assert.AreEqual(
                AuthLoginClient.ComputePanamaKey(result.LoginKey, clientKeys),
                0xAABBCCDDu ^ 0x11111111u ^ 0x22222222u ^ 0x33333333u ^ 0x44444444u);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task Login_Failure_WrongPassword_ReturnsStatus()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await LoginHarness.EstablishAsync(token).ConfigureAwait(false);
            var auth = new AuthLoginClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                byte[] loginWire = await harness.ReadDecryptedAsync(PacketCGLogin3.PacketSize, token).ConfigureAwait(false);
                Assert.IsTrue(PacketCGLogin3Codec.TryDeserialize(loginWire, out PacketCGLogin3 _, out string _));

                await harness.WriteEncryptedAsync(
                    PacketGCLoginFailureCodec.Serialize(new PacketGCLoginFailure("WRONGPWD")), token).ConfigureAwait(false);
            }, token);

            await auth.SendLoginAsync("testuser", "wrong!", AuthLoginClient.GenerateClientKeys(), token).ConfigureAwait(false);
            AuthLoginResult result = await auth.ReceiveResultAsync(token).ConfigureAwait(false);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("WRONGPWD", result.Status);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task Login_ZeroResult_MapsToBesamekey()
        {
            // Mirrors AccountConnector.cpp:318-322 (150 with bResult==0).
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await LoginHarness.EstablishAsync(token).ConfigureAwait(false);
            var auth = new AuthLoginClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.ReadDecryptedAsync(PacketCGLogin3.PacketSize, token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(
                    PacketGCAuthSuccessCodec.Serialize(new PacketGCAuthSuccess(0, 0)), token).ConfigureAwait(false);
            }, token);

            await auth.SendLoginAsync("testuser", "s3cret!", AuthLoginClient.GenerateClientKeys(), token).ConfigureAwait(false);
            AuthLoginResult result = await auth.ReceiveResultAsync(token).ConfigureAwait(false);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("BESAMEKEY", result.Status);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task Login_FragmentedReply_Succeeds()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await LoginHarness.EstablishAsync(token).ConfigureAwait(false);
            var auth = new AuthLoginClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.ReadDecryptedAsync(PacketCGLogin3.PacketSize, token).ConfigureAwait(false);

                byte[] plain = PacketGCAuthSuccessCodec.Serialize(new PacketGCAuthSuccess(42, 1));
                byte[] wire = (byte[])plain.Clone();
                harness.ServerSession.Encrypt(wire, 0, wire.Length);
                await harness.ServerStream.WriteAsync(wire, 0, 2, token).ConfigureAwait(false);
                await harness.ServerStream.WriteAsync(wire, 2, wire.Length - 2, token).ConfigureAwait(false);
                await harness.ServerStream.FlushAsync(token).ConfigureAwait(false);
            }, token);

            await auth.SendLoginAsync("testuser", "s3cret!", AuthLoginClient.GenerateClientKeys(), token).ConfigureAwait(false);
            AuthLoginResult result = await auth.ReceiveResultAsync(token).ConfigureAwait(false);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(42u, result.LoginKey);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task Login_WrongPhaseHeader_ThrowsFailClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await LoginHarness.EstablishAsync(token).ConfigureAwait(false);
            var auth = new AuthLoginClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.ReadDecryptedAsync(PacketCGLogin3.PacketSize, token).ConfigureAwait(false);

                // 0xff frames fine (13B) but is Handshake-only per the registry.
                byte[] fake = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(1, 2, 3));
                await harness.WriteEncryptedAsync(fake, token).ConfigureAwait(false);
            }, token);

            await auth.SendLoginAsync("testuser", "s3cret!", AuthLoginClient.GenerateClientKeys(), token).ConfigureAwait(false);
            Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                await auth.ReceiveResultAsync(token).ConfigureAwait(false));

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task SendLogin_EmptyCredentials_ThrowWithoutLeakingPassword()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await LoginHarness.EstablishAsync(token).ConfigureAwait(false);
            var auth = new AuthLoginClient(harness.Client);

            const string secret = "Sup3rS3cretPw!";
            var ex1 = Assert.ThrowsAsync<ArgumentException>(async () =>
                await auth.SendLoginAsync(string.Empty, secret, AuthLoginClient.GenerateClientKeys(), token).ConfigureAwait(false));
            var ex2 = Assert.ThrowsAsync<ArgumentException>(async () =>
                await auth.SendLoginAsync("testuser", string.Empty, AuthLoginClient.GenerateClientKeys(), token).ConfigureAwait(false));

            Assert.IsFalse(ex1.Message.Contains(secret));
            Assert.IsFalse(ex2.Message.Contains(secret));
        }

        [Test]
        public void Constructor_IncompleteHandshake_Throws()
        {
            using var conn = new TcpConnection();
            using var handshake = new HandshakeClient(conn);

            Assert.Throws<HandshakeFailedException>(() => new AuthLoginClient(handshake));
        }

        [Test]
        public void ComputePanamaKey_MatchesServerFormula()
        {
            // input_auth.cpp:151 — key ^ k0 ^ k1 ^ k2 ^ k3.
            uint key = 0xDEADBEEF;
            uint[] keys = new uint[] { 1, 2, 3, 4 };

            Assert.AreEqual(
                (uint)(0xDEADBEEF ^ 1 ^ 2 ^ 3 ^ 4),
                AuthLoginClient.ComputePanamaKey(key, keys));
        }

        [Test]
        public void GenerateClientKeys_ReturnsFourFreshWords()
        {
            uint[] first = AuthLoginClient.GenerateClientKeys();
            uint[] second = AuthLoginClient.GenerateClientKeys();

            Assert.AreEqual(4, first.Length);
            Assert.AreEqual(4, second.Length);
            CollectionAssert.AreNotEqual(first, second);
        }
    }
}
