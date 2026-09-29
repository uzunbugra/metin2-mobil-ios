using System;
using System.Text;
using NUnit.Framework;
using Metin2.Protocol.Buffer;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketReaderWriterTests
    {
        [Test]
        public void Primitives_WriteAndRead_MatchLittleEndian()
        {
            byte[] buffer = new byte[32];
            var writer = new PacketWriter(buffer);

            writer.WriteByte(0x42);
            writer.WriteUInt16(0x1234);
            writer.WriteInt16(-12345);
            writer.WriteUInt32(0x89ABCDEF);
            writer.WriteInt32(-987654321);

            Assert.AreEqual(1 + 2 + 2 + 4 + 4, writer.BytesWritten);

            // Verify Little-Endian bytes manually
            // UInt16 0x1234 -> 0x34, 0x12
            Assert.AreEqual(0x34, buffer[1]);
            Assert.AreEqual(0x12, buffer[2]);

            // UInt32 0x89ABCDEF -> 0xEF, 0xCD, 0xAB, 0x89
            Assert.AreEqual(0xEF, buffer[5]);
            Assert.AreEqual(0xCD, buffer[6]);
            Assert.AreEqual(0xAB, buffer[7]);
            Assert.AreEqual(0x89, buffer[8]);

            var reader = new PacketReader(buffer.AsSpan(0, writer.BytesWritten));
            Assert.AreEqual(0x42, reader.ReadByte());
            Assert.AreEqual(0x1234, reader.ReadUInt16());
            Assert.AreEqual(-12345, reader.ReadInt16());
            Assert.AreEqual(0x89ABCDEF, reader.ReadUInt32());
            Assert.AreEqual(-987654321, reader.ReadInt32());
            Assert.AreEqual(0, reader.Remaining);
        }

        [Test]
        public void FixedString_ZeroPaddingAndNullTerminator()
        {
            byte[] buffer = new byte[16];
            var writer = new PacketWriter(buffer);

            writer.WriteFixedString("Metin2", 10, Encoding.ASCII);
            Assert.AreEqual(10, writer.BytesWritten);

            // Verify content: 'M', 'e', 't', 'i', 'n', '2', 0x00, 0x00, 0x00, 0x00
            Assert.AreEqual((byte)'M', buffer[0]);
            Assert.AreEqual((byte)'2', buffer[5]);
            Assert.AreEqual(0, buffer[6]);
            Assert.AreEqual(0, buffer[9]);

            var reader = new PacketReader(buffer);
            string readStr = reader.ReadFixedString(10, Encoding.ASCII);
            Assert.AreEqual("Metin2", readStr);
        }

        [Test]
        public void PacketWriter_Overflow_ThrowsArgumentOutOfRangeException()
        {
            byte[] buffer = new byte[4];
            var writer = new PacketWriter(buffer);

            writer.WriteUInt32(0x12345678); // Exactly 4 bytes

            bool threw = false;
            try
            {
                writer.WriteByte(0xFF); // Exceeds capacity
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected ArgumentOutOfRangeException when exceeding capacity.");
        }

        [Test]
        public void PacketWriter_WriteZeros_ZeroPadsCorrectly()
        {
            byte[] buffer = new byte[8];
            for (int i = 0; i < buffer.Length; i++) buffer[i] = 0xFF;

            var writer = new PacketWriter(buffer);
            writer.WriteByte(0xAA);
            writer.WriteZeros(4);
            writer.WriteByte(0xBB);

            Assert.AreEqual(6, writer.BytesWritten);
            Assert.AreEqual(0xAA, buffer[0]);
            Assert.AreEqual(0x00, buffer[1]);
            Assert.AreEqual(0x00, buffer[2]);
            Assert.AreEqual(0x00, buffer[3]);
            Assert.AreEqual(0x00, buffer[4]);
            Assert.AreEqual(0xBB, buffer[5]);
            Assert.AreEqual(0xFF, buffer[6]);
        }

        [Test]
        public void PacketWriter_NegativeCountOrOverflow_ThrowsArgumentOutOfRangeException()
        {
            byte[] buffer = new byte[8];

            bool threw1 = false;
            try
            {
                var writer = new PacketWriter(buffer);
                writer.WriteZeros(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw1 = true;
            }
            Assert.IsTrue(threw1, "Expected ArgumentOutOfRangeException for negative count.");

            bool threw2 = false;
            try
            {
                var writer = new PacketWriter(buffer);
                writer.WriteZeros(9);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw2 = true;
            }
            Assert.IsTrue(threw2, "Expected ArgumentOutOfRangeException for overflow count.");

            bool threw3 = false;
            try
            {
                var writer = new PacketWriter(buffer);
                writer.WriteFixedString("test", -1);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw3 = true;
            }
            Assert.IsTrue(threw3, "Expected ArgumentOutOfRangeException for negative fixedLength.");
        }

        [Test]
        public void PacketReader_Underflow_ThrowsArgumentOutOfRangeException()
        {
            byte[] buffer = new byte[2];
            var reader = new PacketReader(buffer);

            bool threw = false;
            try
            {
                reader.ReadUInt32(); // Needs 4 bytes, only 2 available
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected ArgumentOutOfRangeException on underflow.");
        }

        [Test]
        public void PacketReader_NegativeCountOrOverflow_ThrowsArgumentOutOfRangeException()
        {
            byte[] buffer = new byte[8];

            bool threw1 = false;
            try
            {
                var reader = new PacketReader(buffer);
                reader.ReadFixedString(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw1 = true;
            }
            Assert.IsTrue(threw1, "Expected ArgumentOutOfRangeException for negative fixedLength.");

            bool threw2 = false;
            try
            {
                var reader = new PacketReader(buffer);
                reader.ReadSpan(9);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw2 = true;
            }
            Assert.IsTrue(threw2, "Expected ArgumentOutOfRangeException for overflow count.");
        }
    }
}
