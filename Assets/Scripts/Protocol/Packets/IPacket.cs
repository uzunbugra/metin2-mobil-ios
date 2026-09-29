namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Base interface for all strongly-typed network packet models.
    /// </summary>
    public interface IPacket
    {
        byte Header { get; }
        int Length { get; }
    }
}
