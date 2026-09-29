using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Metin2.Network.Transport
{
    /// <summary>
    /// Default <see cref="ITcpConnection"/> implementation over <see cref="TcpClient"/>.
    /// Guarantees:
    /// - Concurrent SendAsync calls never interleave bytes (send semaphore, per AGENT_DEVELOPMENT_GUIDE.md §5.2).
    /// - ConnectAsync honors cancellation even on netstandard2.1 (no native ct overload there).
    /// - DisconnectAsync/Dispose are idempotent; the socket thread never touches Unity APIs.
    /// No UnityEngine dependency.
    /// </summary>
    public class TcpConnection : ITcpConnection
    {
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private TcpClient _client;
        private bool _disposed;

        public bool IsConnected => !_disposed && _client != null && _client.Connected;

        public string RemoteHost { get; private set; }

        public int RemotePort { get; private set; }

        public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(host))
            {
                throw new ArgumentException("Host must not be null or empty.", nameof(host));
            }

            if (IsConnected)
            {
                throw new InvalidOperationException("Already connected. Disconnect first.");
            }

            DisconnectInternal();

            var client = new TcpClient();
            try
            {
                // netstandard2.1 TcpClient has no ConnectAsync(host, port, ct) overload,
                // so abort the pending connect by closing the client when cancelled.
                using (cancellationToken.Register(() => client.Close()))
                {
                    await client.ConnectAsync(host, port).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();

                _client = client;
                RemoteHost = host;
                RemotePort = port;
            }
            catch
            {
                client.Close();
                throw;
            }
        }

        public Task DisconnectAsync()
        {
            DisconnectInternal();
            return Task.CompletedTask;
        }

        public async Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            // Serialize concurrent senders so packet bytes never interleave on the wire.
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!IsConnected)
                {
                    throw new InvalidOperationException("Connection was closed while waiting to send.");
                }

                var stream = _client.GetStream();
                await stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            var stream = _client.GetStream();
            // Returns 0 when the remote side closed the connection gracefully.
            return await stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisconnectInternal();
            _sendLock.Dispose();
        }

        private void DisconnectInternal()
        {
            var client = _client;
            _client = null;
            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch (SocketException)
                {
                    // Best-effort shutdown; the session layer observes IsConnected == false.
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TcpConnection));
            }
        }
    }
}
