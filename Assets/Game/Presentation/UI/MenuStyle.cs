using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Tactical military visual identity (70-20-10 color rule, chamfered plates, gold active
    /// accents, metallic borders, and smooth unscaled-time UI animations).
    /// </summary>
    public static class MenuStyle
    {
        // ---- Surfaces (70% dark neutral)
        public static readonly Color BgOverlay = new Color32(0x0A, 0x0E, 0x14, 0xB8);     // rgba(10, 14, 20, 0.72)
        public static readonly Color BgPanel = new Color32(0x12, 0x17, 0x1E, 0xE0);       // rgba(18, 23, 30, 0.88)
        public static readonly Color BgPanelStrong = new Color32(0x14, 0x1A, 0x22, 0xE6); // rgba(20, 26, 34, 0.90)
        public static readonly Color BgInput = new Color32(0x16, 0x1C, 0x24, 0xF5);       // #161C24
        public static readonly Color BgHover = new Color32(0x23, 0x2D, 0x3B, 0xF2);       // #232D3B

        // ---- Metallic Borders & Text (20% structure)
        public static readonly Color BorderMetal = new Color32(0x3B, 0x47, 0x54, 0xFF);   // #3B4754
        public static readonly Color BorderHover = new Color32(0x64, 0x74, 0x8B, 0xFF);   // #64748B
        public static readonly Color TextWhite = new Color32(0xF3, 0xF5, 0xF7, 0xFF);     // #F3F5F7
        public static readonly Color TextMuted = new Color32(0x9A, 0xA6, 0xB2, 0xFF);     // #9AA6B2
        public static readonly Color Cream = new Color32(0xF3, 0xF5, 0xF7, 0xFF);         // #F3F5F7

        // ---- Tactical Gold & Status (10% active accent)
        public static readonly Color Gold = new Color32(0xE6, 0x9B, 0x2E, 0xFF);          // #E69B2E
        public static readonly Color GoldDark = new Color32(0xB5, 0x70, 0x16, 0xFF);      // #B57016
        public static readonly Color GoldGlow = new Color(230f / 255f, 155f / 255f, 46f / 255f, 0.35f);
        public static readonly Color Success = new Color32(0x2E, 0xCC, 0x71, 0xFF);       // #2ECC71
        public static readonly Color Danger = new Color32(0xE7, 0x4C, 0x3C, 0xFF);        // #E74C3C

        public const string BackgroundResource = "Adventure3D/UI/MainMenuBackground";

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite ButtonNormal => Get("btn_v4", () =>
            Chamfer(180, 72, 10,
                new Color32(0x22, 0x2B, 0x38, 0xF5),
                new Color32(0x14, 0x1A, 0x23, 0xFA),
                new Color32(0x36, 0x43, 0x56, 0xFF), 1,
                cutTopLeft: true, cutTopRight: false, cutBottomLeft: false, cutBottomRight: true,
                topAccent: new Color(1f, 1f, 1f, 0.12f), topAccentPx: 1));

        public static Sprite ButtonHover => Get("btnHover_v4", () =>
            Chamfer(180, 72, 10,
                new Color32(0x2C, 0x38, 0x4A, 0xFA),
                new Color32(0x1B, 0x23, 0x2F, 0xFA),
                BorderHover, 1,
                cutTopLeft: true, cutTopRight: false, cutBottomLeft: false, cutBottomRight: true,
                topAccent: new Color(1f, 1f, 1f, 0.22f), topAccentPx: 1));

        public static Sprite ButtonSelected => Get("btnSel_v4", () =>
            Chamfer(180, 72, 10,
                new Color32(0x63, 0x45, 0x18, 0xFC),
                new Color32(0x2D, 0x1F, 0x0A, 0xFC),
                Gold, 1,
                cutTopLeft: true, cutTopRight: false, cutBottomLeft: false, cutBottomRight: true,
                topAccent: new Color32(0xFF, 0xD6, 0x85, 0xE0), topAccentPx: 2));

        public static Sprite ButtonGlow => Get("btnGlow_v2", () =>
            SoftGlow(160, 72, 10, GoldGlow));

        public static Sprite Panel => Get("panel_v3", () =>
            Chamfer(160, 160, 12,
                new Color32(0x11, 0x17, 0x20, 0xF2),
                new Color32(0x0B, 0x0F, 0x16, 0xF6),
                new Color32(0x2E, 0x3A, 0x4B, 0xFF), 1,
                cutTopLeft: false, cutTopRight: false, cutBottomLeft: false, cutBottomRight: true,
                topAccent: Gold, topAccentPx: 2));

        public static Sprite CardPanel => Get("card_v1", () =>
            Chamfer(128, 128, 8,
                new Color32(0x16, 0x1D, 0x28, 0xD8),
                new Color32(0x10, 0x15, 0x1E, 0xE2),
                new Color32(0x29, 0x35, 0x46, 0xFF), 1,
                cutTopLeft: false, cutTopRight: false, cutBottomLeft: false, cutBottomRight: false,
                topAccent: new Color(1f, 1f, 1f, 0.08f), topAccentPx: 1));

        public static Sprite RowSurface => Get("row_v1", () =>
            Chamfer(96, 48, 6,
                new Color(1f, 1f, 1f, 0.045f),
                new Color(1f, 1f, 1f, 0.025f),
                new Color(1f, 1f, 1f, 0.075f), 1,
                cutTopLeft: false, cutTopRight: false, cutBottomLeft: false, cutBottomRight: false));

        public static Sprite TabNormal => Get("tabNorm_v1", () =>
            Chamfer(120, 44, 6,
                new Color32(0x18, 0x20, 0x2B, 0xCC),
                new Color32(0x12, 0x18, 0x21, 0xD8),
                new Color32(0x28, 0x33, 0x42, 0xFF), 1,
                cutTopLeft: true, cutTopRight: false, cutBottomLeft: false, cutBottomRight: false));

        public static Sprite TabActive => Get("tabAct_v1", () =>
            Chamfer(120, 44, 6,
                new Color32(0x2A, 0x24, 0x1B, 0xF0),
                new Color32(0x1B, 0x17, 0x12, 0xF5),
                Gold, 1,
                cutTopLeft: true, cutTopRight: false, cutBottomLeft: false, cutBottomRight: false,
                topAccent: new Color32(0xFF, 0xC9, 0x66, 0xFF), topAccentPx: 2));

        public static Sprite HudPanel => Get("hudPanel_v2", () =>
            Chamfer(160, 160, 12,
                BgPanel,
                new Color32(0x0E, 0x13, 0x1A, 0xE0),
                BorderMetal, 1,
                cutTopLeft: false, cutTopRight: false, cutBottomLeft: true, cutBottomRight: false,
                topAccent: Gold, topAccentPx: 2));

        public static Sprite InputField => Get("input_v3", () =>
            Chamfer(160, 64, 8,
                new Color32(0x0E, 0x13, 0x1B, 0xFA),
                new Color32(0x0A, 0x0E, 0x14, 0xFC),
                new Color32(0x2E, 0x3A, 0x4A, 0xFF), 1,
                cutTopLeft: true, cutTopRight: false, cutBottomLeft: false, cutBottomRight: true));

        public static Sprite InputFieldFocused => Get("inputFocus_v3", () =>
            Chamfer(160, 64, 8,
                new Color32(0x15, 0x1C, 0x26, 0xFA),
                new Color32(0x0F, 0x14, 0x1C, 0xFC),
                Gold, 1,
                cutTopLeft: true, cutTopRight: false, cutBottomLeft: false, cutBottomRight: true));

        public static Sprite White => Get("white", () =>
            Chamfer(8, 8, 0, Color.white, Color.white, Color.white, 0, false, false, false, false));

        /// <summary>Luxury armory spotlight backdrop with subtle tactical grid and gold corner accents.</summary>
        public static Sprite ArmoryShowcase => Get("armory_showcase_v1", () =>
        {
            const int w = 256, h = 144;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f) / w - 0.5f;
                    float ny = (y + 0.5f) / h - 0.5f;
                    float dist = Mathf.Sqrt(nx * nx * 1.4f + ny * ny * 2.2f);
                    float spot = Mathf.Clamp01(1f - dist * 1.35f);
                    spot = spot * spot;

                    Color baseCol = new Color(0.055f, 0.075f, 0.102f, 0.96f);
                    Color glowCol = new Color(0.24f, 0.17f, 0.07f, 0.98f);
                    Color c = Color.Lerp(baseCol, glowCol, spot * 0.65f);

                    // Subtle tactical blueprint grid every 16px
                    if (x % 16 == 0 || y % 16 == 0)
                        c = Color.Lerp(c, new Color(0.35f, 0.42f, 0.52f, 1f), 0.09f);

                    // Outer border (2px)
                    bool border = x < 2 || x >= w - 2 || y < 2 || y >= h - 2;
                    if (border) c = BorderMetal;

                    // Gold corner brackets (18px)
                    bool inCornerX = x < 18 || x >= w - 18;
                    bool inCornerY = y < 18 || y >= h - 18;
                    if (border && inCornerX && inCornerY) c = Gold;

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(22, 22, 22, 22));
        });

        /// <summary>Left-to-right darkening so the menu stays readable over any background.</summary>
        public static Sprite LeftShade => Get("shade", () =>
        {
            var t = new Texture2D(256, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < 256; x++)
            {
                float k = x / 255f;
                float a = Mathf.Lerp(0.92f, 0f, Mathf.SmoothStep(0f, 1f, k));
                for (int y = 0; y < 4; y++) t.SetPixel(x, y, new Color(0.039f, 0.055f, 0.078f, a));
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 256, 4), new Vector2(0.5f, 0.5f));
        });

        /// <summary>Subtle dark tactical gradient background when no key art is loaded.</summary>
        public static Sprite TacticalBackdrop => Get("backdrop_v2", () =>
        {
            var t = new Texture2D(64, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color top = new Color32(0x0A, 0x0E, 0x14, 0xFF);
            Color mid = new Color32(0x12, 0x17, 0x1E, 0xFF);
            Color bot = new Color32(0x0C, 0x10, 0x17, 0xFF);
            for (int y = 0; y < 128; y++)
            {
                float v = y / 127f;
                Color c = v > 0.5f ? Color.Lerp(mid, top, (v - 0.5f) * 2f) : Color.Lerp(bot, mid, v * 2f);
                for (int x = 0; x < 64; x++) t.SetPixel(x, y, c);
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 64, 128), new Vector2(0.5f, 0.5f));
        });

        static Sprite Get(string key, System.Func<Sprite> make)
        {
            if (!Cache.TryGetValue(key, out var s) || s == null) Cache[key] = s = make();
            return s;
        }

        static Sprite SoftGlow(int w, int h, int cut, Color glowColor)
        {
            const int pad = 12;
            int tw = w + pad * 2;
            int th = h + pad * 2;
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            const float r2 = 1.41421356f;
            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    float px = x - pad + 0.5f;
                    float py = y - pad + 0.5f;
                    float d = Mathf.Min(Mathf.Min(px, py), Mathf.Min(w - px, h - py));
                    if (cut > 0)
                    {
                        d = Mathf.Min(d, (px + (h - py) - cut) / r2); // top-left
                        d = Mathf.Min(d, ((w - px) + py - cut) / r2); // bottom-right
                    }
                    float a = d >= 0f ? 0f : Mathf.Clamp01(1f + d / pad);
                    a *= a;
                    Color c = glowColor;
                    c.a *= a;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            int b = pad + cut + 4;
            return Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>Rectangle with optional 45° cut corners, vertical gradient, metallic border, and optional top gold bar. 9-sliced.</summary>
        public static Sprite Chamfer(
            int w, int h, int cut,
            Color top, Color bottom, Color border, int borderPx,
            bool cutTopLeft, bool cutTopRight, bool cutBottomLeft, bool cutBottomRight,
            Color? topAccent = null, int topAccentPx = 0)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            const float r2 = 1.41421356f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float d = Mathf.Min(Mathf.Min(px, py), Mathf.Min(w - px, h - py));
                    if (cutTopLeft && cut > 0) d = Mathf.Min(d, (px + (h - py) - cut) / r2);
                    if (cutTopRight && cut > 0) d = Mathf.Min(d, ((w - px) + (h - py) - cut) / r2);
                    if (cutBottomLeft && cut > 0) d = Mathf.Min(d, (px + py - cut) / r2);
                    if (cutBottomRight && cut > 0) d = Mathf.Min(d, ((w - px) + py - cut) / r2);

                    Color c;
                    if (d < 0f)
                    {
                        c = new Color(0, 0, 0, 0);
                    }
                    else
                    {
                        Color fill = Color.Lerp(bottom, top, py / h);
                        bool isTopBar = topAccent.HasValue && topAccentPx > 0 && (h - py) < topAccentPx;
                        if (isTopBar) c = topAccent.Value;
                        else if (borderPx > 0 && d < borderPx) c = border;
                        else c = fill;

                        if (d < 1f) c.a *= d;
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            int cLeft = (cutTopLeft || cutBottomLeft) ? cut + borderPx + 2 : borderPx + 2;
            int cRight = (cutTopRight || cutBottomRight) ? cut + borderPx + 2 : borderPx + 2;
            int cBottom = (cutBottomLeft || cutBottomRight) ? cut + borderPx + 2 : borderPx + 2;
            int cTop = Mathf.Max((cutTopLeft || cutTopRight) ? cut + borderPx + 2 : borderPx + 2, topAccentPx + 2);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(cLeft, cBottom, cRight, cTop));
        }

        public static Image Shape(Transform parent, string name, Sprite sprite, Color? tint = null)
        {
            var rt = UiKit.NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = tint ?? Color.white;
            return img;
        }
    }

    /// <summary>
    /// Smooth unscaled-time entrance animation (fade + subtle scale-in) for tactical panels and dialogs.
    /// Keeps GameObjects immediately active and interactive so tests and gamepad focus work on frame 1.
    /// </summary>
    public sealed class PanelAnimator : MonoBehaviour
    {
        CanvasGroup _group;
        float _t;
        const float Duration = 0.18f;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _t = 0f;
            _group.alpha = 0f;
            transform.localScale = new Vector3(0.97f, 0.97f, 1f);
        }

        void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Duration);
            float ease = 1f - Mathf.Pow(1f - _t, 3f);
            _group.alpha = ease;
            float s = Mathf.Lerp(0.97f, 1f, ease);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>
    /// Tactical button visual: chamfered dark-metallic plate, gold plate + soft glow when selected,
    /// subtle hover highlight, and smooth scale/glow interpolation.
    /// </summary>
    public sealed class MenuButtonVisual : MonoBehaviour,
        ISelectHandler, IDeselectHandler,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        Image _plate;
        Image _glow;
        Image _accentBar;
        Selectable _selectable;
        bool _selected;
        bool _hovered;
        bool _pressed;
        float _glowAlpha;
        float _scale = 1f;

        public void Init(Image plate, Selectable selectable, Image glow = null, Image accentBar = null)
        {
            _plate = plate;
            _selectable = selectable;
            _glow = glow;
            _accentBar = accentBar;
            ApplyImmediate();
        }

        public void OnSelect(BaseEventData e) { _selected = true; RefreshSprite(); }
        public void OnDeselect(BaseEventData e) { _selected = false; _pressed = false; RefreshSprite(); }

        public void OnPointerEnter(PointerEventData e)
        {
            _hovered = true;
            if (_selectable != null && _selectable.interactable && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
            RefreshSprite();
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hovered = false;
            _pressed = false;
            RefreshSprite();
        }

        public void OnPointerDown(PointerEventData e) { _pressed = true; }
        public void OnPointerUp(PointerEventData e) { _pressed = false; }

        void OnEnable()
        {
            _selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            _hovered = false;
            _pressed = false;
            ApplyImmediate();
        }

        void RefreshSprite()
        {
            if (_plate == null) return;
            _plate.sprite = _selected ? MenuStyle.ButtonSelected : (_hovered ? MenuStyle.ButtonHover : MenuStyle.ButtonNormal);
        }

        void ApplyImmediate()
        {
            RefreshSprite();
            _glowAlpha = _selected ? 1f : 0f;
            _scale = _selected ? 1.03f : 1f;
            transform.localScale = new Vector3(_scale, _scale, 1f);
            if (_glow != null) _glow.color = new Color(1f, 1f, 1f, _glowAlpha);
            if (_accentBar != null) _accentBar.color = new Color(MenuStyle.Gold.r, MenuStyle.Gold.g, MenuStyle.Gold.b, _selected ? 1f : 0f);
        }

        void Update()
        {
            float targetScale = _pressed ? 0.98f : (_selected ? 1.03f : (_hovered ? 1.015f : 1f));
            float targetGlow = _selected ? 1f : (_hovered ? 0.35f : 0f);
            float dt = Mathf.Min(1f, Time.unscaledDeltaTime * 16f);

            _scale = Mathf.Lerp(_scale, targetScale, dt);
            _glowAlpha = Mathf.Lerp(_glowAlpha, targetGlow, dt);

            transform.localScale = new Vector3(_scale, _scale, 1f);
            if (_glow != null) _glow.color = new Color(1f, 1f, 1f, _glowAlpha);
            if (_accentBar != null) _accentBar.color = new Color(MenuStyle.Gold.r, MenuStyle.Gold.g, MenuStyle.Gold.b, _selected ? 1f : 0f);
        }
    }

    /// <summary>Switches an InputField's chamfered border to tactical gold while focused.</summary>
    public sealed class TacticalInputVisual : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        Image _plate;
        public void Init(Image plate) => _plate = plate;
        public void OnSelect(BaseEventData e) { if (_plate != null) _plate.sprite = MenuStyle.InputFieldFocused; }
        public void OnDeselect(BaseEventData e) { if (_plate != null) _plate.sprite = MenuStyle.InputField; }
    }
}
