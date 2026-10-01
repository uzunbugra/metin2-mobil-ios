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
    public class InventoryClientTests
    {
        private const int TestTimeoutMs = 15000;

        private static CancellationToken TestToken(CancellationTokenSource cts)
        {
            cts.CancelAfter(TestTimeoutMs);
            return cts.Token;
        }

        private sealed class ItemHarness : IDisposable
        {
            public TcpListener Listener;
            public TcpConnection Connection;
            public HandshakeClient Client;
            public TcpClient ServerTcp;
            public NetworkStream ServerStream;
            public CipherSession ServerSession;

            public static async Task<ItemHarness> EstablishAsync(CancellationToken token)
            {
                var harness = new ItemHarness();
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

                    byte[] hs = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(4, 4, 4));
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
        public async Task ItemLifecycle_SetUpdateClear_FullRound()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await ItemHarness.EstablishAsync(token).ConfigureAwait(false);
            var inventory = new InventoryClient(harness.Client);

            var set = new PacketGCItemSet(
                1, 5, 11243, 1, 4, 0x100, false,
                new int[] { 28448, 0, 0 },
                new ItemAttribute[] { new ItemAttribute { Type = 1, Value = 15 } });
            var update = new PacketGCItemUpdate(
                1, 5, 199, new int[] { 28448, 0, 0 },
                new ItemAttribute[] { new ItemAttribute { Type = 1, Value = 20 } });
            var clear = new PacketGCItemDel(1, 5, 0, 0, new int[3], new ItemAttribute[7]);

            Task serverRound = Task.Run(async () =>
            {
                await harness.WriteEncryptedAsync(PacketGCItemSetCodec.Serialize(set), token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(PacketGCItemUpdateCodec.Serialize(update), token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(PacketGCItemDelCodec.Serialize(clear), token).ConfigureAwait(false);
            }, token);

            ItemEvent assigned = await inventory.ReceiveItemAsync(PhaseType.Loading, token).ConfigureAwait(false);
            Assert.AreEqual(ItemEvent.Kind.Set, assigned.EventKind);
            Assert.AreEqual(1, assigned.Window);
            Assert.AreEqual(5, assigned.Cell);
            Assert.AreEqual(11243u, assigned.Set.Vnum);
            Assert.AreEqual(1, assigned.Set.Count);

            ItemEvent mutated = await inventory.ReceiveItemAsync(PhaseType.Loading, token).ConfigureAwait(false);
            Assert.AreEqual(ItemEvent.Kind.Updated, mutated.EventKind);
            Assert.AreEqual(5, mutated.Cell);
            Assert.AreEqual(199, mutated.Update.Count);
            Assert.AreEqual(20, mutated.Update.Attributes[0].Value);

            ItemEvent cleared = await inventory.ReceiveItemAsync(PhaseType.Loading, token).ConfigureAwait(false);
            Assert.AreEqual(ItemEvent.Kind.Cleared, cleared.EventKind);
            Assert.AreEqual(1, cleared.Window);
            Assert.AreEqual(5, cleared.Cell);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task ItemFrames_AcceptedInGamePhaseToo()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await ItemHarness.EstablishAsync(token).ConfigureAwait(false);
            var inventory = new InventoryClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.WriteEncryptedAsync(
                    PacketGCItemSetCodec.Serialize(new PacketGCItemSet(1, 0, 19, 200, 0, 0, false, null, null)),
                    token).ConfigureAwait(false);
            }, token);

            // Yang stack (vnum 19) arriving mid-game, e.g. after pickup.
            ItemEvent picked = await inventory.ReceiveItemAsync(PhaseType.Game, token).ConfigureAwait(false);
            Assert.AreEqual(ItemEvent.Kind.Set, picked.EventKind);
            Assert.AreEqual(19u, picked.Set.Vnum);
            Assert.AreEqual(200, picked.Set.Count);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task ItemStream_WrongPhaseHeader_ThrowsFailClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await ItemHarness.EstablishAsync(token).ConfigureAwait(false);
            var inventory = new InventoryClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                // 32 is Select-only: rejected in Loading position.
                byte[] fake = new byte[PacketGCLoginSuccess.PacketSize];
                fake[0] = PacketGCLoginSuccess.PacketHeader;
                await harness.WriteEncryptedAsync(fake, token).ConfigureAwait(false);
            }, token);

            Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                await inventory.ReceiveItemAsync(PhaseType.Loading, token).ConfigureAwait(false));

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task ReceiveItem_InvalidPhaseArgument_Throws()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await ItemHarness.EstablishAsync(token).ConfigureAwait(false);
            var inventory = new InventoryClient(harness.Client);

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await inventory.ReceiveItemAsync(PhaseType.Select, token).ConfigureAwait(false));
        }

        [Test]
        public void Constructor_IncompleteHandshake_Throws()
        {
            using var conn = new TcpConnection();
            using var handshake = new HandshakeClient(conn);

            Assert.Throws<HandshakeFailedException>(() => new InventoryClient(handshake));
        }
    }
}
