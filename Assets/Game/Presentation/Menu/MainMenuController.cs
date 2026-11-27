using System.Collections.Generic;
using Adventure3D.Core;
using Adventure3D.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Main menu, built in code to match the approved design: full-screen key art, game title on the
    /// left, a dark chamfered panel with five items (start mission, multiplayer, loadout, settings,
    /// quit), gold highlight on the selected item, version at the bottom left.
    /// Layout reference resolution is 1600x900; the menu column is anchored to the left edge and the
    /// art fills the screen, so other aspect ratios keep the same composition.
    /// Fully usable with mouse, touch, keyboard and gamepad.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        const string PrefLan = "a3d.lan.address";
        const string PrefOnline = "a3d.online.address";
        const string PrefPort = "a3d.port";
        static readonly Vector2 Reference = new Vector2(1600, 900);
        /// <summary>Position of the damma relative to the title centre (tuned on the 1600x900 layout).</summary>
        static readonly Vector2 TitleDammaOffset = new Vector2(172f, -4f);

        enum JoinKind { Lan, Online }

        RectTransform _safe, _main, _multi, _join, _host;
        Button _firstMain, _multiButton, _hostButton, _joinButton, _onlineButton, _multiBack;
        InputField _address, _port, _hostPort;
        Button _connect, _joinBack, _hostStart;
        ArabicText _joinTitle;
        MessageDialog _dialog;
        ConfirmModal _exitConfirm;
        CreateServerModal _createServer;
        ServerBrowserModal _browser;
        SettingsModal _settings;
        LoadoutModal _loadout;
        SplashOverlay _splash;
        static bool _splashShownOnce;
        JoinKind _joinKind;
        Selectable _currentFirst;
        RectTransform _currentScreen;

        public MessageDialog Dialog => _dialog;
        public Button FirstMainButton => _firstMain;
        public Button MultiplayerButton => _multiButton;
        public bool MultiplayerPanelOpen => _multi != null && _multi.gameObject.activeSelf;
        public bool JoinPanelOpen => _join != null && _join.gameObject.activeSelf;
        public InputField AddressField => _address;
        public InputField PortField => _port;
        public Button ConnectButton => _connect;
        public Button JoinButton => _joinButton;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Build();
            var services = MenuServices.Current;
            var reason = services != null ? services.ConsumeLastEndReason() : SessionEndReason.None;
            if (EndReasonText.IsError(reason))
                _dialog.Show(Strings.Get("error.ended_title"), EndReasonText.Message(reason), _firstMain);
        }

        void Update()
        {
            bool back = (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame ?? false)
                        || (UnityEngine.InputSystem.Gamepad.current?.buttonEast.wasPressedThisFrame ?? false);
            if (_splash != null && _splash.gameObject.activeSelf) return;
            if (_dialog != null && _dialog.IsOpen)
            {
                if (back) _dialog.Close();
                return;
            }
            if (_exitConfirm != null && _exitConfirm.IsOpen) { if (back) _exitConfirm.Close(); return; }
            if (_settings != null && _settings.IsOpen) { if (back) _settings.Close(); return; }
            if (_loadout != null && _loadout.IsOpen) { if (back) _loadout.Close(); return; }
            if (_createServer != null && _createServer.IsOpen) { if (back) _createServer.Close(); return; }
            if (_browser != null && _browser.IsOpen) { if (back) _browser.Close(); return; }
            if (back) Back();
            UiFocus.KeepFocus(_currentFirst);
        }

        void Back()
        {
            if (_currentScreen == _join || _currentScreen == _host) ShowMultiplayer();
            else if (_currentScreen == _multi) ShowMain();
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            UiFocus.Clear();
            UiKit.EnsureEventSystem();
            var services = MenuServices.Current;
            var net = services?.Config?.Network;

            var canvas = UiKit.CreateCanvas("MainMenuCanvas", 0);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 1f; // keep vertical proportions of the design on any aspect

            BuildBackground(canvas.transform);
            MenuMusicPlayer.Attach(canvas.transform);
            _safe = UiKit.SafeRoot(canvas.transform);

            BuildTitle(_safe);
            BuildVersion(_safe);

            // ---- Main screen (the five items of the design)
            _main = MenuPanel("MainScreen", _safe, 470f, 475f);
            var items = new List<Selectable>
            {
                (_firstMain = MenuItem(_main, Strings.Get("menu.start"), StartOffline)),
                (_multiButton = MenuItem(_main, Strings.Get("menu.multiplayer"), ShowMultiplayer)),
                MenuItem(_main, Strings.Get("menu.loadout"), () => _loadout.Open(UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>())),
                MenuItem(_main, Strings.Get("menu.settings"), () => _settings.Open(UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>())),
                MenuItem(_main, Strings.Get("menu.quit"), () => _exitConfirm.Show(Strings.Get("exit.title"), Strings.Get("exit.body"), UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>(), () => services?.QuitGame())),
            };
            UiFocus.ChainVertical(items);

            // ---- Multiplayer screen
            _multi = MenuPanel("MultiplayerScreen", _safe, 470f, 620f);
            var multiItems = new List<Selectable>
            {
                (_hostButton = MenuItem(_multi, Strings.Get("menu.host"), ShowHost)),
                (_joinButton = MenuItem(_multi, Strings.Get("menu.join"), () => ShowJoin(JoinKind.Lan))),
                (_onlineButton = MenuItem(_multi, Strings.Get("menu.online"), () => ShowJoin(JoinKind.Online))),
                (_multiBack = MenuItem(_multi, Strings.Get("common.back"), ShowMain)),
                MenuItem(_multi, Strings.Get("menu.create_server"), () => _createServer.Open(_hostButton)),
                MenuItem(_multi, Strings.Get("menu.browser"), () => _browser.Open(_joinButton)),
            };
            UiFocus.ChainVertical(multiItems);
            var guest = UiKit.Label(_multi, Strings.Format("menu.guest", services?.Identity?.DisplayName ?? "-"), 20, TextAnchor.MiddleCenter, 30);
            guest.Target.color = MenuStyle.TextMuted;

            // ---- Join form (LAN / online)
            _join = MenuPanel("JoinScreen", _safe, 470f, 560f);
            _joinTitle = UiKit.Label(_join, Strings.Get("join.title"), 30, TextAnchor.MiddleCenter, 46);
            _joinTitle.Target.color = MenuStyle.Gold;
            _joinTitle.Target.fontStyle = FontStyle.Bold;
            _address = FormInput(_join, Strings.Get("join.address"), PlayerPrefs.GetString(PrefLan, net != null ? net.DefaultLanAddress : "127.0.0.1"), InputField.ContentType.Standard);
            _port = FormInput(_join, Strings.Get("join.port"), PlayerPrefs.GetInt(PrefPort, net != null ? net.DefaultPort : 7777).ToString(), InputField.ContentType.IntegerNumber);
            _connect = MenuItem(_join, Strings.Get("join.connect"), Connect);
            _joinBack = MenuItem(_join, Strings.Get("common.back"), ShowMultiplayer);
            UiFocus.ChainVertical(new List<Selectable> { _address, _port, _connect, _joinBack });

            // ---- Host form
            _host = MenuPanel("HostScreen", _safe, 470f, 500f);
            var ht = UiKit.Label(_host, Strings.Get("host.title"), 30, TextAnchor.MiddleCenter, 46);
            ht.Target.color = MenuStyle.Gold;
            ht.Target.fontStyle = FontStyle.Bold;
            _hostPort = FormInput(_host, Strings.Get("join.port"), PlayerPrefs.GetInt(PrefPort, net != null ? net.DefaultPort : 7777).ToString(), InputField.ContentType.IntegerNumber);
            var hint = UiKit.Label(_host, Strings.Get("host.hint"), 18, TextAnchor.MiddleCenter, 56);
            hint.Target.color = MenuStyle.TextMuted;
            _hostStart = MenuItem(_host, Strings.Get("host.start"), StartHost);
            var hostBack = MenuItem(_host, Strings.Get("common.back"), ShowMultiplayer);
            UiFocus.ChainVertical(new List<Selectable> { _hostPort, _hostStart, hostBack });

            _dialog = MessageDialog.Create(_safe);
            _exitConfirm = ConfirmModal.Create(_safe);
            _createServer = CreateServerModal.Create(_safe, req => MenuServices.Current?.StartSession(req));
            _browser = ServerBrowserModal.Create(_safe, req => MenuServices.Current?.StartSession(req));
            _settings = SettingsModal.Create(_safe);
            _loadout = LoadoutModal.Create(_safe);

            ShowMain();
            if (!_splashShownOnce && SplashOverlay.ShouldShow)
            {
                _splashShownOnce = true;
                _splash = SplashOverlay.Create(canvas.transform, () => UiFocus.Select(_currentFirst));
            }
        }

        static void BuildBackground(Transform canvas)
        {
            var root = UiKit.NewRect("Background", canvas);
            UiKit.Stretch(root);
            var tex = Resources.Load<Texture2D>(MenuStyle.BackgroundResource);
            if (tex != null)
            {
                var art = UiKit.NewRect("KeyArt", root);
                art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
                var raw = art.gameObject.AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
                var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // fill, crop the overflow
                fitter.aspectRatio = (float)tex.width / tex.height;
            }
            else
            {
                var fill = root.gameObject.AddComponent<Image>();
                fill.sprite = MenuStyle.TacticalBackdrop;
                fill.type = Image.Type.Sliced;
                fill.color = Color.white;
                fill.raycastTarget = false;
            }

            // Darken the left side under the menu, like the design.
            var shade = UiKit.NewRect("LeftShade", root);
            shade.anchorMin = new Vector2(0f, 0f);
            shade.anchorMax = new Vector2(0.58f, 1f);
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            var si = shade.gameObject.AddComponent<Image>();
            si.sprite = MenuStyle.LeftShade;
            si.raycastTarget = false;

            // Tactical operative character on the right-center (above shade, beside the flag).
            var opTex = Resources.Load<Texture2D>("Adventure3D/UI/OperativeHero");
            if (opTex != null)
            {
                var opRt = UiKit.NewRect("OperativeHero", root);
                opRt.anchorMin = new Vector2(0.68f, 0f);
                opRt.anchorMax = new Vector2(0.68f, 0f);
                opRt.pivot = new Vector2(0.5f, 0f);
                opRt.anchoredPosition = Vector2.zero;
                opRt.sizeDelta = new Vector2(680f, 840f);
                var opImg = opRt.gameObject.AddComponent<RawImage>();
                opImg.texture = opTex;
                opImg.raycastTarget = false;
                var fitter = opRt.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)opTex.width / opTex.height;
            }
            // Rising fire embers across the tactical background.
            EmberField.Create(root, 34);
        }

        static void BuildTitle(RectTransform safe)
        {
            var logoTex = Resources.Load<Texture2D>("Adventure3D/UI/GameLogo");
            var crestTex = Resources.Load<Texture2D>("Adventure3D/UI/EagleCrest");
            if (crestTex != null)
            {
                var crestRt = UiKit.NewRect("EagleCrest", safe);
                UiKit.Anchor(crestRt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(545f, -148f));
                crestRt.sizeDelta = new Vector2(180f, 128f);
                var cr = crestRt.gameObject.AddComponent<RawImage>();
                cr.texture = crestTex;
                cr.raycastTarget = false;
            }
            if (logoTex != null)
            {
                var logoRt = UiKit.NewRect("GameLogo", safe);
                UiKit.Anchor(logoRt, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(50f, -172f));
                logoRt.sizeDelta = new Vector2(410f, 120f);
                var lg = logoRt.gameObject.AddComponent<RawImage>();
                lg.texture = logoTex;
                lg.raycastTarget = false;
                return;
            }

            // Fallback text title if GameLogo.png is missing.
            // uGUI Text cannot position Arabic harakat, so the title is drawn without them and the
            // damma of «حُماة» is placed by hand over the first letter (TitleDamma below).
            string titleText = System.Text.RegularExpressions.Regex.Replace(Strings.Get("app.title"), "[\u064B-\u065F\u0670]", "");
            var title = UiKit.Label(safe, titleText, 104, TextAnchor.MiddleCenter, 150);
            var rt = (RectTransform)title.transform;
            UiKit.Anchor(rt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(352f, -190f));
            rt.sizeDelta = new Vector2(620f, 150f);
            title.Target.color = MenuStyle.Cream;
            title.Target.fontStyle = FontStyle.Bold;
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 0.08f, 0.05f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(4f, -5f);

            if (Strings.Get("app.title").Contains("\u064F"))
            {
                // Invisible tatweel carries the damma so the font places it like on a real letter.
                var dammaRt = UiKit.NewRect("TitleDamma", rt);
                dammaRt.anchorMin = dammaRt.anchorMax = new Vector2(0.5f, 0.5f);
                dammaRt.anchoredPosition = TitleDammaOffset;
                dammaRt.sizeDelta = new Vector2(120f, 150f);
                var d = dammaRt.gameObject.AddComponent<Text>();
                d.font = UiKit.Font;
                d.fontSize = 104;
                d.fontStyle = FontStyle.Bold;
                d.alignment = TextAnchor.MiddleCenter;
                d.horizontalOverflow = HorizontalWrapMode.Overflow;
                d.verticalOverflow = VerticalWrapMode.Overflow;
                d.supportRichText = true;
                d.raycastTarget = false;
                d.color = MenuStyle.Cream;
                d.text = "<color=#00000000>\u0640</color>\u064F";
                var dShadow = dammaRt.gameObject.AddComponent<Shadow>();
                dShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
                dShadow.effectDistance = new Vector2(4f, -5f);
            }

            // Gold slanted accent under the title.
            for (int i = 0; i < 4; i++)
            {
                var bar = MenuStyle.Shape(safe, "TitleAccent", MenuStyle.White, MenuStyle.Gold);
                var b = bar.rectTransform;
                UiKit.Anchor(b, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(190f + i * 15f, -282f));
                b.sizeDelta = new Vector2(6f, 16f);
                b.localRotation = Quaternion.Euler(0f, 0f, -35f);
                bar.raycastTarget = false;
            }
            var line = MenuStyle.Shape(safe, "TitleAccentLine", MenuStyle.White, MenuStyle.Gold);
            UiKit.Anchor(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(250f, -285f));
            line.rectTransform.sizeDelta = new Vector2(60f, 3f);
            line.raycastTarget = false;
        }

        static void BuildVersion(RectTransform safe)
        {
            var version = UiKit.Label(safe, Strings.Format("menu.version", ProtocolInfo.GameVersion), 18, TextAnchor.MiddleLeft, 28);
            var rt = (RectTransform)version.transform;
            UiKit.Anchor(rt, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(50f, 46f));
            rt.sizeDelta = new Vector2(180f, 28f);
            version.Target.color = MenuStyle.TextMuted;

            var slash = MenuStyle.Shape(safe, "VersionAccent", MenuStyle.White, MenuStyle.Gold);
            UiKit.Anchor(slash.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(215f, 46f));
            slash.rectTransform.sizeDelta = new Vector2(4f, 16f);
            slash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            slash.raycastTarget = false;
            var line = MenuStyle.Shape(safe, "VersionLine", MenuStyle.White, new Color(MenuStyle.BorderMetal.r, MenuStyle.BorderMetal.g, MenuStyle.BorderMetal.b, 0.65f));
            UiKit.Anchor(line.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(230f, 46f));
            line.rectTransform.sizeDelta = new Vector2(480f, 1f);
            line.raycastTarget = false;
        }

        /// <summary>Dark chamfered panel on the left, under the title (x 50..520, y 295..770 in the design).</summary>
        static RectTransform MenuPanel(string name, RectTransform safe, float width, float height)
        {
            var panel = MenuStyle.Shape(safe, name, MenuStyle.Panel);
            var rt = panel.rectTransform;
            UiKit.Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -298f));
            rt.sizeDelta = new Vector2(width, height);
            rt.gameObject.AddComponent<PanelAnimator>();
            var col = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(16, 36, 38, 24);
            col.spacing = 14f;
            col.childAlignment = TextAnchor.UpperLeft;
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            return rt;
        }

        static Button MenuItem(Transform panel, string label, System.Action onClick)
        {
            var plate = MenuStyle.Shape(panel, "Item", MenuStyle.ButtonNormal);
            var rt = plate.rectTransform;

            var glow = MenuStyle.Shape(rt, "Glow", MenuStyle.ButtonGlow);
            UiKit.Stretch(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(-10f, -10f);
            glow.rectTransform.offsetMax = new Vector2(10f, 10f);
            glow.raycastTarget = false;
            glow.color = new Color(1f, 1f, 1f, 0f);

            var accent = MenuStyle.Shape(rt, "ActiveBar", MenuStyle.White, new Color(MenuStyle.Gold.r, MenuStyle.Gold.g, MenuStyle.Gold.b, 0f));
            accent.rectTransform.anchorMin = new Vector2(1f, 0.18f);
            accent.rectTransform.anchorMax = new Vector2(1f, 0.82f);
            accent.rectTransform.pivot = new Vector2(1f, 0.5f);
            accent.rectTransform.anchoredPosition = new Vector2(-8f, 0f);
            accent.rectTransform.sizeDelta = new Vector2(3f, 0f);
            accent.raycastTarget = false;

            var btn = plate.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = plate;
            btn.navigation = new Navigation { mode = Navigation.Mode.Vertical };
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiKit.Fit(rt, 64f);
            plate.gameObject.AddComponent<MenuButtonVisual>().Init(plate, btn, glow, accent);

            var text = UiKit.Label(plate.transform, label, 26, TextAnchor.MiddleCenter, 64f);
            var trt = (RectTransform)text.transform;
            UiKit.Stretch(trt);
            trt.offsetMax = new Vector2(-18f, 0f); // optical centre, away from the chamfer
            text.Target.color = MenuStyle.TextWhite;
            text.Target.fontStyle = FontStyle.Bold;
            return btn;
        }

        static InputField FormInput(Transform panel, string caption, string value, InputField.ContentType type)
        {
            var field = UiKit.LtrInput(panel, caption, value, type);
            field.textComponent.color = MenuStyle.Gold;
            field.textComponent.fontSize = 26;
            UiKit.Fit((RectTransform)field.transform, 58f);
            return field;
        }

        // ------------------------------------------------------------------ navigation

        void Show(RectTransform screen, Selectable first)
        {
            foreach (var s in new[] { _main, _multi, _join, _host })
                s.gameObject.SetActive(s == screen);
            _currentScreen = screen;
            _currentFirst = first;
            UiFocus.Select(first);
        }

        public void ShowMain() => Show(_main, _firstMain);
        public void ShowMultiplayer() => Show(_multi, _hostButton);
        void ShowHost() => Show(_host, _hostStart);

        void ShowJoin(JoinKind kind)
        {
            _joinKind = kind;
            var net = MenuServices.Current?.Config?.Network;
            _joinTitle.SetText(Strings.Get(kind == JoinKind.Lan ? "join.title" : "online.title"));
            _address.text = kind == JoinKind.Lan
                ? PlayerPrefs.GetString(PrefLan, net != null ? net.DefaultLanAddress : "127.0.0.1")
                : PlayerPrefs.GetString(PrefOnline, net != null ? net.DefaultOnlineAddress : "127.0.0.1");
            Show(_join, _connect);
        }

        public void OpenJoinLan() => ShowJoin(JoinKind.Lan);

        void ComingSoon(string section) =>
            _dialog.Show(section, Strings.Get("menu.coming_soon"), UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>());

        // ------------------------------------------------------------------ actions

        void StartOffline() => MenuServices.Current?.StartSession(LaunchRequest.Offline());

        void StartHost()
        {
            if (!AddressTools.TryParsePort(_hostPort.text, out var port))
            {
                _dialog.Show(Strings.Get("error.title"), Strings.Get("error.InvalidAddress"), _hostStart);
                return;
            }
            PlayerPrefs.SetInt(PrefPort, port);
            MenuServices.Current?.StartSession(new LaunchRequest { Mode = SessionMode.Host, Port = port });
        }

        /// <summary>Validates the form; invalid input shows an Arabic error and keeps the form open.</summary>
        public void Connect()
        {
            string address = (_address.text ?? "").Trim();
            if (AddressTools.Classify(address) == AddressTools.Kind.Invalid || !AddressTools.TryParsePort(_port.text, out var port))
            {
                _dialog.Show(Strings.Get("error.title"), Strings.Get("error.InvalidAddress"), _connect);
                return;
            }
            PlayerPrefs.SetString(_joinKind == JoinKind.Lan ? PrefLan : PrefOnline, address);
            PlayerPrefs.SetInt(PrefPort, port);
            MenuServices.Current?.StartSession(new LaunchRequest { Mode = SessionMode.Client, Address = address, Port = port });
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && _currentFirst != null && !(_dialog != null && _dialog.IsOpen)) UiFocus.KeepFocus(_currentFirst);
        }
    }
}
