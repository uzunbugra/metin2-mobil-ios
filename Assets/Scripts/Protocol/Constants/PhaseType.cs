namespace Metin2.Protocol.Constants
{
    /// <summary>
    /// Source-verified phase enum matching EPhase in Server packet.h:797-812.
    /// </summary>
    public enum PhaseType : byte
    {
        Close = 0,
        Handshake = 1,
        Login = 2,
        Select = 3,
        Loading = 4,
        Game = 5,
        Dead = 6,
        ClientConnecting = 7,
        DbClient = 8,
        P2P = 9,
        Auth = 10,
        Teen = 11
    }
}
