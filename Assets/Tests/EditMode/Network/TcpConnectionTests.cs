using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Metin2.Network.Transport;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Network
{
    [TestFixture]
    public class TcpConnectionTests
    {
        private const int TestTimeoutMs = 5000;

        private static CancellationToken TestToken(CancellationTokenSource cts)
        {
            cts.CancelAfter(TestTimeoutMs);
            return cts.Token;
        }

        [Test]
        public async Task Connect_Loopback_SucceedsAndTracksEndpoint()
        {
            using var cts = new CancellationTokenSource();
            var token = TestToken(cts);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var acceptTask = listener.AcceptTcpClientAsync();

                using var conn = new TcpConnection();
                Assert.IsFalse(conn.IsConnected);

                await conn.ConnectAsync("127.0.0.1", port, token);

                Assert.IsTrue(conn.IsConnected);
                Assert.AreEqual("127.0.0.1", conn.RemoteHost);
                Assert.AreEqual(port, conn.RemotePort);

                using var server = await acceptTask;
                Assert.IsTrue(server.Connected);

                await conn.DisconnectAsync();
                Assert.IsFalse(conn.IsConnected);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public async Task SendReceive_Loopback_RoundTripExactBytes()
        {
            using var cts = new CancellationTokenSource();
            var token = TestToken(cts);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var acceptTask = listener.AcceptTcpClientAsync();

                using var conn = new TcpConnection();
                await conn.ConnectAsync("127.0.0.1", port, token);
                using var server = await acceptTask;
                var serverStream = server.GetStream();

                // Client -> server: golden handshake bytes arrive intact.
                byte[] golden = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(0x12345678, 123456, 50));
                await conn.SendAsync(golden, 0, golden.Length, token);

                byte[] received = new byte[golden.Length];
                int totalRead = 0;
                while (totalRead < received.Length)
                {
                    int read = await serverStream.ReadAsync(received, totalRead, received.Length - totalRead, token);
                    Assert.Greater(read, 0, "Server stream closed before full packet arrived.");
                    totalRead += read;
                }
                CollectionAssert.AreEqual(golden, received);

                // Server -> client: phase bytes arrive intact via ReceiveAsync.
                byte[] phase = PacketGCPhaseCodec.Serialize(new PacketGCPhase(Metin2.Protocol.Constants.PhaseType.Game));
                await serverStream.WriteAsync(phase, 0, phase.Length, token);

                byte[] phaseReceived = new byte[phase.Length];
                int phaseRead = 0;
                while (phaseRead < phaseReceived.Length)
                {
                    int read = await conn.ReceiveAsync(phaseReceived, phaseRead, phaseReceived.Length - phaseRead, token);
                    Assert.Greater(read, 0, "Client stream closed before full packet arrived.");
                    phaseRead += read;
                }
                CollectionAssert.AreEqual(phase, phaseReceived);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public async Task Disconnect_IsIdempotentAndSendAfterDisconnectThrows()
        {
            using var cts = new CancellationTokenSource();
            var token = TestToken(cts);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var acceptTask = listener.AcceptTcpClientAsync();

                using var conn = new TcpConnection();
                await conn.ConnectAsync("127.0.0.1", port, token);
                using var server = await acceptTask;

                await conn.DisconnectAsync();
                await conn.DisconnectAsync(); // must not throw
                Assert.IsFalse(conn.IsConnected);

                Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                {
                    await conn.SendAsync(new byte[] { 0x01 }, 0, 1, token);
                });
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void Connect_ClosedPort_ThrowsSocketException()
        {
            using var cts = new CancellationTokenSource();
            var token = TestToken(cts);

            // Reserve then release a loopback port so nothing listens on it.
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int closedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            using var conn = new TcpConnection();
            Assert.ThrowsAsync<SocketException>(async () =>
            {
                await conn.ConnectAsync("127.0.0.1", closedPort, token);
            });
            Assert.IsFalse(conn.IsConnected);
        }

        [Test]
        public async Task Receive_GracefulServerClose_ReturnsZero()
        {
            using var cts = new CancellationTokenSource();
            var token = TestToken(cts);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var acceptTask = listener.AcceptTcpClientAsync();

                using var conn = new TcpConnection();
                await conn.ConnectAsync("127.0.0.1", port, token);
                using var server = await acceptTask;

                server.Close(); // graceful close from the server side

                byte[] buffer = new byte[64];
                int read = await conn.ReceiveAsync(buffer, 0, buffer.Length, token);
                Assert.AreEqual(0, read);
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
