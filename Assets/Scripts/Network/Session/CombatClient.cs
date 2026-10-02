using System;
using System.Threading;
using System.Threading.Tasks;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;
using Metin2.Protocol.Registry;

namespace Metin2.Network.Session
{
    /// <summary>
    /// One combat event received from the server.
    /// </summary>
    public readonly struct CombatEvent
    {
        public enum Kind
        {
            PointChanged,
            Stunned,
            Dead,
            Motion,
            DamageInfo
        }

        public Kind EventKind { get; }
        public PacketGCPointChange PointChange { get; }
        public PacketGCStun Stun { get; }
        public PacketGCDead Dead { get; }
        public PacketGCMotion Motion { get; }
        public PacketGCDamageInfo Damage { get; }

        private CombatEvent(Kind kind, PacketGCPointChange pointChange, PacketGCStun stun,
            PacketGCDead dead, PacketGCMotion motion, PacketGCDamageInfo damage)
        {
            EventKind = kind;
            PointChange = pointChange;
            Stun = stun;
            Dead = dead;
            Motion = motion;
            Damage = damage;
        }

        public static CombatEvent FromPointChange(PacketGCPointChange pointChange)
        {
            return new CombatEvent(Kind.PointChanged, pointChange, default, default, default, default);
        }

        public static CombatEvent FromStun(PacketGCStun stun)
        {
            return new CombatEvent(Kind.Stunned, default, stun, default, default, default);
        }

        public static CombatEvent FromDead(PacketGCDead dead)
        {
            return new CombatEvent(Kind.Dead, default, default, dead, default, default);
        }

        public static CombatEvent FromMotion(PacketGCMotion motion)
        {
            return new CombatEvent(Kind.Motion, default, default, default, motion, default);
        }

        public static CombatEvent FromDamageInfo(PacketGCDamageInfo damage)
        {
            return new CombatEvent(Kind.DamageInfo, default, default, default, default, damage);
        }
    }

    /// <summary>
    /// Combat client over an already-handshaked secure channel.
    /// Wire frames (docs/protocol/connection-flow.md §6):
    /// - CG_ATTACK (2, 8B): attack intent — type 0 is a normal attack, type &gt; 0
    ///   a skill id (server CInputMain::Attack, input_main.cpp:1690-1770; the
    ///   victim must be attackable — NPC/WARP/GOTO/self are rejected server-side,
    ///   and the attack RATE is server-enforced via IS_SPEED_HACK, guide §6.1).
    /// - GC_POINT_CHANGE (17, 17B): point delta — own HP/SP/exp/gold changes and
    ///   broadcast stat changes (char.cpp:3595-3613). Valid in Select/Loading/Game.
    /// - GC_STUN (13, 5B) / GC_DEAD (14, 5B): stun/death events, broadcast around
    ///   (char_battle.cpp:429-432 / 1468-1471). Game-only.
    /// - GC_MOTION (36, 11B): attack/animation broadcast for other characters
    ///   (char.cpp:3773-3778). Game-only.
    /// - GC_DAMAGE_INFO (135, 10B): damage number to the victim and attacker
    ///   descriptors only (char_battle.cpp:1584-1605). Game-only.
    ///
    /// Client never decides damage or death — every outcome arrives as a
    /// server packet (guide §2.2/§8.3).
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class CombatClient
    {
        private readonly HandshakeClient _handshake;
        private readonly PacketRegistry _registry;

        public CombatClient(HandshakeClient handshake, PacketRegistry registry = null)
        {
            if (handshake == null)
            {
                throw new ArgumentNullException(nameof(handshake));
            }

            if (!handshake.Completed || handshake.Session == null)
            {
                throw new HandshakeFailedException("Handshake must be completed before combat traffic.");
            }

            _handshake = handshake;
            _registry = registry ?? PacketRegistry.CreateGameRegistry();
        }

        /// <summary>
        /// Sends one CG_ATTACK (encrypted). type 0 = normal attack, type &gt; 0 =
        /// skill id. The server re-validates range, speed and target — sending
        /// is intent, not outcome.
        /// </summary>
        public async Task SendAttackAsync(
            uint victimVid, byte type = 0, CancellationToken cancellationToken = default)
        {
            if (victimVid == 0)
            {
                throw new ArgumentException("Victim VID must not be zero.", nameof(victimVid));
            }

            var packet = new PacketCGAttack(type, victimVid);
            byte[] wire = PacketCGAttackCodec.Serialize(packet);
            try
            {
                await _handshake.SendSecureAsync(wire, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                throw new HandshakeFailedException("Failed to send CG_ATTACK packet.", ex);
            }
            finally
            {
                Array.Clear(wire, 0, wire.Length);
            }
        }

        /// <summary>
        /// Receives one combat event. phase selects the caller's current phase;
        /// per-header legality is enforced by the registry (point-change is the
        /// only event also valid in Select/Loading).
        /// </summary>
        public async Task<CombatEvent> ReceiveEventAsync(
            PhaseType phase, CancellationToken cancellationToken = default)
        {
            if (phase != PhaseType.Select && phase != PhaseType.Loading && phase != PhaseType.Game)
            {
                throw new ArgumentException("Combat frames arrive in Select, Loading or Game phase.", nameof(phase));
            }

            byte[] frame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            byte header = frame[0];

            if (!_registry.IsAllowed(header, phase))
            {
                throw new HandshakeFailedException(
                    $"Combat header 0x{header:X2} is not valid in the {phase} phase.");
            }

            if (header == PacketGCPointChange.PacketHeader)
            {
                if (!PacketGCPointChangeCodec.TryDeserialize(frame, out PacketGCPointChange pointChange, out string error))
                {
                    throw new HandshakeFailedException($"Invalid point-change packet: {error}");
                }

                return CombatEvent.FromPointChange(pointChange);
            }

            if (header == PacketGCStun.PacketHeader)
            {
                if (!PacketGCStunCodec.TryDeserialize(frame, out PacketGCStun stun, out string error))
                {
                    throw new HandshakeFailedException($"Invalid stun packet: {error}");
                }

                return CombatEvent.FromStun(stun);
            }

            if (header == PacketGCDead.PacketHeader)
            {
                if (!PacketGCDeadCodec.TryDeserialize(frame, out PacketGCDead dead, out string error))
                {
                    throw new HandshakeFailedException($"Invalid dead packet: {error}");
                }

                return CombatEvent.FromDead(dead);
            }

            if (header == PacketGCMotion.PacketHeader)
            {
                if (!PacketGCMotionCodec.TryDeserialize(frame, out PacketGCMotion motion, out string error))
                {
                    throw new HandshakeFailedException($"Invalid motion packet: {error}");
                }

                return CombatEvent.FromMotion(motion);
            }

            if (header == PacketGCDamageInfo.PacketHeader)
            {
                if (!PacketGCDamageInfoCodec.TryDeserialize(frame, out PacketGCDamageInfo damage, out string error))
                {
                    throw new HandshakeFailedException($"Invalid damage-info packet: {error}");
                }

                return CombatEvent.FromDamageInfo(damage);
            }

            throw new HandshakeFailedException($"Unexpected combat header 0x{header:X2}.");
        }

        private async Task<byte[]> ReceiveFrameAsync(CancellationToken cancellationToken)
        {
            byte[] frame;
            try
            {
                frame = await _handshake.ReceiveSecureFrameAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is HandshakeFailedException))
            {
                throw new HandshakeFailedException("Failed to receive combat packet.", ex);
            }

            if (frame == null || frame.Length == 0)
            {
                throw new HandshakeFailedException("Empty combat frame.");
            }

            return frame;
        }
    }
}
