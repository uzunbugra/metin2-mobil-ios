using Metin2.Protocol.Constants;

namespace Metin2.Network.Session
{
    /// <summary>
    /// Tracks client connection lifecycle state, phase transitions, and synchronized server time.
    /// </summary>
    public class NetworkSessionState
    {
        public PhaseType CurrentPhase { get; private set; } = PhaseType.Close;
        public bool IsConnected { get; private set; }
        public uint HandshakeId { get; private set; }
        public uint ServerTimeBase { get; private set; }
        public int LatencyDelta { get; private set; }

        public void SetConnected(bool connected)
        {
            IsConnected = connected;
            if (!connected)
            {
                CurrentPhase = PhaseType.Close;
            }
        }

        public void SetPhase(PhaseType phase)
        {
            CurrentPhase = phase;
        }

        public void ApplyHandshake(uint handshakeId, uint serverTime, int latencyDelta)
        {
            HandshakeId = handshakeId;
            ServerTimeBase = serverTime;
            LatencyDelta = latencyDelta;
        }

        public void Reset()
        {
            CurrentPhase = PhaseType.Close;
            IsConnected = false;
            HandshakeId = 0;
            ServerTimeBase = 0;
            LatencyDelta = 0;
        }
    }
}
