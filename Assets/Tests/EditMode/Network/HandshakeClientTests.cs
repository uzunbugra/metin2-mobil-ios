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
    public class HandshakeClientTests
    {
        private const int TestTimeoutMs = 15000;

        private static CancellationToken TestToken(CancellationTokenSource cts)
        {
            cts.CancelAfter(TestTimeoutMs);
            return cts.Token;
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

        private static async Task WriteAllAsync(NetworkStream stream, byte[] data, CancellationToken token)
        {
            await stream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        [Test]
        public async Task FullHandshake_Loopback_EncryptedPhaseRoundTrip()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

                using var conn = new TcpConnection();
                await conn.ConnectAsync("127.0.0.1", port, token).ConfigureAwait(false);
                using TcpClient serverTcp = await acceptTask.ConfigureAwait(false);
                NetworkStream serverStream = serverTcp.GetStream();

                var serverHandshake = new PacketGCHandshake(0x12345678, 987654, 42);

                Task serverTask = Task.Run(async () =>
                {
                    using var serverAgreement = Dh2KeyAgreement.Generate();
                    byte[] serverPub = serverAgreement.ExportPublicData();

                    await WriteAllAsync(serverStream, PacketGCHandshakeCodec.Serialize(serverHandshake), token).ConfigureAwait(false);

                    var serverKa = new PacketKeyAgreement(
                        DiffieHellmanGroup.AgreedValueLength, DiffieHellmanGroup.KeyDataLength, serverPub);
                    await WriteAllAsync(serverStream, PacketKeyAgreementCodec.Serialize(serverKa), token).ConfigureAwait(false);

                    byte[] cgReply = await ReadExactAsync(serverStream, PacketKeyAgreement.PacketSize, token).ConfigureAwait(false);
                    Assert.IsTrue(PacketKeyAgreementCodec.TryDeserialize(cgReply, out PacketKeyAgreement cgKa, out string _));
                    Assert.AreEqual(DiffieHellmanGroup.AgreedValueLength, cgKa.AgreedLength);
                    Assert.AreEqual(DiffieHellmanGroup.KeyDataLength, cgKa.DataLength);

                    Assert.IsTrue(serverAgreement.TryAgree(cgKa.AgreedLength, cgKa.Data, out byte[] serverShared));
                    Assert.IsTrue(CipherKeyDerivation.TryDerive(serverShared, out CipherKeyMaterial serverMaterial));
                    Array.Clear(serverShared, 0, serverShared.Length);

                    using var serverSession = new CipherSession(false, serverMaterial, BlockCipherEngineFactory.ForSession());

                    await WriteAllAsync(serverStream, new byte[] { PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED, 0, 0, 0 }, token).ConfigureAwait(false);
                    serverSession.SetActivated(true);

                    // Server -> client: encrypted GC_PHASE (Game).
                    byte[] phasePlain = PacketGCPhaseCodec.Serialize(new PacketGCPhase(PhaseType.Game));
                    byte[] phaseWire = (byte[])phasePlain.Clone();
                    serverSession.Encrypt(phaseWire, 0, phaseWire.Length);
                    await WriteAllAsync(serverStream, phaseWire, token).ConfigureAwait(false);

                    // Client -> server echo: read 2 encrypted bytes, decrypt, must match.
                    byte[] echoWire = await ReadExactAsync(serverStream, phasePlain.Length, token).ConfigureAwait(false);
                    serverSession.Decrypt(echoWire, 0, echoWire.Length);
                    CollectionAssert.AreEqual(phasePlain, echoWire);
                }, token);

                using var client = new HandshakeClient(conn);
                CipherSession session = await client.RunAsync(token).ConfigureAwait(false);

                Assert.IsTrue(client.Completed);
                Assert.IsNotNull(session);
                Assert.IsTrue(session.Activated);
                Assert.IsTrue(client.HasServerHandshake);
                Assert.AreEqual(0x12345678u, client.ServerHandshake.Handshake);
                Assert.AreEqual(987654u, client.ServerHandshake.Time);
                Assert.AreEqual(42, client.ServerHandshake.Delta);

                byte[] securePhase = await client.ReceiveSecureFrameAsync(token).ConfigureAwait(false);
                PacketGCPhase phase = PacketGCPhaseCodec.Deserialize(securePhase);
                Assert.AreEqual(PhaseType.Game, phase.Phase);

                // Echo the same plaintext back through the secure channel.
                await client.SendSecureAsync(securePhase, token).ConfigureAwait(false);

                await serverTask.ConfigureAwait(false);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public async Task FullHandshake_FragmentedServerWrites_Succeeds()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

                using var conn = new TcpConnection();
                await conn.ConnectAsync("127.0.0.1", port, token).ConfigureAwait(false);
                using TcpClient serverTcp = await acceptTask.ConfigureAwait(false);
                NetworkStream serverStream = serverTcp.GetStream();

                Task serverTask = Task.Run(async () =>
                {
                    using var serverAgreement = Dh2KeyAgreement.Generate();
                    byte[] serverPub = serverAgreement.ExportPublicData();

                    byte[] hs = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(1, 2, 3));
                    byte[] ka = PacketKeyAgreementCodec.Serialize(
                        new PacketKeyAgreement(DiffieHellmanGroup.AgreedValueLength, DiffieHellmanGroup.KeyDataLength, serverPub));

                    // Byte-at-a-time handshake, split key-agreement, split 0xfa.
                    for (int i = 0; i < hs.Length; i++)
                    {
                        await serverStream.WriteAsync(hs, i, 1, token).ConfigureAwait(false);
                    }

                    await serverStream.WriteAsync(ka, 0, 100, token).ConfigureAwait(false);
                    await serverStream.WriteAsync(ka, 100, ka.Length - 100, token).ConfigureAwait(false);
                    await serverStream.FlushAsync(token).ConfigureAwait(false);

                    byte[] cgReply = await ReadExactAsync(serverStream, PacketKeyAgreement.PacketSize, token).ConfigureAwait(false);
                    Assert.IsTrue(PacketKeyAgreementCodec.TryDeserialize(cgReply, out PacketKeyAgreement cgKa, out string _));
                    Assert.IsTrue(serverAgreement.TryAgree(cgKa.AgreedLength, cgKa.Data, out byte[] _));

                    byte[] done = new byte[] { PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED, 0, 0, 0 };
                    await serverStream.WriteAsync(done, 0, 1, token).ConfigureAwait(false);
                    await serverStream.WriteAsync(done, 1, done.Length - 1, token).ConfigureAwait(false);
                    await serverStream.FlushAsync(token).ConfigureAwait(false);
                }, token);

                using var client = new HandshakeClient(conn);
                CipherSession session = await client.RunAsync(token).ConfigureAwait(false);

                Assert.IsTrue(client.Completed);
                Assert.IsTrue(session.Activated);
                await serverTask.ConfigureAwait(false);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void Handshake_BadAgreedLength_FailsClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

                using var conn = new TcpConnection();
                conn.ConnectAsync("127.0.0.1", port, token).GetAwaiter().GetResult();
                using TcpClient serverTcp = acceptTask.GetAwaiter().GetResult();
                NetworkStream serverStream = serverTcp.GetStream();

                Task serverTask = Task.Run(async () =>
                {
                    await WriteAllAsync(serverStream, PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(1, 2, 3)), token).ConfigureAwait(false);
                    var badKa = new PacketKeyAgreement(0, DiffieHellmanGroup.KeyDataLength, new byte[DiffieHellmanGroup.KeyDataLength]);
                    await WriteAllAsync(serverStream, PacketKeyAgreementCodec.Serialize(badKa), token).ConfigureAwait(false);
                }, token);

                using var client = new HandshakeClient(conn);
                var ex = Assert.ThrowsAsync<HandshakeFailedException>(async () => await client.RunAsync(token).ConfigureAwait(false));
                Assert.IsFalse(client.Completed);
                Assert.IsNull(client.Session);
                StringAssert.Contains("agreed", ex.Message.ToLowerInvariant());

                serverTask.GetAwaiter().GetResult();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void Handshake_InvalidPeerKeys_FailsClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

                using var conn = new TcpConnection();
                conn.ConnectAsync("127.0.0.1", port, token).GetAwaiter().GetResult();
                using TcpClient serverTcp = acceptTask.GetAwaiter().GetResult();
                NetworkStream serverStream = serverTcp.GetStream();

                Task serverTask = Task.Run(async () =>
                {
                    await WriteAllAsync(serverStream, PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(1, 2, 3)), token).ConfigureAwait(false);
                    // All-zero public data: fails the y^q == 1 subgroup check.
                    var badKa = new PacketKeyAgreement(
                        DiffieHellmanGroup.AgreedValueLength, DiffieHellmanGroup.KeyDataLength, new byte[DiffieHellmanGroup.KeyDataLength]);
                    await WriteAllAsync(serverStream, PacketKeyAgreementCodec.Serialize(badKa), token).ConfigureAwait(false);
                }, token);

                using var client = new HandshakeClient(conn);
                Assert.ThrowsAsync<HandshakeFailedException>(async () => await client.RunAsync(token).ConfigureAwait(false));
                Assert.IsFalse(client.Completed);

                serverTask.GetAwaiter().GetResult();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void Handshake_ServerClosesMidHandshake_FailsClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

                using var conn = new TcpConnection();
                conn.ConnectAsync("127.0.0.1", port, token).GetAwaiter().GetResult();
                using TcpClient serverTcp = acceptTask.GetAwaiter().GetResult();
                NetworkStream serverStream = serverTcp.GetStream();

                serverStream.Write(PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(1, 2, 3)), 0, PacketGCHandshake.PacketSize);
                serverTcp.Close(); // close before key agreement

                using var client = new HandshakeClient(conn);
                Assert.ThrowsAsync<HandshakeFailedException>(async () => await client.RunAsync(token).ConfigureAwait(false));
                Assert.IsFalse(client.Completed);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void SecureTraffic_BeforeHandshake_Throws()
        {
            using var conn = new TcpConnection();
            using var client = new HandshakeClient(conn);

            Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                await client.SendSecureAsync(new byte[] { 0xfd, 0x05 }).ConfigureAwait(false));
            Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                await client.ReceiveSecureFrameAsync().ConfigureAwait(false));
        }

        [Test]
        public async Task Run_Twice_ThrowsInvalidOperation()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

                using var conn = new TcpConnection();
                await conn.ConnectAsync("127.0.0.1", port, token).ConfigureAwait(false);
                using TcpClient serverTcp = await acceptTask.ConfigureAwait(false);
                NetworkStream serverStream = serverTcp.GetStream();

                Task serverTask = Task.Run(async () =>
                {
                    using var serverAgreement = Dh2KeyAgreement.Generate();
                    await WriteAllAsync(serverStream, PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(1, 2, 3)), token).ConfigureAwait(false);
                    var serverKa = new PacketKeyAgreement(
                        DiffieHellmanGroup.AgreedValueLength, DiffieHellmanGroup.KeyDataLength, serverAgreement.ExportPublicData());
                    await WriteAllAsync(serverStream, PacketKeyAgreementCodec.Serialize(serverKa), token).ConfigureAwait(false);
                    byte[] cgReply = await ReadExactAsync(serverStream, PacketKeyAgreement.PacketSize, token).ConfigureAwait(false);
                    Assert.IsTrue(PacketKeyAgreementCodec.TryDeserialize(cgReply, out PacketKeyAgreement cgKa, out string _));
                    Assert.IsTrue(serverAgreement.TryAgree(cgKa.AgreedLength, cgKa.Data, out byte[] _));
                    await WriteAllAsync(serverStream, new byte[] { PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED, 0, 0, 0 }, token).ConfigureAwait(false);
                }, token);

                using var client = new HandshakeClient(conn);
                await client.RunAsync(token).ConfigureAwait(false);
                Assert.IsTrue(client.Completed);

                Assert.ThrowsAsync<InvalidOperationException>(async () => await client.RunAsync(token).ConfigureAwait(false));

                await serverTask.ConfigureAwait(false);
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
