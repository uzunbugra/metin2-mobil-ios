using System;
using NUnit.Framework;
using Metin2.Protocol.Constants;
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
    }
}
