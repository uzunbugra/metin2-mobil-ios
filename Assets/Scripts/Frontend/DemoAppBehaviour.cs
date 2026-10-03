using System;
using System.Threading;
using System.Threading.Tasks;
using Metin2.Gameplay.Demo;
using Metin2.Gameplay.Flow;
using Metin2.Network.Session;
using Metin2.Protocol.Packets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Metin2.Frontend
{
    /// <summary>
    /// Screen-flow front-end for the demo: LOGIN (credentials form) →
    /// CHARACTER SELECT (slot buttons) → world hand-off to
    /// <see cref="DemoWorldBehaviour"/>. The whole journey runs against the
    /// in-process <see cref="DemoServer"/> through the real
    /// <see cref="GameFlow"/> (genuine DH2 + cipher), mirroring the C++
    /// client's screen sequence (PhaseLogin → PhaseSelect → PhaseGame).
    ///
    /// UI is constructed in code (UGUI) so the scene stays minimal and the
    /// flow stays reviewable. Awaits from UI handlers resume on Unity's main
    /// thread via the synchronization context; GameFlow's internal
    /// ConfigureAwait(false) does not affect the caller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoAppBehaviour : MonoBehaviour
    {
        private Font _font;
        private Canvas _canvas;
        private GameObject _loginPanel;
        private GameObject _selectPanel;
        private Text _statusText;
        private InputField _username;
        private InputField _password;
        private Button _connectButton;

        private DemoServer _demo;
        private GameFlow _flow;
        private DemoWorldBehaviour _world;
        private bool _busy;
        private bool _worldAttached;

        private void Start()
        {
            _world = GetComponent<DemoWorldBehaviour>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                // OS font fallback (builtin resource unavailable)
                _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            CreateCanvas();
            BuildLoginScreen();
        }

        // --- journey ----------------------------------------------------------------

        private async void OnConnectClicked()
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            _connectButton.interactable = false;
            SetStatus("Bağlanıyor...");

            try
            {
                _demo = new DemoServer();
                (int authPort, int gamePort) = _demo.Start();

                _flow = new GameFlow();
                AuthLoginResult login = await _flow.LoginAsync(
                    "127.0.0.1", authPort, _username.text, _password.text).ConfigureAwait(true);
                if (!login.Succeeded)
                {
                    SetStatus($"Giriş reddedildi: {login.Status}");
                    _connectButton.interactable = true;
                    _busy = false;
                    return;
                }

                SetStatus("Karakterler yükleniyor...");
                ChannelSelectData slots = await _flow.ConnectChannelAsync(
                    "127.0.0.1", gamePort, _username.text).ConfigureAwait(true);

                BuildCharacterSelectScreen(slots);
                _busy = false;
            }
            catch (Exception ex)
            {
                SetStatus($"Hata: {ex.Message}");
                _connectButton.interactable = true;
                _busy = false;
            }
        }

        private async void OnSlotClicked(byte slot)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            SetStatus("Dünyaya giriliyor...");

            try
            {
                await _flow.SelectCharacterAsync(slot, 2).ConfigureAwait(true);
                await _flow.EnterWorldAsync().ConfigureAwait(true);

                Destroy(_loginPanel);
                Destroy(_selectPanel);

                // Hand the session to the world view; it takes over rendering,
                // input and the event pump.
                _world.enabled = true;
                _world.Attach(_flow, _demo);
                _worldAttached = true;
            }
            catch (Exception ex)
            {
                SetStatus($"Hata: {ex.Message}");
                _busy = false;
            }
        }

        /// <summary>
        /// iOS/Android lifecycle (guide §9.2): backgrounding kills the
        /// session server-side (keepalive timeout ~60 s), so on pause the
        /// session is torn down deterministically and the user returns to
        /// the login screen. No zombie connections, no fabricated state.
        /// </summary>
        private void OnApplicationPause(bool pause)
        {
            if (!pause)
            {
                return;
            }

            if (!_worldAttached && _flow == null && _demo == null)
            {
                // No session yet — the login screen is static, nothing to
                // tear down or rebuild.
                return;
            }

            if (_worldAttached)
            {
                // The world owns flow/demo after Attach — its teardown
                // disposes them exactly once.
                _world.DetachAndTearDown();
                _world.enabled = false;
                _worldAttached = false;
                _flow = null;
                _demo = null;
            }
            else
            {
                _flow?.Dispose();
                _flow = null;
                _demo?.Dispose();
                _demo = null;
            }

            Destroy(_selectPanel);
            _selectPanel = null;
            Destroy(_loginPanel);
            _loginPanel = null;

            BuildLoginScreen();
            _busy = false;
            _connectButton.interactable = true;
            SetStatus("Arka plana alındı — oturum kapatıldı. Tekrar giriş yapın.");
        }

        private void OnApplicationQuit()
        {
            if (_worldAttached)
            {
                _world.DetachAndTearDown();
                _worldAttached = false;
                _flow = null;
                _demo = null;
            }
            else
            {
                _flow?.Dispose();
                _flow = null;
                _demo?.Dispose();
                _demo = null;
            }
        }

        // --- UI construction ----------------------------------------------------------

        private void CreateCanvas()
        {
            var canvasGo = new GameObject("DemoAppCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystemGo = new GameObject("EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void BuildLoginScreen()
        {
            _loginPanel = CreatePanel("LoginPanel");

            CreateLabel(_loginPanel.transform, "METIN2 — DEMO",
                new Vector2(0f, 160f), 44, new Color(0.95f, 0.85f, 0.4f));
            CreateLabel(_loginPanel.transform, "DemoServer (gerçek DH2 + cipher, süreç içi)",
                new Vector2(0f, 120f), 18, new Color(1f, 1f, 1f, 0.5f));

            _username = CreateInputField(_loginPanel.transform, "Kullanıcı Adı", "demo",
                new Vector2(0f, 50f), password: false);
            _password = CreateInputField(_loginPanel.transform, "Şifre", "demo",
                new Vector2(0f, -5f), password: true);

            _connectButton = CreateButton(_loginPanel.transform, "GİRİŞ YAP",
                new Vector2(0f, -70f), OnConnectClicked);

            _statusText = CreateLabel(_loginPanel.transform, "Hazır — demo/demo ile giriş yapın",
                new Vector2(0f, -130f), 20, new Color(1f, 1f, 1f, 0.75f));
        }

        private void BuildCharacterSelectScreen(ChannelSelectData slots)
        {
            Destroy(_loginPanel);
            _loginPanel = null;

            _selectPanel = CreatePanel("SelectPanel");

            CreateLabel(_selectPanel.transform, "KARAKTER SEÇ",
                new Vector2(0f, 160f), 36, new Color(0.95f, 0.85f, 0.4f));

            for (byte i = 0; i < slots.Players.Length; i++)
            {
                SimplePlayer slot = slots.Players[i];
                byte index = i;
                bool hasCharacter = slot.Id != 0;
                string label = hasCharacter
                    ? $"{slot.Name}  —  Lv{slot.Level}  (slot {index + 1})"
                    : $"Boş slot {index + 1}";

                Button button = CreateButton(_selectPanel.transform, label,
                    new Vector2(0f, 90f - i * 60f),
                    hasCharacter ? (Action)(() => OnSlotClicked(index)) : null);
                if (!hasCharacter)
                {
                    button.interactable = false;
                    button.GetComponentInChildren<Text>().color = new Color(1f, 1f, 1f, 0.3f);
                }
            }

            _statusText = CreateLabel(_selectPanel.transform, "Bir karakter seçin",
                new Vector2(0f, -150f), 20, new Color(1f, 1f, 1f, 0.75f));
        }

        // --- UI helpers ---------------------------------------------------------------

        private GameObject CreatePanel(string name)
        {
            var panelGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_canvas.transform, false);

            var rect = (RectTransform)panelGo.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(520f, 520f);

            panelGo.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.1f, 0.94f);
            return panelGo;
        }

        private Text CreateLabel(Transform parent, string text, Vector2 position, int size, Color color)
        {
            var go = new GameObject($"Label_{text}", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(480f, size + 12f);
            rect.anchoredPosition = position;

            var label = go.GetComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = text;
            return label;
        }

        private InputField CreateInputField(
            Transform parent, string placeholder, string initial, Vector2 position, bool password)
        {
            var root = new GameObject($"Field_{placeholder}", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);

            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(340f, 42f);
            rect.anchoredPosition = position;

            var background = root.GetComponent<Image>();
            background.color = new Color(0.13f, 0.14f, 0.17f, 1f);

            var field = root.AddComponent<InputField>();
            field.targetGraphic = background;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(root.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 2f);
            textRect.offsetMax = new Vector2(-10f, -2f);
            // The constructor already added the Text component; do not add a
            // second one (two Texts on one CanvasRenderer corrupts the UI).
            var text = textGo.GetComponent<Text>();
            if (text == null)
            {
                throw new InvalidOperationException("InputField text component was not created.");
            }

            text.font = _font;
            text.fontSize = 20;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            field.textComponent = text;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(root.transform, false);
            var placeholderRect = (RectTransform)placeholderGo.transform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10f, 2f);
            placeholderRect.offsetMax = new Vector2(-10f, -2f);
            var placeholderText = placeholderGo.GetComponent<Text>();
            if (placeholderText == null)
            {
                throw new InvalidOperationException("InputField placeholder component was not created.");
            }

            placeholderText.font = _font;
            placeholderText.fontSize = 20;
            placeholderText.color = new Color(1f, 1f, 1f, 0.35f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.text = placeholder;
            field.placeholder = placeholderText;

            field.text = initial;
            if (password)
            {
                field.contentType = InputField.ContentType.Password;
            }

            return field;
        }

        private Button CreateButton(
            Transform parent, string label, Vector2 position, Action onClick)
        {
            var root = new GameObject($"Button_{label}",
                typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);

            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(340f, 46f);
            rect.anchoredPosition = position;

            var image = root.GetComponent<Image>();
            image.color = new Color(0.2f, 0.35f, 0.55f, 1f);

            var button = root.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(root.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            // Constructor already added the Text; fetch it, never add a second.
            var text = textGo.GetComponent<Text>();
            if (text == null)
            {
                throw new InvalidOperationException("Button text component was not created.");
            }

            text.font = _font;
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;

            return button;
        }

        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }
        }
    }
}
