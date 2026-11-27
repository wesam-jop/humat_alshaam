using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Builds the tactical uGUI interface from code using <see cref="MenuStyle"/> design tokens.
    /// Every fixed-height helper sets <c>flexibleHeight = 0</c> so rows never stretch inside panels,
    /// and ScrollRects automatically reset to the top on enable.
    /// </summary>
    public static class UiKit
    {
        static Font _font;
        static Font _monoFont;
        static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        public static readonly Color PanelColor = MenuStyle.BgPanel;
        public static readonly Color ButtonColor = new Color32(0x25, 0x2E, 0x3A, 0xFF);
        public static readonly Color AccentColor = MenuStyle.Gold;

        public const string FontResource = "Adventure3D/Fonts/Cairo";

        static readonly string[] OsFonts =
        {
            "Segoe UI", "Tahoma", "Arial", "IBM Plex Sans Arabic", "Cairo",
            "Noto Naskh Arabic", "Noto Sans Arabic", "Droid Arabic Naskh", "Roboto",
            "Geeza Pro", "Damascus", "Arial Hebrew",
        };

        static readonly string[] MonoOsFonts =
        {
            "JetBrains Mono", "Roboto Mono", "Cascadia Mono", "Consolas", "Courier New", "Liberation Mono", "Menlo",
        };

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                try { _font = Font.CreateDynamicFontFromOSFont(OsFonts, 32); }
                catch (Exception) { _font = null; }
                if (_font == null) _font = Resources.Load<Font>(FontResource);
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Font MonoFont
        {
            get
            {
                if (_monoFont != null) return _monoFont;
                try { _monoFont = Font.CreateDynamicFontFromOSFont(MonoOsFonts, 32); }
                catch (Exception) { _monoFont = null; }
                if (_monoFont == null) _monoFont = Font;
                return _monoFont;
            }
        }

        public static Sprite LoadSprite(string resourcePath)
        {
            if (SpriteCache.TryGetValue(resourcePath, out var cached) && cached != null) return cached;
            var sp = Resources.Load<Sprite>(resourcePath);
            if (sp == null)
            {
                var tex = Resources.Load<Texture2D>(resourcePath);
                if (tex != null)
                    sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            if (sp != null) SpriteCache[resourcePath] = sp;
            return sp;
        }

        public static EventSystem EnsureEventSystem()
        {
            var es = EventSystem.current != null ? EventSystem.current : UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (es != null)
            {
                var existing = es.GetComponent<InputSystemUIInputModule>();
                if (existing != null && existing.actionsAsset == null) existing.AssignDefaultActions();
                return es;
            }
            var go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            return es;
        }

        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform SafeRoot(Transform canvas)
        {
            var rt = NewRect("SafeArea", canvas);
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static RectTransform Panel(string name, Transform parent, Vector2 size, Color? color = null)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            if (color.HasValue)
            {
                img.color = color.Value;
            }
            else
            {
                img.sprite = MenuStyle.Panel;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            rt.gameObject.AddComponent<PanelAnimator>();
            return rt;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, float spacing = 12f, int padding = 24)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static ArabicText Label(Transform parent, string logical, int size = 28, TextAnchor anchor = TextAnchor.MiddleRight, float height = -1f)
        {
            var rt = NewRect("Label", parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.color = MenuStyle.TextWhite;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var label = rt.gameObject.AddComponent<ArabicText>();
            label.SetText(logical);
            Fit(rt, height > 0 ? height : size * 1.5f * Mathf.Max(1, CountLines(logical)));
            return label;
        }

        public static Text LtrLabel(Transform parent, string text, int size = 24, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rt = NewRect("LtrLabel", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = MonoFont;
            t.fontSize = size;
            t.color = MenuStyle.Gold;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            t.raycastTarget = false;
            Fit(rt, size * 1.5f * Mathf.Max(1, CountLines(text)));
            return t;
        }

        public static Button Button(Transform parent, string logical, Action onClick, float height = 56f)
        {
            var plate = MenuStyle.Shape(parent, "Button", MenuStyle.ButtonNormal);
            var rt = plate.rectTransform;

            var glow = MenuStyle.Shape(rt, "Glow", MenuStyle.ButtonGlow);
            Stretch(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(-10f, -10f);
            glow.rectTransform.offsetMax = new Vector2(10f, 10f);
            glow.raycastTarget = false;
            glow.color = new Color(1f, 1f, 1f, 0f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = plate;
            btn.navigation = new Navigation { mode = Navigation.Mode.Vertical };
            btn.onClick.AddListener(() => onClick?.Invoke());
            Fit(rt, height);
            rt.gameObject.AddComponent<MenuButtonVisual>().Init(plate, btn, glow);

            int fontSize = Mathf.Clamp(Mathf.RoundToInt(height * 0.40f), 18, 28);
            var label = Label(rt, logical, fontSize, TextAnchor.MiddleCenter, height);
            label.Target.fontStyle = FontStyle.Bold;
            label.Target.color = MenuStyle.TextWhite;
            var lrt = (RectTransform)label.transform;
            Stretch(lrt);
            lrt.offsetMin = new Vector2(14f, 0f);
            lrt.offsetMax = new Vector2(-14f, 0f);
            return btn;
        }

        public static InputField LtrInput(Transform parent, string caption, string value, InputField.ContentType type)
        {
            if (!string.IsNullOrEmpty(caption))
            {
                var cap = Label(parent, caption, 20, TextAnchor.MiddleRight, 28f);
                cap.Target.color = MenuStyle.TextMuted;
            }
            var plate = MenuStyle.Shape(parent, "Input", MenuStyle.InputField);
            var rt = plate.rectTransform;
            Fit(rt, 46f);

            var textRt = NewRect("Text", rt);
            Stretch(textRt);
            textRt.offsetMin = new Vector2(16, 4);
            textRt.offsetMax = new Vector2(-16, -4);
            var text = textRt.gameObject.AddComponent<Text>();
            text.font = MonoFont;
            text.fontSize = 22;
            text.color = MenuStyle.TextWhite;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            var input = rt.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.contentType = type;
            input.text = value;
            input.caretColor = MenuStyle.Gold;
            input.selectionColor = MenuStyle.GoldGlow;
            input.navigation = new Navigation { mode = Navigation.Mode.Vertical };
            rt.gameObject.AddComponent<TacticalInputVisual>().Init(plate);
            return input;
        }

        public static void Fit(RectTransform rt, float height)
        {
            var le = rt.gameObject.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
        }

        public static void FixedWidth(GameObject go, float width)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minWidth = width;
            le.preferredWidth = width;
            le.flexibleWidth = 0f;
        }

        static int CountLines(string s) => string.IsNullOrEmpty(s) ? 1 : s.Split('\n').Length;

        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
        }

        public static RectTransform Row(Transform parent, string name, float height = 46f, float spacing = 12f)
        {
            var rt = NewRect(name, parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = false;
            Fit(rt, height);
            return rt;
        }

        public static void Divider(Transform parent, float height = 1f)
        {
            var rt = NewRect("Divider", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = MenuStyle.BorderMetal;
            img.raycastTarget = false;
            Fit(rt, height);
        }

        /// <summary>Standard CS-style tactical modal header: GameLogo on left, title centered, EagleCrest on right (no overlap).</summary>
        public static void ModalHeader(Transform parent, string titleLogical)
        {
            var row = Row(parent, "Header", 48f, 12f);

            var leftSlot = NewRect("LogoSlot", row);
            Fit(leftSlot, 44f);
            FixedWidth(leftSlot.gameObject, 150f);
            var logoSp = LoadSprite("Adventure3D/UI/GameLogo");
            if (logoSp != null)
            {
                var img = leftSlot.gameObject.AddComponent<Image>();
                img.sprite = logoSp;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            var title = Label(row, titleLogical, 28, TextAnchor.MiddleCenter, 44f);
            title.Target.color = MenuStyle.TextWhite;
            title.Target.fontStyle = FontStyle.Bold;

            var rightSlot = NewRect("CrestSlot", row);
            Fit(rightSlot, 44f);
            FixedWidth(rightSlot.gameObject, 150f);
            var crestSp = LoadSprite("Adventure3D/UI/EagleCrest");
            if (crestSp != null)
            {
                var img = rightSlot.gameObject.AddComponent<Image>();
                img.sprite = crestSp;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
        }

        public static void TwoColumns(Transform parent, out RectTransform leftCol, out RectTransform rightCol)
        {
            var split = Row(parent, "TwoColumns", 400f, 16f);
            Stretch(split);
            var splitLe = split.gameObject.GetComponent<LayoutElement>();
            splitLe.flexibleHeight = 1f;
            splitLe.flexibleWidth = 1f;
            var h = split.GetComponent<HorizontalLayoutGroup>();
            h.childForceExpandHeight = true;
            h.padding = new RectOffset(6, 6, 4, 4);

            var leftCard = MenuStyle.Shape(split, "LeftCol", MenuStyle.CardPanel);
            leftCol = leftCard.rectTransform;
            var leftLe = leftCol.gameObject.AddComponent<LayoutElement>();
            leftLe.flexibleWidth = 1f;
            leftLe.flexibleHeight = 1f;
            Column(leftCol, 12f, 16);

            var rightCard = MenuStyle.Shape(split, "RightCol", MenuStyle.CardPanel);
            rightCol = rightCard.rectTransform;
            var rightLe = rightCol.gameObject.AddComponent<LayoutElement>();
            rightLe.flexibleWidth = 1f;
            rightLe.flexibleHeight = 1f;
            Column(rightCol, 12f, 16);
        }

        public static Button[] TabBar(Transform parent, string[] labels, Action<int> onSelect, float height = 44f)
        {
            var row = Row(parent, "TabBar", height, 8f);
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var plate = MenuStyle.Shape(row, "Tab_" + i, MenuStyle.TabNormal);
                Fit(plate.rectTransform, height);
                var btn = plate.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = plate;
                btn.onClick.AddListener(() => onSelect?.Invoke(idx));
                var text = Label(plate.transform, labels[i], 19, TextAnchor.MiddleCenter, height);
                text.Target.fontStyle = FontStyle.Bold;
                Stretch((RectTransform)text.transform);
                buttons[i] = btn;
            }
            return buttons;
        }

        public static void SetActiveTab(Button[] tabs, int activeIndex)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                var img = tabs[i].GetComponent<Image>();
                if (img != null) img.sprite = i == activeIndex ? MenuStyle.TabActive : MenuStyle.TabNormal;
                var txt = tabs[i].GetComponentInChildren<Text>();
                if (txt != null) txt.color = i == activeIndex ? MenuStyle.Gold : MenuStyle.TextMuted;
            }
        }

        /// <summary>Tactical RTL checkbox row: card surface, crisp 24x24 gold-checked box on left, Arabic label on right.</summary>
        public static Toggle Checkbox(Transform parent, string logical, bool initial, Action<bool> onChanged, float height = 46f)
        {
            var row = Row(parent, "CheckboxRow", height, 12f);
            var rowImg = row.gameObject.AddComponent<Image>();
            rowImg.sprite = MenuStyle.RowSurface;
            rowImg.type = Image.Type.Sliced;
            rowImg.color = Color.white;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(14, 14, 4, 4);

            // Checkbox box on the left
            var boxRt = NewRect("Box", row);
            Fit(boxRt, 24f);
            FixedWidth(boxRt.gameObject, 24f);
            var boxBg = boxRt.gameObject.AddComponent<Image>();
            boxBg.sprite = MenuStyle.InputField;
            boxBg.type = Image.Type.Sliced;
            boxBg.color = Color.white;

            var checkRt = NewRect("Check", boxRt);
            checkRt.anchorMin = new Vector2(0.18f, 0.18f);
            checkRt.anchorMax = new Vector2(0.82f, 0.82f);
            checkRt.offsetMin = checkRt.offsetMax = Vector2.zero;
            var checkImg = checkRt.gameObject.AddComponent<Image>();
            checkImg.color = MenuStyle.Gold;
            checkImg.raycastTarget = false;

            // Label on the right (Arabic RTL)
            var label = Label(row, logical, 19, TextAnchor.MiddleRight, height - 8f);
            label.Target.color = MenuStyle.TextWhite;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxBg;
            toggle.graphic = checkImg;
            toggle.isOn = initial;
            toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return toggle;
        }

        public static Slider SliderRow(Transform parent, string logical, float min, float max, float value, bool wholeNumbers, string format, Action<float> onChanged, float height = 46f)
        {
            var row = Row(parent, "SliderRow", height, 10f);
            var rowImg = row.gameObject.AddComponent<Image>();
            rowImg.sprite = MenuStyle.RowSurface;
            rowImg.type = Image.Type.Sliced;
            rowImg.color = Color.white;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(14, 14, 4, 4);

            // Value readout pill on the left
            var pill = MenuStyle.Shape(row, "ValuePill", MenuStyle.InputField);
            Fit(pill.rectTransform, 32f);
            FixedWidth(pill.gameObject, 66f);
            var readout = LtrLabel(pill.transform, value.ToString(format, System.Globalization.CultureInfo.InvariantCulture), 17, TextAnchor.MiddleCenter);
            readout.color = MenuStyle.Gold;
            var readoutRt = (RectTransform)readout.transform;
            readoutRt.anchorMin = Vector2.zero;
            readoutRt.anchorMax = Vector2.one;
            readoutRt.offsetMin = readoutRt.offsetMax = Vector2.zero;
            var readoutLe = readout.gameObject.GetComponent<LayoutElement>();
            if (readoutLe != null) UnityEngine.Object.DestroyImmediate(readoutLe);

            // Slider track area
            var sliderRt = NewRect("Slider", row);
            Fit(sliderRt, 32f);
            var sLe = sliderRt.gameObject.GetComponent<LayoutElement>();
            sLe.flexibleWidth = 1f;

            // Track background (thin bar)
            var bg = NewRect("Bg", sliderRt);
            bg.anchorMin = new Vector2(0f, 0.34f);
            bg.anchorMax = new Vector2(1f, 0.66f);
            bg.offsetMin = new Vector2(6f, 0f);
            bg.offsetMax = new Vector2(-6f, 0f);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = MenuStyle.InputField;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            // Fill area — anchors set so the Slider component can stretch the fill rect horizontally
            var fillArea = NewRect("FillArea", sliderRt);
            fillArea.anchorMin = new Vector2(0f, 0.34f);
            fillArea.anchorMax = new Vector2(1f, 0.66f);
            fillArea.offsetMin = new Vector2(6f, 0f);
            fillArea.offsetMax = new Vector2(-6f, 0f);

            // Fill image — MUST start fully stretched inside fillArea so Slider.fillRect works
            var fillRt = NewRect("Fill", fillArea);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.color = MenuStyle.Gold;
            fillImg.raycastTarget = false;

            // Handle sliding area
            var handleArea = NewRect("HandleArea", sliderRt);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(6f, 0f);
            handleArea.offsetMax = new Vector2(-6f, 0f);

            // Handle knob
            var handleRt = NewRect("Handle", handleArea);
            handleRt.sizeDelta = new Vector2(12f, 22f);
            var handleImg = handleRt.gameObject.AddComponent<Image>();
            handleImg.color = MenuStyle.TextWhite;

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = Mathf.Clamp(value, min, max);
            slider.onValueChanged.AddListener(v =>
            {
                readout.text = v.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
                onChanged?.Invoke(v);
            });

            // Arabic label on the right
            var label = Label(row, logical, 19, TextAnchor.MiddleRight, height - 8f);
            label.Target.color = MenuStyle.TextMuted;
            FixedWidth(label.gameObject, 155f);
            return slider;
        }

        public static RectTransform ScrollList(Transform parent, string name, float spacing = 6f, int padding = 10)
        {
            var root = MenuStyle.Shape(parent, name, MenuStyle.InputField);
            var rootLe = root.gameObject.AddComponent<LayoutElement>();
            rootLe.flexibleHeight = 1f;
            rootLe.minHeight = 160f;

            var viewport = NewRect("Viewport", root.transform);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(18f, 6f);
            viewport.offsetMax = new Vector2(-8f, -6f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;

            var col = Column(content, spacing, padding);
            col.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Sleek tactical scrollbar on left edge
            var sbRt = MenuStyle.Shape(root.transform, "Scrollbar", MenuStyle.InputField);
            sbRt.rectTransform.anchorMin = new Vector2(0f, 0f);
            sbRt.rectTransform.anchorMax = new Vector2(0f, 1f);
            sbRt.rectTransform.pivot = new Vector2(0f, 0.5f);
            sbRt.rectTransform.offsetMin = new Vector2(4f, 8f);
            sbRt.rectTransform.offsetMax = new Vector2(12f, -8f);
            var sbSlide = NewRect("SlideArea", sbRt.transform);
            Stretch(sbSlide);
            var sbHandle = MenuStyle.Shape(sbSlide, "Handle", MenuStyle.White, MenuStyle.BorderHover);
            Stretch(sbHandle.rectTransform);
            var sb = sbRt.gameObject.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = sbHandle.rectTransform;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 28f;
            root.gameObject.AddComponent<ScrollTopReset>().Init(scroll);
            return content;
        }

        public static Button CycleRow(Transform parent, string logical, string[] options, int initialIndex, Action<int> onChanged, float height = 46f)
        {
            var row = Row(parent, "CycleRow", height, 10f);
            var rowImg = row.gameObject.AddComponent<Image>();
            rowImg.sprite = MenuStyle.RowSurface;
            rowImg.type = Image.Type.Sliced;
            rowImg.color = Color.white;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(14, 14, 4, 4);

            int idx = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, options.Length - 1));

            // Value button on the left
            var plateRt = NewRect("ValueBtn", row);
            Fit(plateRt, height - 10f);
            var plateLe = plateRt.gameObject.GetComponent<LayoutElement>() ?? plateRt.gameObject.AddComponent<LayoutElement>();
            plateLe.flexibleWidth = 1f;
            var plateBg = plateRt.gameObject.AddComponent<Image>();
            plateBg.sprite = MenuStyle.InputField;
            plateBg.type = Image.Type.Sliced;
            plateBg.color = Color.white;

            var btn = plateRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = plateBg;

            // Arrow indicator
            var arrowRt = NewRect("Arrow", plateRt);
            arrowRt.anchorMin = new Vector2(0f, 0f);
            arrowRt.anchorMax = new Vector2(0f, 1f);
            arrowRt.pivot = new Vector2(0f, 0.5f);
            arrowRt.offsetMin = new Vector2(10f, 0f);
            arrowRt.offsetMax = new Vector2(32f, 0f);
            var arrowTxt = arrowRt.gameObject.AddComponent<Text>();
            arrowTxt.font = Font;
            arrowTxt.fontSize = 13;
            arrowTxt.text = "◄ ►";
            arrowTxt.color = MenuStyle.Gold;
            arrowTxt.alignment = TextAnchor.MiddleCenter;
            arrowTxt.raycastTarget = false;

            // Value text centered
            var valRt = NewRect("Value", plateRt);
            valRt.anchorMin = Vector2.zero;
            valRt.anchorMax = Vector2.one;
            valRt.offsetMin = new Vector2(34f, 0f);
            valRt.offsetMax = new Vector2(-8f, 0f);
            var valTxt = valRt.gameObject.AddComponent<Text>();
            valTxt.font = Font;
            valTxt.fontSize = 18;
            valTxt.fontStyle = FontStyle.Bold;
            valTxt.color = MenuStyle.TextWhite;
            valTxt.alignment = TextAnchor.MiddleCenter;
            valTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            valTxt.verticalOverflow = VerticalWrapMode.Overflow;
            valTxt.raycastTarget = false;
            var valArabic = valRt.gameObject.AddComponent<ArabicText>();
            valArabic.SetText(options.Length > 0 ? options[idx] : "-");

            btn.onClick.AddListener(() =>
            {
                if (options.Length == 0) return;
                idx = (idx + 1) % options.Length;
                valArabic.SetText(options[idx]);
                onChanged?.Invoke(idx);
            });

            // Arabic label on the right
            var label = Label(row, logical, 19, TextAnchor.MiddleRight, height - 8f);
            label.Target.color = MenuStyle.TextMuted;
            FixedWidth(label.gameObject, 155f);
            return btn;
        }
    }

    /// <summary>Resets a ScrollRect to the top (1.0) whenever its panel is opened.</summary>
    public sealed class ScrollTopReset : MonoBehaviour
    {
        ScrollRect _scroll;
        public void Init(ScrollRect scroll) => _scroll = scroll;
        void OnEnable() { if (_scroll != null) StartCoroutine(Snap()); }
        System.Collections.IEnumerator Snap()
        {
            yield return null;
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
