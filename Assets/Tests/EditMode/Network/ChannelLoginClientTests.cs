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
using Metin2.Tests.EditMode.Protocol;

namespace Metin2.Tests.EditMode.Network
{
    [TestFixture]
    public class ChannelLoginClientTests
    {
        private const int TestTimeoutMs = 15000;

        private static CancellationToken TestToken(CancellationTokenSource cts)
        {
            cts.CancelAfter(TestTimeoutMs);
            return cts.Token;
        }

        private sealed class ChannelHarness : IDisposable
        {
            public TcpListener Listener;
            public TcpConnection Connection;
            public HandshakeClient Client;
            public TcpClient ServerTcp;
            public NetworkStream ServerStream;
            public CipherSession ServerSession;

            public static async Task<ChannelHarness> EstablishAsync(CancellationToken token)
            {
                var harness = new ChannelHarness();
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

                    byte[] hs = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(9, 8, 7));
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

        private static PacketGCLoginSuccess SampleSelectPacket()
        {
            var players = new SimplePlayer[4];
            players[0] = SimplePlayerCodecTests.SampleSlot();
            players[1] = new SimplePlayer
            {
                Id = 4242,
                Name = "Ninja99",
                Job = 2,
                Level = 75,
                PlayMinutes = 555,
                St = 60,
                Ht = 60,
                Dx = 90,
                Iq = 50,
                MainPart = 40400,
                HairPart = 3200,
                X = 300,
                Y = 400,
                // 192.168.1.10 in network order (inet_addr output).
                AddrNetworkOrder = 0x0A01A8C0u,
                Port = 13002,
                SkillGroup = 1
            };

            return new PacketGCLoginSuccess(
                players,
                new uint[] { 11, 0, 22, 0 },
                new string[] { "Alpha", string.Empty, "Beta", string.Empty },
                0x00C0FFEE,
                0x00DEC0DE);
        }

        [Test]
        public async Task FullChannelLogin_ReturnsEmpireSlotsAndEndpoints()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await ChannelHarness.EstablishAsync(token).ConfigureAwait(false);
            var channel = new ChannelLoginClient(harness.Client);

            uint[] clientKeys = new uint[] { 5, 6, 7, 8 };
            PacketGCLoginSuccess selectPacket = SampleSelectPacket();

            Task serverRound = Task.Run(async () =>
            {
                byte[] loginWire = await harness.ReadDecryptedAsync(PacketCGLogin2.PacketSize, token).ConfigureAwait(false);
                Assert.IsTrue(PacketCGLogin2Codec.TryDeserialize(loginWire, out PacketCGLogin2 login2, out string _));
                Assert.AreEqual("testuser", login2.Login);
                Assert.AreEqual(0xAABBCCDDu, login2.LoginKey);
                CollectionAssert.AreEqual(clientKeys, login2.ClientKeys);

                // Empire first (still Login phase), slots second (Select phase).
                await harness.WriteEncryptedAsync(PacketGCEmpireCodec.Serialize(new PacketGCEmpire(1)), token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(PacketGCLoginSuccessCodec.Serialize(selectPacket), token).ConfigureAwait(false);
            }, token);

            await channel.SendChannelLoginAsync("testuser", 0xAABBCCDD, clientKeys, token).ConfigureAwait(false);
            ChannelLoginResult result = await channel.ReceiveSelectDataAsync(token).ConfigureAwait(false);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, result.Data.Empire);
            Assert.AreEqual("Warrior01", result.Data.Players[0].Name);
            Assert.AreEqual(35, result.Data.Players[0].Level);
            Assert.AreEqual("Ninja99", result.Data.Players[1].Name);
            Assert.AreEqual(0u, result.Data.Players[2].Id);
            CollectionAssert.AreEqual(new uint[] { 11, 0, 22, 0 }, result.Data.GuildIds);
            CollectionAssert.AreEqual(new string[] { "Alpha", string.Empty, "Beta", string.Empty }, result.Data.GuildNames);
            Assert.AreEqual(0x00C0FFEEu, result.Data.Handle);
            Assert.AreEqual(0x00DEC0DEu, result.Data.RandomKey);

            (IPAddress addr0, ushort port0) = ChannelLoginClient.GetSlotEndpoint(result.Data.Players[0]);
            Assert.AreEqual("127.0.0.1", addr0.ToString());
            Assert.AreEqual(13001, port0);

            (IPAddress addr1, ushort port1) = ChannelLoginClient.GetSlotEndpoint(result.Data.Players[1]);
            Assert.AreEqual("192.168.1.10", addr1.ToString());
            Assert.AreEqual(13002, port1);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task ChannelLogin_ServerFull_ReturnsFailureStatus()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await ChannelHarness.EstablishAsync(token).ConfigureAwait(false);
            var channel = new ChannelLoginClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.ReadDecryptedAsync(PacketCGLogin2.PacketSize, token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(
                    PacketGCLoginFailureCodec.Serialize(new PacketGCLoginFailure("FULL")), token).ConfigureAwait(false);
            }, token);

            await channel.SendChannelLoginAsync("testuser", 1, new uint[4], token).ConfigureAwait(false);
            ChannelLoginResult result = await channel.ReceiveSelectDataAsync(token).ConfigureAwait(false);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("FULL", result.Status);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task ChannelLogin_FragmentedSuccessPacket_Succeeds()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await ChannelHarness.EstablishAsync(token).ConfigureAwait(false);
            var channel = new ChannelLoginClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.ReadDecryptedAsync(PacketCGLogin2.PacketSize, token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(PacketGCEmpireCodec.Serialize(new PacketGCEmpire(3)), token).ConfigureAwait(false);

                byte[] plain = PacketGCLoginSuccessCodec.Serialize(SampleSelectPacket());
                byte[] wire = (byte[])plain.Clone();
                harness.ServerSession.Encrypt(wire, 0, wire.Length);
                await harness.ServerStream.WriteAsync(wire, 0, 100, token).ConfigureAwait(false);
                await harness.ServerStream.WriteAsync(wire, 100, wire.Length - 100, token).ConfigureAwait(false);
                await harness.ServerStream.FlushAsync(token).ConfigureAwait(false);
            }, token);

            await channel.SendChannelLoginAsync("testuser", 1, new uint[4], token).ConfigureAwait(false);
            ChannelLoginResult result = await channel.ReceiveSelectDataAsync(token).ConfigureAwait(false);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(3, result.Data.Empire);
            Assert.AreEqual("Warrior01", result.Data.Players[0].Name);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task ChannelLogin_ReversedOrder_ThrowsFailClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await ChannelHarness.EstablishAsync(token).ConfigureAwait(false);
            var channel = new ChannelLoginClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.ReadDecryptedAsync(PacketCGLogin2.PacketSize, token).ConfigureAwait(false);

                // Slots before empire: 32 is Select-only, rejected in Login position.
                await harness.WriteEncryptedAsync(
                    PacketGCLoginSuccessCodec.Serialize(SampleSelectPacket()), token).ConfigureAwait(false);
            }, token);

            await channel.SendChannelLoginAsync("testuser", 1, new uint[4], token).ConfigureAwait(false);
            Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                await channel.ReceiveSelectDataAsync(token).ConfigureAwait(false));

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task SendChannelLogin_EmptyLogin_Throws()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await ChannelHarness.EstablishAsync(token).ConfigureAwait(false);
            var channel = new ChannelLoginClient(harness.Client);

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await channel.SendChannelLoginAsync(string.Empty, 1, new uint[4], token).ConfigureAwait(false));
        }

        [Test]
        public void Constructor_IncompleteHandshake_Throws()
        {
            using var conn = new TcpConnection();
            using var handshake = new HandshakeClient(conn);

            Assert.Throws<HandshakeFailedException>(() => new ChannelLoginClient(handshake));
        }

        [Test]
        public void GetSlotEndpoint_EmptySlot_ThrowsFailClosed()
        {
            Assert.Throws<HandshakeFailedException>(() =>
                ChannelLoginClient.GetSlotEndpoint(new SimplePlayer()));
        }
    }
}
