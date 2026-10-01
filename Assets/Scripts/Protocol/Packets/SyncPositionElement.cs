using System;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// One sync-position element, shared by the CG and GC dynamic packets
    /// (12 bytes).
    /// Server `packet.h:597-602` (CG) / `packet.h:1310-1315` (GC);
    /// client `Packet.h:706-711` / `Packet.h:1947-1953` — identical.
    /// </summary>
    public struct SyncPositionElement : IEquatable<SyncPositionElement>
    {
        public const int FieldSize = 4 + 4 + 4; // 12 bytes

        public uint Vid { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public bool Equals(SyncPositionElement other)
        {
            return Vid == other.Vid && X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is SyncPositionElement other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(Vid, X, Y);
        }

        public override string ToString()
        {
            return $"SyncElement(Vid={Vid}, Pos=({X},{Y}))";
        }
    }
}
