using System;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// One item bonus attribute (TPlayerItemAttribute, 3 bytes).
    /// Server `common/tables.h:310-314`, client
    /// `UserInterface/GameType.h:312-316` — identical.
    /// </summary>
    public struct ItemAttribute : IEquatable<ItemAttribute>
    {
        public const int FieldSize = 1 + 2; // 3 bytes

        public byte Type { get; set; }
        public short Value { get; set; }

        public bool Equals(ItemAttribute other)
        {
            return Type == other.Type && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ItemAttribute other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(Type, Value);
        }

        public override string ToString()
        {
            return $"ItemAttribute(Type={Type}, Value={Value})";
        }
    }
}
