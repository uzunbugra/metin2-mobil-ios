using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Metin2.Gameplay.Demo;
using Metin2.Gameplay.Flow;
using Metin2.Network.Session;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Packets;
using UnityEngine;

namespace Metin2.Frontend
{
    /// <summary>
    /// First playable visual: a protocol-driven demo world. On Play it starts
    /// the in-process <see cref="DemoServer"/>, drives the real
    /// <see cref="GameFlow"/> through the full journey (auth -> channel ->
    /// select -> world entry) and renders the server-authoritative state:
    /// entities appear from spawn events, the player capsule moves only on
    /// GC_MOVE rebroadcasts, combat results arrive as server packets.
    ///
    /// Threading contract (AGENT_DEVELOPMENT_GUIDE.md §5.2): the flow runs on
    /// background threads; every Unity API call is marshalled through a
    /// concurrent queue drained in Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoWorldBehaviour : MonoBehaviour
    {
        private const float MoveSendInterval = 0.2f;
        private const float MoveSpeedCmPerSecond = 300f;
        private const int CentimetersPerUnit = 100;

        private DemoServer _demo;
        private GameFlow _flow;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private bool _attached;

        private readonly Dictionary<uint, EntityView> _entities = new Dictionary<uint, EntityView>();
        private Transform _cameraTransform;
        private Vector2 _playerPos;
        private float _playerHeading;
        private int _hp = 1000;
        private int _maxHp = 1000;
        private float _lastMoveSent;
        private bool _journeyFailed;

        private readonly struct EntityView
        {
            public readonly Transform Transform;
            public readonly string Kind;

            public EntityView(Transform transform, string kind)
            {
                Transform = transform;
                Kind = kind;
            }
        }

        /// <summary>
        /// Takes over an already-connected session from
        /// <see cref="DemoAppBehaviour"/>: renders the world, handles input and
        /// runs the event pump. The caller has already completed login, channel,
        /// character select and world entry.
        /// </summary>
        public void Attach(GameFlow flow, DemoServer demo)
        {
            if (flow == null || demo == null)
            {
                throw new ArgumentNullException(flow == null ? nameof(flow) : nameof(demo));
            }

            if (_attached || _flow != null)
            {
                throw new InvalidOperationException("World view is already attached.");
            }

            _attached = true;
            _demo = demo;
            _flow = flow;
            _cts = new CancellationTokenSource();

            CreateWorld();
            SubscribeFlowEvents();

            _maxHp = flow.Stats.Points[PointTypes.Hp];
            _hp = flow.Stats.Points[PointTypes.Hp];
            SpawnPlayer(flow.MainCharacter);
            Log($"attached: {flow.MainCharacter.Name} (VID {flow.MainCharacter.Vid}) HP {_hp}/{_maxHp}");

            Task.Run(() => flow.RunEventPumpAsync(_cts.Token), _cts.Token);
        }

        private void Start()
        {
            if (_attached || _flow != null)
            {
                // Attached mode: the app already initialized us.
                return;
            }

            _demo = new DemoServer();
            (int authPort, int gamePort) = _demo.Start();
            _cts = new CancellationTokenSource();

            CreateWorld();

            _flow = new GameFlow();
            SubscribeFlowEvents();

            Log("demo server started, connecting...");
            Task.Run(() => RunJourneyAsync(authPort, gamePort, _cts.Token), _cts.Token);
        }

        private void SubscribeFlowEvents()
        {
            _flow.StateChanged += state => Enqueue(() => Log($"flow state -> {state}"));
            _flow.ServerPhaseChanged += phase => Enqueue(() => Log($"server phase -> {phase}"));
            _flow.EntitySpawned += add => Enqueue(() => SpawnEntity(add));
            _flow.EntityDespawned += vid => Enqueue(() => DespawnEntity(vid));
            _flow.EntityMoved += move => Enqueue(() => MovePlayer(move));
            _flow.ItemChanged += item => Enqueue(() => Log($"item: {item.EventKind} cell {item.Cell}"));
            _flow.CombatEventReceived += combat => Enqueue(() => OnCombatEvent(combat));
        }

        private async Task RunJourneyAsync(int authPort, int gamePort, CancellationToken token)
        {
            try
            {
                var login = await _flow.LoginAsync("127.0.0.1", authPort, "demo", "demo", token).ConfigureAwait(false);
                if (!login.Succeeded)
                {
                    Enqueue(() => Log($"LOGIN FAILED: {login.Status}"));
                    return;
                }

                var slots = await _flow.ConnectChannelAsync("127.0.0.1", gamePort, "demo", token).ConfigureAwait(false);
                Enqueue(() => Log($"channel ok: empire {slots.Empire}, slot0 = {slots.Players[0].Name} Lv{slots.Players[0].Level}"));

                var main = await _flow.SelectCharacterAsync(0, 2, token).ConfigureAwait(false);
                _maxHp = _flow.Stats.Points[PointTypes.Hp];
                _hp = _flow.Stats.Points[PointTypes.Hp];
                Enqueue(() =>
                {
                    SpawnPlayer(main);
                    Log($"entered loading: {main.Name} (VID {main.Vid}) HP {_hp}/{_maxHp}");
                });

                var entry = await _flow.EnterWorldAsync(token).ConfigureAwait(false);
                Enqueue(() => Log($"in world: channel {entry.Channel}, server time {entry.ServerTime}"));

                _ = Task.Run(() => _flow.RunEventPumpAsync(token), token);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _journeyFailed = true;
                Enqueue(() => Log($"JOURNEY ERROR: {ex.Message}"));
            }
        }

        private void Update()
        {
            while (_mainThread.TryDequeue(out Action action))
            {
                action();
            }

            if (_flow == null || _flow.State != GameFlowState.InWorld || _journeyFailed)
            {
                return;
            }

            HandleMoveInput();
            HandleAttackInput();
            FollowPlayer();
        }

        // --- world construction -------------------------------------------------

        private void CreateWorld()
        {
            var lightGo = new GameObject("Sun");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            Colorize(ground, new Color(0.18f, 0.26f, 0.16f));

            _cameraTransform = Camera.main != null ? Camera.main.transform : null;
            if (_cameraTransform == null)
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                cameraGo.AddComponent<Camera>();
                _cameraTransform = cameraGo.transform;
            }
        }

        private void SpawnPlayer(PacketGCMainCharacter main)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = $"Player {main.Name} ({main.Vid})";
            Colorize(player, new Color(0.25f, 0.5f, 0.95f));
            _playerPos = new Vector2(main.X, main.Y);
            _entities[main.Vid] = new EntityView(player.transform, "player");
            player.transform.position = ToWorld(main.X, main.Y);
            Log("player spawned (server GC_CHARACTER_ADD path pending; placed from 113)");
        }

        private void SpawnEntity(PacketGCCharacterAdd add)
        {
            if (_entities.ContainsKey(add.Vid))
            {
                return;
            }

            PrimitiveType shape = add.Vid == DemoServer.SpawnedMobVid
                ? PrimitiveType.Cube
                : PrimitiveType.Sphere;
            var entity = GameObject.CreatePrimitive(shape);
            entity.name = $"Entity {add.Vid}";
            Colorize(entity, add.Vid == DemoServer.SpawnedMobVid
                ? new Color(0.85f, 0.25f, 0.2f)
                : new Color(0.3f, 0.85f, 0.4f));
            entity.transform.position = ToWorld(add.X, add.Y);
            entity.transform.rotation = Quaternion.Euler(0f, add.Angle * Mathf.Rad2Deg, 0f);
            _entities[add.Vid] = new EntityView(entity.transform, add.Vid == DemoServer.SpawnedMobVid ? "mob" : "player");
            Log($"spawn {add.Vid} ({_entities[add.Vid].Kind}) at ({add.X}, {add.Y})");
        }

        private void DespawnEntity(uint vid)
        {
            if (_entities.TryGetValue(vid, out EntityView view))
            {
                Destroy(view.Transform.gameObject);
                _entities.Remove(vid);
                Log($"despawn {vid}");
            }
        }

        private void MovePlayer(PacketGCMove move)
        {
            if (!_entities.TryGetValue(move.Vid, out EntityView view))
            {
                return;
            }

            // Server-authoritative position: the visual only follows GC_MOVE.
            _playerPos = new Vector2(move.X, move.Y);
            _playerHeading = move.Rot * 5f; // wire rot is degrees/5
            view.Transform.position = ToWorld(move.X, move.Y);
            view.Transform.rotation = Quaternion.Euler(0f, _playerHeading, 0f);
        }

        private void OnCombatEvent(CombatEvent combat)
        {
            switch (combat.EventKind)
            {
                case CombatEvent.Kind.DamageInfo:
                    Log($"damage {combat.Damage.Damage} -> VID {combat.Damage.Vid}");
                    break;
                case CombatEvent.Kind.PointChanged:
                    if (combat.PointChange.Type == PointTypes.Hp)
                    {
                        _hp = combat.PointChange.Value;
                        Log($"HP -> {_hp}/{_maxHp}");
                    }

                    break;
                case CombatEvent.Kind.Motion:
                    Log($"motion: VID {combat.Motion.Vid} -> {combat.Motion.VictimVid}");
                    break;
                case CombatEvent.Kind.Stunned:
                    Log($"stunned: VID {combat.Stun.Vid}");
                    break;
                case CombatEvent.Kind.Dead:
                    Log($"dead: VID {combat.Dead.Vid}");
                    break;
            }
        }

        // --- input ---------------------------------------------------------------

        private void HandleMoveInput()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f))
            {
                return;
            }

            if (Time.time - _lastMoveSent < MoveSendInterval)
            {
                return;
            }

            _lastMoveSent = Time.time;

            var direction = new Vector2(horizontal, vertical).normalized;
            var target = _playerPos + direction * (MoveSpeedCmPerSecond * MoveSendInterval);
            float headingDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Send intent only; the visual moves when the server rebroadcasts.
            _ = _flow.Movement.SendMoveAsync(
                MoveFunc.Move,
                0,
                headingDegrees,
                (int)target.x,
                (int)target.y,
                (uint)Environment.TickCount);
        }

        private void HandleAttackInput()
        {
            if (!Input.GetKeyDown(KeyCode.Space))
            {
                return;
            }

            // Nearest mob first: VID 201 in the demo script.
            if (_entities.TryGetValue(DemoServer.SpawnedMobVid, out _))
            {
                _ = _flow.Combat.SendAttackAsync(DemoServer.SpawnedMobVid, 0);
            }
        }

        private void FollowPlayer()
        {
            if (_cameraTransform == null || !_entities.TryGetValue(DemoServer.MainCharacterVid, out EntityView player))
            {
                return;
            }

            Vector3 target = player.Transform.position;
            _cameraTransform.position = new Vector3(target.x, 12f, target.z - 9f);
            _cameraTransform.rotation = Quaternion.Euler(55f, 0f, 0f);
        }

        // --- helpers ---------------------------------------------------------------

        private Vector3 ToWorld(int xCm, int yCm)
        {
            // Metin2 centimeters -> Unity units, relative to the spawn point so
            // the demo world sits near the origin.
            float originX = 959900f;
            float originY = 269500f;
            return new Vector3(
                (xCm - originX) / CentimetersPerUnit,
                0.5f,
                (yCm - originY) / CentimetersPerUnit);
        }

        private static void Colorize(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                material.color = color;
                renderer.material = material;
            }
        }

        private void Enqueue(Action action)
        {
            _mainThread.Enqueue(action);
        }

        private readonly List<string> _logLines = new List<string>();

        private void Log(string line)
        {
            _logLines.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
            if (_logLines.Count > 10)
            {
                _logLines.RemoveAt(0);
            }
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(10, 10, 420, 168), "METIN2 UNITY — DEMO MODE (DemoServer in-process)");
            string state = _flow == null ? "starting" : _journeyFailed ? "FAILED" : _flow.State.ToString();
            string phase = _flow?.ServerPhase.ToString() ?? "-";
            GUI.Label(new Rect(20, 34, 400, 20), $"Flow: {state}    Server phase: {phase}");
            GUI.Label(new Rect(20, 54, 400, 20),
                _flow?.MainCharacter.Vid > 0
                    ? $"Character: {_flow.MainCharacter.Name} (VID {_flow.MainCharacter.Vid})"
                    : "Character: -");
            GUI.Label(new Rect(20, 74, 400, 20), $"HP: {_hp}/{_maxHp}    Entities: {_entities.Count}");
            GUI.Label(new Rect(20, 94, 400, 20), $"Pos (cm): {_playerPos.x:0}, {_playerPos.y:0}    Heading: {_playerHeading:0}");

            GUI.Box(new Rect(10, Screen.height - 190, 620, 140), "Server events (wire-verified)");
            for (int i = 0; i < _logLines.Count; i++)
            {
                GUI.Label(new Rect(20, Screen.height - 166 + i * 18, 600, 18), _logLines[i]);
            }

            GUI.Box(new Rect(Screen.width - 320, 10, 310, 74), "Controls");
            GUI.Label(new Rect(Screen.width - 310, 34, 290, 60),
                "WASD / arrows: move (server-authoritative)\nSPACE: attack nearest mob (201)");
        }

        private void OnApplicationQuit()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _flow?.Dispose();
            _demo?.Dispose();
        }
    }
}
