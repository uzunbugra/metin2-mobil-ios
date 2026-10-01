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
    public class CharacterSelectClientTests
    {
        private const int TestTimeoutMs = 15000;

        private static CancellationToken TestToken(CancellationTokenSource cts)
        {
            cts.CancelAfter(TestTimeoutMs);
            return cts.Token;
        }

        private sealed class SelectHarness : IDisposable
        {
            public TcpListener Listener;
            public TcpConnection Connection;
            public HandshakeClient Client;
            public TcpClient ServerTcp;
            public NetworkStream ServerStream;
            public CipherSession ServerSession;

            public static async Task<SelectHarness> EstablishAsync(CancellationToken token)
            {
                var harness = new SelectHarness();
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

                    byte[] hs = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(7, 7, 7));
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
        public async Task Select_CreateDeleteEnter_FullRound()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await SelectHarness.EstablishAsync(token).ConfigureAwait(false);
            var select = new CharacterSelectClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                // 1. Select (no reply).
                byte[] selectWire = await harness.ReadDecryptedAsync(PacketCGCharacterSelect.PacketSize, token).ConfigureAwait(false);
                Assert.IsTrue(PacketCGCharacterSelectCodec.TryDeserialize(selectWire, out PacketCGCharacterSelect sel, out string _));
                Assert.AreEqual(1, sel.Index);

                // 2. Create → 8 success.
                byte[] createWire = await harness.ReadDecryptedAsync(PacketCGCharacterCreate.PacketSize, token).ConfigureAwait(false);
                Assert.IsTrue(PacketCGCharacterCreateCodec.TryDeserialize(createWire, out PacketCGCharacterCreate create, out string _));
                Assert.AreEqual(2, create.Index);
                Assert.AreEqual("Hero", create.Name);
                Assert.AreEqual(1, create.Job);

                var created = new SimplePlayer
                {
                    Id = 999,
                    Name = "Hero",
                    Job = 1,
                    Level = 1,
                    PlayMinutes = 0,
                    St = 6,
                    Ht = 4,
                    Dx = 5,
                    Iq = 3,
                    AddrNetworkOrder = 0x0100007Fu,
                    Port = 13001
                };
                await harness.WriteEncryptedAsync(
                    PacketGCCreateSuccessCodec.Serialize(new PacketGCCreateSuccess(2, created)), token).ConfigureAwait(false);

                // 3. Delete → 10 success.
                byte[] deleteWire = await harness.ReadDecryptedAsync(PacketCGCharacterDelete.PacketSize, token).ConfigureAwait(false);
                Assert.IsTrue(PacketCGCharacterDeleteCodec.TryDeserialize(deleteWire, out PacketCGCharacterDelete del, out string _));
                Assert.AreEqual(0, del.Index);
                Assert.AreEqual("1234567", del.PrivateCode);

                await harness.WriteEncryptedAsync(
                    PacketGCDeleteSuccessCodec.Serialize(new PacketGCDeleteSuccess(0)), token).ConfigureAwait(false);

                // 4. EnterGame (no reply).
                byte[] enterWire = await harness.ReadDecryptedAsync(PacketCGEnterGame.PacketSize, token).ConfigureAwait(false);
                Assert.IsTrue(PacketCGEnterGameCodec.TryDeserialize(enterWire, out PacketCGEnterGame _, out string _));
            }, token);

            await select.SendSelectAsync(1, token).ConfigureAwait(false);
            await select.SendCreateAsync(2, "Hero", 1, 0, 6, 4, 5, 3, token).ConfigureAwait(false);

            CharacterCreateResult created2 = await select.AwaitCreateResultAsync(token).ConfigureAwait(false);
            Assert.IsTrue(created2.Succeeded);
            Assert.AreEqual(2, created2.Slot);
            Assert.AreEqual(999u, created2.Player.Id);
            Assert.AreEqual("Hero", created2.Player.Name);

            await select.SendDeleteAsync(0, "1234567", token).ConfigureAwait(false);
            CharacterDeleteResult deleted = await select.AwaitDeleteResultAsync(token).ConfigureAwait(false);
            Assert.IsTrue(deleted.Succeeded);
            Assert.AreEqual(0, deleted.Index);

            await select.SendEnterGameAsync(token).ConfigureAwait(false);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task Create_ServerFailure_ReturnsType()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await SelectHarness.EstablishAsync(token).ConfigureAwait(false);
            var select = new CharacterSelectClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.ReadDecryptedAsync(PacketCGCharacterCreate.PacketSize, token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(
                    PacketGCCreateFailureCodec.Serialize(new PacketGCCreateFailure(1)), token).ConfigureAwait(false);
            }, token);

            await select.SendCreateAsync(0, "Bad Name!", 1, 0, 1, 1, 1, 1, token).ConfigureAwait(false);
            CharacterCreateResult result = await select.AwaitCreateResultAsync(token).ConfigureAwait(false);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(1, result.FailureType);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task Delete_ServerFailure_ReturnsFailure()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await SelectHarness.EstablishAsync(token).ConfigureAwait(false);
            var select = new CharacterSelectClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.ReadDecryptedAsync(PacketCGCharacterDelete.PacketSize, token).ConfigureAwait(false);
                await harness.WriteEncryptedAsync(
                    PacketGCDeleteFailureCodec.Serialize(new PacketGCDeleteFailure()), token).ConfigureAwait(false);
            }, token);

            await select.SendDeleteAsync(3, "0000000", token).ConfigureAwait(false);
            CharacterDeleteResult result = await select.AwaitDeleteResultAsync(token).ConfigureAwait(false);

            Assert.IsFalse(result.Succeeded);

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task Create_WrongReplyKind_ThrowsFailClosed()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await SelectHarness.EstablishAsync(token).ConfigureAwait(false);
            var select = new CharacterSelectClient(harness.Client);

            Task serverRound = Task.Run(async () =>
            {
                await harness.ReadDecryptedAsync(PacketCGCharacterCreate.PacketSize, token).ConfigureAwait(false);

                // 10 is Select-valid but not a create reply.
                await harness.WriteEncryptedAsync(
                    PacketGCDeleteSuccessCodec.Serialize(new PacketGCDeleteSuccess(0)), token).ConfigureAwait(false);
            }, token);

            await select.SendCreateAsync(0, "Hero", 1, 0, 1, 1, 1, 1, token).ConfigureAwait(false);
            Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                await select.AwaitCreateResultAsync(token).ConfigureAwait(false));

            await serverRound.ConfigureAwait(false);
        }

        [Test]
        public async Task Validation_RejectsBadArguments()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken token = TestToken(cts);

            using var harness = await SelectHarness.EstablishAsync(token).ConfigureAwait(false);
            var select = new CharacterSelectClient(harness.Client);

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await select.SendSelectAsync(4, token).ConfigureAwait(false));
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await select.SendCreateAsync(0, string.Empty, 1, 0, 1, 1, 1, 1, token).ConfigureAwait(false));
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await select.SendCreateAsync(0, " waytoolongname", 1, 0, 1, 1, 1, 1, token).ConfigureAwait(false));
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await select.SendDeleteAsync(0, null, token).ConfigureAwait(false));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await select.SendSelectEmpireAsync(4, token).ConfigureAwait(false));
        }

        [Test]
        public void Constructor_IncompleteHandshake_Throws()
        {
            using var conn = new TcpConnection();
            using var handshake = new HandshakeClient(conn);

            Assert.Throws<HandshakeFailedException>(() => new CharacterSelectClient(handshake));
        }
    }
}
