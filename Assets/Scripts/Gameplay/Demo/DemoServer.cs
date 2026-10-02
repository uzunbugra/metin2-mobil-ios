using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Packets;
using Metin2.Protocol.Security;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Gameplay.Demo
{
    /// <summary>
    /// In-process fake server speaking the REAL wire protocol (genuine DH2
    /// handshake + cipher on both hops, faithful frame order including the
    /// GC_PHASE pushes between step replies, keepalive pings). It exists to
    /// drive <see cref="Metin2.Gameplay.Flow.GameFlow"/> and the future Unity
    /// front-end without a live server, and doubles as the end-to-end
    /// integration harness. Server authority is simulated with scripted
    /// rules; nothing here talks to a database.
    ///
    /// Script (docs/protocol/connection-flow.md §3-5):
    /// - Auth listener: handshake → PHASE_AUTH → CG_LOGIN3 → 150 (or 7 on
    ///   wrong credentials) → close.
    /// - Game listener: handshake → PHASE_LOGIN → CG_LOGIN2 → [90][PHASE_SELECT][32]
    ///   (slot 0 = demo character, endpoint points at this listener) →
    ///   CG_SELECT → [PHASE_LOADING][113][16][76][21][21] → CG_ENTERGAME →
    ///   [PHASE_GAME][106][121][spawn][spawn][ping] → world rules:
    ///   CG_ATTACK → damage + HP delta + motion (2nd hit on a victim kills),
    ///   CG_MOVE → rebroadcast as GC_MOVE, CG_SYNC_POSITION → echoed,
    ///   CG_ITEM_MOVE → item re-set at the destination cell, CG_ITEM_USE →
    ///   +100 HP point change.
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class DemoServer : IDisposable
    {
        private TcpListener _authListener;
        private TcpListener _gameListener;
        private CancellationTokenSource _cts;
        private Task _authLoop;
        private Task _gameLoop;
        private bool _disposed;

        /// <summary>Credentials accepted by the auth script.</summary>
        public string ExpectedLogin { get; set; } = "demo";

        public string ExpectedPassword { get; set; } = "demo";

        /// <summary>Login key handed out by the auth script.</summary>
        public uint IssuedLoginKey { get; }

        /// <summary>Main character VID used in the 113 push and rebroadcasts.</summary>
        public const uint MainCharacterVid = 100;

        public const uint SpawnedMobVid = 201;
        public const uint SpawnedPlayerVid = 202;

        /// <summary>Observed client traffic, for assertions in tests.</summary>
        public int AttacksReceived { get; private set; }

        public int MovesReceived { get; private set; }
        public List<PacketCGItemMove> ItemMovesReceived { get; } = new List<PacketCGItemMove>();
        public List<PacketCGItemUse> ItemUsesReceived { get; } = new List<PacketCGItemUse>();

        public DemoServer()
        {
            IssuedLoginKey = 0x5EED_0001;
        }

        /// <summary>Starts both listeners on the loopback address.</summary>
        public (int AuthPort, int GamePort) Start()
        {
            if (_authListener != null)
            {
                throw new InvalidOperationException("DemoServer is already running.");
            }

            _cts = new CancellationTokenSource();
            _authListener = new TcpListener(IPAddress.Loopback, 0);
            _authListener.Start();
            int authPort = ((IPEndPoint)_authListener.LocalEndpoint).Port;

            _gameListener = new TcpListener(IPAddress.Loopback, 0);
            _gameListener.Start();
            int gamePort = ((IPEndPoint)_gameListener.LocalEndpoint).Port;

            _authLoop = Task.Run(() => AcceptLoopAsync(_authListener, AuthSessionAsync, _cts.Token), _cts.Token);
            _gameLoop = Task.Run(() => AcceptLoopAsync(_gameListener, client => GameSessionAsync(client, gamePort), _cts.Token), _cts.Token);

            return (authPort, gamePort);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts?.Cancel();
            _authListener?.Stop();
            _gameListener?.Stop();
            try
            {
                _authLoop?.Wait(TimeSpan.FromSeconds(2));
                _gameLoop?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // Accept loops torn down.
            }

            _cts?.Dispose();
        }

        private static async Task AcceptLoopAsync(
            TcpListener listener, Func<TcpClient, Task> session, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    break;
                }

                try
                {
                    await session(client).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // A dying demo session must not take the listener down.
                }
                finally
                {
                    client.Close();
                }
            }
        }

        private async Task AuthSessionAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            CipherSession session = await ServerHandshakeAsync(stream).ConfigureAwait(false);

            await SendEncryptedAsync(stream, session,
                PacketGCPhaseCodec.Serialize(new PacketGCPhase(PhaseType.Auth))).ConfigureAwait(false);

            byte[] loginWire = await ReadExactAsync(stream, PacketCGLogin3.PacketSize).ConfigureAwait(false);
            session.Decrypt(loginWire, 0, loginWire.Length);
            PacketCGLogin3 login = PacketCGLogin3Codec.Deserialize(loginWire);

            bool ok = login.Login == ExpectedLogin && login.Password == ExpectedPassword;
            if (!ok)
            {
                await SendEncryptedAsync(stream, session,
                    PacketGCLoginFailureCodec.Serialize(new PacketGCLoginFailure("WRONGPWD"))).ConfigureAwait(false);
                return;
            }

            await SendEncryptedAsync(stream, session,
                PacketGCAuthSuccessCodec.Serialize(new PacketGCAuthSuccess(IssuedLoginKey, 1))).ConfigureAwait(false);
        }

        private async Task GameSessionAsync(TcpClient client, int gamePort)
        {
            NetworkStream stream = client.GetStream();
            CipherSession session = await ServerHandshakeAsync(stream).ConfigureAwait(false);

            // Channel login: PHASE_LOGIN, CG_LOGIN2, [90][PHASE_SELECT][32].
            await SendEncryptedAsync(stream, session,
                PacketGCPhaseCodec.Serialize(new PacketGCPhase(PhaseType.Login))).ConfigureAwait(false);

            byte[] login2Wire = await ReadExactAsync(stream, PacketCGLogin2.PacketSize).ConfigureAwait(false);
            session.Decrypt(login2Wire, 0, login2Wire.Length);
            PacketCGLogin2 login2 = PacketCGLogin2Codec.Deserialize(login2Wire);
            if (login2.LoginKey != IssuedLoginKey)
            {
                await SendEncryptedAsync(stream, session,
                    PacketGCLoginFailureCodec.Serialize(new PacketGCLoginFailure("NOID"))).ConfigureAwait(false);
                return;
            }

            var slot0 = new SimplePlayer
            {
                Id = 77,
                Name = "Democius",
                Job = 0,
                Level = 42,
                St = 40,
                Ht = 20,
                Dx = 30,
                Iq = 10,
                MainPart = 1001,
                ChangeName = 0,
                HairPart = 0,
                X = 959900,
                Y = 269500,
                // 127.0.0.1 as inet_addr read little-endian: bytes 7F 00 00 01.
                AddrNetworkOrder = 0x0100007Fu,
                Port = (ushort)gamePort,
                SkillGroup = 0
            };

            await SendEncryptedAsync(stream, session, PacketGCEmpireCodec.Serialize(new PacketGCEmpire(1))).ConfigureAwait(false);
            await SendEncryptedAsync(stream, session,
                PacketGCPhaseCodec.Serialize(new PacketGCPhase(PhaseType.Select))).ConfigureAwait(false);
            await SendEncryptedAsync(stream, session, PacketGCLoginSuccessCodec.Serialize(
                new PacketGCLoginSuccess(
                    new[] { slot0, default, default, default },
                    new uint[] { 0, 0, 0, 0 },
                    new[] { string.Empty, string.Empty, string.Empty, string.Empty },
                    handle: 55,
                    randomKey: 777))).ConfigureAwait(false);

            // Character select: [PHASE_LOADING][113][16][76][21][21].
            byte[] selectWire = await ReadExactAsync(stream, PacketCGCharacterSelect.PacketSize).ConfigureAwait(false);
            session.Decrypt(selectWire, 0, selectWire.Length);
            PacketCGCharacterSelect select = PacketCGCharacterSelectCodec.Deserialize(selectWire);
            if (select.Index != 0)
            {
                return; // demo account owns exactly one character
            }

            await SendEncryptedAsync(stream, session,
                PacketGCPhaseCodec.Serialize(new PacketGCPhase(PhaseType.Loading))).ConfigureAwait(false);
            await SendEncryptedAsync(stream, session, PacketGCMainCharacterCodec.Serialize(
                new PacketGCMainCharacter(
                    vid: MainCharacterVid, race: 0, name: "Democius",
                    x: 959900, y: 269500, z: 0, empire: 1, skillGroup: 0))).ConfigureAwait(false);

            var points = new int[PacketGCPoints.PointCount];
            points[PointTypes.Level] = 42;
            points[PointTypes.Hp] = 1000;
            points[PointTypes.MaxHp] = 1000;
            points[PointTypes.Sp] = 500;
            points[PointTypes.MaxSp] = 500;
            points[PointTypes.St] = 40;
            points[PointTypes.Ht] = 20;
            points[PointTypes.Dx] = 30;
            points[PointTypes.Iq] = 10;
            await SendEncryptedAsync(stream, session,
                PacketGCPointsCodec.Serialize(new PacketGCPoints(points))).ConfigureAwait(false);
            await SendEncryptedAsync(stream, session,
                PacketGCSkillLevelCodec.Serialize(new PacketGCSkillLevel(Array.Empty<PlayerSkill>()))).ConfigureAwait(false);

            await SendEncryptedAsync(stream, session, PacketGCItemSetCodec.Serialize(
                new PacketGCItemSet(1, 0, 19, 200, 0, 0, false, null, null))).ConfigureAwait(false);
            await SendEncryptedAsync(stream, session, PacketGCItemSetCodec.Serialize(
                new PacketGCItemSet(1, 1, 11243, 1, 4, 0x100, false,
                    new[] { 28448, 0, 0 },
                    new[] { new ItemAttribute { Type = 1, Value = 15 } }))).ConfigureAwait(false);

            // Enter game: [PHASE_GAME][106][121][spawn][spawn][ping].
            byte[] enterWire = await ReadExactAsync(stream, PacketCGEnterGame.PacketSize).ConfigureAwait(false);
            session.Decrypt(enterWire, 0, enterWire.Length);
            PacketCGEnterGameCodec.Deserialize(enterWire); // header validation only

            await SendEncryptedAsync(stream, session,
                PacketGCPhaseCodec.Serialize(new PacketGCPhase(PhaseType.Game))).ConfigureAwait(false);
            await SendEncryptedAsync(stream, session,
                PacketGCTimeCodec.Serialize(new PacketGCTime(1700000000))).ConfigureAwait(false);
            await SendEncryptedAsync(stream, session,
                PacketGCChannelCodec.Serialize(new PacketGCChannel(1))).ConfigureAwait(false);

            await SendEncryptedAsync(stream, session, PacketGCCharacterAddCodec.Serialize(
                new PacketGCCharacterAdd
                {
                    Vid = SpawnedMobVid, Angle = 1.25f, X = 960500, Y = 270000, Z = 0,
                    Type = 0, Race = 101, MovingSpeed = 100, AttackSpeed = 100,
                    StateFlag = 0, AffectFlag0 = 0, AffectFlag1 = 0
                })).ConfigureAwait(false);
            await SendEncryptedAsync(stream, session, PacketGCCharacterAddCodec.Serialize(
                new PacketGCCharacterAdd
                {
                    Vid = SpawnedPlayerVid, Angle = 3.5f, X = 959300, Y = 269100, Z = 0,
                    Type = 0, Race = 0, MovingSpeed = 150, AttackSpeed = 125,
                    StateFlag = 0, AffectFlag0 = 0, AffectFlag1 = 0
                })).ConfigureAwait(false);
            await SendEncryptedAsync(stream, session,
                PacketGCPingCodec.Serialize(new PacketGCPing())).ConfigureAwait(false);

            await WorldLoopAsync(stream, session).ConfigureAwait(false);
        }

        /// <summary>
        /// Scripted world rules after entry. Every reply keeps the server's
        /// authority: the client only ever receives server-computed results.
        /// </summary>
        private async Task WorldLoopAsync(NetworkStream stream, CipherSession session)
        {
            var victimHitCounts = new Dictionary<uint, int>();
            var carriedItems = new Dictionary<ushort, (uint Vnum, byte Count)>();
            carriedItems[0] = (19, 200);
            carriedItems[1] = (11243, 1);

            while (true)
            {
                byte[] headerByte = await ReadExactAsync(stream, 1).ConfigureAwait(false);
                session.Decrypt(headerByte, 0, 1);
                byte header = headerByte[0];

                switch (header)
                {
                    case PacketCGAttack.PacketHeader:
                    {
                        byte[] rest = await ReadExactAsync(stream, PacketCGAttack.PacketSize - 1).ConfigureAwait(false);
                        session.Decrypt(rest, 0, rest.Length);
                        byte[] whole = Combine(headerByte, rest);
                        PacketCGAttack attack = PacketCGAttackCodec.Deserialize(whole);
                        AttacksReceived++;

                        victimHitCounts.TryGetValue(attack.VictimVid, out int hits);
                        hits++;
                        victimHitCounts[attack.VictimVid] = hits;

                        await SendEncryptedAsync(stream, session, PacketGCDamageInfoCodec.Serialize(
                            new PacketGCDamageInfo(attack.VictimVid, 1, 150))).ConfigureAwait(false);
                        await SendEncryptedAsync(stream, session, PacketGCPointChangeCodec.Serialize(
                            new PacketGCPointChange(attack.VictimVid, PointTypes.Hp, -150, 850))).ConfigureAwait(false);
                        await SendEncryptedAsync(stream, session, PacketGCMotionCodec.Serialize(
                            new PacketGCMotion(MainCharacterVid, attack.VictimVid, 3))).ConfigureAwait(false);

                        if (hits >= 2)
                        {
                            await SendEncryptedAsync(stream, session, PacketGCDeadCodec.Serialize(
                                new PacketGCDead(attack.VictimVid))).ConfigureAwait(false);
                        }

                        break;
                    }

                    case PacketCGMove.PacketHeader:
                    {
                        byte[] rest = await ReadEncryptedExactAsync(stream, session, PacketCGMove.PacketSize - 1).ConfigureAwait(false);
                        PacketCGMove move = PacketCGMoveCodec.Deserialize(Combine(headerByte, rest));
                        MovesReceived++;

                        await SendEncryptedAsync(stream, session, PacketGCMoveCodec.Serialize(new PacketGCMove
                        {
                            Func = move.Func,
                            Arg = move.Arg,
                            Rot = move.Rot,
                            Vid = MainCharacterVid,
                            X = move.X,
                            Y = move.Y,
                            Time = move.Time,
                            Duration = 0
                        })).ConfigureAwait(false);
                        break;
                    }

                    case PacketCGSyncPosition.PacketHeader:
                    {
                        byte[] sizeWire = await ReadEncryptedExactAsync(stream, session, PacketCGSyncPosition.HeaderSize - 1).ConfigureAwait(false);
                        int declared = sizeWire[1] | (sizeWire[2] << 8);
                        if (declared < PacketCGSyncPosition.HeaderSize)
                        {
                            return;
                        }

                        byte[] rest = await ReadEncryptedExactAsync(stream, session, declared - PacketCGSyncPosition.HeaderSize).ConfigureAwait(false);
                        var elements = new List<SyncPositionElement>();
                        for (int offset = 0; offset + SyncPositionElement.FieldSize <= rest.Length; offset += SyncPositionElement.FieldSize)
                        {
                            elements.Add(SyncPositionElementCodec.Deserialize(
                                new ReadOnlySpan<byte>(rest, offset, SyncPositionElement.FieldSize)));
                        }

                        await SendEncryptedAsync(stream, session, PacketGCSyncPositionCodec.Serialize(
                            new PacketGCSyncPosition(elements.ToArray()))).ConfigureAwait(false);
                        break;
                    }

                    case PacketCGItemMove.PacketHeader:
                    {
                        byte[] rest = await ReadEncryptedExactAsync(stream, session, PacketCGItemMove.PacketSize - 1).ConfigureAwait(false);
                        PacketCGItemMove move = PacketCGItemMoveCodec.Deserialize(Combine(headerByte, rest));
                        ItemMovesReceived.Add(move);

                        if (carriedItems.TryGetValue(move.Cell, out (uint Vnum, byte Count) item))
                        {
                            carriedItems.Remove(move.Cell);
                            carriedItems[move.CellTo] = item;
                            await SendEncryptedAsync(stream, session, PacketGCItemDelCodec.Serialize(
                                new PacketGCItemDel(move.Window, move.Cell, 0, 0, null, null))).ConfigureAwait(false);
                            await SendEncryptedAsync(stream, session, PacketGCItemSetCodec.Serialize(
                                new PacketGCItemSet(move.WindowTo, move.CellTo, item.Vnum, item.Count, 0, 0, false, null, null))).ConfigureAwait(false);
                        }

                        break;
                    }

                    case PacketCGItemUse.PacketHeader:
                    {
                        byte[] rest = await ReadEncryptedExactAsync(stream, session, PacketCGItemUse.PacketSize - 1).ConfigureAwait(false);
                        PacketCGItemUse use = PacketCGItemUseCodec.Deserialize(Combine(headerByte, rest));
                        ItemUsesReceived.Add(use);

                        // Potion rule: red potion restores HP.
                        await SendEncryptedAsync(stream, session, PacketGCPointChangeCodec.Serialize(
                            new PacketGCPointChange(MainCharacterVid, PointTypes.Hp, 100, 1100))).ConfigureAwait(false);
                        break;
                    }

                    case PacketCGPong.PacketHeader:
                    {
                        // Keepalive answer to our own GC_PING — header-only,
                        // nothing to read. The keepalive path is exercised
                        // end-to-end precisely here.
                        break;
                    }

                    default:
                    {
                        // Unknown/other CG traffic (drops, pickups, pongs...):
                        // consume one fixed-size frame when known, else bail.
                        if (TryGetKnownClientBodySize(header, out int bodySize))
                        {
                            await ReadEncryptedExactAsync(stream, session, bodySize).ConfigureAwait(false);
                        }
                        else
                        {
                            return;
                        }

                        break;
                    }
                }
            }
        }

        private static bool TryGetKnownClientBodySize(byte header, out int bodySize)
        {
            switch (header)
            {
                case PacketCGItemDrop.PacketHeader: bodySize = PacketCGItemDrop.PacketSize - 1; return true;
                case PacketCGItemDrop2.PacketHeader: bodySize = PacketCGItemDrop2.PacketSize - 1; return true;
                case PacketCGItemPickup.PacketHeader: bodySize = PacketCGItemPickup.PacketSize - 1; return true;
                case PacketCGItemUseToItem.PacketHeader: bodySize = PacketCGItemUseToItem.PacketSize - 1; return true;
                case PacketCGItemUse.PacketHeader: bodySize = PacketCGItemUse.PacketSize - 1; return true;
                default: bodySize = 0; return false;
            }
        }

        /// <summary>
        /// Server side of the DH2 handshake (mirrors desc.cpp:615-640 /
        /// input.cpp:556-583): GC_HANDSHAKE → GC_KEY_AGREEMENT → read
        /// CG_KEY_AGREEMENT → agree + derive → GC_KEY_AGREEMENT_COMPLETED →
        /// activate with server polarity (false).
        /// </summary>
        private static async Task<CipherSession> ServerHandshakeAsync(NetworkStream stream)
        {
            using var agreement = Dh2KeyAgreement.Generate();
            byte[] pub = agreement.ExportPublicData();

            byte[] hs = PacketGCHandshakeCodec.Serialize(new PacketGCHandshake(1, 2, 3));
            await stream.WriteAsync(hs, 0, hs.Length).ConfigureAwait(false);

            byte[] ka = PacketKeyAgreementCodec.Serialize(
                new PacketKeyAgreement(DiffieHellmanGroup.AgreedValueLength, DiffieHellmanGroup.KeyDataLength, pub));
            await stream.WriteAsync(ka, 0, ka.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            byte[] reply = await ReadExactAsync(stream, PacketKeyAgreement.PacketSize).ConfigureAwait(false);
            if (!PacketKeyAgreementCodec.TryDeserialize(reply, out PacketKeyAgreement clientKa, out string _)
                || !agreement.TryAgree(clientKa.AgreedLength, clientKa.Data, out byte[] shared)
                || !CipherKeyDerivation.TryDerive(shared, out CipherKeyMaterial material))
            {
                throw new InvalidOperationException("Demo handshake failed.");
            }

            Array.Clear(shared, 0, shared.Length);
            var session = new CipherSession(false, material, BlockCipherEngineFactory.ForSession());
            byte[] done = new byte[] { PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED, 0, 0, 0 };
            await stream.WriteAsync(done, 0, done.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            session.SetActivated(true);
            return session;
        }

        private static async Task SendEncryptedAsync(
            NetworkStream stream, CipherSession session, byte[] plaintext)
        {
            byte[] wire = (byte[])plaintext.Clone();
            session.Encrypt(wire, 0, wire.Length);
            await stream.WriteAsync(wire, 0, wire.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        private static async Task<byte[]> ReadEncryptedExactAsync(
            NetworkStream stream, CipherSession session, int length)
        {
            byte[] buffer = await ReadExactAsync(stream, length).ConfigureAwait(false);
            session.Decrypt(buffer, 0, buffer.Length);
            return buffer;
        }

        private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[length];
            int total = 0;
            while (total < length)
            {
                int read = await stream.ReadAsync(buffer, total, length - total).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new InvalidOperationException("Peer closed before all bytes arrived.");
                }

                total += read;
            }

            return buffer;
        }

        private static byte[] Combine(byte[] first, byte[] second)
        {
            byte[] result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }
    }
}
