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
        public void CreateDeleteReplies_FrameAtSourceVerifiedLengths()
        {
            // 8 = 65B (packet.h:556-561), 9 = 2B (packet.h:862-866),
            // 10 = 2B (input_db.cpp:285-286), 11 = 1B (input_db.cpp:296).
            var framer = new PacketFramer();
            byte[] createOk = PacketGCCreateSuccessCodec.Serialize(
                new PacketGCCreateSuccess(1, SimplePlayerCodecTests.SampleSlot()));
            byte[] createFail = PacketGCCreateFailureCodec.Serialize(new PacketGCCreateFailure(1));
            byte[] deleteOk = PacketGCDeleteSuccessCodec.Serialize(new PacketGCDeleteSuccess(2));
            byte[] deleteFail = PacketGCDeleteFailureCodec.Serialize(new PacketGCDeleteFailure());

            Assert.AreEqual(65, createOk.Length);
            Assert.AreEqual(2, createFail.Length);
            Assert.AreEqual(2, deleteOk.Length);
            Assert.AreEqual(1, deleteFail.Length);

            framer.Append(createOk);
            framer.Append(createFail);
            framer.Append(deleteOk);
            framer.Append(deleteFail);

            Assert.IsTrue(framer.TryDequeue(out byte[] f1));
            CollectionAssert.AreEqual(createOk, f1);
            Assert.IsTrue(framer.TryDequeue(out byte[] f2));
            CollectionAssert.AreEqual(createFail, f2);
            Assert.IsTrue(framer.TryDequeue(out byte[] f3));
            CollectionAssert.AreEqual(deleteOk, f3);
            Assert.IsTrue(framer.TryDequeue(out byte[] f4));
            CollectionAssert.AreEqual(deleteFail, f4);
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

        [Test]
        public void WorldEntryPackets_FrameAtSourceVerifiedLengths()
        {
            // 113 = 46B (packet.h:982-991), 106 = 5B (packet.h:1891-1895),
            // 121 = 2B (packet.h:1995-1999).
            var framer = new PacketFramer();
            byte[] mainChar = PacketGCMainCharacterCodec.Serialize(
                PacketGCMainCharacterTests.SamplePacket());
            byte[] time = PacketGCTimeCodec.Serialize(new PacketGCTime(1));
            byte[] channel = PacketGCChannelCodec.Serialize(new PacketGCChannel(1));

            Assert.AreEqual(46, mainChar.Length);
            Assert.AreEqual(5, time.Length);
            Assert.AreEqual(2, channel.Length);

            framer.Append(mainChar);
            framer.Append(time);
            framer.Append(channel);

            Assert.IsTrue(framer.TryDequeue(out byte[] f1));
            CollectionAssert.AreEqual(mainChar, f1);
            Assert.IsTrue(framer.TryDequeue(out byte[] f2));
            CollectionAssert.AreEqual(time, f2);
            Assert.IsTrue(framer.TryDequeue(out byte[] f3));
            CollectionAssert.AreEqual(channel, f3);
            Assert.AreEqual(0, framer.BufferedBytes);
        }

        [Test]
        public void StatsAndSpawn_FrameAtSourceVerifiedLengths()
        {
            // 16 = 1021B (packet.h:1030-1034), 76 = 1531B (packet.h:1036-1040),
            // 1 = 35B (packet.h:886-903), 2 = 5B (packet.h:959-963).
            var framer = new PacketFramer();
            byte[] points = new byte[PacketGCPoints.PacketSize];
            points[0] = PacketGCPoints.PacketHeader;
            byte[] skills = new byte[PacketGCSkillLevel.PacketSize];
            skills[0] = PacketGCSkillLevel.PacketHeader;
            byte[] add = new byte[PacketGCCharacterAdd.PacketSize];
            add[0] = PacketGCCharacterAdd.PacketHeader;
            byte[] del = PacketGCCharacterDeleteCodec.Serialize(new PacketGCCharacterDelete(7));

            Assert.AreEqual(1021, points.Length);
            Assert.AreEqual(1531, skills.Length);
            Assert.AreEqual(35, add.Length);
            Assert.AreEqual(5, del.Length);

            framer.Append(points);
            framer.Append(skills);
            framer.Append(add);
            framer.Append(del);

            Assert.IsTrue(framer.TryDequeue(out byte[] f1));
            CollectionAssert.AreEqual(points, f1);
            Assert.IsTrue(framer.TryDequeue(out byte[] f2));
            CollectionAssert.AreEqual(skills, f2);
            Assert.IsTrue(framer.TryDequeue(out byte[] f3));
            CollectionAssert.AreEqual(add, f3);
            Assert.IsTrue(framer.TryDequeue(out byte[] f4));
            CollectionAssert.AreEqual(del, f4);
            Assert.AreEqual(0, framer.BufferedBytes);
        }

        [Test]
        public void ItemPackets_FrameAtSourceVerifiedLengths()
        {
            // 21 = 51B (packet.h:1073-1084), 20 = 42B (packet.h:1063-1071),
            // 25 = 38B (packet.h:1108-1115).
            var framer = new PacketFramer();
            byte[] set = new byte[PacketGCItemSet.PacketSize];
            set[0] = PacketGCItemSet.PacketHeader;
            byte[] clear = new byte[PacketGCItemDel.PacketSize];
            clear[0] = PacketGCItemDel.PacketHeader;
            byte[] update = new byte[PacketGCItemUpdate.PacketSize];
            update[0] = PacketGCItemUpdate.PacketHeader;

            Assert.AreEqual(51, set.Length);
            Assert.AreEqual(42, clear.Length);
            Assert.AreEqual(38, update.Length);

            framer.Append(set);
            framer.Append(clear);
            framer.Append(update);

            Assert.IsTrue(framer.TryDequeue(out byte[] f1));
            CollectionAssert.AreEqual(set, f1);
            Assert.IsTrue(framer.TryDequeue(out byte[] f2));
            CollectionAssert.AreEqual(clear, f2);
            Assert.IsTrue(framer.TryDequeue(out byte[] f3));
            CollectionAssert.AreEqual(update, f3);
            Assert.AreEqual(0, framer.BufferedBytes);
        }

        [Test]
        public void Ping_SingleByteFrame_DequeuedNotCountedAsGarbage()
        {
            // 44 = 1B TPacketGCPing (packet.h:1259-1262; client Packet.h:1839-1842).
            // Regression: header 44 was previously unregistered, so every server
            // keepalive ping was dropped and counted as garbage (DroppedBytes).
            var framer = new PacketFramer();
            byte[] ping = { Metin2.Protocol.Constants.PacketHeaders.HEADER_GC_PING };

            framer.Append(ping);

            Assert.IsTrue(framer.TryDequeue(out byte[] frame));
            CollectionAssert.AreEqual(ping, frame);
            Assert.AreEqual(0, framer.BufferedBytes);
            Assert.AreEqual(0, framer.DroppedBytes);
        }

        [Test]
        public void Ping_CoalescedBetweenFrames_DequeuedInOrder()
        {
            var framer = new PacketFramer();
            byte[] phase = GoldenPhase();
            byte[] ping = { Metin2.Protocol.Constants.PacketHeaders.HEADER_GC_PING };

            byte[] coalesced = new byte[phase.Length + ping.Length + phase.Length];
            phase.CopyTo(coalesced, 0);
            ping.CopyTo(coalesced, phase.Length);
            phase.CopyTo(coalesced, phase.Length + ping.Length);
            framer.Append(coalesced);

            Assert.IsTrue(framer.TryDequeue(out byte[] f1));
            CollectionAssert.AreEqual(phase, f1);
            Assert.IsTrue(framer.TryDequeue(out byte[] f2));
            CollectionAssert.AreEqual(ping, f2);
            Assert.IsTrue(framer.TryDequeue(out byte[] f3));
            CollectionAssert.AreEqual(phase, f3);
            Assert.AreEqual(0, framer.DroppedBytes);
        }

        [Test]
        public void CombatPackets_FrameAtSourceVerifiedLengths()
        {
            // 17 = 17B (packet.h:1042-1049), 13/14 = 5B (packet.h:1051-1061),
            // 36 = 11B (packet.h:1158-1164), 135 = 10B (packet.h:2093-2099).
            var framer = new PacketFramer();
            byte[] pointChange = PacketGCPointChangeCodec.Serialize(
                new PacketGCPointChange(1, 5, -100, 900));
            byte[] stun = PacketGCStunCodec.Serialize(new PacketGCStun(2));
            byte[] dead = PacketGCDeadCodec.Serialize(new PacketGCDead(3));
            byte[] motion = PacketGCMotionCodec.Serialize(new PacketGCMotion(4, 5, 6));
            byte[] damage = PacketGCDamageInfoCodec.Serialize(new PacketGCDamageInfo(6, 1, 77));

            Assert.AreEqual(17, pointChange.Length);
            Assert.AreEqual(5, stun.Length);
            Assert.AreEqual(5, dead.Length);
            Assert.AreEqual(11, motion.Length);
            Assert.AreEqual(10, damage.Length);

            framer.Append(pointChange);
            framer.Append(stun);
            framer.Append(dead);
            framer.Append(motion);
            framer.Append(damage);

            Assert.IsTrue(framer.TryDequeue(out byte[] f1));
            CollectionAssert.AreEqual(pointChange, f1);
            Assert.IsTrue(framer.TryDequeue(out byte[] f2));
            CollectionAssert.AreEqual(stun, f2);
            Assert.IsTrue(framer.TryDequeue(out byte[] f3));
            CollectionAssert.AreEqual(dead, f3);
            Assert.IsTrue(framer.TryDequeue(out byte[] f4));
            CollectionAssert.AreEqual(motion, f4);
            Assert.IsTrue(framer.TryDequeue(out byte[] f5));
            CollectionAssert.AreEqual(damage, f5);
            Assert.AreEqual(0, framer.BufferedBytes);
            Assert.AreEqual(0, framer.DroppedBytes);
        }

        [Test]
        public void PointChange_InteriorZeroBytes_NeverTreatedAsPadding()
        {
            // The int-header quirk puts three 0x00 bytes at offsets 1..3 of the
            // frame; they are INSIDE the 17-byte frame and must be consumed
            // atomically — the padding skipper must not eat them between two
            // coalesced point-change frames.
            var framer = new PacketFramer();
            byte[] first = PacketGCPointChangeCodec.Serialize(
                new PacketGCPointChange(1, 5, -1, 99));
            byte[] second = PacketGCPointChangeCodec.Serialize(
                new PacketGCPointChange(2, 7, -2, 88));

            byte[] coalesced = new byte[first.Length + second.Length];
            first.CopyTo(coalesced, 0);
            second.CopyTo(coalesced, first.Length);
            framer.Append(coalesced);

            Assert.IsTrue(framer.TryDequeue(out byte[] f1));
            CollectionAssert.AreEqual(first, f1);
            Assert.IsTrue(framer.TryDequeue(out byte[] f2));
            CollectionAssert.AreEqual(second, f2);
            Assert.AreEqual(0, framer.BufferedBytes);
            Assert.AreEqual(0, framer.DroppedBytes);
        }

        [Test]
        public void SyncFrame_FragmentedWaits_CoalescedSplits()
        {
            // Dynamic wSize framing (packet.h:1317-1322): 2 elements = 27B.
            var framer = new PacketFramer();
            byte[] sync = PacketGCSyncPositionCodec.Serialize(new PacketGCSyncPosition(
                new SyncPositionElement[]
                {
                    new SyncPositionElement { Vid = 1, X = 10, Y = 20 },
                    new SyncPositionElement { Vid = 2, X = 30, Y = 40 }
                }));
            Assert.AreEqual(27, sync.Length);

            byte[] part1 = new byte[10];
            byte[] part2 = new byte[sync.Length - 10];
            System.Array.Copy(sync, 0, part1, 0, 10);
            System.Array.Copy(sync, 10, part2, 0, part2.Length);

            framer.Append(part1);
            Assert.IsFalse(framer.TryDequeue(out byte[] _));

            framer.Append(part2);
            Assert.IsTrue(framer.TryDequeue(out byte[] frame));
            CollectionAssert.AreEqual(sync, frame);
        }

        [Test]
        public void SyncFrame_MalformedSize_DroppedAndCounted()
        {
            var framer = new PacketFramer();
            // wSize 7: (7-3) % 12 != 0 -> invalid; trailing bytes (0x07 needs
            // 10B, only 1 buffered) cannot complete either.
            framer.Append(new byte[] { 0x05, 0x07, 0x00 });

            Assert.IsFalse(framer.TryDequeue(out byte[] _));
            Assert.Greater(framer.DroppedBytes, 0);
        }

        [Test]
        public void SyncFrame_OverClampSize_Dropped()
        {
            var framer = new PacketFramer();
            // wSize claims 17 elements (> 16 clamp) -> invalid.
            int badSize = 3 + (12 * 17);
            framer.Append(new byte[] { 0x05, (byte)(badSize & 0xFF), (byte)(badSize >> 8) });

            Assert.IsFalse(framer.TryDequeue(out byte[] _));
            Assert.Greater(framer.DroppedBytes, 0);
        }
    }
}
