using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Screens 11–16: Full CS 1.5 Settings Modal (Keyboard, Mouse, Video, Audio, Player, Advanced).
    /// Matches the 2-column RTL tactical layouts, segmented mic LED meter, and live crosshair preview.
    /// </summary>
    public sealed class SettingsModal : MonoBehaviour
    {
        readonly List<KeybindEntry> _keybinds = TacticalCatalog.DefaultKeybinds();
        Button[] _tabs;
        RectTransform[] _pages;
        RectTransform _keyListContent;
        int _selectedKeyIdx = -1;
        bool _waitingKey;
        Image[] _micLeds;
        bool _testingMic;
        Image _crossPreviewH, _crossPreviewV, _crossRing;
        Selectable _returnFocus;

        public bool IsOpen => gameObject.activeSelf;

        public static SettingsModal Create(Transform parent)
        {
            var blocker = UiKit.NewRect("SettingsModal", parent);
            UiKit.Stretch(blocker);
            blocker.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var modal = blocker.gameObject.AddComponent<SettingsModal>();
            modal.Build(blocker);
            blocker.gameObject.SetActive(false);
            return modal;
        }

        void Build(RectTransform blocker)
        {
            var panel = UiKit.Panel("Panel", blocker, new Vector2(1140, 740));
            UiKit.Column(panel, 10, 24);

            UiKit.ModalHeader(panel, Strings.Get("settings.title"));

            // RTL tab order: Keyboard on right -> Advanced on left
            _tabs = UiKit.TabBar(panel, new[]
            {
                Strings.Get("settings.tab.advanced"),
                Strings.Get("settings.tab.player"),
                Strings.Get("settings.tab.audio"),
                Strings.Get("settings.tab.video"),
                Strings.Get("settings.tab.mouse"),
                Strings.Get("settings.tab.keyboard"),
            }, idx => ShowTab(5 - idx), 44f);

            var body = UiKit.NewRect("TabBody", panel);
            var bodyLe = body.gameObject.AddComponent<LayoutElement>();
            bodyLe.flexibleHeight = 1f;
            bodyLe.minHeight = 460f;

            _pages = new[]
            {
                BuildKeyboardTab(body),
                BuildMouseTab(body),
                BuildVideoTab(body),
                BuildAudioTab(body),
                BuildPlayerTab(body),
                BuildAdvancedTab(body),
            };

            var footer = UiKit.Row(panel, "Footer", 52f, 14f);
            UiKit.Button(footer, Strings.Get("common.cancel"), Close, 50f);
            UiKit.Button(footer, Strings.Get("common.default"), () => { TacticalSettings.Current.ResetDefaults(); Close(); }, 50f);
            var applyBtn = UiKit.Button(footer, Strings.Get("common.apply"), () => { TacticalSettings.Current.SaveAndApply(); Close(); }, 50f);
            applyBtn.GetComponent<Image>().sprite = MenuStyle.ButtonSelected;

            ShowTab(0);
        }

        // ---- 11: Keyboard
        RectTransform BuildKeyboardTab(Transform parent)
        {
            var root = UiKit.NewRect("KeyboardTab", parent);
            UiKit.Stretch(root);
            UiKit.Column(root, 8, 8);

            // Table column header
            var hdr = UiKit.Row(root, "KeyHeaders", 32f, 8f);
            hdr.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(22, 16, 0, 0);
            var hSec = UiKit.Label(hdr, Strings.Get("settings.key.secondary"), 18, TextAnchor.MiddleCenter, 30f);
            hSec.Target.color = MenuStyle.Gold;
            UiKit.FixedWidth(hSec.gameObject, 150f);

            var hPri = UiKit.Label(hdr, Strings.Get("settings.key.primary"), 18, TextAnchor.MiddleCenter, 30f);
            hPri.Target.color = MenuStyle.Gold;
            UiKit.FixedWidth(hPri.gameObject, 170f);

            var hAct = UiKit.Label(hdr, Strings.Get("settings.key.action"), 18, TextAnchor.MiddleRight, 30f);
            hAct.Target.color = MenuStyle.Gold;

            _keyListContent = UiKit.ScrollList(root, "Keybinds", 4f, 6);
            RefreshKeyList();

            var bar = UiKit.Row(root, "KeyActions", 46f, 12f);
            UiKit.Button(bar, Strings.Get("settings.key.clear"), () => { if (_selectedKeyIdx >= 0) { _keybinds[_selectedKeyIdx].PrimaryKey = "-"; RefreshKeyList(); } }, 44f);
            UiKit.Button(bar, Strings.Get("settings.key.edit"), () => { if (_selectedKeyIdx >= 0) _waitingKey = true; RefreshKeyList(); }, 44f);
            return root;
        }

        void RefreshKeyList()
        {
            for (int i = _keyListContent.childCount - 1; i >= 0; i--)
                Destroy(_keyListContent.GetChild(i).gameObject);

            for (int i = 0; i < _keybinds.Count; i++)
            {
                int idx = i;
                var kb = _keybinds[i];
                bool sel = idx == _selectedKeyIdx;
                var plate = MenuStyle.Shape(_keyListContent, "KeyRow", sel ? MenuStyle.ButtonSelected : MenuStyle.ButtonNormal);
                var btn = plate.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = plate;
                btn.onClick.AddListener(() => { _selectedKeyIdx = idx; _waitingKey = false; RefreshKeyList(); });
                UiKit.Fit(plate.rectTransform, 40f);

                var row = UiKit.Row(plate.transform, "Cols", 38f, 8f);
                UiKit.Stretch(row);
                row.offsetMin = new Vector2(14, 0);
                row.offsetMax = new Vector2(-14, 0);

                var sec = UiKit.LtrLabel(row, kb.SecondaryKey, 18, TextAnchor.MiddleCenter);
                sec.color = MenuStyle.TextMuted;
                UiKit.FixedWidth(sec.gameObject, 150f);

                string priText = (sel && _waitingKey) ? "..." : kb.PrimaryKey;
                var pri = UiKit.LtrLabel(row, priText, 18, TextAnchor.MiddleCenter);
                pri.color = sel ? MenuStyle.Gold : MenuStyle.TextWhite;
                UiKit.FixedWidth(pri.gameObject, 170f);

                var act = UiKit.Label(row, kb.ActionName, 19, TextAnchor.MiddleRight, 36f);
                act.Target.color = sel ? MenuStyle.Gold : MenuStyle.TextWhite;
            }
        }

        // ---- 12: Mouse
        RectTransform BuildMouseTab(Transform parent)
        {
            var s = TacticalSettings.Current;
            var root = UiKit.NewRect("MouseTab", parent);
            UiKit.Stretch(root);
            var card = MenuStyle.Shape(root, "MouseCard", MenuStyle.CardPanel);
            UiKit.Stretch(card.rectTransform);
            card.rectTransform.offsetMin = new Vector2(6, 4);
            card.rectTransform.offsetMax = new Vector2(-6, -4);
            UiKit.Column(card.rectTransform, 14, 24);

            UiKit.SliderRow(card.rectTransform, Strings.Get("settings.mouse.sens"), 0.2f, 10f, s.MouseSensitivity, false, "0.00", v => s.MouseSensitivity = v, 48f);
            UiKit.Divider(card.rectTransform);
            UiKit.Checkbox(card.rectTransform, Strings.Get("settings.mouse.invert"), s.ReverseMouse, v => s.ReverseMouse = v, 46f);
            UiKit.Checkbox(card.rectTransform, Strings.Get("settings.mouse.filter"), s.MouseFilter, v => s.MouseFilter = v, 46f);
            UiKit.Checkbox(card.rectTransform, Strings.Get("settings.mouse.raw"), s.RawInput, v => s.RawInput = v, 46f);
            return root;
        }

        // ---- 13: Video
        RectTransform BuildVideoTab(Transform parent)
        {
            var s = TacticalSettings.Current;
            var root = UiKit.NewRect("VideoTab", parent);
            UiKit.Stretch(root);
            UiKit.TwoColumns(root, out var left, out var right);

            UiKit.CycleRow(right, Strings.Get("settings.video.res"), new[] { "1920x1080", "1600x900", "1280x720" }, s.ResolutionIndex, i => s.ResolutionIndex = i);
            UiKit.CycleRow(right, Strings.Get("settings.video.aspect"), new[] { "4:3", "16:9" }, s.AspectRatioIndex, i => s.AspectRatioIndex = i);
            UiKit.Checkbox(right, Strings.Get("settings.video.fullscreen"), s.Fullscreen, v => s.Fullscreen = v);
            UiKit.CycleRow(right, Strings.Get("settings.video.quality"), new[] { "منخفضة", "متوسطة", "عالية", "فائقة" }, s.QualityIndex, i => s.QualityIndex = i);

            UiKit.SliderRow(left, Strings.Get("settings.video.brightness"), 0.5f, 1.5f, s.Brightness, false, "0.00", v => s.Brightness = v);
            UiKit.SliderRow(left, Strings.Get("settings.video.gamma"), 1.5f, 3.0f, s.Gamma, false, "0.00", v => s.Gamma = v);
            UiKit.SliderRow(left, Strings.Get("settings.video.fps"), 30, 300, s.MaxFps, true, "0", v => s.MaxFps = Mathf.RoundToInt(v));
            UiKit.Checkbox(left, Strings.Get("settings.video.vsync"), s.VSync, v => s.VSync = v);
            return root;
        }

        // ---- 14: Audio & Voice (16-LED meter matching design)
        RectTransform BuildAudioTab(Transform parent)
        {
            var s = TacticalSettings.Current;
            var root = UiKit.NewRect("AudioTab", parent);
            UiKit.Stretch(root);
            UiKit.TwoColumns(root, out var left, out var right);

            UiKit.SliderRow(right, Strings.Get("settings.audio.master"), 0f, 1f, s.MasterVolume, false, "0.00", v => s.MasterVolume = v);
            UiKit.SliderRow(right, Strings.Get("settings.audio.sfx"), 0f, 1f, s.SfxVolume, false, "0.00", v => s.SfxVolume = v);
            UiKit.SliderRow(right, Strings.Get("settings.audio.music"), 0f, 1f, s.MusicVolume, false, "0.00", v => s.MusicVolume = v);

            UiKit.Checkbox(left, Strings.Get("settings.audio.voice"), s.EnableVoice, v => s.EnableVoice = v);
            UiKit.SliderRow(left, Strings.Get("settings.audio.mic"), 0f, 1f, s.MicVolume, false, "0.00", v => s.MicVolume = v);

            var testRow = UiKit.Row(left, "MicTest", 46f, 10f);
            var ledBox = MenuStyle.Shape(testRow, "LedMeter", MenuStyle.InputField);
            UiKit.Fit(ledBox.rectTransform, 32f);
            var ledRow = UiKit.Row(ledBox.transform, "Leds", 24f, 4f);
            UiKit.Stretch(ledRow);
            ledRow.offsetMin = new Vector2(6, 4);
            ledRow.offsetMax = new Vector2(-6, -4);
            _micLeds = new Image[14];
            for (int i = 0; i < _micLeds.Length; i++)
            {
                var led = MenuStyle.Shape(ledRow, "L_" + i, MenuStyle.White, MenuStyle.BorderMetal);
                led.raycastTarget = false;
                _micLeds[i] = led;
            }

            var testBtn = UiKit.Button(testRow, Strings.Get("settings.audio.mic_test"), () => _testingMic = !_testingMic, 44f);
            UiKit.FixedWidth(testBtn.gameObject, 190f);
            return root;
        }

        // ---- 15: Player & Crosshair
        RectTransform BuildPlayerTab(Transform parent)
        {
            var s = TacticalSettings.Current;
            var root = UiKit.NewRect("PlayerTab", parent);
            UiKit.Stretch(root);
            UiKit.TwoColumns(root, out var left, out var right);

            var nameInput = UiKit.LtrInput(right, Strings.Get("settings.player.name"), s.PlayerName, InputField.ContentType.Standard);
            nameInput.onEndEdit.AddListener(v => s.PlayerName = v);
            UiKit.CycleRow(right, Strings.Get("settings.player.spray"), new[] { "نسر الشام", "درع القلعة", "رمز تكتيكي", "شعار الكتيبة" }, s.SprayIndex, i => s.SprayIndex = i);

            var sprayBox = MenuStyle.Shape(right, "SprayPreview", MenuStyle.InputField);
            UiKit.Fit(sprayBox.rectTransform, 110f);
            var crest = UiKit.LoadSprite("Adventure3D/UI/EagleCrest");
            if (crest != null)
            {
                var imgRt = UiKit.NewRect("SprayImg", sprayBox.transform);
                UiKit.Stretch(imgRt);
                imgRt.offsetMin = new Vector2(12, 12);
                imgRt.offsetMax = new Vector2(-12, -12);
                var img = imgRt.gameObject.AddComponent<Image>();
                img.sprite = crest;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            var previewBox = MenuStyle.Shape(left, "CrosshairPreview", MenuStyle.InputField);
            UiKit.Fit(previewBox.rectTransform, 100f);
            _crossRing = MenuStyle.Shape(previewBox.transform, "Ring", MenuStyle.White);
            _crossRing.rectTransform.anchorMin = _crossRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _crossRing.rectTransform.sizeDelta = new Vector2(4f, 4f);
            _crossPreviewH = MenuStyle.Shape(previewBox.transform, "H", MenuStyle.White);
            _crossPreviewH.rectTransform.anchorMin = _crossPreviewH.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _crossPreviewV = MenuStyle.Shape(previewBox.transform, "V", MenuStyle.White);
            _crossPreviewV.rectTransform.anchorMin = _crossPreviewV.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            UpdateCrossPreview();

            UiKit.CycleRow(left, Strings.Get("settings.player.cross_size"), new[] { "تلقائي", "صغير", "متوسط", "كبير" }, s.CrosshairSize, i => { s.CrosshairSize = i; UpdateCrossPreview(); });
            UiKit.CycleRow(left, Strings.Get("settings.player.cross_color"), new[] { "أخضر", "أحمر", "أصفر", "سماوي" }, s.CrosshairColor, i => { s.CrosshairColor = i; UpdateCrossPreview(); });
            UiKit.Checkbox(left, Strings.Get("settings.player.cross_alpha"), s.CrosshairTranslucent, v => { s.CrosshairTranslucent = v; UpdateCrossPreview(); });
            UiKit.Checkbox(left, Strings.Get("settings.player.cross_dynamic"), s.CrosshairDynamic, v => s.CrosshairDynamic = v);
            return root;
        }

        void UpdateCrossPreview()
        {
            if (_crossPreviewH == null) return;
            var s = TacticalSettings.Current;
            Color c = s.GetCrosshairColor();
            c.a = s.CrosshairTranslucent ? 0.8f : 1f;
            float len = 18f + s.CrosshairSize * 8f;
            _crossRing.color = c;
            _crossPreviewH.color = c;
            _crossPreviewV.color = c;
            _crossPreviewH.rectTransform.sizeDelta = new Vector2(len, 2f);
            _crossPreviewV.rectTransform.sizeDelta = new Vector2(2f, len);
        }

        // ---- 16: Advanced
        RectTransform BuildAdvancedTab(Transform parent)
        {
            var s = TacticalSettings.Current;
            var root = UiKit.NewRect("AdvancedTab", parent);
            UiKit.Stretch(root);
            UiKit.TwoColumns(root, out var left, out var right);

            UiKit.CycleRow(right, Strings.Get("settings.adv.hand"), new[] { "اليد اليمنى", "اليد اليسرى" }, s.RightHandWeapon ? 0 : 1, i => s.RightHandWeapon = i == 0);
            UiKit.Checkbox(left, Strings.Get("settings.adv.autoswitch"), s.AutoSwitchWeapon, v => s.AutoSwitchWeapon = v);
            UiKit.Checkbox(left, Strings.Get("settings.adv.center_id"), s.ShowCenterId, v => s.ShowCenterId = v);
            UiKit.Checkbox(left, Strings.Get("settings.adv.net_graph"), s.NetGraph, v => s.NetGraph = v);
            return root;
        }

        void ShowTab(int logicalIdx)
        {
            UiKit.SetActiveTab(_tabs, 5 - logicalIdx);
            for (int i = 0; i < _pages.Length; i++)
                _pages[i].gameObject.SetActive(i == logicalIdx);
        }

        void Update()
        {
            if (_waitingKey && _selectedKeyIdx >= 0 && Keyboard.current != null)
            {
                foreach (var key in Keyboard.current.allKeys)
                {
                    if (key != null && key.wasPressedThisFrame)
                    {
                        _keybinds[_selectedKeyIdx].PrimaryKey = key.displayName;
                        _waitingKey = false;
                        RefreshKeyList();
                        break;
                    }
                }
            }
            if (_micLeds != null)
            {
                float level = _testingMic ? (0.3f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f))) : 0f;
                int lit = Mathf.RoundToInt(level * _micLeds.Length);
                for (int i = 0; i < _micLeds.Length; i++)
                {
                    if (i < lit)
                        _micLeds[i].color = i < 9 ? MenuStyle.Success : (i < 12 ? MenuStyle.Gold : MenuStyle.Danger);
                    else
                        _micLeds[i].color = MenuStyle.BorderMetal;
                }
            }
        }

        public void Open(Selectable returnFocus)
        {
            _returnFocus = returnFocus;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowTab(0);
            UiFocus.PushDialog(_tabs[5]);
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            _waitingKey = false;
            _testingMic = false;
            gameObject.SetActive(false);
            UiFocus.PopDialog(_returnFocus);
        }
    }
}
