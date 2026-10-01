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
    public class WorldEntryClientTests
    {
        private const int TestTimeoutMs = 15000;

        private static CancellationToken TestToken(CancellationTokenSource cts)
        {
            cts.CancelAfter(TestTimeoutMs);
            return cts.Token;
        }

        private sealed class EntryHarness : IDisposable
        {
            public TcpListener Listener;
            public TcpConnection Connection;
            public HandshakeClient Client;
            public TcpClient ServerTcp;
            public NetworkStream ServerStream;
            public CipherSession ServerSession;

            public static async Task<EntryHarness> EstablishAsync(CancellationToken token)
            {
                var harness = new EntryHarness();
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

                    byte[] hs = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(5, 5, 5));
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
        public async Task FullWorldEntry_MainCharEnterGameTimeChannel()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await EntryHarness.EstablishAsync(token).ConfigureAwait(false);
            var entry = new WorldEntryClient(harness.Client);
            var selector = new CharacterSelectClient(harness.Client);

            PacketGCMainCharacter mainChar = PacketGCMainCharacterTests.SamplePacket();

            Task serverRound = Task.Run(async () =>
            {
                // Loading phase: own-character spawn (PlayerLoad path).
                await harness.WriteEncryptedAsync(
                    PacketGCMainCharacterCodec.Serialize(mainChar), token).ConfigureAwait(false);

                // Client answers ENTERGAME (PhaseLoading SendEnterGame).
                byte[] enterWire = await harness.ReadDecryptedAsync(PacketCGEnterGame.PacketSize, token).ConfigureAwait(false);
                Assert.IsTrue(PacketCGEnterGameCodec.TryDeserialize(enterWire, out PacketCGEnterGame _, out string _));

                // Game phase: TIME then CHANNEL (Entergame order).
                await harness.WriteEncryptedAsync(
                    PacketGCTimeCodec.Serialize(new PacketGCTime(1727712000)), token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(
                    PacketGCChannelCodec.Serialize(new PacketGCChannel(2)), token).ConfigureAwait(false);
            }, token);

            PacketGCMainCharacter received =
                await entry.ReceiveMainCharacterAsync(token).ConfigureAwait(false);

            Assert.AreEqual(mainChar.Vid, received.Vid);
            Assert.AreEqual(mainChar.Race, received.Race);
            Assert.AreEqual("HeroName", received.Name);
            Assert.AreEqual(474387, received.X);
            Assert.AreEqual(954234, received.Y);
            Assert.AreEqual(2, received.Empire);
            Assert.AreEqual(1, received.SkillGroup);

            await selector.SendEnterGameAsync(token).ConfigureAwait(false);

            WorldEntryData data =
                await entry.ReceiveGameEntryAsync(received, token).ConfigureAwait(false);

            Assert.AreEqual(received.Vid, data.Vid);
            Assert.AreEqual("HeroName", data.Name);
            Assert.AreEqual(474387, data.X);
            Assert.AreEqual(1727712000u, data.ServerTime);
            Assert.AreEqual(2, data.Channel);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task GameEntry_ReversedOrder_ThrowsFailClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await EntryHarness.EstablishAsync(token).ConfigureAwait(false);
            var entry = new WorldEntryClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.WriteEncryptedAsync(
                    PacketGCMainCharacterCodec.Serialize(PacketGCMainCharacterTests.SamplePacket()), token).ConfigureAwait(false);
                // CHANNEL before TIME: violates Entergame order.
                await harness.WriteEncryptedAsync(
                    PacketGCChannelCodec.Serialize(new PacketGCChannel(1)), token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(
                    PacketGCTimeCodec.Serialize(new PacketGCTime(1)), token).ConfigureAwait(false);
            }, token);

            PacketGCMainCharacter main =
                await entry.ReceiveMainCharacterAsync(token).ConfigureAwait(false);
            Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                await entry.ReceiveGameEntryAsync(main, token).ConfigureAwait(false));

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task MainCharacter_SelectPhaseHeader_ThrowsFailClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await EntryHarness.EstablishAsync(token).ConfigureAwait(false);
            var entry = new WorldEntryClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                // 32 is Select-only: rejected in Loading position.
                byte[] fake = new byte[PacketGCLoginSuccess.PacketSize];
                fake[0] = PacketGCLoginSuccess.PacketHeader;
                await harness.WriteEncryptedAsync(fake, token).ConfigureAwait(false);
            }, token);

            Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                await entry.ReceiveMainCharacterAsync(token).ConfigureAwait(false));

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public void Constructor_IncompleteHandshake_Throws()
        {
            using var conn = new TcpConnection();
            using var handshake = new HandshakeClient(conn);

            Assert.Throws<HandshakeFailedException>(() => new WorldEntryClient(handshake));
        }
    }
}
