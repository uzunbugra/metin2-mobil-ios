using System;
using NUnit.Framework;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Framing;
using Metin2.Protocol.Registry;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketRegistryTests
    {
        [Test]
        public void HandshakeRegistry_ContainsFourSourceVerifiedEntries()
        {
            var registry = PacketRegistry.CreateHandshakeRegistry();

            Assert.AreEqual(4, registry.Count);
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_HANDSHAKE, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_KEY_AGREEMENT, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_PHASE, out _));
        }

        [Test]
        public void HandshakePacket_AllowedOnlyInHandshakePhase()
        {
            var registry = PacketRegistry.CreateHandshakeRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_HANDSHAKE, PhaseType.Handshake));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_HANDSHAKE, PhaseType.Login));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_HANDSHAKE, PhaseType.Select));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_HANDSHAKE, PhaseType.Game));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_HANDSHAKE, PhaseType.Auth));
        }

        [Test]
        public void KeyAgreementPackets_AllowedOnlyInHandshakePhase()
        {
            var registry = PacketRegistry.CreateHandshakeRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_KEY_AGREEMENT, PhaseType.Handshake));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_KEY_AGREEMENT, PhaseType.Game));

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED, PhaseType.Handshake));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED, PhaseType.Login));
        }

        [Test]
        public void PhasePacket_AllowedInEveryPhase()
        {
            var registry = PacketRegistry.CreateHandshakeRegistry();

            foreach (PhaseType phase in Enum.GetValues(typeof(PhaseType)))
            {
                Assert.IsTrue(
                    registry.IsAllowed(PacketHeaders.HEADER_GC_PHASE, phase),
                    $"HEADER_GC_PHASE must be allowed in phase {phase} (server pushes it on every SetPhase, desc.cpp:518).");
            }
        }

        [Test]
        public void UnknownHeader_DeniedInAllPhases()
        {
            var registry = PacketRegistry.CreateHandshakeRegistry();

            Assert.IsFalse(registry.TryGet(0x42, out _));
            foreach (PhaseType phase in Enum.GetValues(typeof(PhaseType)))
            {
                Assert.IsFalse(registry.IsAllowed(0x42, phase));
            }
        }

        [Test]
        public void DuplicateRegistration_ThrowsInvalidOperationException()
        {
            var registry = new PacketRegistry();
            registry.Register(new PacketDescriptor(
                0xff, "HEADER_GC_HANDSHAKE", PacketDirection.ServerToClient, 13,
                allowedPhases: new[] { PhaseType.Handshake }));

            Assert.Throws<InvalidOperationException>(() =>
            {
                registry.Register(new PacketDescriptor(
                    0xff, "DUPLICATE", PacketDirection.ServerToClient, 13,
                    allowedPhases: new[] { PhaseType.Handshake }));
            });
        }

        [Test]
        public void DescriptorValidation_RejectsInvalidMetadata()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                new PacketDescriptor(0xff, null, PacketDirection.ServerToClient, 13,
                    allowedPhases: new[] { PhaseType.Handshake });
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new PacketDescriptor(0xff, "X", PacketDirection.ServerToClient, 0,
                    allowedPhases: new[] { PhaseType.Handshake });
            });

            Assert.Throws<ArgumentException>(() =>
            {
                new PacketDescriptor(0xff, "X", PacketDirection.ServerToClient, 13);
            });
        }

        [Test]
        public void AuthRegistry_ExtendsHandshakeWithAuthReplies()
        {
            var registry = PacketRegistry.CreateAuthRegistry();

            Assert.AreEqual(6, registry.Count);
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_HANDSHAKE, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_AUTH_SUCCESS, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_LOGIN_FAILURE, out _));
        }

        [Test]
        public void AuthSuccess_AllowedOnlyInAuthPhase()
        {
            var registry = PacketRegistry.CreateAuthRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_AUTH_SUCCESS, PhaseType.Auth));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_AUTH_SUCCESS, PhaseType.Login));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_AUTH_SUCCESS, PhaseType.Game));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_AUTH_SUCCESS, PhaseType.Handshake));
        }

        [Test]
        public void LoginFailure_AllowedInAuthAndLoginPhases()
        {
            // Shared LoginFailure() helper (input.cpp:177-188) serves both phases.
            var registry = PacketRegistry.CreateAuthRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_LOGIN_FAILURE, PhaseType.Auth));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_LOGIN_FAILURE, PhaseType.Login));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_LOGIN_FAILURE, PhaseType.Game));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_LOGIN_FAILURE, PhaseType.Handshake));
        }

        [Test]
        public void ChannelRegistry_ExtendsAuthWithSelectReplies()
        {
            var registry = PacketRegistry.CreateChannelRegistry();

            Assert.AreEqual(8, registry.Count);
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_EMPIRE, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_LOGIN_SUCCESS_NEWSLOT, out _));
            // Auth entries survive.
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_AUTH_SUCCESS, PhaseType.Auth));
        }

        [Test]
        public void Empire_AllowedOnlyInLoginPhase()
        {
            // Server sends empire BEFORE SetPhase(SELECT) (input_db.cpp:157-172).
            var registry = PacketRegistry.CreateChannelRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_EMPIRE, PhaseType.Login));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_EMPIRE, PhaseType.Select));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_EMPIRE, PhaseType.Game));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_EMPIRE, PhaseType.Handshake));
        }

        [Test]
        public void LoginSuccessNewslot_AllowedOnlyInSelectPhase()
        {
            // desc.cpp:892-919 SendLoginSuccessPacket runs after PHASE_SELECT.
            var registry = PacketRegistry.CreateChannelRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_LOGIN_SUCCESS_NEWSLOT, PhaseType.Select));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_LOGIN_SUCCESS_NEWSLOT, PhaseType.Login));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_LOGIN_SUCCESS_NEWSLOT, PhaseType.Game));
        }

        [Test]
        public void SelectRegistry_ExtendsChannelWithCreateDeleteReplies()
        {
            var registry = PacketRegistry.CreateSelectRegistry();

            Assert.AreEqual(12, registry.Count);
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_CHARACTER_CREATE_SUCCESS, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_CHARACTER_CREATE_FAILURE, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_CHARACTER_DELETE_SUCCESS, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_CHARACTER_DELETE_WRONG_SOCIAL_ID, out _));
            // Channel entries survive.
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_LOGIN_SUCCESS_NEWSLOT, PhaseType.Select));
        }

        [Test]
        public void SelectReplies_AllowedOnlyInSelectPhase()
        {
            // SELECT and LOGIN share m_inputLogin (desc.cpp:539-547); the
            // create/delete replies are produced only on the select screen.
            var registry = PacketRegistry.CreateSelectRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_CREATE_SUCCESS, PhaseType.Select));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_CREATE_FAILURE, PhaseType.Select));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_DELETE_SUCCESS, PhaseType.Select));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_DELETE_WRONG_SOCIAL_ID, PhaseType.Select));

            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_CREATE_SUCCESS, PhaseType.Login));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_CREATE_SUCCESS, PhaseType.Game));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_DELETE_SUCCESS, PhaseType.Login));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_DELETE_WRONG_SOCIAL_ID, PhaseType.Handshake));
        }

        [Test]
        public void WorldEntryRegistry_ExtendsSelectWithEntryPackets()
        {
            var registry = PacketRegistry.CreateWorldEntryRegistry();

            Assert.AreEqual(15, registry.Count);
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_MAIN_CHARACTER2_EMPIRE, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_TIME, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_CHANNEL, out _));
            // Select entries survive.
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_CREATE_SUCCESS, PhaseType.Select));
        }

        [Test]
        public void MainCharacter_AllowedOnlyInLoadingPhase()
        {
            // Sent from PlayerLoad (input_db.cpp:427-428, PHASE_LOADING).
            var registry = PacketRegistry.CreateWorldEntryRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_MAIN_CHARACTER2_EMPIRE, PhaseType.Loading));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_MAIN_CHARACTER2_EMPIRE, PhaseType.Select));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_MAIN_CHARACTER2_EMPIRE, PhaseType.Game));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_MAIN_CHARACTER2_EMPIRE, PhaseType.Handshake));
        }

        [Test]
        public void TimeAndChannel_AllowedOnlyInGamePhase()
        {
            // Sent right after PHASE_GAME (input_login.cpp:579-620).
            var registry = PacketRegistry.CreateWorldEntryRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_TIME, PhaseType.Game));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_CHANNEL, PhaseType.Game));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_TIME, PhaseType.Loading));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_CHANNEL, PhaseType.Loading));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_TIME, PhaseType.Select));
        }

        [Test]
        public void GameRegistry_ExtendsWorldEntryWithStatsAndSpawn()
        {
            var registry = PacketRegistry.CreateGameRegistry();

            Assert.AreEqual(24, registry.Count);
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_CHARACTER_POINTS, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_SKILL_LEVEL, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_CHARACTER_ADD, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_CHARACTER_DEL, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_ITEM_SET, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_ITEM_DEL, out _));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_ITEM_UPDATE, out _));
            // World-entry entries survive.
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_MAIN_CHARACTER2_EMPIRE, PhaseType.Loading));
        }

        [Test]
        public void LoadingStats_AllowedOnlyInLoadingPhase()
        {
            // Sent from PlayerLoad (input_db.cpp:457-458, PHASE_LOADING).
            var registry = PacketRegistry.CreateGameRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_POINTS, PhaseType.Loading));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_SKILL_LEVEL, PhaseType.Loading));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_POINTS, PhaseType.Game));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_SKILL_LEVEL, PhaseType.Select));
        }

        [Test]
        public void SpawnPackets_AllowedOnlyInGamePhase()
        {
            // View/Show packets (char.cpp:812), client PhaseGame.cpp:253-274.
            var registry = PacketRegistry.CreateGameRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_ADD, PhaseType.Game));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_DEL, PhaseType.Game));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_ADD, PhaseType.Loading));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_CHARACTER_DEL, PhaseType.Select));
        }

        [Test]
        public void ItemPackets_AllowedInGameAndLoadingPhases()
        {
            // Loading bundle (ItemLoad, still LOADING) and live play
            // (pickup/move/equip in Game) share the same frames.
            var registry = PacketRegistry.CreateGameRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_ITEM_SET, PhaseType.Game));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_ITEM_SET, PhaseType.Loading));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_ITEM_DEL, PhaseType.Game));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_ITEM_DEL, PhaseType.Loading));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_ITEM_UPDATE, PhaseType.Game));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_ITEM_UPDATE, PhaseType.Loading));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_ITEM_SET, PhaseType.Select));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_ITEM_UPDATE, PhaseType.Handshake));
        }

        [Test]
        public void MoveAndSync_AllowedOnlyInGamePhase()
        {
            // GC_MOVE broadcast (input_main.cpp:1651-1663) and GC sync batches
            // are Game-phase view traffic. Sync descriptor length is the
            // 195-byte upper bound (dynamic wSize, framed by PacketFramer).
            var registry = PacketRegistry.CreateGameRegistry();

            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_MOVE, PhaseType.Game));
            Assert.IsTrue(registry.IsAllowed(PacketHeaders.HEADER_GC_SYNC_POSITION, PhaseType.Game));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_MOVE, PhaseType.Loading));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_SYNC_POSITION, PhaseType.Loading));
            Assert.IsFalse(registry.IsAllowed(PacketHeaders.HEADER_GC_MOVE, PhaseType.Select));
            Assert.IsTrue(registry.TryGet(PacketHeaders.HEADER_GC_SYNC_POSITION, out PacketDescriptor sync));
            Assert.AreEqual(PacketLengthTable.MaxSyncPacketSize, sync.Length);
        }
    }
}
