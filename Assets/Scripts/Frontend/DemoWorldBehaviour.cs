#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Metin2.Frontend.Proto;
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
        private const float JoystickRadiusPx = 140f;
        private const float JoystickDeadZonePx = 20f;

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

        // Touch controls (guide §9.1): left half = virtual joystick, bottom
        // right = attack zone. Zone-based multi-touch — no EventSystem needed,
        // so the joystick and the attack button work simultaneously.
        private int _joystickFingerId = -1;
        private Vector2 _joystickOrigin;
        private Vector2 _joystickCurrent;

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
            _flow.ItemChanged += item => Enqueue(() => OnItemEvent(item));
            _flow.CombatEventReceived += combat => Enqueue(() => OnCombatEvent(combat));
        }

        // --- inventory strip (real proto data: icons + locale names) ----------

        private readonly Dictionary<ushort, (uint Vnum, byte Count)> _inventory =
            new Dictionary<ushort, (uint, byte)>();

        private ItemDatabase? _itemDatabase;

        private void OnItemEvent(ItemEvent item)
        {
            if (item.Window != ItemWindow.Inventory)
            {
                return;
            }

            uint vnum;
            switch (item.EventKind)
            {
                case ItemEvent.Kind.Set:
                    vnum = item.Set.Vnum;
                    _inventory[item.Cell] = (item.Set.Vnum, item.Set.Count);
                    break;
                case ItemEvent.Kind.Updated:
                    vnum = _inventory.TryGetValue(item.Cell, out (uint Vnum, byte Count) current)
                        ? current.Vnum
                        : 0;
                    if (vnum != 0)
                    {
                        _inventory[item.Cell] = (vnum, item.Update.Count);
                    }

                    break;
                default:
                    vnum = _inventory.TryGetValue(item.Cell, out (uint Vnum, byte Count) before)
                        ? before.Vnum
                        : 0;
                    _inventory.Remove(item.Cell);
                    break;
            }

            ItemDef? def = LookupItem(vnum);
            string name = def != null ? def.LocaleName : "bilinmeyen item";
            Log($"envanter: hücre {item.Cell} {item.EventKind} → {name}");
        }

        private ItemDef? LookupItem(uint vnum)
        {
            if (vnum == 0)
            {
                return null;
            }

            _itemDatabase ??= Resources.Load<ItemDatabase>("GameData/ItemDatabase");
            return _itemDatabase?.Find(vnum);
        }

        /// <summary>
        /// Bottom-center strip: server-authoritative inventory cells with
        /// real icons from the imported item_proto/icon pipeline (local-only
        /// assets, ADR-0003). Falls back to vnum text when the database is
        /// not imported.
        /// </summary>
        private void DrawInventoryStrip(Rect safe)
        {
            if (_inventory.Count == 0)
            {
                return;
            }

            const float cellSize = 52f;
            const float gap = 6f;
            const int maxCells = 8;

            (ushort Cell, uint Vnum, byte Count)[] cells = _inventory
                .OrderBy(kv => kv.Key)
                .Take(maxCells)
                .Select(kv => (kv.Key, kv.Value.Vnum, kv.Value.Count))
                .ToArray();

            float width = cells.Length * cellSize + (cells.Length - 1) * gap;
            float x = safe.x + safe.width * 0.5f - width * 0.5f;
            float y = safe.y + safe.height - cellSize - 16f;

            GUI.Box(new Rect(x - 8f, y - 24f, width + 16f, cellSize + 32f), "ENVANTER");

            var labelStyle = GUI.skin.GetStyle("Label");
            var previousAlignment = labelStyle.alignment;
            var previousFontSize = labelStyle.fontSize;

            for (int i = 0; i < cells.Length; i++)
            {
                (ushort cellIndex, uint vnum, byte count) = cells[i];
                Rect rect = new Rect(x + i * (cellSize + gap), y, cellSize, cellSize);
                GUI.Box(rect, "");

                ItemDef? def = LookupItem(vnum);
                if (def?.Icon != null)
                {
                    GUI.DrawTexture(rect, def.Icon.texture, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.Label(rect, vnum.ToString());
                }

                // Count overlay, bottom-right of the cell.
                labelStyle.alignment = TextAnchor.LowerRight;
                labelStyle.fontSize = 12;
                GUI.Label(
                    new Rect(rect.x - 6f, rect.y, rect.width + 6f, rect.height),
                    count > 1 ? $"x{count}" : string.Empty);
            }

            labelStyle.alignment = previousAlignment;
            labelStyle.fontSize = previousFontSize;
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

            HandleTouchInput();
            HandleMoveInput();
            HandleAttackInput();
            FollowPlayer();
        }

        // --- world construction -------------------------------------------------

        private GameObject _sun;
        private GameObject _ground;

        private void CreateWorld()
        {
            // Mobile: explicit 60 FPS (iOS defaults to 30 for battery).
            Application.targetFrameRate = 60;

            var lightGo = new GameObject("Sun");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            _sun = lightGo;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            Colorize(ground, new Color(0.18f, 0.26f, 0.16f));
            _ground = ground;

            _cameraTransform = Camera.main != null ? Camera.main.transform : null;
            if (_cameraTransform == null)
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                cameraGo.AddComponent<Camera>();
                _cameraTransform = cameraGo.transform;
            }
        }

        /// <summary>
        /// iOS/Android lifecycle (guide §9.2): backgrounding the app kills
        /// the session (server keepalive timeout ~60 s, desc.cpp:174-180),
        /// so tear it down deterministically instead of leaving a zombie
        /// connection. Cancels the event pump, destroys world visuals and
        /// disposes the flow/demo it owns (attached mode). Idempotent.
        /// </summary>
        public void DetachAndTearDown()
        {
            if (!_attached && _flow == null)
            {
                return;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            foreach (EntityView view in _entities.Values)
            {
                if (view.Transform != null)
                {
                    Destroy(view.Transform.gameObject);
                }
            }

            _entities.Clear();

            if (_sun != null)
            {
                Destroy(_sun);
                _sun = null;
            }

            if (_ground != null)
            {
                Destroy(_ground);
                _ground = null;
            }

            var flow = _flow;
            var demo = _demo;
            _flow = null;
            _demo = null;
            _attached = false;
            _journeyFailed = false;
            _cameraTransform = null;

            // Dispose last; guard against double-dispose from the owner.
            try
            {
                flow?.Dispose();
            }
            catch (Exception)
            {
                // Already disposed — teardown must stay safe.
            }

            try
            {
                demo?.Dispose();
            }
            catch (Exception)
            {
                // Already disposed.
            }

            Log("oturum kapatildi (arka plan / lifecycle)");
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

            Transform entity;
            string kind;
            if (add.Vid == DemoServer.SpawnedMobVid)
            {
                kind = "mob";
                var model = Resources.Load<GameObject>("GameData/Characters/wolf");
                if (model != null)
                {
                    // SP10-5 pipeline: Metin2 GR2 -> divine -> GLB -> GLTFast
                    // import -> WolfPilot.BuildPrefab (doku + 0.01 ölçek hazır).
                    var wolf = Instantiate(model);
                    wolf.name = $"Entity {add.Vid} (wolf)";
                    entity = wolf.transform;
                    Log($"spawn {add.Vid} (wolf model, {add.X}, {add.Y})");
                }
                else
                {
                    // Wolf pipeline asset'ı yok (ADR-0003: DATA commit edilmez)
                    // -> eski küp fallback.
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = $"Entity {add.Vid}";
                    Colorize(cube, new Color(0.85f, 0.25f, 0.2f));
                    entity = cube.transform;
                    Log($"spawn {add.Vid} (mob, küp fallback - wolf.prefab yok)");
                }
            }
            else
            {
                kind = "player";
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Entity {add.Vid}";
                Colorize(sphere, new Color(0.3f, 0.85f, 0.4f));
                entity = sphere.transform;
            }

            entity.position = ToWorld(add.X, add.Y);
            entity.rotation = Quaternion.Euler(0f, add.Angle * Mathf.Rad2Deg, 0f);
            _entities[add.Vid] = new EntityView(entity, kind);
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

        private void HandleTouchInput()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (touch.phase == TouchPhase.Began)
                {
                    if (_joystickFingerId < 0 && touch.position.x < Screen.width * 0.5f)
                    {
                        _joystickFingerId = touch.fingerId;
                        _joystickOrigin = touch.position;
                        _joystickCurrent = touch.position;
                    }
                    else if (AttackZoneScreen.Contains(touch.position))
                    {
                        TryAttack();
                    }
                }
                else if (touch.fingerId == _joystickFingerId)
                {
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        _joystickFingerId = -1;
                    }
                    else
                    {
                        _joystickCurrent = touch.position;
                    }
                }
            }
        }

        /// <summary>
        /// Attack zone in screen coordinates (origin bottom-left, like
        /// Touch.position), inset from the safe area so the home indicator
        /// and rounded corners don't swallow touches.
        /// </summary>
        private Rect AttackZoneScreen
        {
            get
            {
                Rect safe = Screen.safeArea;
                return new Rect(safe.xMax - 300f, safe.yMin + 20f, 280f, 200f);
            }
        }

        private Vector2 ReadMoveDirection()
        {
            // Keyboard (editor / desktop testing)
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            if (!Mathf.Approximately(horizontal, 0f) || !Mathf.Approximately(vertical, 0f))
            {
                return new Vector2(horizontal, vertical).normalized;
            }

            // Touch joystick
            if (_joystickFingerId < 0)
            {
                return Vector2.zero;
            }

            Vector2 delta = _joystickCurrent - _joystickOrigin;
            if (delta.magnitude < JoystickDeadZonePx)
            {
                return Vector2.zero;
            }

            return Vector2.ClampMagnitude(delta, JoystickRadiusPx) / JoystickRadiusPx;
        }

        private void HandleMoveInput()
        {
            Vector2 direction = ReadMoveDirection();
            if (Mathf.Approximately(direction.x, 0f) && Mathf.Approximately(direction.y, 0f))
            {
                return;
            }

            if (Time.time - _lastMoveSent < MoveSendInterval)
            {
                return;
            }

            _lastMoveSent = Time.time;

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
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryAttack();
            }
        }

        private void TryAttack()
        {
            if (_flow == null || _flow.State != GameFlowState.InWorld)
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
            // Safe area (notch / Dynamic Island / home indicator) — GUI
            // coordinates, top-left origin. Corner-anchored HUD elements
            // inset from the safe area instead of the raw screen.
            Rect safe = Screen.safeArea;
            float safeLeft = safe.x;
            float safeTop = Screen.height - safe.y - safe.height;
            float safeRight = safe.x + safe.width;
            float safeBottom = safeTop + safe.height;

            GUI.Box(new Rect(safeLeft + 10f, safeTop + 10f, 420f, 168f), "METIN2 UNITY — DEMO MODE (DemoServer in-process)");
            string state = _flow == null ? "starting" : _journeyFailed ? "FAILED" : _flow.State.ToString();
            string phase = _flow?.ServerPhase.ToString() ?? "-";
            GUI.Label(new Rect(safeLeft + 20f, safeTop + 34f, 400f, 20f), $"Flow: {state}    Server phase: {phase}");
            GUI.Label(new Rect(safeLeft + 20f, safeTop + 54f, 400f, 20f),
                _flow?.MainCharacter.Vid > 0
                    ? $"Character: {_flow.MainCharacter.Name} (VID {_flow.MainCharacter.Vid})"
                    : "Character: -");
            GUI.Label(new Rect(safeLeft + 20f, safeTop + 74f, 400f, 20f), $"HP: {_hp}/{_maxHp}    Entities: {_entities.Count}");
            GUI.Label(new Rect(safeLeft + 20f, safeTop + 94f, 400f, 20f), $"Pos (cm): {_playerPos.x:0}, {_playerPos.y:0}    Heading: {_playerHeading:0}");

            GUI.Box(new Rect(safeLeft + 10f, safeBottom - 190f, 620f, 140f), "Server events (wire-verified)");
            for (int i = 0; i < _logLines.Count; i++)
            {
                GUI.Label(new Rect(safeLeft + 20f, safeBottom - 166f + i * 18f, 600f, 18f), _logLines[i]);
            }

            GUI.Box(new Rect(safeRight - 320f, safeTop + 10f, 310f, 74f), "Controls");
            GUI.Label(new Rect(safeRight - 310f, safeTop + 34f, 290f, 60f),
                "Touch: sol yarı = joystick, SALDIR = attack\nKeyboard: WASD move, SPACE attack");

            DrawTouchControls();
            DrawInventoryStrip(safe);
        }

        /// <summary>
        /// Renders the virtual joystick (while active) and the attack zone.
        /// GUI coordinates are top-left origin; Touch.position is bottom-left,
        /// so Y is flipped when mapping.
        /// </summary>
        private void DrawTouchControls()
        {
            if (_flow == null || _flow.State != GameFlowState.InWorld)
            {
                return;
            }

            // Attack zone (visual only — input is zone-based multi-touch).
            Rect attackGui = ToGuiRect(AttackZoneScreen);
            Color previous = GUI.color;
            GUI.color = new Color(0.75f, 0.25f, 0.2f, 0.45f);
            GUI.Box(attackGui, "SALDIR");
            GUI.color = previous;

            if (_joystickFingerId < 0)
            {
                return;
            }

            Vector2 knobDelta = Vector2.ClampMagnitude(_joystickCurrent - _joystickOrigin, JoystickRadiusPx);
            Vector2 knobScreen = _joystickOrigin + knobDelta;

            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            DrawCircle(new Vector2(_joystickOrigin.x, Screen.height - _joystickOrigin.y), JoystickRadiusPx);
            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            DrawCircle(new Vector2(knobScreen.x, Screen.height - knobScreen.y), JoystickRadiusPx * 0.4f);
            GUI.color = previous;
        }

        private static Rect ToGuiRect(Rect screenRect)
        {
            return new Rect(
                screenRect.x,
                Screen.height - screenRect.y - screenRect.height,
                screenRect.width,
                screenRect.height);
        }

        private static Texture2D _circleTexture;

        private static void DrawCircle(Vector2 guiCenter, float radius)
        {
            if (_circleTexture == null)
            {
                int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                        texture.SetPixel(x, y, distance <= center ? Color.white : Color.clear);
                    }
                }

                texture.Apply();
                _circleTexture = texture;
            }

            GUI.DrawTexture(new Rect(guiCenter.x - radius, guiCenter.y - radius, radius * 2f, radius * 2f), _circleTexture);
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
