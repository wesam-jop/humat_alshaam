using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>Procedural military HUD, Scoreboard, and Tactical Shop sprites, SDF icons, and brand chrome.</summary>
    public static class TacticalHudArt
    {
        public static readonly Color GoldBright = new Color32(0xED, 0xC0, 0x5E, 0xFF);
        public static readonly Color Gold = new Color32(0xD4, 0xA6, 0x4E, 0xFF);
        public static readonly Color GoldSoft = new Color32(0xE2, 0xC4, 0x82, 0xFF);
        public static readonly Color GoldLine = new Color32(0x8C, 0x70, 0x3C, 0xCC);
        public static readonly Color Cream = new Color32(0xF0, 0xEE, 0xE8, 0xFF);
        public static readonly Color Muted = new Color32(0x86, 0x90, 0x9A, 0xFF);
        public static readonly Color AllyBlue = new Color32(0x5E, 0xAD, 0xE2, 0xFF);
        public static readonly Color RadarAlly = new Color32(0x3E, 0xA2, 0xFF, 0xFF);
        public static readonly Color RadarEnemy = new Color32(0xEA, 0x3C, 0x34, 0xFF);
        public static readonly Color RadarGreen = new Color32(0x3C, 0xE6, 0x6A, 0xFF);
        public static readonly Color OverlayBackdrop = new Color32(0x09, 0x0C, 0x11, 0xB8);

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static Sprite Get(string k, Func<Sprite> b) => Cache.TryGetValue(k, out var s) && s != null ? s : (Cache[k] = b());
        static Sprite Plate(string k, int w, int h, int cut, Color top, Color bot, Color rim, int bpx, bool tl = false, bool tr = false) =>
            Get(k, () => MenuStyle.Chamfer(w, h, cut, top, bot, rim, bpx, tl, tr, false, false));

        // 9-sliced beveled plates & chrome
        public static Sprite VitalsPlate => Plate("v_plt", 96, 60, 16, new Color32(0x24, 0x2F, 0x3C, 0xE2), new Color32(0x16, 0x1E, 0x27, 0xE6), new Color32(0x5E, 0x6E, 0x7E, 0xE0), 2, tr: true);
        public static Sprite AmmoPlate => Plate("a_plt", 96, 56, 18, new Color32(0xB4, 0x80, 0x2B, 0xE6), new Color32(0x6D, 0x4A, 0x16, 0xE8), new Color32(0xF2, 0xC6, 0x5E, 0xF5), 2, tl: true);
        public static Sprite ScoreTableBg => Plate("sc_bg", 64, 64, 0, new Color32(0x10, 0x14, 0x1A, 0xCC), new Color32(0x0C, 0x10, 0x15, 0xD2), new Color32(0x9E, 0x7E, 0x40, 0xD8), 1);
        public static Sprite ScoreHeaderGold => Plate("sc_hg", 64, 56, 0, new Color32(0x8E, 0x6B, 0x32, 0xB4), new Color32(0x6C, 0x50, 0x24, 0xAC), new Color32(0xB4, 0x8E, 0x48, 0xAA), 1);
        public static Sprite ScoreHeaderSlate => Plate("sc_hs", 64, 56, 0, new Color32(0x46, 0x68, 0x80, 0xBA), new Color32(0x32, 0x4C, 0x60, 0xB2), new Color32(0x6A, 0x8D, 0xA6, 0xAA), 1);
        public static Sprite ScoreRowHighlight => Plate("sc_hl", 64, 46, 0, new Color32(0x9C, 0x72, 0x2A, 0x74), new Color32(0x74, 0x52, 0x1C, 0x64), new Color32(0xD8, 0xAC, 0x52, 0xE0), 1);
        public static Sprite ShopPanelBg => Plate("sh_bg", 64, 64, 0, new Color32(0x12, 0x16, 0x1C, 0xDC), new Color32(0x0E, 0x12, 0x18, 0xE2), new Color32(0x9C, 0x7E, 0x40, 0xDC), 1);
        public static Sprite ShopBalanceBg => Plate("sh_bl", 64, 64, 0, new Color32(0x8C, 0x68, 0x2E, 0xBC), new Color32(0x5E, 0x45, 0x1E, 0xB8), new Color32(0xBC, 0x94, 0x48, 0xC4), 1);
        public static Sprite ShopTabActive => Plate("sh_ta", 64, 64, 0, new Color32(0xDB, 0xA4, 0x44, 0xF2), new Color32(0x9C, 0x6C, 0x20, 0xF2), new Color32(0xF8, 0xD2, 0x72, 0xFF), 2);
        public static Sprite ShopTabNormal => Plate("sh_tn", 64, 64, 0, new Color32(0x50, 0x62, 0x72, 0xDC), new Color32(0x2D, 0x38, 0x43, 0xE2), new Color32(0x74, 0x88, 0x99, 0xC0), 1);
        public static Sprite ShopCardBg => Plate("sh_cd", 64, 64, 0, new Color32(0x18, 0x1D, 0x24, 0xD8), new Color32(0x12, 0x16, 0x1C, 0xE2), new Color32(0x8A, 0x70, 0x3C, 0xCC), 1);
        public static Sprite BuyBtnNormal => Plate("sh_bn", 48, 32, 0, new Color32(0x8D, 0x98, 0xA2, 0xEE), new Color32(0x5E, 0x69, 0x73, 0xEE), new Color32(0xBC, 0xC6, 0xCE, 0xE0), 1);
        public static Sprite BuyBtnDisabled => Plate("sh_bd", 48, 32, 0, new Color32(0x3D, 0x45, 0x4E, 0xB8), new Color32(0x2B, 0x31, 0x38, 0xB8), new Color32(0x55, 0x60, 0x6A, 0x99), 1);

        // Circular HUD graphics, gradients & icons
        public static Sprite TimerRing => Get("tm_ring", BuildTimerRing);
        public static Sprite RadarDish => Get("rd_dish", BuildRadarDish);
        public static Sprite RadarFloorplan => Get("rd_floor", BuildRadarFloorplan);
        public static Sprite RadarVisionCone => Get("rd_cone", BuildRadarCone);
        public static Sprite PlayerArrow => Get("pl_arr", BuildPlayerArrow);
        public static Sprite CircleDot => Get("c_dot", BuildCircleDot);
        public static Sprite KillRowGrad => Get("kf_grad", () => RenderTex(128, 4, (x, y) => new Color(0.04f, 0.06f, 0.09f, Mathf.SmoothStep(0f, 0.42f, x / 127f))));
        public static Sprite DoubleGoldRule => Get("dg_rule", () => RenderTex(160, 6, (x, y) =>
        {
            if (y != 1 && y != 4) return Color.clear;
            float u = x / 159f;
            float f = Mathf.Min(Mathf.Clamp01(u * 5f), Mathf.Clamp01((1f - u) * 5f));
            return new Color(0.84f, 0.68f, 0.34f, (y == 4 ? 0.85f : 0.45f) * f);
        }));

        public static Sprite HeartIcon => Get("i_heart", () => RenderIcon((u, v) =>
        {
            float d1 = Vector2.Distance(new Vector2(Mathf.Abs(u), v), new Vector2(0.36f, 0.28f)) - 0.40f;
            float tri = Mathf.Abs(u) * 0.92f - (v + 0.68f) * 0.82f;
            return v < -0.74f ? 0f : Mathf.Clamp01(0.5f - (v > 0.14f ? d1 : Mathf.Min(d1, tri)) * 16f);
        }));

        public static Sprite ShieldIcon => Get("i_shield", () => RenderIcon((u, v) =>
        {
            float ax = Mathf.Abs(u), top = 0.78f - ax * 0.12f;
            float w = v > 0.05f ? 0.72f : Mathf.Lerp(0f, 0.72f, Mathf.Clamp01((v + 0.86f) / 0.91f));
            float oA = Mathf.Clamp01(0.5f - Mathf.Max(ax - w, Mathf.Max(v - top, -0.84f - v)) * 22f);
            float iA = Mathf.Clamp01(0.5f - Mathf.Max(ax - (w - 0.18f), Mathf.Max(v - (top - 0.18f), -0.64f - v)) * 22f);
            return Mathf.Clamp01(Mathf.Max(Mathf.Clamp01(oA - iA * 0.75f), Mathf.Clamp01(0.5f - (ax - 0.36f) * 20f) * iA * 0.85f));
        }));

        public static Sprite SkullIcon => Get("i_skull", () => RenderIcon((u, v) =>
        {
            float ax = Mathf.Abs(u);
            float dome = Vector2.Distance(new Vector2(u * 0.92f, v - 0.16f), Vector2.zero) - 0.64f;
            float jaw = Mathf.Max(ax - 0.38f, Mathf.Max(v, -0.70f - v));
            float body = Mathf.Clamp01(0.5f - Mathf.Min(dome, jaw) * 22f);
            float eye = Vector2.Distance(new Vector2(ax - 0.25f, v - 0.08f), Vector2.zero) - 0.17f;
            float nose = Mathf.Max(ax - 0.08f, Mathf.Abs(v + 0.18f) - 0.12f);
            float teeth = (v < -0.42f && v > -0.72f && (Mathf.Abs(ax - 0.13f) < 0.035f || ax < 0.03f)) ? 0f : 1f;
            return body * Mathf.Min(Mathf.Clamp01(eye * 22f + 0.5f), Mathf.Clamp01(nose * 24f + 0.5f)) * teeth;
        }));

        public static Sprite TombstoneIcon => Get("i_tomb", () => RenderIcon((u, v) =>
        {
            float ax = Mathf.Abs(u);
            float arch = Vector2.Distance(new Vector2(ax, Mathf.Max(0f, v - 0.20f)), Vector2.zero) - 0.48f;
            float rect = Mathf.Max(ax - 0.48f, Mathf.Max(v - 0.20f, -0.58f - v));
            float slab = Mathf.Max(ax - 0.64f, Mathf.Abs(v + 0.68f) - 0.12f);
            float body = Mathf.Clamp01(0.5f - Mathf.Min(Mathf.Min(arch, rect), slab) * 22f);
            float cV = Mathf.Max(ax - 0.07f, Mathf.Abs(v - 0.18f) - 0.26f);
            float cH = Mathf.Max(ax - 0.22f, Mathf.Abs(v - 0.26f) - 0.07f);
            return body * Mathf.Clamp01(Mathf.Min(cV, cH) * 24f + 0.5f);
        }));

        public static Sprite StarIcon => Get("i_star", () => RenderIcon((u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v), sec = 2f * Mathf.PI / 5f;
            float loc = Mathf.Abs(((Mathf.Atan2(u, v) % sec) + sec) % sec - sec * 0.5f);
            return Mathf.Clamp01(0.5f + (Mathf.Lerp(0.84f, 0.34f, loc / (sec * 0.5f)) - r) * 20f);
        }));

        public static Sprite TrophyIcon => Get("i_trophy", () => RenderIcon((u, v) =>
        {
            float ax = Mathf.Abs(u);
            float bW = Mathf.Lerp(0.24f, 0.52f, Mathf.Clamp01((v + 0.10f) / 0.85f));
            float bowl = Mathf.Max(ax - bW, Mathf.Max(v - 0.72f, -0.14f - v));
            float hD = Vector2.Distance(new Vector2(ax - 0.52f, v - 0.38f), Vector2.zero);
            float handles = Mathf.Max(hD - 0.24f, -(hD - 0.13f));
            float stem = Mathf.Max(ax - 0.11f, Mathf.Max(v + 0.12f, -0.54f - v));
            float ped = Mathf.Max(ax - 0.44f, Mathf.Abs(v + 0.64f) - 0.10f);
            return Mathf.Clamp01(0.5f - Mathf.Min(Mathf.Min(bowl, handles), Mathf.Min(stem, ped)) * 22f);
        }));

        public static Sprite CoinsIcon => Get("i_coins", () => RenderTex(48, 48, (x, y) =>
        {
            float u = (x + 0.5f) / 24f - 1f, v = (y + 0.5f) / 24f - 1f, a = 0f, sh = 1f;
            for (int i = 0; i < 5; i++)
            {
                float e = Vector2.Distance(new Vector2((u - 0.18f) * 1.15f, (v - (-0.52f + i * 0.26f)) * 2.9f), Vector2.zero) - 0.58f;
                float c = Mathf.Clamp01(0.5f - e * 18f);
                if (c > 0.01f) { a = Mathf.Max(a, c); sh = e > -0.14f ? 0.80f : 1f; }
            }
            for (int i = 0; i < 3; i++)
            {
                float e = Vector2.Distance(new Vector2((u + 0.34f) * 1.25f, (v - (-0.58f + i * 0.24f)) * 2.9f), Vector2.zero) - 0.50f;
                float c = Mathf.Clamp01(0.5f - e * 18f);
                if (c > 0.01f) { a = Mathf.Max(a, c); sh = e > -0.14f ? 0.74f : 0.96f; }
            }
            return new Color(sh, sh, sh, a);
        }));

        // Shared UI helpers
        public static Image Icon(Transform parent, string name, Sprite sp, Vector2 size, Color color)
        {
            var rt = UiKit.NewRect(name, parent);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static void AddShadow(Graphic g, float alpha = 0.75f)
        {
            var sh = g.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, alpha);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
        }

        public static void CreateTopLeftBrand(Transform parent, Vector2 pos)
        {
            var root = UiKit.NewRect("BrandHeader", parent);
            UiKit.Anchor(root, new Vector2(0f, 1f), new Vector2(0f, 1f), pos);
            root.sizeDelta = new Vector2(236f, 58f);

            var crest = UiKit.LoadSprite("Adventure3D/UI/EagleCrest");
            if (crest != null)
            {
                var c = Icon(root, "Crest", crest, new Vector2(54f, 54f), Color.white);
                UiKit.Anchor(c.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero);
            }

            var title = UiKit.Label(root, Strings.Get("app.title"), 28, TextAnchor.MiddleLeft, 36f);
            title.Target.color = GoldSoft;
            title.Target.fontStyle = FontStyle.Bold;
            title.Target.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddShadow(title.Target);
            var tRt = (RectTransform)title.transform;
            UiKit.Anchor(tRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 5f));
            tRt.sizeDelta = new Vector2(170f, 36f);

            var rule = Icon(root, "GoldRule", DoubleGoldRule, new Vector2(138f, 6f), Color.white);
            rule.preserveAspect = false;
            UiKit.Anchor(rule.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, -15f));
        }

        public static void CreateModalTitle(Transform parent, string titleLogical, float topOffset = -26f)
        {
            var row = UiKit.NewRect("ModalTitleRow", parent);
            UiKit.Anchor(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, topOffset));
            row.sizeDelta = new Vector2(460f, 64f);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = false;

            var crest = UiKit.LoadSprite("Adventure3D/UI/EagleCrest");
            if (crest != null) Icon(row, "TitleCrest", crest, new Vector2(62f, 62f), Color.white);

            var lbl = UiKit.Label(row, titleLogical, 42, TextAnchor.MiddleCenter, 54f);
            lbl.Target.color = GoldSoft;
            lbl.Target.fontStyle = FontStyle.Bold;
            lbl.Target.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddShadow(lbl.Target);
            ((RectTransform)lbl.transform).sizeDelta = new Vector2(280f, 54f);
        }

        static Sprite RenderTex(int w, int h, Func<int, int, Color> fn)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, fn(x, y));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite RenderIcon(Func<float, float, float> alphaFn) =>
            RenderTex(48, 48, (x, y) => new Color(1f, 1f, 1f, alphaFn((x + 0.5f) / 24f - 1f, (y + 0.5f) / 24f - 1f)));

        static Sprite BuildTimerRing()
        {
            const int S = 128; float c = 64f, outer = 60f, inner = 56.8f;
            var fill = new Color32(0x10, 0x15, 0x1C, 0xC8);
            var rim = new Color32(0xC9, 0x9F, 0x4A, 0xF5);
            var sub = new Color32(0x94, 0x76, 0x3A, 0x70);
            var pip = new Color32(0xF6, 0xD2, 0x72, 0xFF);
            return RenderTex(S, S, (x, y) =>
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c, r = Mathf.Sqrt(dx * dx + dy * dy);
                Color px = Color.clear;
                if (r < outer + 1.2f)
                {
                    float aa = Mathf.Clamp01(outer + 0.8f - r);
                    if (r >= inner) px = new Color(rim.r / 255f, rim.g / 255f, rim.b / 255f, (rim.a / 255f) * aa);
                    else { px = fill; float ir = Mathf.Abs(r - (inner - 5.5f)); if (ir < 1.1f) px = Color.Lerp(px, sub, 1f - ir / 1.1f); }
                }
                float pd = Mathf.Abs(dx) + Mathf.Abs(y + 0.5f - (c + outer - 1.5f));
                if (pd < 5.5f) px = Color.Lerp(px, pip, Mathf.Clamp01((5.5f - pd) * 0.7f));
                return px;
            });
        }

        static Sprite BuildRadarDish()
        {
            const int S = 256; float c = 128f, outer = 123f, inner = 119.4f;
            var bg = new Color32(0x13, 0x17, 0x1D, 0xEA);
            var mid = new Color32(0x1A, 0x20, 0x27, 0xEC);
            var rim = new Color32(0xC5, 0x9C, 0x49, 0xF2);
            var ring = new Color32(0x88, 0x70, 0x3E, 0x55);
            var pip = new Color32(0xF5, 0xCF, 0x70, 0xFF);
            return RenderTex(S, S, (x, y) =>
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c, r = Mathf.Sqrt(dx * dx + dy * dy);
                Color px = Color.clear;
                if (r < outer + 1.2f)
                {
                    float aa = Mathf.Clamp01(outer + 0.8f - r);
                    if (r >= inner) px = new Color(rim.r / 255f, rim.g / 255f, rim.b / 255f, (rim.a / 255f) * aa);
                    else
                    {
                        px = Color.Lerp(mid, bg, Mathf.Clamp01(r / inner) * 0.85f);
                        float m1 = Mathf.Abs(r - inner * 0.52f); if (m1 < 1f) px = Color.Lerp(px, ring, 1f - m1);
                        float m2 = Mathf.Abs(r - (inner - 4f)); if (m2 < 1f) px = Color.Lerp(px, ring, 1f - m2);
                    }
                }
                float ny = (c + outer + 1f) - (y + 0.5f);
                if (ny >= -2f && ny <= 7f && Mathf.Abs(dx) <= (7f - ny) * 0.9f) px = pip;
                return px;
            });
        }

        static Sprite BuildRadarFloorplan()
        {
            const int S = 224; float c = 112f, maxR = 108f;
            var fl = new Color32(0x36, 0x40, 0x4A, 0x9E);
            var cr = new Color32(0x28, 0x30, 0x38, 0x8E);
            var wl = new Color32(0x88, 0x94, 0xA0, 0xB8);
            var rooms = new[] { new Rect(0.24f, 0.22f, 0.24f, 0.34f), new Rect(0.46f, 0.32f, 0.28f, 0.22f), new Rect(0.32f, 0.54f, 0.22f, 0.26f), new Rect(0.54f, 0.54f, 0.20f, 0.22f), new Rect(0.28f, 0.16f, 0.40f, 0.12f), new Rect(0.42f, 0.20f, 0.10f, 0.62f) };
            var cuts = new[] { new Rect(0.44f, 0.42f, 0.08f, 0.10f), new Rect(0.60f, 0.36f, 0.08f, 0.12f) };
            return RenderTex(S, S, (x, y) =>
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                if (r > maxR) return Color.clear;
                float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                bool inR = false, onW = false;
                foreach (var rc in rooms) if (rc.Contains(new Vector2(u, v))) { inR = true; if (Mathf.Min(Mathf.Min(u - rc.xMin, rc.xMax - u), Mathf.Min(v - rc.yMin, rc.yMax - v)) < 0.011f) onW = true; }
                foreach (var rc in cuts) if (rc.Contains(new Vector2(u, v))) { inR = false; if (Mathf.Min(Mathf.Min(u - rc.xMin, rc.xMax - u), Mathf.Min(v - rc.yMin, rc.yMax - v)) < 0.010f) onW = true; }
                Color px = onW ? wl : (inR ? (((x / 12 + y / 12) % 2 == 0) ? fl : cr) : Color.clear);
                px.a *= Mathf.Clamp01((maxR - r) * 0.25f);
                return px;
            });
        }

        static Sprite BuildRadarCone() => RenderTex(224, 224, (x, y) =>
        {
            float dx = x + 0.5f - 112f, dy = y + 0.5f - 112f, r = Mathf.Sqrt(dx * dx + dy * dy);
            if (dy <= 0f || r < 4f || r > 102f) return Color.clear;
            float ang = Mathf.Abs(Mathf.Atan2(dx, dy)) * Mathf.Rad2Deg;
            return new Color(0.92f, 0.78f, 0.46f, Mathf.Clamp01((30f - ang) / 8f) * Mathf.Clamp01(1f - r / 102f) * 0.42f);
        });

        static Sprite BuildPlayerArrow() => RenderTex(32, 32, (x, y) =>
        {
            float px = x + 0.5f - 16f, py = (y + 0.5f) / 32f;
            float hw = Mathf.Lerp(10.5f, 0f, Mathf.Clamp01((py - 0.16f) / 0.72f));
            float notch = 0.16f + Mathf.Clamp01(1f - Mathf.Abs(px) / 6f) * 0.10f;
            if (py < notch || py > 0.88f || Mathf.Abs(px) > hw) return Color.clear;
            return Mathf.Min(hw - Mathf.Abs(px), (py - notch) * 32f) < 1.4f ? new Color(0.05f, 0.14f, 0.08f, 0.92f) : Color.white;
        });

        static Sprite BuildCircleDot() => RenderTex(24, 24, (x, y) =>
        {
            float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(12f, 12f));
            if (r >= 9.5f) return Color.clear;
            return r > 7.2f ? new Color(0.05f, 0.07f, 0.10f, 0.85f * Mathf.Clamp01(9.5f - r)) : Color.white;
        });
    }
}
