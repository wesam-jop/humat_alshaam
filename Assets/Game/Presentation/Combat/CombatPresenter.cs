using System.Collections.Generic;
using Adventure3D.Core;
using Adventure3D.Gameplay;
using Adventure3D.Input;
using Adventure3D.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Connects real combat state to everything the player sees and hears: HUD vitals, ammo, weapon,
    /// money, kill feed, hit markers, damage flash and direction, death / respawn panel, sniper scope,
    /// tracers, impacts, remote muzzle flashes and 3D gunfire, team colours and weapons on other
    /// players. Lives only in processes with a local player (never on the dedicated server).
    /// </summary>
    public sealed class CombatPresenter : MonoBehaviour
    {
        GameSession _session;
        ArenaHud _hud;
        FirstPersonViewModel _viewModel;
        CombatFx _fx;
        Camera _camera;
        AudioSource _ui;

        Image _damageFlash, _scope, _damageArrow;
        RectTransform _damageArrowRt;
        ArabicText _center, _deathTitle, _deathTimer, _reloadHint;
        GameObject _deathPanel;
        float _centerTimer, _damageTimer, _arrowTimer;
        Vector3 _lastDamageFrom;

        readonly Dictionary<ushort, byte> _worldWeapon = new Dictionary<ushort, byte>();
        readonly Dictionary<ushort, GameObject> _worldWeaponGo = new Dictionary<ushort, GameObject>();
        readonly Dictionary<PlayerAvatar, string> _appliedSkinKey = new Dictionary<PlayerAvatar, string>();

        /// <summary>Character model fallback: Resources/Adventure3D/Characters/Operative (GLB via glTFast).</summary>
        public const string CharacterResource = CharacterCatalog.FallbackResource;
        /// <summary>Rotation applied to the model so it faces the player's forward direction.</summary>
        public static float CharacterYawOffset = 0f;
        int _lastHealth = 100;
        string _lastWeaponKey;

        public static readonly Color DefendersColor = new Color(0.26f, 0.32f, 0.2f);
        public static readonly Color AttackersColor = new Color(0.46f, 0.18f, 0.13f);

        public static CombatPresenter Create(GameSession session, ArenaHud hud, FirstPersonViewModel viewModel, Camera camera)
        {
            var go = new GameObject("CombatPresenter");
            var p = go.AddComponent<CombatPresenter>();
            p._session = session;
            p._hud = hud;
            p._viewModel = viewModel;
            p._camera = camera;
            p._fx = CombatFx.Create(camera);
            p._fx.transform.SetParent(go.transform, false);
            p._ui = go.AddComponent<AudioSource>();
            p._ui.spatialBlend = 0f;
            p.BuildOverlays();
            session.CombatEventRaised += p.OnCombatEvent;
            HudInteractive.Create(session, hud, viewModel, camera);
            TacticalGrenadeSystem.Create(session, hud.Input, camera, hud.SafeRootRect);
            return p;
        }

        void OnDestroy()
        {
            if (_session != null) _session.CombatEventRaised -= OnCombatEvent;
        }

        // ------------------------------------------------------------------ overlays

        void BuildOverlays()
        {
            var root = _hud.SafeRootRect;
            var canvas = root.parent;

            _scope = FullScreen(canvas, "ScopeOverlay", ScopeSprite(), Color.white);
            _scope.transform.SetSiblingIndex(0);
            _scope.gameObject.SetActive(false);
            var scopeFit = _scope.gameObject.AddComponent<AspectRatioFitter>();
            scopeFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            scopeFit.aspectRatio = 1f;

            _damageFlash = FullScreen(canvas, "DamageFlash", VignetteSprite(), new Color(0.7f, 0f, 0f, 0f));
            _damageFlash.transform.SetSiblingIndex(1);

            _damageArrowRt = UiKit.NewRect("DamageDirection", root);
            _damageArrowRt.anchorMin = _damageArrowRt.anchorMax = new Vector2(0.5f, 0.5f);
            _damageArrowRt.sizeDelta = new Vector2(320f, 320f);
            var arrowChild = UiKit.NewRect("Arrow", _damageArrowRt);
            arrowChild.anchorMin = arrowChild.anchorMax = new Vector2(0.5f, 1f);
            arrowChild.sizeDelta = new Vector2(90f, 26f);
            _damageArrow = arrowChild.gameObject.AddComponent<Image>();
            _damageArrow.sprite = ArcSprite();
            _damageArrow.color = new Color(0.9f, 0.1f, 0.05f, 0f);
            _damageArrow.raycastTarget = false;

            _center = UiKit.Label(root, "", 34, TextAnchor.MiddleCenter, 50f);
            var crt = (RectTransform)_center.transform;
            UiKit.Anchor(crt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f));
            crt.sizeDelta = new Vector2(900f, 50f);
            _center.Target.color = TacticalHudArt.GoldBright;
            _center.Target.fontStyle = FontStyle.Bold;
            TacticalHudArt.AddShadow(_center.Target);

            _reloadHint = UiKit.Label(root, Strings.Get("combat.reloading"), 24, TextAnchor.MiddleCenter, 34f);
            var rrt = (RectTransform)_reloadHint.transform;
            UiKit.Anchor(rrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f));
            rrt.sizeDelta = new Vector2(400f, 34f);
            _reloadHint.Target.color = TacticalHudArt.Cream;
            TacticalHudArt.AddShadow(_reloadHint.Target);
            _reloadHint.gameObject.SetActive(false);

            var panel = MenuStyle.Shape(root, "DeathPanel", MenuStyle.Panel);
            panel.raycastTarget = false;
            UiKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f));
            panel.rectTransform.sizeDelta = new Vector2(640f, 150f);
            UiKit.Column(panel.rectTransform, 6, 22);
            _deathTitle = UiKit.Label(panel.transform, "", 34, TextAnchor.MiddleCenter, 48f);
            _deathTitle.Target.color = TacticalHudArt.RadarEnemy;
            _deathTitle.Target.fontStyle = FontStyle.Bold;
            _deathTimer = UiKit.Label(panel.transform, "", 26, TextAnchor.MiddleCenter, 40f);
            _deathTimer.Target.color = TacticalHudArt.Cream;
            _deathPanel = panel.gameObject;
            _deathPanel.SetActive(false);
        }

        static Image FullScreen(Transform parent, string name, Sprite sprite, Color color)
        {
            var rt = UiKit.NewRect(name, parent);
            UiKit.Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        void ShowCenter(string text, float seconds)
        {
            _center.SetText(text);
            _centerTimer = seconds;
        }

        // ------------------------------------------------------------------ events

        void OnCombatEvent(CombatEvent e)
        {
            ushort me = _session.LocalPlayerId;
            var w = WeaponCatalog.Get(e.Weapon);
            switch (e.Type)
            {
                case CombatEventType.Shot:
                {
                    bool mine = e.Actor == me;
                    Vector3 from = e.Origin;
                    if (mine)
                    {
                        _viewModel?.OnLocalShot(e.Weapon);
                        from = _camera.transform.position + _camera.transform.right * 0.14f - _camera.transform.up * 0.1f + _camera.transform.forward * 0.6f;
                    }
                    else if (_session.World.TryGet(e.Actor, out var shooter))
                    {
                        from = shooter.ViewRoot.position + Vector3.up * 1.4f + shooter.ViewRoot.forward * 0.7f + shooter.ViewRoot.right * 0.18f;
                        if (!w.IsKnife)
                        {
                            _fx.MuzzleFlash(from);
                            AudioSource.PlayClipAtPoint(ProceduralAudio.Shot(w.Id, w.Class), from, 0.85f);
                        }
                    }
                    if (w.IsKnife) break;
                    if (!mine || Random.value < 0.6f) _fx.Tracer(from, e.Point, w.Class == WeaponClass.Sniper);
                    if (e.HitPlayer) _fx.Blood(e.Point, e.Point - e.Origin);
                    else if (e.Normal != Vector3.zero) _fx.Impact(e.Point, e.Normal);
                    break;
                }
                case CombatEventType.Damage:
                    if (e.Actor == me && e.Target != me)
                    {
                        _hud.FlashHitmarker(e.Headshot);
                        _ui.PlayOneShot(e.Headshot ? ProceduralAudio.Headshot : ProceduralAudio.Hit, 0.7f);
                    }
                    if (e.Target == me)
                    {
                        _damageTimer = Mathf.Clamp01(e.Amount / 60f) * 0.5f + 0.35f;
                        _lastDamageFrom = e.Origin;
                        _arrowTimer = 1.2f;
                        bool fall = e.Actor == e.Target;
                        _ui.PlayOneShot(fall ? ProceduralAudio.FallPain() : ProceduralAudio.Hurt, 0.65f);
                    }
                    else if (_session.World.TryGet(e.Target, out var hurtPlayer))
                    {
                        var clip = e.Actor == e.Target ? ProceduralAudio.FallPain()
                            : hurtPlayer.Combat.Armor > 0 ? ProceduralAudio.ArmorHit(e.Headshot)
                            : ProceduralAudio.FleshHit();
                        if (clip != null) AudioSource.PlayClipAtPoint(clip, e.Point, 0.7f);
                    }
                    break;
                case CombatEventType.Kill:
                {
                    var killerTeam = TeamOf(e.Actor);
                    var victimTeam = TeamOf(e.Target);
                    string weaponIcon = w.Id;
                    _hud.PushKillEvent(_session.NameOf(e.Actor), killerTeam == Team.Defenders, weaponIcon, _session.NameOf(e.Target), victimTeam == Team.Defenders);
                    var deathClip = ProceduralAudio.Death();
                    if (deathClip != null) AudioSource.PlayClipAtPoint(deathClip, e.Point, 0.8f);
                    if (e.Actor == me && e.Target != me)
                    {
                        ShowCenter(Strings.Format("combat.kill_confirm", _session.NameOf(e.Target)) + (e.Headshot ? " • " + Strings.Get("combat.headshot") : ""), 2f);
                        _ui.PlayOneShot(ProceduralAudio.Kill, 0.6f);
                    }
                    if (e.Target == me)
                        _deathTitle.SetText(e.Actor == me ? Strings.Get("combat.suicide") : Strings.Format("combat.killed_by", _session.NameOf(e.Actor)));
                    break;
                }
                case CombatEventType.DryFire:
                    if (e.Actor == me) _ui.PlayOneShot(ProceduralAudio.DryFire, 0.8f);
                    break;
                case CombatEventType.RoundRestart:
                    ShowCenter(Strings.Get("combat.round_restart"), 2.5f);
                    _ui.PlayOneShot(ProceduralAudio.RoundGo, 0.7f);
                    break;
                case CombatEventType.RoundWin:
                {
                    bool defWon = e.Actor == (ushort)Team.Defenders;
                    ShowCenter(defWon ? "حماة الشام انتصروا في الجولة!" : "الفريق المنافس انتصر في الجولة!", 4f);
                    _ui.PlayOneShot(ProceduralAudio.RoundResult(defWon), 0.85f);
                    break;
                }
                case CombatEventType.BombPlanted:
                    ShowCenter("تم زرع القنبلة C4!", 3.5f);
                    _ui.PlayOneShot(ProceduralAudio.BombPlanted, 0.85f);
                    break;
                case CombatEventType.BombDefused:
                    ShowCenter("تم تفكيك القنبلة بنجاح!", 3.5f);
                    _ui.PlayOneShot(ProceduralAudio.BombDefused, 0.85f);
                    break;
                case CombatEventType.BombExploded:
                {
                    ShowCenter("انفجرت القنبلة C4!", 3.5f);
                    var exp = Resources.Load<AudioClip>("Adventure3D/Audio/Weapons/c4_explode1");
                    if (exp != null) AudioSource.PlayClipAtPoint(exp, e.Point, 1f);
                    break;
                }
            }
        }

        Team TeamOf(ushort id) => _session.World.TryGet(id, out var a) ? a.Combat.Team : Team.Attackers;

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (_session == null || _hud == null) return;
            float dt = Time.unscaledDeltaTime;
            var local = _session.LocalAvatar;

            if (local != null)
            {
                var c = local.Combat;
                _hud.SetVitals(c.Health, c.Armor);
                _hud.SetMoney(c.Money);
                var w = WeaponCatalog.Get(c.ActiveWeapon);
                int clip = w.UsesAmmo ? c.ActiveClip : 0, reserve = w.UsesAmmo ? c.ActiveReserve : 0;
                string key = w.Id + "|" + clip + "|" + reserve;
                if (key != _lastWeaponKey)
                {
                    _lastWeaponKey = key;
                    _hud.SetWeaponState(w.Id, w.NameAr, clip, reserve);
                }
                if (c.Health < _lastHealth && c.Alive) _damageTimer = Mathf.Max(_damageTimer, 0.35f);
                _lastHealth = c.Health;

                bool dead = !c.Alive;
                _deathPanel.SetActive(dead && !_hud.PauseOpen);
                if (dead) _deathTimer.SetText(Strings.Format("combat.respawn_in", Mathf.CeilToInt(Mathf.Max(0f, c.RespawnTimer))));
                _reloadHint.gameObject.SetActive(c.Alive && c.Reloading);
                if (c.Alive && !c.Reloading && w.UsesAmmo && clip == 0 && reserve == 0 && _centerTimer <= 0f)
                    ShowCenter(Strings.Get("combat.no_ammo"), 0.5f);
            }

            // Damage flash and direction
            _damageTimer = Mathf.Max(0f, _damageTimer - dt);
            var df = _damageFlash.color;
            df.a = Mathf.Clamp01(_damageTimer * 1.4f) * 0.75f;
            _damageFlash.color = df;
            _arrowTimer = Mathf.Max(0f, _arrowTimer - dt);
            if (_arrowTimer > 0f && local != null)
            {
                Vector3 to = _lastDamageFrom - local.State.Position;
                float ang = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg - _camera.transform.eulerAngles.y;
                _damageArrowRt.localRotation = Quaternion.Euler(0f, 0f, -ang);
            }
            _damageArrow.color = new Color(0.9f, 0.1f, 0.05f, Mathf.Clamp01(_arrowTimer) * 0.9f);

            // Centre messages
            _centerTimer -= dt;
            _center.gameObject.SetActive(_centerTimer > 0f);

            // Sniper scope
            bool scope = _viewModel != null && _viewModel.ScopeActive;
            if (_scope.gameObject.activeSelf != scope) _scope.gameObject.SetActive(scope);

            UpdateWorldWeapons();
            UpdateFootsteps(dt);
            if (_session.PlantProgress > 0.01f)
                ShowCenter($"جاري زرع القنبلة... {Mathf.RoundToInt(_session.PlantProgress * 100f)}%", 0.15f);
            else if (_session.DefuseProgress > 0.01f)
                ShowCenter($"جاري تفكيك القنبلة... {Mathf.RoundToInt(_session.DefuseProgress * 100f)}%", 0.15f);
        }

        readonly Dictionary<ushort, float> _stepTimer = new Dictionary<ushort, float>();

        void UpdateFootsteps(float dt)
        {
            foreach (var a in _session.World.Players)
            {
                if (a == null || !a.Alive || !a.State.Grounded || a.State.Crouching) continue;
                float speed = a.State.HorizontalSpeed;
                if (speed < 3.4f) continue; // silent walk threshold
                _stepTimer.TryGetValue(a.PlayerId, out float t);
                t -= dt;
                if (t <= 0f)
                {
                    t = speed > 6.0f ? 0.30f : 0.42f;
                    var clip = ProceduralAudio.Footstep();
                    if (clip != null)
                    {
                        if (a.IsLocal) _ui.PlayOneShot(clip, 0.32f);
                        else AudioSource.PlayClipAtPoint(clip, a.State.Position, 0.55f);
                    }
                }
                _stepTimer[a.PlayerId] = t;
            }
        }

        void UpdateWorldWeapons()
        {
            var quality = (CharacterQuality)TacticalSettings.Current.CharacterQuality;
            foreach (var a in _session.World.Players)
            {
                if (a == null || a.IsLocal) continue;
                var charSpec = CharacterCatalog.GetForBot(a.Combat.Team, a.PlayerId);
                string skinKey = $"{charSpec.Id}_{(int)quality}";
                bool hasModel = _appliedSkinKey.TryGetValue(a, out var prevKey);
                if (!hasModel || prevKey != skinKey)
                {
                    var prefab = CharacterCatalog.LoadModelPrefab(charSpec.Id, quality);
                    if (prefab != null)
                    {
                        a.ReplaceBody(prefab, a.Controller.height, CharacterYawOffset);
                        _appliedSkinKey[a] = skinKey;
                        hasModel = true;
                    }
                    else if (!hasModel)
                    {
                        _appliedSkinKey[a] = string.Empty;
                        a.SetBodyColor(a.Combat.Team == Team.Defenders ? DefendersColor : AttackersColor);
                    }
                }
                byte weapon = a.Combat.ActiveWeapon;
                if (!_worldWeapon.TryGetValue(a.PlayerId, out var shown) || shown != weapon || !_worldWeaponGo.ContainsKey(a.PlayerId) || _worldWeaponGo[a.PlayerId] == null)
                {
                    if (_worldWeaponGo.TryGetValue(a.PlayerId, out var old) && old != null) Destroy(old);
                    var go = ViewModelBuilder.BuildWorldWeapon(WeaponCatalog.Get(weapon), a.ViewRoot);
                    go.transform.localPosition = new Vector3(0.2f, 1.12f, 0.3f);
                    _worldWeaponGo[a.PlayerId] = go;
                    _worldWeapon[a.PlayerId] = weapon;
                    if (!hasModel)
                        a.SetBodyColor(a.Combat.Team == Team.Defenders ? DefendersColor : AttackersColor);
                }
                // Aim the weapon with the player's pitch.
                var g = _worldWeaponGo[a.PlayerId];
                if (g != null) g.transform.localRotation = Quaternion.Euler(a.State.Pitch, 0f, 0f);
            }
        }

        // ------------------------------------------------------------------ generated overlay art

        static Sprite _scopeSprite, _vignette, _arc;

        static Sprite ScopeSprite()
        {
            if (_scopeSprite != null) return _scopeSprite;
            const int size = 512;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                    float a = Mathf.Clamp01((d - 0.9f) / 0.02f);                 // black outside the lens
                    bool line = (Mathf.Abs(x - c) < 1.2f || Mathf.Abs(y - c) < 1.2f) && d < 0.9f;
                    bool thick = (Mathf.Abs(x - c) < 3f || Mathf.Abs(y - c) < 3f) && d > 0.45f && d < 0.9f;
                    if (line || thick) a = 1f;
                    t.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            t.Apply();
            return _scopeSprite = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite VignetteSprite()
        {
            if (_vignette != null) return _vignette;
            const int size = 256;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.45f) / 0.6f))));
                }
            t.Apply();
            return _vignette = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite ArcSprite()
        {
            if (_arc != null) return _arc;
            const int w = 128, h = 32;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - w / 2f) / (w / 2f);
                    float curve = (1f - nx * nx) * 0.6f;
                    float ny = y / (float)h;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(ny - curve) * 6f) * (1f - Mathf.Abs(nx));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return _arc = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }
    }
}
