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
    public class GameWorldClientTests
    {
        private const int TestTimeoutMs = 15000;

        private static CancellationToken TestToken(CancellationTokenSource cts)
        {
            cts.CancelAfter(TestTimeoutMs);
            return cts.Token;
        }

        private sealed class WorldHarness : IDisposable
        {
            public TcpListener Listener;
            public TcpConnection Connection;
            public HandshakeClient Client;
            public TcpClient ServerTcp;
            public NetworkStream ServerStream;
            public CipherSession ServerSession;

            public static async Task<WorldHarness> EstablishAsync(CancellationToken token)
            {
                var harness = new WorldHarness();
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

                    byte[] hs = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(3, 3, 3));
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
        public async Task LoadingStats_PointsThenSkills_RoundTrip()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await WorldHarness.EstablishAsync(token).ConfigureAwait(false);
            var world = new GameWorldClient(harness.Client);

            var points = new int[255];
            points[0] = 42;
            points[254] = -7;
            var skills = new PlayerSkill[255];
            skills[0] = new PlayerSkill { MasterType = 1, Level = 20, NextRead = 999 };
            skills[100] = new PlayerSkill { MasterType = 3, Level = 40, NextRead = 1 };

            Task serverRound = Task.Run(async () =>
            {
                await harness.WriteEncryptedAsync(
                    PacketGCPointsCodec.Serialize(new PacketGCPoints(points)), token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(
                    PacketGCSkillLevelCodec.Serialize(new PacketGCSkillLevel(skills)), token).ConfigureAwait(false);
            }, token);

            LoadingStatsData stats = await world.ReceiveLoadingStatsAsync(token).ConfigureAwait(false);

            Assert.AreEqual(42, stats.Points[0]);
            Assert.AreEqual(-7, stats.Points[254]);
            Assert.AreEqual(255, stats.Points.Length);
            Assert.IsTrue(new PlayerSkill { MasterType = 1, Level = 20, NextRead = 999 }.Equals(stats.Skills[0]));
            Assert.IsTrue(new PlayerSkill { MasterType = 3, Level = 40, NextRead = 1 }.Equals(stats.Skills[100]));
            Assert.AreEqual(255, stats.Skills.Length);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task Spawn_AddThenRemove_Events()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await WorldHarness.EstablishAsync(token).ConfigureAwait(false);
            var world = new GameWorldClient(harness.Client);

            var add = new PacketGCCharacterAdd
            {
                Vid = 0x0000BEEF,
                Angle = 0.5f,
                X = 1000,
                Y = 2000,
                Z = 0,
                Type = 0,
                Race = 101,
                MovingSpeed = 150,
                AttackSpeed = 150,
                StateFlag = 0,
                AffectFlag0 = 0,
                AffectFlag1 = 0
            };

            Task serverRound = Task.Run(async () =>
            {
                await harness.WriteEncryptedAsync(PacketGCCharacterAddCodec.Serialize(add), token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(
                    PacketGCCharacterDeleteCodec.Serialize(new PacketGCCharacterDelete(0x0000BEEF)), token).ConfigureAwait(false);
            }, token);

            SpawnEvent added = await world.ReceiveSpawnAsync(token).ConfigureAwait(false);
            Assert.IsTrue(added.Added);
            Assert.AreEqual(0x0000BEEFu, added.Add.Vid);
            Assert.AreEqual(101, added.Add.Race);
            Assert.AreEqual(1000, added.Add.X);

            SpawnEvent removed = await world.ReceiveSpawnAsync(token).ConfigureAwait(false);
            Assert.IsFalse(removed.Added);
            Assert.AreEqual(0x0000BEEFu, removed.RemovedVid);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task LoadingStats_ReversedOrder_ThrowsFailClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await WorldHarness.EstablishAsync(token).ConfigureAwait(false);
            var world = new GameWorldClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                // Skills before points: violates PlayerLoad order.
                await harness.WriteEncryptedAsync(
                    PacketGCSkillLevelCodec.Serialize(new PacketGCSkillLevel(new PlayerSkill[255])), token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(
                    PacketGCPointsCodec.Serialize(new PacketGCPoints(new int[255])), token).ConfigureAwait(false);
            }, token);

            Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                await world.ReceiveLoadingStatsAsync(token).ConfigureAwait(false));

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task Spawn_GamePhaseHeader_ThrowsFailClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await WorldHarness.EstablishAsync(token).ConfigureAwait(false);
            var world = new GameWorldClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                // TIME is Game-valid but not a spawn frame.
                await harness.WriteEncryptedAsync(
                    PacketGCTimeCodec.Serialize(new PacketGCTime(1)), token).ConfigureAwait(false);
            }, token);

            Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                await world.ReceiveSpawnAsync(token).ConfigureAwait(false));

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public void Constructor_IncompleteHandshake_Throws()
        {
            using var conn = new TcpConnection();
            using var handshake = new HandshakeClient(conn);

            Assert.Throws<HandshakeFailedException>(() => new GameWorldClient(handshake));
        }
    }
}
