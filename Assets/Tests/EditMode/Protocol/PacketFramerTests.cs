using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Framing;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketFramerTests
    {
        private static byte[] GoldenHandshake()
        {
            return PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(0x12345678, 123456, 50));
        }

        private static byte[] GoldenPhase()
        {
            return PacketGCPhaseCodec.Serialize(new PacketGCPhase(Metin2.Protocol.Constants.PhaseType.Game));
        }

        [Test]
        public void SingleCompleteFrame_DequeueExactBytes()
        {
            var framer = new PacketFramer();
            byte[] golden = GoldenHandshake();

            framer.Append(golden);

            Assert.IsTrue(framer.TryDequeue(out byte[] frame));
            CollectionAssert.AreEqual(golden, frame);
            Assert.AreEqual(0, framer.BufferedBytes);
        }

        [Test]
        public void AuthReplies_FrameAtSourceVerifiedLengths()
        {
            // 150 = 6B (packet.h:849-854), 7 = 10B (packet.h:856-860).
            var framer = new PacketFramer();
            byte[] success = PacketGCAuthSuccessCodec.Serialize(new PacketGCAuthSuccess(1, 1));
            byte[] failure = PacketGCLoginFailureCodec.Serialize(new PacketGCLoginFailure("NOID"));

            Assert.AreEqual(6, success.Length);
            Assert.AreEqual(10, failure.Length);

            framer.Append(success);
            framer.Append(failure);

            Assert.IsTrue(framer.TryDequeue(out byte[] first));
            CollectionAssert.AreEqual(success, first);
            Assert.IsTrue(framer.TryDequeue(out byte[] second));
            CollectionAssert.AreEqual(failure, second);
            Assert.AreEqual(0, framer.BufferedBytes);
        }

        [Test]
        public void SelectReplies_FrameAtSourceVerifiedLengths()
        {
            // 90 = 2B (packet.h:1638-1642), 32 = 329B (packet.h:838-847).
            var framer = new PacketFramer();
            byte[] empire = PacketGCEmpireCodec.Serialize(new PacketGCEmpire(2));
            byte[] slots = new byte[PacketGCLoginSuccess.PacketSize];
            slots[0] = PacketGCLoginSuccess.PacketHeader;

            Assert.AreEqual(2, empire.Length);
            Assert.AreEqual(329, slots.Length);

            framer.Append(empire);
            framer.Append(slots);

            Assert.IsTrue(framer.TryDequeue(out byte[] first));
            CollectionAssert.AreEqual(empire, first);
            Assert.IsTrue(framer.TryDequeue(out byte[] second));
            CollectionAssert.AreEqual(slots, second);
            Assert.AreEqual(0, framer.BufferedBytes);
        }

        [Test]
        public void Coalesced_TwoFramesInOneAppend_DequeueInOrder()
        {
            var framer = new PacketFramer();
            byte[] handshake = GoldenHandshake();
            byte[] phase = GoldenPhase();

            byte[] coalesced = new byte[handshake.Length + phase.Length];
            handshake.CopyTo(coalesced, 0);
            phase.CopyTo(coalesced, handshake.Length);
            framer.Append(coalesced);

            Assert.IsTrue(framer.TryDequeue(out byte[] first));
            CollectionAssert.AreEqual(handshake, first);

            Assert.IsTrue(framer.TryDequeue(out byte[] second));
            CollectionAssert.AreEqual(phase, second);

            Assert.IsFalse(framer.TryDequeue(out byte[] _));
        }

        [Test]
        public void Fragmented_SplitAcrossAppends_WaitsForCompletion()
        {
            var framer = new PacketFramer();
            byte[] golden = GoldenHandshake();

            byte[] part1 = new byte[5];
            byte[] part2 = new byte[golden.Length - 5];
            System.Array.Copy(golden, 0, part1, 0, 5);
            System.Array.Copy(golden, 5, part2, 0, part2.Length);

            framer.Append(part1);
            Assert.IsFalse(framer.TryDequeue(out byte[] _));
            Assert.AreEqual(5, framer.BufferedBytes);

            framer.Append(part2);
            Assert.IsTrue(framer.TryDequeue(out byte[] frame));
            CollectionAssert.AreEqual(golden, frame);
        }

        [Test]
        public void Fragmented_OneByteAtATime_CompletesOnLastByte()
        {
            var framer = new PacketFramer();
            byte[] golden = GoldenHandshake();

            for (int i = 0; i < golden.Length - 1; i++)
            {
                framer.Append(new byte[] { golden[i] });
                Assert.IsFalse(framer.TryDequeue(out byte[] _));
            }

            framer.Append(new byte[] { golden[golden.Length - 1] });
            Assert.IsTrue(framer.TryDequeue(out byte[] frame));
            CollectionAssert.AreEqual(golden, frame);
        }

        [Test]
        public void PaddingBytes_SkippedSilently()
        {
            var framer = new PacketFramer();
            byte[] golden = GoldenHandshake();

            byte[] padded = new byte[] { 0x00, 0x00, 0x00 };
            framer.Append(padded);
            framer.Append(golden);

            Assert.IsTrue(framer.TryDequeue(out byte[] frame));
            CollectionAssert.AreEqual(golden, frame);
            Assert.AreEqual(0, framer.DroppedBytes);
        }

        [Test]
        public void UnknownHeader_DroppedSafely_NextFrameStillDecodes()
        {
            var framer = new PacketFramer();
            byte[] golden = GoldenHandshake();

            framer.Append(new byte[] { 0x42 }); // 0x42 is not a registered S2C header
            framer.Append(golden);

            Assert.IsTrue(framer.TryDequeue(out byte[] frame));
            CollectionAssert.AreEqual(golden, frame);
            Assert.AreEqual(1, framer.DroppedBytes);
        }

        [Test]
        public void Incomplete_ReturnsFalseAndPreservesBuffer()
        {
            var framer = new PacketFramer();
            byte[] golden = GoldenHandshake();

            byte[] truncated = new byte[golden.Length - 1];
            System.Array.Copy(golden, truncated, truncated.Length);
            framer.Append(truncated);

            Assert.IsFalse(framer.TryDequeue(out byte[] frame));
            Assert.IsNull(frame);
            Assert.AreEqual(truncated.Length, framer.BufferedBytes);
        }

        [Test]
        public void OversizeAppend_ThrowsPacketException()
        {
            var framer = new PacketFramer();
            byte[] huge = new byte[PacketFramer.MaxFrameLength + 1];

            Assert.Throws<PacketException>(() => framer.Append(huge));
        }

        [Test]
        public void KeyAgreement_LargeFrame_FragmentedAndCoalesced()
        {
            var framer = new PacketFramer();
            byte[] data = new byte[PacketKeyAgreement.MaxDataLen];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i & 0xff);
            }

            byte[] golden = PacketKeyAgreementCodec.Serialize(new PacketKeyAgreement(16, 128, data));
            Assert.AreEqual(261, golden.Length);

            byte[] part1 = new byte[100];
            byte[] part2 = new byte[golden.Length - 100];
            System.Array.Copy(golden, 0, part1, 0, 100);
            System.Array.Copy(golden, 100, part2, 0, part2.Length);

            framer.Append(part1);
            Assert.IsFalse(framer.TryDequeue(out byte[] _));

            framer.Append(part2);
            Assert.IsTrue(framer.TryDequeue(out byte[] frame));
            CollectionAssert.AreEqual(golden, frame);
        }

        [Test]
        public void Reset_ClearsBufferAndCounters()
        {
            var framer = new PacketFramer();
            framer.Append(new byte[] { 0x42 });
            framer.TryDequeue(out byte[] _);
            Assert.AreEqual(1, framer.DroppedBytes);

            framer.Reset();
            Assert.AreEqual(0, framer.BufferedBytes);
            Assert.AreEqual(0, framer.DroppedBytes);
            Assert.IsFalse(framer.TryDequeue(out byte[] _));
        }
    }
}
