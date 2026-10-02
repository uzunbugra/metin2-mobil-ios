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
    public class CombatClientTests
    {
        private const int TestTimeoutMs = 15000;

        /// <summary>
        /// Loopback server side of the handshake; returns the activated
        /// server-polarity cipher session plus the accepted stream.
        /// </summary>
        private static async Task<(NetworkStream Stream, CipherSession Session)> AcceptAndHandshakeAsync(
            TcpListener listener, CancellationToken token)
        {
            using var serverAgreement = Dh2KeyAgreement.Generate();
            byte[] serverPub = serverAgreement.ExportPublicData();

            TcpClient serverTcp = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            NetworkStream stream = serverTcp.GetStream();

            await stream.WriteAsync(PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(1, 2, 3)), token).ConfigureAwait(false);
            var serverKa = new PacketKeyAgreement(
                DiffieHellmanGroup.AgreedValueLength, DiffieHellmanGroup.KeyDataLength, serverPub);
            await stream.WriteAsync(PacketKeyAgreementCodec.Serialize(serverKa), token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);

            byte[] cgReply = new byte[PacketKeyAgreement.PacketSize];
            int total = 0;
            while (total < cgReply.Length)
            {
                int read = await stream.ReadAsync(cgReply, total, cgReply.Length - total, token).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new InvalidOperationException("Client closed during handshake.");
                }

                total += read;
            }

            Assert.IsTrue(PacketKeyAgreementCodec.TryDeserialize(cgReply, out PacketKeyAgreement cgKa, out string _));
            Assert.IsTrue(serverAgreement.TryAgree(cgKa.AgreedLength, cgKa.Data, out byte[] shared));
            Assert.IsTrue(CipherKeyDerivation.TryDerive(shared, out CipherKeyMaterial material));
            Array.Clear(shared, 0, shared.Length);

            var session = new CipherSession(false, material, BlockCipherEngineFactory.ForSession());
            await stream.WriteAsync(new byte[] { PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED, 0, 0, 0 }, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
            session.SetActivated(true);
            return (stream, session);
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
                    throw new InvalidOperationException("Peer closed before all bytes arrived.");
                }

                total += read;
            }

            return buffer;
        }

        private static async Task SendEncryptedAsync(
            NetworkStream stream, CipherSession session, byte[] plaintext, CancellationToken token)
        {
            byte[] wire = (byte[])plaintext.Clone();
            session.Encrypt(wire, 0, wire.Length);
            await stream.WriteAsync(wire, 0, wire.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        [Test]
        public async Task AttackRoundTrip_Loopback_FullCombatEventFlow()
        {
            using var cts = new CancellationTokenSource(TestTimeoutMs);
            CancellationToken token = cts.Token;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<(NetworkStream Stream, CipherSession Session)> serverTask =
                    AcceptAndHandshakeAsync(listener, token);

                using var conn = new TcpConnection();
                await conn.ConnectAsync("127.0.0.1", port, token).ConfigureAwait(false);
                using var handshake = new HandshakeClient(conn);
                await handshake.RunAsync(token).ConfigureAwait(false);

                var server = await serverTask.ConfigureAwait(false);
                var combat = new CombatClient(handshake);

                // Client attacks VID 42 with a normal attack.
                await combat.SendAttackAsync(42, 0, token).ConfigureAwait(false);

                byte[] attackWire = await ReadExactAsync(server.Stream, PacketCGAttack.PacketSize, token).ConfigureAwait(false);
                server.Session.Decrypt(attackWire, 0, attackWire.Length);
                PacketCGAttack attack = PacketCGAttackCodec.Deserialize(attackWire);
                Assert.AreEqual(0, attack.Type);
                Assert.AreEqual(42u, attack.VictimVid);

                // Server answers with the combat event flow in wire order.
                await SendEncryptedAsync(server.Stream, server.Session,
                    PacketGCDamageInfoCodec.Serialize(new PacketGCDamageInfo(42, 1, 150)), token).ConfigureAwait(false);
                await SendEncryptedAsync(server.Stream, server.Session,
                    PacketGCPointChangeCodec.Serialize(new PacketGCPointChange(42, PointTypes.Hp, -150, 850)), token).ConfigureAwait(false);
                await SendEncryptedAsync(server.Stream, server.Session,
                    PacketGCMotionCodec.Serialize(new PacketGCMotion(99, 42, 3)), token).ConfigureAwait(false);
                await SendEncryptedAsync(server.Stream, server.Session,
                    PacketGCStunCodec.Serialize(new PacketGCStun(42)), token).ConfigureAwait(false);
                await SendEncryptedAsync(server.Stream, server.Session,
                    PacketGCDeadCodec.Serialize(new PacketGCDead(42)), token).ConfigureAwait(false);

                CombatEvent damage = await combat.ReceiveEventAsync(PhaseType.Game, token).ConfigureAwait(false);
                Assert.AreEqual(CombatEvent.Kind.DamageInfo, damage.EventKind);
                Assert.AreEqual(42u, damage.Damage.Vid);
                Assert.AreEqual(150, damage.Damage.Damage);

                CombatEvent hp = await combat.ReceiveEventAsync(PhaseType.Game, token).ConfigureAwait(false);
                Assert.AreEqual(CombatEvent.Kind.PointChanged, hp.EventKind);
                Assert.AreEqual(PointTypes.Hp, hp.PointChange.Type);
                Assert.AreEqual(-150, hp.PointChange.Amount);
                Assert.AreEqual(850, hp.PointChange.Value);

                CombatEvent motion = await combat.ReceiveEventAsync(PhaseType.Game, token).ConfigureAwait(false);
                Assert.AreEqual(CombatEvent.Kind.Motion, motion.EventKind);
                Assert.AreEqual(99u, motion.Motion.Vid);
                Assert.AreEqual(42u, motion.Motion.VictimVid);

                CombatEvent stun = await combat.ReceiveEventAsync(PhaseType.Game, token).ConfigureAwait(false);
                Assert.AreEqual(CombatEvent.Kind.Stunned, stun.EventKind);
                Assert.AreEqual(42u, stun.Stun.Vid);

                CombatEvent dead = await combat.ReceiveEventAsync(PhaseType.Game, token).ConfigureAwait(false);
                Assert.AreEqual(CombatEvent.Kind.Dead, dead.EventKind);
                Assert.AreEqual(42u, dead.Dead.Vid);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public async Task StunInLoadingPhase_FailsClosed()
        {
            using var cts = new CancellationTokenSource(TestTimeoutMs);
            CancellationToken token = cts.Token;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<(NetworkStream Stream, CipherSession Session)> serverTask =
                    AcceptAndHandshakeAsync(listener, token);

                using var conn = new TcpConnection();
                await conn.ConnectAsync("127.0.0.1", port, token).ConfigureAwait(false);
                using var handshake = new HandshakeClient(conn);
                await handshake.RunAsync(token).ConfigureAwait(false);

                var server = await serverTask.ConfigureAwait(false);
                var combat = new CombatClient(handshake);

                // Stun is a Game-only packet (PhaseGame.cpp:303); receiving it
                // while the caller is in Loading must fail closed.
                await SendEncryptedAsync(server.Stream, server.Session,
                    PacketGCStunCodec.Serialize(new PacketGCStun(7)), token).ConfigureAwait(false);

                var ex = Assert.ThrowsAsync<HandshakeFailedException>(async () =>
                    await combat.ReceiveEventAsync(PhaseType.Loading, token).ConfigureAwait(false));
                StringAssert.Contains("not valid", ex.Message);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public async Task PointChangeInLoadingPhase_Accepted()
        {
            using var cts = new CancellationTokenSource(TestTimeoutMs);
            CancellationToken token = cts.Token;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<(NetworkStream Stream, CipherSession Session)> serverTask =
                    AcceptAndHandshakeAsync(listener, token);

                using var conn = new TcpConnection();
                await conn.ConnectAsync("127.0.0.1", port, token).ConfigureAwait(false);
                using var handshake = new HandshakeClient(conn);
                await handshake.RunAsync(token).ConfigureAwait(false);

                var server = await serverTask.ConfigureAwait(false);
                var combat = new CombatClient(handshake);

                // Point-change is valid in Loading too (PhaseLoading.cpp:129) —
                // e.g. item load applies stat deltas before the game phase.
                await SendEncryptedAsync(server.Stream, server.Session,
                    PacketGCPointChangeCodec.Serialize(new PacketGCPointChange(1, PointTypes.Hp, 10, 910)), token).ConfigureAwait(false);

                CombatEvent ev = await combat.ReceiveEventAsync(PhaseType.Loading, token).ConfigureAwait(false);
                Assert.AreEqual(CombatEvent.Kind.PointChanged, ev.EventKind);
                Assert.AreEqual(PointTypes.Hp, ev.PointChange.Type);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public async Task Attack_ZeroVictimVid_ThrowsArgument()
        {
            using var cts = new CancellationTokenSource(TestTimeoutMs);
            CancellationToken token = cts.Token;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task<(NetworkStream Stream, CipherSession Session)> serverTask =
                    AcceptAndHandshakeAsync(listener, token);

                using var conn = new TcpConnection();
                await conn.ConnectAsync("127.0.0.1", port, token).ConfigureAwait(false);
                using var handshake = new HandshakeClient(conn);
                await handshake.RunAsync(token).ConfigureAwait(false);

                var server = await serverTask.ConfigureAwait(false);
                var combat = new CombatClient(handshake);

                Assert.ThrowsAsync<ArgumentException>(async () =>
                    await combat.SendAttackAsync(0, 0, token).ConfigureAwait(false));
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void Constructor_UncompletedHandshake_FailsClosed()
        {
            using var conn = new TcpConnection();
            using var handshake = new HandshakeClient(conn);

            Assert.Throws<HandshakeFailedException>(() =>
            {
                new CombatClient(handshake);
            });
        }
    }
}
