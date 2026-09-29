using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Metin2.Core.Logging;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Packets;

namespace Metin2.Network.Transport
{
    public class TcpProbeResult
    {
        public bool Connected { get; set; }
        public bool HandshakeReceived { get; set; }
        public PacketGCHandshake HandshakePacket { get; set; }
        public string ErrorMessage { get; set; }
        public long ResponseTimeMs { get; set; }
    }

    /// <summary>
    /// Non-destructive TCP probe utility for testing server connectivity and receiving initial TPacketGCHandshake.
    /// Strictly complies with AGENT_DEVELOPMENT_GUIDE.md § 0.1 rule 6 & rule 7.
    /// </summary>
    public class SimpleTcpProbe
    {
        private readonly ILogger _logger;

        public SimpleTcpProbe(ILogger logger = null)
        {
            _logger = logger;
        }

        public async Task<TcpProbeResult> ProbeAuthServerAsync(string host, int port, int timeoutMs = 5000)
        {
            var result = new TcpProbeResult();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var cts = new CancellationTokenSource(timeoutMs);
            using var client = new TcpClient();

            try
            {
                _logger?.LogInfo($"Attempting probe connection to {host}:{port}...");
                var connectTask = client.ConnectAsync(host, port);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(timeoutMs, cts.Token));

                if (completedTask != connectTask)
                {
                    result.ErrorMessage = $"Connection timed out after {timeoutMs}ms";
                    _logger?.LogWarning(result.ErrorMessage);
                    return result;
                }

                await connectTask; // Propagate exceptions if any
                result.Connected = true;

                var stream = client.GetStream();
                byte[] buffer = new byte[PacketGCHandshake.PacketSize];
                int totalRead = 0;

                while (totalRead < PacketGCHandshake.PacketSize)
                {
                    int read = await stream.ReadAsync(buffer, totalRead, PacketGCHandshake.PacketSize - totalRead, cts.Token);
                    if (read == 0)
                    {
                        break;
                    }
                    totalRead += read;
                }

                sw.Stop();
                result.ResponseTimeMs = sw.ElapsedMilliseconds;

                if (totalRead == PacketGCHandshake.PacketSize)
                {
                    if (PacketGCHandshakeCodec.TryDeserialize(buffer, out var handshake, out string error))
                    {
                        result.HandshakeReceived = true;
                        result.HandshakePacket = handshake;
                        _logger?.LogInfo($"Probe received valid TPacketGCHandshake: Handshake=0x{handshake.Handshake:X8}, Time={handshake.Time}ms, Delta={handshake.Delta}");
                    }
                    else
                    {
                        result.ErrorMessage = $"Received 13 bytes but failed to decode TPacketGCHandshake: {error}";
                        _logger?.LogWarning(result.ErrorMessage);
                    }
                }
                else
                {
                    result.ErrorMessage = $"Received {totalRead}/{PacketGCHandshake.PacketSize} bytes before connection terminated.";
                    _logger?.LogWarning(result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.ErrorMessage = ex.Message;
                _logger?.LogWarning($"Probe connection failed: {ex.Message}");
            }

            return result;
        }
    }
}
