using System;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Full stat block (TPacketGCPoints, 1021 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:1030-1034` (`INT points[POINT_MAX_NUM]`,
    /// `POINT_MAX_NUM=255`, `common/length.h:58`), sent `char.cpp:1557-1581`
    /// (`PointsPacket`: level/exp/hp/sp/gold/stamina + all `GetPoint(i)`);
    /// Client `UserInterface/Packet.h:1600-1604` (`long points[255]`,
    /// `POINT_MAX_NUM=255`, `StdAfx.h:42`).
    /// Wire layout (total 1021 = 1+4*255):
    /// [0]       BYTE header (16 = 0x10)
    /// [1..1020] INT  points[255] (4 bytes LE each)
    /// </summary>
    public struct PacketGCPoints : IPacket
    {
        public const byte PacketHeader = Constants.PacketHeaders.HEADER_GC_CHARACTER_POINTS;
        public const int PointCount = 255; // POINT_MAX_NUM
        public const int PacketSize = 1 + (4 * PointCount); // 1021 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public int[] Points { get; set; }

        public PacketGCPoints(int[] points)
        {
            Points = Pad(points);
        }

        private static int[] Pad(int[] source)
        {
            var result = new int[PointCount];
            if (source != null)
            {
                Array.Copy(source, 0, result, 0, Math.Min(source.Length, PointCount));
            }

            return result;
        }

        public override string ToString()
        {
            return $"PacketGCPoints(Count={PointCount})";
        }
    }
}
