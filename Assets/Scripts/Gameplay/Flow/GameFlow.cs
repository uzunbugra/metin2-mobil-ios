using System;
using System.Threading;
using System.Threading.Tasks;
using Metin2.Network.Session;
using Metin2.Network.Transport;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;
using Metin2.Protocol.Registry;

namespace Metin2.Gameplay.Flow
{
    /// <summary>
    /// High-level connection lifecycle, mirroring the C++ client's phase hops
    /// (docs/protocol/connection-flow.md §3-5). Engine-independent: the Unity
    /// layer drives this from MonoBehaviours and binds the events to the UI
    /// (marshalling to the main thread is the subscriber's responsibility).
    ///
    /// Standard path (single game-core connection after the auth hop):
    ///   1. LoginAsync        — auth core: handshake + CG_LOGIN3 -> 150 (login key)
    ///   2. ConnectChannelAsync — game core: handshake + CG_LOGIN2 -> [90][PHASE(Select)][32]
    ///   3. SelectCharacterAsync — CG_SELECT -> [PHASE(Loading)][113][16][76][items]
    ///   4. EnterWorldAsync   — CG_ENTERGAME -> [PHASE(Game)][106][121]
    ///   5. RunEventPumpAsync — game-phase dispatcher (spawn/items/combat/move/sync)
    ///
    /// The slot-endpoint direct-enter path (ConnectGameServer,
    /// PythonNetworkStream.cpp:462-473) is a separate reconnect flow and is
    /// deliberately NOT implemented here yet.
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class GameFlow : IDisposable
    {
        private TcpConnection _authConnection;
        private HandshakeClient _authHandshake;

        private TcpConnection _gameConnection;
        private HandshakeClient _gameHandshake;
        private PacketRegistry _gameRegistry;

        private bool _disposed;

        public GameFlowState State { get; private set; }
        public PhaseType? ServerPhase { get; private set; }

        /// <summary>Login key issued by the auth core (150, bResult 1).</summary>
        public uint LoginKey { get; private set; }

        /// <summary>Character slots received from the channel login (32).</summary>
        public ChannelSelectData? Slots { get; private set; }

        /// <summary>Main character pushed by PlayerLoad (113).</summary>
        public PacketGCMainCharacter MainCharacter { get; private set; }

        /// <summary>Loading-bundle stats (16 points + 76 skills).</summary>
        public LoadingStatsData Stats { get; private set; }

        /// <summary>Game entry data (time + channel) after ENTERGAME.</summary>
        public WorldEntryData? WorldEntry { get; private set; }

        /// <summary>Step clients on the game connection, exposed for sends.</summary>
        public CharacterSelectClient Select { get; private set; }
        public CombatClient Combat { get; private set; }
        public InventoryClient Inventory { get; private set; }
        public MovementClient Movement { get; private set; }

        /// <summary>Raised on every flow-state transition (from the calling thread).</summary>
        public event Action<GameFlowState> StateChanged;

        /// <summary>
        /// Raised on every server GC_PHASE push (from the receive thread —
        /// Unity subscribers must marshal to the main thread).
        /// </summary>
        public event Action<PhaseType> ServerPhaseChanged;

        // Typed game events (raised from the pump thread).
        public event Action<PacketGCCharacterAdd> EntitySpawned;
        public event Action<uint> EntityDespawned;
        public event Action<ItemEvent> ItemChanged;
        public event Action<CombatEvent> CombatEventReceived;
        public event Action<PacketGCMove> EntityMoved;
        public event Action<PacketGCSyncPosition> PositionsSynced;

        /// <summary>
        /// Auth-core hop: connect, handshake, CG_LOGIN3, wait for 150/7.
        /// On success the auth connection is closed and the login key is kept
        /// for the channel hop (the C++ client does the same — the auth core
        /// is only used to obtain the key).
        /// </summary>
        public async Task<AuthLoginResult> LoginAsync(
            string host, int port, string login, string password, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (State != GameFlowState.Idle)
            {
                throw new InvalidOperationException("LoginAsync requires the Idle state.");
            }

            _authConnection = new TcpConnection();
            await _authConnection.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);

            _authHandshake = new HandshakeClient(_authConnection);
            _authHandshake.PhaseChanged += OnServerPhase;
            await _authHandshake.RunAsync(cancellationToken).ConfigureAwait(false);

            var auth = new AuthLoginClient(_authHandshake);
            uint[] clientKeys = AuthLoginClient.GenerateClientKeys();
            await auth.SendLoginAsync(login, password, clientKeys, cancellationToken).ConfigureAwait(false);
            AuthLoginResult result = await auth.ReceiveResultAsync(cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                _authConnection.Dispose();
                _authConnection = null;
                SetState(GameFlowState.Idle);
                return result;
            }

            LoginKey = result.LoginKey;
            _authConnection.Dispose();
            _authConnection = null;
            SetState(GameFlowState.LoggedIn);
            return result;
        }

        /// <summary>
        /// Game-core hop: connect, handshake, CG_LOGIN2 with the auth key,
        /// receive [90 empire][GC_PHASE(Select)][32 slots] (the interleaved
        /// phase push is consumed transparently by the handshake client).
        /// </summary>
        public async Task<ChannelSelectData> ConnectChannelAsync(
            string host, int port, string login, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (State != GameFlowState.LoggedIn)
            {
                throw new InvalidOperationException("ConnectChannelAsync requires the LoggedIn state.");
            }

            _gameConnection = new TcpConnection();
            await _gameConnection.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);

            _gameHandshake = new HandshakeClient(_gameConnection);
            _gameHandshake.PhaseChanged += OnServerPhase;
            await _gameHandshake.RunAsync(cancellationToken).ConfigureAwait(false);

            var channel = new ChannelLoginClient(_gameHandshake);
            await channel.SendChannelLoginAsync(
                login, LoginKey, AuthLoginClient.GenerateClientKeys(), cancellationToken).ConfigureAwait(false);

            ChannelLoginResult result = await channel.ReceiveSelectDataAsync(cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new HandshakeFailedException($"Channel login failed: {result.Status}");
            }

            Slots = result.Data;

            Select = new CharacterSelectClient(_gameHandshake);
            Combat = new CombatClient(_gameHandshake);
            Inventory = new InventoryClient(_gameHandshake);
            Movement = new MovementClient(_gameHandshake);
            _gameRegistry = PacketRegistry.CreateGameRegistry();

            SetState(GameFlowState.ChannelReady);
            return result.Data;
        }

        /// <summary>
        /// Selects a character and consumes the loading bundle:
        /// CG_SELECT -> [PHASE(Loading)][113][16][76] then drains
        /// <paramref name="expectedInitialItems"/> item events (raised via
        /// <see cref="ItemChanged"/>). The explicit count mirrors the C++
        /// client, which simply consumes frames until its loading screen is
        /// ready — the wire has no loading-complete terminator.
        /// </summary>
        public async Task<PacketGCMainCharacter> SelectCharacterAsync(
            byte slot, int expectedInitialItems, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (State != GameFlowState.ChannelReady)
            {
                throw new InvalidOperationException("SelectCharacterAsync requires the ChannelReady state.");
            }

            await Select.SendSelectAsync(slot, cancellationToken).ConfigureAwait(false);

            var worldEntry = new WorldEntryClient(_gameHandshake);
            MainCharacter = await worldEntry.ReceiveMainCharacterAsync(cancellationToken).ConfigureAwait(false);
            Stats = await new GameWorldClient(_gameHandshake).ReceiveLoadingStatsAsync(cancellationToken).ConfigureAwait(false);

            for (int i = 0; i < expectedInitialItems; i++)
            {
                ItemEvent item = await Inventory.ReceiveItemAsync(PhaseType.Loading, cancellationToken).ConfigureAwait(false);
                ItemChanged?.Invoke(item);
            }

            SetState(GameFlowState.CharacterReady);
            return MainCharacter;
        }

        /// <summary>
        /// Enters the world: CG_ENTERGAME -> [PHASE(Game)][106 time][121 channel].
        /// </summary>
        public async Task<WorldEntryData> EnterWorldAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (State != GameFlowState.CharacterReady)
            {
                throw new InvalidOperationException("EnterWorldAsync requires the CharacterReady state.");
            }

            await Select.SendEnterGameAsync(cancellationToken).ConfigureAwait(false);

            var worldEntry = new WorldEntryClient(_gameHandshake);
            WorldEntryData data = await worldEntry.ReceiveGameEntryAsync(MainCharacter, cancellationToken).ConfigureAwait(false);
            WorldEntry = data;

            SetState(GameFlowState.InWorld);
            return data;
        }

        /// <summary>
        /// Game-phase event pump: reads frames and dispatches them to the typed
        /// events, mirroring the C++ PhaseGame dispatch loop. Unknown or
        /// wrong-phase headers fail closed (guide §5.3). Runs until the
        /// connection closes, an invalid frame arrives, or the token fires.
        /// </summary>
        public async Task RunEventPumpAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (State != GameFlowState.InWorld)
            {
                throw new InvalidOperationException("RunEventPumpAsync requires the InWorld state.");
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] frame;
                try
                {
                    frame = await _gameHandshake.ReceiveSecureFrameAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (!(ex is HandshakeFailedException))
                {
                    throw new HandshakeFailedException("Game event pump receive failed.", ex);
                }

                byte header = frame.Length > 0 ? frame[0] : (byte)0;
                if (!_gameRegistry.IsAllowed(header, PhaseType.Game))
                {
                    throw new HandshakeFailedException(
                        $"Game event header 0x{header:X2} is not valid in the Game phase.");
                }

                DispatchGameFrame(header, frame);
            }
        }

        private void DispatchGameFrame(byte header, byte[] frame)
        {
            switch (header)
            {
                case PacketGCCharacterAdd.PacketHeader:
                    EntitySpawned?.Invoke(PacketGCCharacterAddCodec.Deserialize(frame));
                    break;
                case PacketGCCharacterDelete.PacketHeader:
                    EntityDespawned?.Invoke(PacketGCCharacterDeleteCodec.Deserialize(frame).Vid);
                    break;
                case PacketGCItemSet.PacketHeader:
                case PacketGCItemDel.PacketHeader:
                case PacketGCItemUpdate.PacketHeader:
                    ItemChanged?.Invoke(DeserializeItemEvent(frame));
                    break;
                case PacketGCPointChange.PacketHeader:
                case PacketGCStun.PacketHeader:
                case PacketGCDead.PacketHeader:
                case PacketGCMotion.PacketHeader:
                case PacketGCDamageInfo.PacketHeader:
                    CombatEventReceived?.Invoke(DeserializeCombatEvent(frame));
                    break;
                case PacketGCMove.PacketHeader:
                    EntityMoved?.Invoke(PacketGCMoveCodec.Deserialize(frame));
                    break;
                case PacketGCSyncPosition.PacketHeader:
                    PositionsSynced?.Invoke(PacketGCSyncPositionCodec.Deserialize(frame));
                    break;
                default:
                    throw new HandshakeFailedException($"Unhandled game event header 0x{header:X2}.");
            }
        }

        private static ItemEvent DeserializeItemEvent(byte[] frame)
        {
            switch (frame[0])
            {
                case PacketGCItemSet.PacketHeader:
                    if (!PacketGCItemSetCodec.TryDeserialize(frame, out PacketGCItemSet set, out string setError))
                    {
                        throw new HandshakeFailedException($"Invalid item-set packet: {setError}");
                    }
                    return ItemEvent.Assigned(set);
                case PacketGCItemDel.PacketHeader:
                    if (!PacketGCItemDelCodec.TryDeserialize(frame, out PacketGCItemDel del, out string delError))
                    {
                        throw new HandshakeFailedException($"Invalid item-del packet: {delError}");
                    }
                    return ItemEvent.Cleared(del.Window, del.Cell);
                default:
                    if (!PacketGCItemUpdateCodec.TryDeserialize(frame, out PacketGCItemUpdate update, out string updateError))
                    {
                        throw new HandshakeFailedException($"Invalid item-update packet: {updateError}");
                    }
                    return ItemEvent.Mutated(update);
            }
        }

        private static CombatEvent DeserializeCombatEvent(byte[] frame)
        {
            switch (frame[0])
            {
                case PacketGCPointChange.PacketHeader:
                    if (!PacketGCPointChangeCodec.TryDeserialize(frame, out PacketGCPointChange pointChange, out string pcError))
                    {
                        throw new HandshakeFailedException($"Invalid point-change packet: {pcError}");
                    }
                    return CombatEvent.FromPointChange(pointChange);
                case PacketGCStun.PacketHeader:
                    if (!PacketGCStunCodec.TryDeserialize(frame, out PacketGCStun stun, out string stunError))
                    {
                        throw new HandshakeFailedException($"Invalid stun packet: {stunError}");
                    }
                    return CombatEvent.FromStun(stun);
                case PacketGCDead.PacketHeader:
                    if (!PacketGCDeadCodec.TryDeserialize(frame, out PacketGCDead dead, out string deadError))
                    {
                        throw new HandshakeFailedException($"Invalid dead packet: {deadError}");
                    }
                    return CombatEvent.FromDead(dead);
                case PacketGCMotion.PacketHeader:
                    if (!PacketGCMotionCodec.TryDeserialize(frame, out PacketGCMotion motion, out string motionError))
                    {
                        throw new HandshakeFailedException($"Invalid motion packet: {motionError}");
                    }
                    return CombatEvent.FromMotion(motion);
                default:
                    if (!PacketGCDamageInfoCodec.TryDeserialize(frame, out PacketGCDamageInfo damage, out string damageError))
                    {
                        throw new HandshakeFailedException($"Invalid damage-info packet: {damageError}");
                    }
                    return CombatEvent.FromDamageInfo(damage);
            }
        }

        private void OnServerPhase(PhaseType phase)
        {
            ServerPhase = phase;
            ServerPhaseChanged?.Invoke(phase);
        }

        private void SetState(GameFlowState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GameFlow));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _authHandshake?.Dispose();
            _authConnection?.Dispose();
            _gameHandshake?.Dispose();
            _gameConnection?.Dispose();
        }
    }
}
