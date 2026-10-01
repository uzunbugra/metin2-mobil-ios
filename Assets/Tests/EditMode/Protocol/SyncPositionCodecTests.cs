using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class SyncPositionCodecTests
    {
        private static SyncPositionElement[] SampleElements()
        {
            return new SyncPositionElement[]
            {
                new SyncPositionElement { Vid = 100, X = 1000, Y = 2000 },
                new SyncPositionElement { Vid = 200, X = -50, Y = 75 }
            };
        }

        [Test]
        public void Element_RoundTrip()
        {
            byte[] buffer = new byte[SyncPositionElement.FieldSize];
            var element = new SyncPositionElement { Vid = 0xDEADBEEF, X = -1, Y = 2 };

            SyncPositionElementCodec.Serialize(element, buffer);
            Assert.IsTrue(SyncPositionElementCodec.TryDeserialize(buffer, out SyncPositionElement restored, out string _));
            Assert.IsTrue(element.Equals(restored));
        }

        [Test]
        public void CgSync_Serialize_ProducesSizePrefixedBatch()
        {
            byte[] bytes = PacketCGSyncPositionCodec.Serialize(new PacketCGSyncPosition(SampleElements()));

            // 3 + 2*12 = 27; wSize LE = 27 at [1..2].
            Assert.AreEqual(27, bytes.Length);
            Assert.AreEqual(0x08, bytes[0]);
            Assert.AreEqual(0x1B, bytes[1]);
            Assert.AreEqual(0x00, bytes[2]);
            // First vid 100 = 0x64 at [3..6].
            Assert.AreEqual(0x64, bytes[3]);
        }

        [Test]
        public void CgSync_RoundTrip_PreservesElements()
        {
            var original = new PacketCGSyncPosition(SampleElements());
            Assert.IsTrue(PacketCGSyncPositionCodec.TryDeserialize(
                PacketCGSyncPositionCodec.Serialize(original), out PacketCGSyncPosition restored, out string _));

            Assert.AreEqual(2, restored.ElementCount);
            Assert.IsTrue(SampleElements()[0].Equals(restored.Elements[0]));
            Assert.IsTrue(SampleElements()[1].Equals(restored.Elements[1]));
        }

        [Test]
        public void CgSync_TooManyElements_ThrowsFailClosed()
        {
            var elements = new SyncPositionElement[17];
            Assert.Throws<System.ArgumentException>(() =>
            {
                PacketCGSyncPositionCodec.Serialize(new PacketCGSyncPosition(elements));
            });
        }

        [Test]
        public void CgSync_MisalignedSizeField_ReturnsFalse()
        {
            // wSize 10: (10-3) % 12 != 0.
            Assert.IsFalse(PacketCGSyncPositionCodec.TryDeserialize(
                new byte[] { 0x08, 0x0A, 0x00 }, out PacketCGSyncPosition _, out string error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void GcSync_RoundTrip_PreservesElements()
        {
            byte[] wire = PacketGCSyncPositionCodec.Serialize(new PacketGCSyncPosition(SampleElements()));

            Assert.AreEqual(0x05, wire[0]);
            Assert.IsTrue(PacketGCSyncPositionCodec.TryDeserialize(
                wire, out PacketGCSyncPosition restored, out string _));
            Assert.AreEqual(2, restored.Elements.Length);
            Assert.AreEqual(100u, restored.Elements[0].Vid);
            Assert.AreEqual(-50, restored.Elements[1].X);
        }

        [Test]
        public void GcSync_OverClampCount_ReturnsFalse()
        {
            // wSize claims 17 elements: rejected like the server clamp logic.
            byte[] wire = new byte[3 + (12 * 17)];
            wire[0] = 0x05;
            wire[1] = (byte)((3 + (12 * 17)) & 0xFF);
            wire[2] = (byte)((3 + (12 * 17)) >> 8);

            Assert.IsFalse(PacketGCSyncPositionCodec.TryDeserialize(
                wire, out PacketGCSyncPosition _, out string error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void Deserialize_WrongHeader_ThrowsPacketParseException()
        {
            byte[] wire = PacketGCSyncPositionCodec.Serialize(new PacketGCSyncPosition(SampleElements()));

            Assert.Throws<PacketParseException>(() =>
            {
                PacketCGSyncPositionCodec.Deserialize(wire);
            });
        }
    }
}
