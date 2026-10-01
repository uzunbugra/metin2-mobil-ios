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
    public class MovementClientTests
    {
        private const int TestTimeoutMs = 15000;

        private static CancellationToken TestToken(CancellationTokenSource cts)
        {
            cts.CancelAfter(TestTimeoutMs);
            return cts.Token;
        }

        private sealed class MoveHarness : IDisposable
        {
            public TcpListener Listener;
            public TcpConnection Connection;
            public HandshakeClient Client;
            public TcpClient ServerTcp;
            public NetworkStream ServerStream;
            public CipherSession ServerSession;

            public static async Task<MoveHarness> EstablishAsync(CancellationToken token)
            {
                var harness = new MoveHarness();
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

                    byte[] hs = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(2, 2, 2));
                    await harness.ServerStream.WriteAsync(hs, 0, hs.Length, token).ConfigureAwait(false);

                    var serverKa = new PacketKeyAgreement(
                        DiffieHellmanGroup.AgreedValueLength, DiffieHellmanGroup.KeyDataLength, serverPub);
                    byte[] ka = PacketKeyAgreementCodec.Serialize(serverKa);
                    await harness.ServerStream.WriteAsync(ka, 0, ka.Length, token).ConfigureAwait(false);
                    await harness.ServerStream.FlushAsync(token).ConfigureAwait(false);

                    byte[] cgReply = await ReadExactAsync(harness.ServerStream, PacketKeyAgreement.PacketSize, token).ConfigureAwait(false);
                    Assert.IsTrue(PacketKeyAgreementCodec.TryDeserialize(cgReply, out PacketKeyAgreement cgKa2, out string _));
                    Assert.IsTrue(serverAgreement.TryAgree(cgKa2.AgreedLength, cgKa2.Data, out byte[] serverShared));
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
        public async Task MoveIntent_ServerReads_AndBroadcastReceived()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await MoveHarness.EstablishAsync(token).ConfigureAwait(false);
            var movement = new MovementClient(harness.Client);

            var broadcast = new PacketGCMove
            {
                Func = MoveFunc.Move,
                Arg = 0,
                Rot = 18,
                Vid = 777,
                X = 480000,
                Y = 960000,
                Time = 123456,
                Duration = 900
            };

            Task serverRound = Task.Run(async () =>
            {
                // Client intent (16B).
                byte[] moveWire = await harness.ReadDecryptedAsync(PacketCGMove.PacketSize, token).ConfigureAwait(false);
                Assert.IsTrue(PacketCGMoveCodec.TryDeserialize(moveWire, out PacketCGMove intent, out string _));
                Assert.AreEqual(MoveFunc.Move, intent.Func);
                Assert.AreEqual(18, intent.Rot);
                Assert.AreEqual(480000, intent.X);

                // Server rebroadcast for another actor.
                await harness.WriteEncryptedAsync(PacketGCMoveCodec.Serialize(broadcast), token).ConfigureAwait(false);
            }, token);

            // 90 degrees -> quantized 18 (server restores 18*5).
            await movement.SendMoveAsync(MoveFunc.Move, 0, 90f, 480000, 960000, 123000, token).ConfigureAwait(false);

            PacketGCMove received = await movement.ReceiveMoveAsync(token).ConfigureAwait(false);
            Assert.AreEqual(777u, received.Vid);
            Assert.AreEqual(MoveFunc.Move, received.Func);
            Assert.AreEqual(480000, received.X);
            Assert.AreEqual(900u, received.Duration);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task SyncBatch_SendAndReceive_RoundTrip()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await MoveHarness.EstablishAsync(token).ConfigureAwait(false);
            var movement = new MovementClient(harness.Client);

            var elements = new SyncPositionElement[]
            {
                new SyncPositionElement { Vid = 11, X = 100, Y = 200 },
                new SyncPositionElement { Vid = 22, X = 300, Y = 400 }
            };

            Task serverRound = Task.Run(async () =>
            {
                byte[] syncWire = await harness.ReadDecryptedAsync(3 + (12 * 2), token).ConfigureAwait(false);
                Assert.IsTrue(PacketCGSyncPositionCodec.TryDeserialize(syncWire, out PacketCGSyncPosition sent, out string _));
                Assert.AreEqual(2, sent.ElementCount);

                byte[] reply = PacketGCSyncPositionCodec.Serialize(new PacketGCSyncPosition(elements));
                await harness.WriteEncryptedAsync(reply, token).ConfigureAwait(false);
            }, token);

            await movement.SendSyncAsync(elements, token).ConfigureAwait(false);

            PacketGCSyncPosition received = await movement.ReceiveSyncAsync(token).ConfigureAwait(false);
            Assert.AreEqual(2, received.ElementCount);
            Assert.AreEqual(11u, received.Elements[0].Vid);
            Assert.AreEqual(300, received.Elements[1].X);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public void QuantizeRotation_MatchesClientFormula()
        {
            Assert.AreEqual(0, MovementClient.QuantizeRotation(0f));
            Assert.AreEqual(18, MovementClient.QuantizeRotation(90f));
            Assert.AreEqual(0, MovementClient.QuantizeRotation(360f));
            Assert.AreEqual(54, MovementClient.QuantizeRotation(-90f));
            Assert.AreEqual(1, MovementClient.QuantizeRotation(725f));
        }

        [Test]
        public async Task SendMove_InvalidFuncOrCoords_Throws()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await MoveHarness.EstablishAsync(token).ConfigureAwait(false);
            var movement = new MovementClient(harness.Client);

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await movement.SendMoveAsync(0x40, 0, 0f, 100, 100, 0, token).ConfigureAwait(false));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await movement.SendMoveAsync(MoveFunc.Move, 0, 0f, -5, 100, 0, token).ConfigureAwait(false));
        }

        [Test]
        public async Task SendSync_TooManyElements_Throws()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await MoveHarness.EstablishAsync(token).ConfigureAwait(false);
            var movement = new MovementClient(harness.Client);

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await movement.SendSyncAsync(new SyncPositionElement[17], token).ConfigureAwait(false));
        }

        [Test]
        public void Constructor_IncompleteHandshake_Throws()
        {
            using var conn = new TcpConnection();
            using var handshake = new HandshakeClient(conn);

            Assert.Throws<HandshakeFailedException>(() => new MovementClient(handshake));
        }
    }
}
