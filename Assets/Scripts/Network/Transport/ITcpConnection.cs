using System;
using System.Threading;
using System.Threading.Tasks;

namespace Metin2.Network.Transport
{
    /// <summary>
    /// Abstraction for TCP connection operations.
    /// Decouples network logic from raw socket APIs.
    /// </summary>
    public interface ITcpConnection : IDisposable
    {
        bool IsConnected { get; }
        string RemoteHost { get; }
        int RemotePort { get; }

        Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
        Task DisconnectAsync();
        Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
        Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
    }
}
