using System.Collections.Generic;
using System.Globalization;
using Adventure3D.Core;
using Adventure3D.Gameplay;
using Adventure3D.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Reactive layer on top of the HUD:
    ///  - dynamic crosshair whose gap follows the real weapon spread (moving, jumping, firing) and
    ///    that collapses when aiming down sights; radial reload progress around it;
    ///  - floating damage numbers at the hit player's head (gold for headshots);
    ///  - name tags with health bars: allies always, enemies when under the crosshair and visible;
    ///  - multi-kill and kill-streak banners with sound;
    ///  - low-health heartbeat pulse and low-ammo warning.
    /// </summary>
    public sealed class HudInteractive : MonoBehaviour
    {
        GameSession _session;
        FirstPersonViewModel _viewModel;
        Camera _camera;
        RectTransform _root;
        AudioSource _audio;

        // crosshair
        RectTransform[] _ticks;
        Image[] _tickImages;
        Image _dot;
        Image _reloadRing;
        float _gap = 8f;

        // damage numbers
        struct Floater { public RectTransform Rt; public Text T; public Vector3 World; public float Life, Max; public Color C; }
        readonly List<Floater> _floaters = new List<Floater>();
        readonly Stack<Text> _floaterPool = new Stack<Text>();

        // name tags
        sealed class Tag { public RectTransform Rt; public ArabicText Name; public Image Bar, BarBg; public ushort Id; }
        readonly List<Tag> _tags = new List<Tag>();
        static readonly RaycastHit[] Hits = new RaycastHit[16];

        // banners and warnings
        ArabicText _banner, _warning;
        float _bannerTimer, _bannerScale;
        float _heartTimer;
        Image _lowHealth;
        int _streak, _multi;
        float _lastKillTime = -10f;
        float _reloadElapsed;
        bool _wasReloading;
        string _lastWarning;

        public static HudInteractive Create(GameSession session, ArenaHud hud, FirstPersonViewModel viewModel, Camera camera)
        {
            var go = new GameObject("HudInteractive", typeof(RectTransform));
            go.transform.SetParent(hud.SafeRootRect, false);
            var h = go.AddComponent<HudInteractive>();
            h._session = session;
            h._viewModel = viewModel;
            h._camera = camera;
            h._root = (RectTransform)go.transform;
            UiKit.Stretch(h._root);
            h._audio = go.AddComponent<AudioSource>();
            h._audio.spatialBlend = 0f;
            h.Build(hud);
            session.CombatEventRaised += h.OnEvent;
            return h;
        }

        void OnDestroy()
        {
            if (_session != null) _session.CombatEventRaised -= OnEvent;
        }

        void Build(ArenaHud hud)
        {
            // The static crosshair ticks of the base HUD are replaced; its hit marker stays.
            var oldCross = hud.SafeRootRect.Find("Crosshair");
            if (oldCross != null)
                foreach (Transform c in oldCross)
                    if (c.name == "Tick") c.gameObject.SetActive(false);

            var cs = TacticalSettings.Current;
            Color cc = cs.GetCrosshairColor();
            _ticks = new RectTransform[4];
            _tickImages = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var rt = UiKit.NewRect("DynTick", _root);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = i < 2 ? new Vector2(2.5f, 10f) : new Vector2(10f, 2.5f);
                var img = rt.gameObject.AddComponent<Image>();
                img.color = cc;
                img.raycastTarget = false;
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.8f);
                o.effectDistance = new Vector2(1f, -1f);
                _ticks[i] = rt;
                _tickImages[i] = img;
            }
            var dotRt = UiKit.NewRect("DynDot", _root);
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = new Vector2(3f, 3f);
            _dot = dotRt.gameObject.AddComponent<Image>();
            _dot.color = cc;
            _dot.raycastTarget = false;

            var ringRt = UiKit.NewRect("ReloadRing", _root);
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(46f, 46f);
            _reloadRing = ringRt.gameObject.AddComponent<Image>();
            _reloadRing.sprite = RingSprite();
            _reloadRing.type = Image.Type.Filled;
            _reloadRing.fillMethod = Image.FillMethod.Radial360;
            _reloadRing.fillOrigin = (int)Image.Origin360.Top;
            _reloadRing.color = new Color(1f, 0.82f, 0.4f, 0.9f);
            _reloadRing.raycastTarget = false;

            _banner = UiKit.Label(_root, "", 46, TextAnchor.MiddleCenter, 64f);
            var brt = (RectTransform)_banner.transform;
            UiKit.Anchor(brt, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -190f));
            brt.sizeDelta = new Vector2(900f, 64f);
            _banner.Target.color = new Color(1f, 0.78f, 0.25f);
            _banner.Target.fontStyle = FontStyle.Bold;
            var bo = _banner.gameObject.AddComponent<Outline>();
            bo.effectColor = new Color(0.25f, 0.08f, 0f, 0.9f);
            bo.effectDistance = new Vector2(2f, -2f);
            _banner.gameObject.SetActive(false);

            _warning = UiKit.Label(_root, "", 24, TextAnchor.MiddleCenter, 34f);
            var wrt = (RectTransform)_warning.transform;
            UiKit.Anchor(wrt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f));
            wrt.sizeDelta = new Vector2(700f, 34f);
            _warning.Target.fontStyle = FontStyle.Bold;
            _warning.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            _warning.gameObject.SetActive(false);

            var lh = UiKit.NewRect("LowHealthPulse", _root);
            UiKit.Stretch(lh);
            lh.SetAsFirstSibling();
            _lowHealth = lh.gameObject.AddComponent<Image>();
            _lowHealth.sprite = EdgeSprite();
            _lowHealth.color = new Color(0.6f, 0f, 0f, 0f);
            _lowHealth.raycastTarget = false;
        }

        // ------------------------------------------------------------------ events

        void OnEvent(CombatEvent e)
        {
            ushort me = _session.LocalPlayerId;
            if (e.Type == CombatEventType.Damage && e.Actor == me && e.Target != me && _session.World.TryGet(e.Target, out var victim))
                SpawnFloater(victim.State.Position + Vector3.up * 2f, e.Amount, e.Headshot);

            if (e.Type == CombatEventType.Kill)
            {
                if (e.Target == me) { _streak = 0; _multi = 0; }
                else if (e.Actor == me)
                {
                    _streak++;
                    _multi = Time.unscaledTime - _lastKillTime < 4f ? _multi + 1 : 1;
                    _lastKillTime = Time.unscaledTime;
                    string text = _multi >= 4 ? Strings.Get("combat.multi_kill")
                        : _multi == 3 ? Strings.Get("combat.triple_kill")
                        : _multi == 2 ? Strings.Get("combat.double_kill")
                        : _streak >= 3 ? Strings.Format("combat.streak", _streak)
                        : null;
                    if (text != null)
                    {
                        _banner.SetText(text);
                        _bannerTimer = 2.2f;
                        _bannerScale = 1.6f;
                        _audio.PlayOneShot(ProceduralAudio.Streak, 0.7f);
                    }
                }
            }
            if (e.Type == CombatEventType.RoundRestart) { _streak = 0; _multi = 0; }
        }

        void SpawnFloater(Vector3 world, int amount, bool head)
        {
            var t = _floaterPool.Count > 0 ? _floaterPool.Pop() : NewFloater();
            t.gameObject.SetActive(true);
            t.text = amount.ToString(CultureInfo.InvariantCulture);
            t.fontSize = head ? 34 : 26;
            var c = head ? new Color(1f, 0.75f, 0.2f) : Color.white;
            t.color = c;
            world += new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
            _floaters.Add(new Floater { Rt = (RectTransform)t.transform, T = t, World = world, Life = 0.9f, Max = 0.9f, C = c });
        }

        Text NewFloater()
        {
            var t = UiKit.LtrLabel(_root, "", 26, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;
            var rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 40f);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            return t;
        }

        // ------------------------------------------------------------------ per frame

        void LateUpdate()
        {
            if (_session == null || _camera == null) return;
            float dt = Time.unscaledDeltaTime;
            var local = _session.LocalAvatar;
            bool alive = local != null && local.Alive;
            var c = alive ? local.Combat : default;
            var w = WeaponCatalog.Get(c.ActiveWeapon);

            // ---- crosshair from the real spread
            float aim = _viewModel != null ? _viewModel.AimBlend : 0f;
            bool show = alive && aim < 0.5f && !(_viewModel != null && _viewModel.ScopeActive);
            float spread = alive ? CombatRules.CurrentSpread(local, w) : 0f;
            float halfH = _root.rect.height * 0.5f;
            float targetGap = 5f + Mathf.Tan(spread * Mathf.Deg2Rad) / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * halfH;
            if (w.IsKnife) targetGap = 4f;
            _gap = Mathf.Lerp(_gap, Mathf.Clamp(targetGap, 4f, 120f), 1f - Mathf.Exp(-18f * dt));
            float len = w.Class == WeaponClass.Sniper ? 6f : 10f;
            _ticks[0].anchoredPosition = new Vector2(0f, _gap + len * 0.5f);
            _ticks[1].anchoredPosition = new Vector2(0f, -_gap - len * 0.5f);
            _ticks[2].anchoredPosition = new Vector2(-_gap - len * 0.5f, 0f);
            _ticks[3].anchoredPosition = new Vector2(_gap + len * 0.5f, 0f);
            for (int i = 0; i < 4; i++) _tickImages[i].enabled = show && !w.IsKnife;
            _dot.enabled = show;

            // ---- reload progress
            bool reloading = alive && c.Reloading;
            if (reloading && !_wasReloading) _reloadElapsed = 0f;
            _wasReloading = reloading;
            if (reloading) _reloadElapsed += dt;
            _reloadRing.enabled = reloading;
            if (reloading) _reloadRing.fillAmount = Mathf.Clamp01(_reloadElapsed / Mathf.Max(0.1f, w.ReloadTime));

            UpdateFloaters(dt);
            UpdateTags(local);

            // ---- banner punch animation
            _bannerTimer -= dt;
            _banner.gameObject.SetActive(_bannerTimer > 0f);
            if (_bannerTimer > 0f)
            {
                _bannerScale = Mathf.Lerp(_bannerScale, 1f, 1f - Mathf.Exp(-12f * dt));
                _banner.transform.localScale = Vector3.one * _bannerScale;
                var bc = _banner.Target.color;
                bc.a = Mathf.Clamp01(_bannerTimer / 0.4f);
                _banner.Target.color = bc;
            }

            // ---- low health pulse + heartbeat, low ammo warning
            bool lowHp = alive && c.Health <= 30;
            float pulse = lowHp ? (0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 6.5f)) * (1f - c.Health / 40f) : 0f;
            _lowHealth.color = new Color(0.6f, 0f, 0f, Mathf.MoveTowards(_lowHealth.color.a, pulse, dt * 3f));
            if (lowHp)
            {
                _heartTimer -= dt;
                if (_heartTimer <= 0f) { _audio.PlayOneShot(ProceduralAudio.Heartbeat, 0.6f); _heartTimer = 0.95f; }
            }

            string warning = null;
            Color warnColor = Color.white;
            if (lowHp) { warning = Strings.Get("combat.low_health"); warnColor = new Color(1f, 0.35f, 0.3f); }
            else if (alive && w.UsesAmmo && !reloading && c.ActiveClip <= Mathf.Max(1, w.ClipSize / 4) && c.ActiveReserve > 0)
            { warning = Strings.Get("combat.low_ammo"); warnColor = new Color(1f, 0.8f, 0.3f); }
            _warning.gameObject.SetActive(warning != null);
            if (warning != null)
            {
                if (warning != _lastWarning) _warning.SetText(warning);
                warnColor.a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f);
                _warning.Target.color = warnColor;
            }
            _lastWarning = warning;
        }

        bool ToCanvas(Vector3 world, out Vector2 pos)
        {
            pos = default;
            Vector3 sp = _camera.WorldToScreenPoint(world);
            if (sp.z <= 0.1f) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out pos);
        }

        void UpdateFloaters(float dt)
        {
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i];
                f.Life -= dt;
                f.World += Vector3.up * dt * 0.8f;
                bool visible = ToCanvas(f.World, out var pos);
                if (f.Life <= 0f)
                {
                    f.T.gameObject.SetActive(false);
                    _floaterPool.Push(f.T);
                    _floaters.RemoveAt(i);
                    continue;
                }
                f.T.enabled = visible;
                f.Rt.anchoredPosition = pos;
                float k = f.Life / f.Max;
                f.Rt.localScale = Vector3.one * (1f + (1f - k) * 0.3f);
                var col = f.C;
                col.a = Mathf.Clamp01(k * 1.5f);
                f.T.color = col;
                _floaters[i] = f;
            }
        }

        void UpdateTags(PlayerAvatar local)
        {
            int used = 0;
            if (local != null && local.Alive)
            {
                Vector3 eye = _camera.transform.position;
                Vector3 fwd = _camera.transform.forward;
                foreach (var p in _session.World.Players)
                {
                    if (p == null || p == local || !p.Alive) continue;
                    Vector3 head = p.ViewRoot.position + Vector3.up * 2.15f;
                    Vector3 to = head - eye;
                    float dist = to.magnitude;
                    if (dist > 45f) continue;
                    bool ally = p.Combat.Team == local.Combat.Team;
                    if (!ally)
                    {
                        float ang = Vector3.Angle(fwd, (p.ViewRoot.position + Vector3.up * 1.2f) - eye);
                        if (ang > 5f || !Visible(eye, p)) continue;
                    }
                    if (!ToCanvas(head, out var pos)) continue;
                    var tag = used < _tags.Count ? _tags[used] : NewTag();
                    used++;
                    tag.Rt.gameObject.SetActive(true);
                    tag.Rt.anchoredPosition = pos;
                    float scale = Mathf.Clamp(14f / Mathf.Max(dist, 1f), 0.55f, 1.1f);
                    tag.Rt.localScale = Vector3.one * scale;
                    if (tag.Id != p.PlayerId) { tag.Name.SetText(_session.NameOf(p.PlayerId)); tag.Id = p.PlayerId; }
                    var col = ally ? new Color(0.45f, 0.75f, 1f) : new Color(1f, 0.35f, 0.3f);
                    tag.Name.Target.color = col;
                    tag.Bar.fillAmount = p.Combat.Health / 100f;
                    tag.Bar.color = col;
                }
            }
            for (int i = used; i < _tags.Count; i++) _tags[i].Rt.gameObject.SetActive(false);
        }

        static bool Visible(Vector3 eye, PlayerAvatar target)
        {
            Vector3 aim = target.ViewRoot.position + Vector3.up * 1.2f;
            Vector3 d = aim - eye;
            int n = Physics.RaycastNonAlloc(eye, d.normalized, Hits, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i].collider;
                if (col == target.Controller) continue;
                if (col.GetComponent<PlayerAvatar>() != null) continue;
                return false;
            }
            return true;
        }

        Tag NewTag()
        {
            var rt = UiKit.NewRect("NameTag", _root);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 40f);
            var name = UiKit.Label(rt, "", 20, TextAnchor.MiddleCenter, 26f);
            var nrt = (RectTransform)name.transform;
            UiKit.Anchor(nrt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero);
            nrt.sizeDelta = new Vector2(200f, 26f);
            name.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            var bgRt = UiKit.NewRect("BarBg", rt);
            UiKit.Anchor(bgRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f));
            bgRt.sizeDelta = new Vector2(70f, 6f);
            var bg = bgRt.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;
            var barRt = UiKit.NewRect("Bar", bgRt);
            UiKit.Stretch(barRt);
            var bar = barRt.gameObject.AddComponent<Image>();
            bar.sprite = MenuStyle.White;
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.raycastTarget = false;
            var tag = new Tag { Rt = rt, Name = name, Bar = bar, BarBg = bg, Id = 0 };
            _tags.Add(tag);
            return tag;
        }

        // ------------------------------------------------------------------ generated sprites

        static Sprite _ring, _edge;

        static Sprite RingSprite()
        {
            if (_ring != null) return _ring;
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.08f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return _ring = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite EdgeSprite()
        {
            if (_edge != null) return _edge;
            const int size = 256;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nx = Mathf.Abs(x / (size - 1f) - 0.5f) * 2f, ny = Mathf.Abs(y / (size - 1f) - 0.5f) * 2f;
                    float d = Mathf.Max(nx, ny);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.6f) / 0.4f))));
                }
            t.Apply();
            return _edge = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
