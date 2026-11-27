using System;
using System.Collections.Generic;
using System.Text;
using Adventure3D.Core;
using Adventure3D.Input;
using Adventure3D.Networking;
using Adventure3D.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Live tactical combat HUD: top-left Eagle brand + circular radar, top-right borderless kill feed,
    /// bottom-left chamfered vitals, bottom-center circular gold timer, and bottom-right weapon + gold ammo plate.
    /// </summary>
    public sealed class ArenaHud : MonoBehaviour
    {
        public struct RadarContact
        {
            public Vector2 LocalOffset;
            public bool IsAlly;
        }

        struct KillFeedRow
        {
            public GameObject Root;
            public ArabicText Killer, Victim;
            public Image WeaponIcon;
        }

        const int MaxKillRows = 4, MaxRadarBlips = 8;

        GameSession _session;
        LocalPlayerInput _input;
        Action _leaveToMenu;
        Text _listen, _netGraphText, _hpText, _armorText, _timerText, _ammoClipText, _ammoReserveText;
        RectTransform _pause, _radarFloorRt;
        ArabicText _pauseTitle, _weaponNameText;
        Button _resume, _leave;
        MessageDialog _dialog;
        LoadingOverlay _loading;
        MotdModal _motd;
        TeamSelectModal _teamSelect;
        BuyMenuModal _buyMenu;
        ScoreboardModal _scoreboard;
        TacticalConsole _console;
        SettingsModal _settings;
        Image _timerRingImg, _weaponIconImg;
        Image[] _hitmarkerTicks;
        readonly Image[] _radarBlips = new Image[MaxRadarBlips];
        readonly KillFeedRow[] _killRows = new KillFeedRow[MaxKillRows];

        int _health = 100, _armor = 100, _money = 4500, _ammo = 30, _reserve = 90;
        string _weaponIconId = "ak47", _weaponDisplayName = "كلاشنكوف AK-47";
        float _roundTime = 90f, _hitmarkerTimer, _fpsSmooth = 60f;
        bool _ended;
        List<string> _listenAddresses;
        ushort _listenPortShown;
        IReadOnlyList<RadarContact> _customRadarContacts;

        static readonly RadarContact[] DefaultSquadContacts =
        {
            new RadarContact { LocalOffset = new Vector2(-0.18f, -0.05f), IsAlly = true },
            new RadarContact { LocalOffset = new Vector2(-0.22f, -0.14f), IsAlly = true },
            new RadarContact { LocalOffset = new Vector2(-0.34f, -0.24f), IsAlly = true },
            new RadarContact { LocalOffset = new Vector2(-0.04f, -0.50f), IsAlly = true },
            new RadarContact { LocalOffset = new Vector2(0.08f, -0.52f), IsAlly = true },
            new RadarContact { LocalOffset = new Vector2(0.18f, -0.18f), IsAlly = false },
        };

        public bool PauseOpen => _pause != null && _pause.gameObject.activeSelf;
        public MessageDialog Dialog => _dialog;
        public Button ResumeButton => _resume;
        public Button LeaveButton => _leave;
        public string ListenText => _listen != null ? _listen.text : "";
        public GameSession Session => _session;
        public bool AnyMenuOpen => AnyModalOpen;
        public RectTransform SafeRootRect { get; private set; }

        public static ArenaHud Create(GameSession session, LocalPlayerInput input, Action leaveToMenu)
        {
            var canvas = UiKit.CreateCanvas("ArenaHudCanvas", 10);
            var hud = canvas.gameObject.AddComponent<ArenaHud>();
            hud._session = session;
            hud._input = input;
            hud._leaveToMenu = leaveToMenu;
            hud.Build(canvas);
            return hud;
        }

        void Build(Canvas canvas)
        {
            UiKit.EnsureEventSystem();
            var safe = UiKit.SafeRoot(canvas.transform);
            SafeRootRect = safe;

            TacticalHudArt.CreateTopLeftBrand(safe, new Vector2(22f, -16f));
            BuildRadar(safe);
            BuildKillFeed(safe);
            BuildNetworkListenBadge(safe);
            BuildVitalsPlate(safe);
            BuildRoundTimer(safe);
            BuildWeaponAndAmmo(safe);
            BuildCrosshair(safe);

            _netGraphText = UiKit.LtrLabel(safe, "FPS: 60", 15, TextAnchor.LowerRight);
            _netGraphText.color = TacticalHudArt.RadarGreen;
            UiKit.Anchor((RectTransform)_netGraphText.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 152f));
            _netGraphText.gameObject.SetActive(TacticalSettings.Current.NetGraph);

            if (PlatformInfo.UseTouchUi) TouchControlsView.Create(safe, _input);

            BuildPauseMenu(safe);
            _dialog = MessageDialog.Create(safe);
            _loading = LoadingOverlay.Create(safe, Leave);
            _motd = MotdModal.Create(safe, () => _teamSelect.Show());
            _teamSelect = TeamSelectModal.Create(safe, OnTeamAndCharacterSelected);
            _buyMenu = BuyMenuModal.Create(safe, OnWeaponBought);
            _scoreboard = ScoreboardModal.Create(safe);
            _console = TacticalConsole.Create(safe, _session, Leave);
            _settings = SettingsModal.Create(safe);

            RefreshWeaponDisplay();
            RefreshVitalsDisplay();

            if (_input != null) _input.MenuPressed += TogglePause;
            _session.StatusChanged += OnStatusChanged;
            ApplyCursor();
        }

        void BuildRadar(Transform safe)
        {
            var radar = UiKit.NewRect("TacticalRadar", safe);
            UiKit.Anchor(radar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -80f));
            radar.sizeDelta = new Vector2(212f, 212f);
            var dish = radar.gameObject.AddComponent<Image>();
            dish.sprite = TacticalHudArt.RadarDish;
            dish.raycastTarget = false;

            _radarFloorRt = TacticalHudArt.Icon(radar, "Floorplan", TacticalHudArt.RadarFloorplan, new Vector2(190f, 190f), Color.white).rectTransform;
            TacticalHudArt.Icon(radar, "VisionCone", TacticalHudArt.RadarVisionCone, new Vector2(190f, 190f), Color.white);

            for (int i = 0; i < MaxRadarBlips; i++)
            {
                _radarBlips[i] = TacticalHudArt.Icon(radar, "Blip_" + i, TacticalHudArt.CircleDot, new Vector2(12f, 12f), TacticalHudArt.RadarAlly);
                _radarBlips[i].gameObject.SetActive(false);
            }
            TacticalHudArt.Icon(radar, "PlayerArrow", TacticalHudArt.PlayerArrow, new Vector2(18f, 18f), TacticalHudArt.RadarGreen);
        }

        void BuildKillFeed(Transform safe)
        {
            var feed = UiKit.NewRect("KillFeed", safe);
            UiKit.Anchor(feed, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -20f));
            feed.sizeDelta = new Vector2(430f, 152f);
            var v = feed.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.childControlWidth = true;
            v.childControlHeight = false;

            for (int i = 0; i < MaxKillRows; i++)
            {
                var rowRt = UiKit.NewRect("KillRow_" + i, feed);
                UiKit.Fit(rowRt, 32f);
                var bg = rowRt.gameObject.AddComponent<Image>();
                bg.sprite = TacticalHudArt.KillRowGrad;
                bg.raycastTarget = false;

                var h = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(10, 8, 2, 2);
                h.spacing = 12f;
                h.childAlignment = TextAnchor.MiddleRight;
                h.childControlWidth = false;
                h.childControlHeight = false;

                var victim = UiKit.Label(rowRt, "", 20, TextAnchor.MiddleRight, 28f);
                victim.Target.horizontalOverflow = HorizontalWrapMode.Overflow;
                TacticalHudArt.AddShadow(victim.Target);
                ((RectTransform)victim.transform).sizeDelta = new Vector2(160f, 28f);

                var wImg = TacticalHudArt.Icon(rowRt, "Weapon", null, new Vector2(74f, 26f), TacticalHudArt.Gold);

                var killer = UiKit.Label(rowRt, "", 20, TextAnchor.MiddleRight, 28f);
                killer.Target.horizontalOverflow = HorizontalWrapMode.Overflow;
                TacticalHudArt.AddShadow(killer.Target);
                ((RectTransform)killer.transform).sizeDelta = new Vector2(124f, 28f);

                rowRt.gameObject.SetActive(false);
                _killRows[i] = new KillFeedRow { Root = rowRt.gameObject, Killer = killer, WeaponIcon = wImg, Victim = victim };
            }
        }

        void BuildNetworkListenBadge(Transform safe)
        {
            var rt = UiKit.NewRect("ListenBadge", safe);
            UiKit.Anchor(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f));
            rt.sizeDelta = new Vector2(360f, 34f);
            _listen = UiKit.LtrLabel(rt, "", 16, TextAnchor.MiddleCenter);
            _listen.color = TacticalHudArt.GoldSoft;
            TacticalHudArt.AddShadow(_listen);
            UiKit.Stretch((RectTransform)_listen.transform);

            if (_session.Mode == SessionMode.Host) _listenAddresses = AddressTools.LocalIPv4Addresses();
            else if (_session.Mode == SessionMode.Client) _listen.text = _session.Request.Address + ":" + _session.Request.Port;
            else rt.gameObject.SetActive(false);
        }

        void BuildVitalsPlate(Transform safe)
        {
            var plate = MenuStyle.Shape(safe, "VitalsPlate", TacticalHudArt.VitalsPlate);
            plate.raycastTarget = false;
            UiKit.Anchor(plate.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24f, 24f));
            plate.rectTransform.sizeDelta = new Vector2(272f, 60f);

            var h = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(18, 22, 6, 6);
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = false;

            TacticalHudArt.Icon(plate.transform, "Heart", TacticalHudArt.HeartIcon, new Vector2(26f, 26f), TacticalHudArt.Cream);
            _hpText = UiKit.LtrLabel(plate.transform, "100", 32, TextAnchor.MiddleLeft);
            _hpText.color = TacticalHudArt.Cream;
            _hpText.fontStyle = FontStyle.Bold;
            TacticalHudArt.AddShadow(_hpText);
            ((RectTransform)_hpText.transform).sizeDelta = new Vector2(78f, 42f);

            TacticalHudArt.Icon(plate.transform, "Shield", TacticalHudArt.ShieldIcon, new Vector2(26f, 26f), TacticalHudArt.Cream);
            _armorText = UiKit.LtrLabel(plate.transform, "100", 32, TextAnchor.MiddleLeft);
            _armorText.color = TacticalHudArt.Cream;
            _armorText.fontStyle = FontStyle.Bold;
            TacticalHudArt.AddShadow(_armorText);
            ((RectTransform)_armorText.transform).sizeDelta = new Vector2(74f, 42f);
        }

        void BuildRoundTimer(Transform safe)
        {
            var rt = UiKit.NewRect("RoundTimer", safe);
            UiKit.Anchor(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f));
            rt.sizeDelta = new Vector2(104f, 104f);
            _timerRingImg = rt.gameObject.AddComponent<Image>();
            _timerRingImg.sprite = TacticalHudArt.TimerRing;
            _timerRingImg.raycastTarget = false;

            _timerText = UiKit.LtrLabel(rt, "01:30", 26, TextAnchor.MiddleCenter);
            _timerText.color = TacticalHudArt.Cream;
            _timerText.fontStyle = FontStyle.Bold;
            TacticalHudArt.AddShadow(_timerText);
            UiKit.Stretch((RectTransform)_timerText.transform);
        }

        void BuildWeaponAndAmmo(Transform safe)
        {
            var wrap = UiKit.NewRect("WeaponHud", safe);
            UiKit.Anchor(wrap, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f));
            wrap.sizeDelta = new Vector2(276f, 134f);

            _weaponNameText = UiKit.Label(wrap, _weaponDisplayName, 19, TextAnchor.MiddleRight, 26f);
            _weaponNameText.Target.color = TacticalHudArt.Cream;
            _weaponNameText.Target.horizontalOverflow = HorizontalWrapMode.Overflow;
            TacticalHudArt.AddShadow(_weaponNameText.Target);
            var nRt = (RectTransform)_weaponNameText.transform;
            UiKit.Anchor(nRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, 0f));
            nRt.sizeDelta = new Vector2(260f, 26f);

            _weaponIconImg = TacticalHudArt.Icon(wrap, "WeaponSilhouette", null, new Vector2(142f, 44f), TacticalHudArt.Gold);
            UiKit.Anchor(_weaponIconImg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -26f));

            var plate = MenuStyle.Shape(wrap, "AmmoPlate", TacticalHudArt.AmmoPlate);
            plate.raycastTarget = false;
            UiKit.Anchor(plate.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero);
            plate.rectTransform.sizeDelta = new Vector2(272f, 56f);

            var row = UiKit.NewRect("AmmoRow", plate.transform);
            UiKit.Stretch(row);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(32, 22, 4, 4);
            h.spacing = 4f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = false;

            _ammoClipText = UiKit.LtrLabel(row, "30", 34, TextAnchor.MiddleRight);
            _ammoClipText.color = new Color32(0xFF, 0xEC, 0xB8, 0xFF);
            _ammoClipText.fontStyle = FontStyle.Bold;
            TacticalHudArt.AddShadow(_ammoClipText);
            ((RectTransform)_ammoClipText.transform).sizeDelta = new Vector2(64f, 44f);

            _ammoReserveText = UiKit.LtrLabel(row, "/ 90", 23, TextAnchor.MiddleLeft);
            _ammoReserveText.color = TacticalHudArt.GoldSoft;
            _ammoReserveText.fontStyle = FontStyle.Bold;
            TacticalHudArt.AddShadow(_ammoReserveText);
            ((RectTransform)_ammoReserveText.transform).sizeDelta = new Vector2(76f, 36f);
        }

        void BuildCrosshair(Transform safe)
        {
            var cross = UiKit.NewRect("Crosshair", safe);
            cross.sizeDelta = new Vector2(36f, 36f);
            var cs = TacticalSettings.Current;
            Color cc = cs.GetCrosshairColor();
            cc.a = cs.CrosshairTranslucent ? 0.85f : 1f;
            float len = 5f + cs.CrosshairSize * 1.5f, off = 6f + cs.CrosshairSize * 1.2f;

            AddCrossTick(cross, new Vector2(0f, off), new Vector2(2f, len), cc);
            AddCrossTick(cross, new Vector2(0f, -off), new Vector2(2f, len), cc);
            AddCrossTick(cross, new Vector2(-off, 0f), new Vector2(len, 2f), cc);
            AddCrossTick(cross, new Vector2(off, 0f), new Vector2(len, 2f), cc);
            AddCrossTick(cross, Vector2.zero, new Vector2(2.5f, 2.5f), cc);

            _hitmarkerTicks = new Image[4];
            Vector2[] dirs = { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) };
            for (int i = 0; i < 4; i++)
            {
                var hm = UiKit.NewRect("Hit_" + i, cross);
                hm.anchoredPosition = dirs[i] * 8f;
                hm.sizeDelta = new Vector2(10f, 2f);
                hm.localRotation = Quaternion.Euler(0f, 0f, (i == 0 || i == 3) ? 45f : -45f);
                var img = hm.gameObject.AddComponent<Image>();
                img.color = Color.clear;
                img.raycastTarget = false;
                _hitmarkerTicks[i] = img;
            }
        }

        void BuildPauseMenu(Transform safe)
        {
            _pause = UiKit.NewRect("PauseMenu", safe);
            UiKit.Stretch(_pause);
            _pause.gameObject.AddComponent<Image>().color = TacticalHudArt.OverlayBackdrop;
            var pausePanel = UiKit.Panel("Panel", _pause, new Vector2(720, 620));
            UiKit.Column(pausePanel, 12, 32);
            _pauseTitle = UiKit.Label(pausePanel, "", 36, TextAnchor.MiddleCenter);
            _pauseTitle.Target.color = MenuStyle.Gold;
            _pauseTitle.Target.fontStyle = FontStyle.Bold;
            _resume = UiKit.Button(pausePanel, Strings.Get("pause.resume"), () => SetPaused(false), 58f);
            var teamBtn = UiKit.Button(pausePanel, Strings.Get("ingame.pause.team"), () => { SetPaused(false); _teamSelect.Show(); }, 58f);
            var playersBtn = UiKit.Button(pausePanel, Strings.Get("ingame.pause.players"), () => _dialog.Show(Strings.Get("ingame.pause.players"), $"{TacticalSettings.Current.PlayerName} (Host)", _resume), 58f);
            var settingsBtn = UiKit.Button(pausePanel, Strings.Get("ingame.pause.settings"), () => _settings.Open(_resume), 58f);
            _leave = UiKit.Button(pausePanel, Strings.Get("ingame.pause.disconnect"), Leave, 58f);
            UiFocus.ChainVertical(new List<Selectable> { _resume, teamBtn, playersBtn, settingsBtn, _leave });
            _pause.gameObject.SetActive(false);
        }

        static void AddCrossTick(RectTransform parent, Vector2 offset, Vector2 size, Color color)
        {
            var rt = UiKit.NewRect("Tick", parent);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.03f, 0.05f, 0.07f, 0.85f);
            o.effectDistance = new Vector2(1f, -1f);
        }

        public void SetVitals(int health, int armor)
        {
            _health = Mathf.Clamp(health, 0, 100);
            _armor = Mathf.Clamp(armor, 0, 100);
            RefreshVitalsDisplay();
        }

        public void SetWeaponState(string iconId, string displayName, int ammo, int reserve)
        {
            if (!string.IsNullOrEmpty(iconId)) _weaponIconId = iconId;
            if (!string.IsNullOrEmpty(displayName)) _weaponDisplayName = displayName;
            _ammo = Mathf.Max(0, ammo);
            _reserve = Mathf.Max(0, reserve);
            RefreshWeaponDisplay();
        }

        public void SetMoney(int money) => _money = Mathf.Max(0, money);
        public void SetRadarContacts(IReadOnlyList<RadarContact> contacts) => _customRadarContacts = contacts;

        public void FlashHitmarker(bool headshot = false)
        {
            _hitmarkerTimer = 0.16f;
            Color c = headshot ? TacticalHudArt.RadarEnemy : TacticalHudArt.Cream;
            if (_hitmarkerTicks != null) foreach (var t in _hitmarkerTicks) if (t != null) t.color = c;
        }

        public void PushKillEvent(string killer, bool killerIsDef, string weaponIcon, string victim, bool victimIsDef)
        {
            for (int i = MaxKillRows - 1; i > 0; i--)
            {
                var s = _killRows[i - 1]; var d = _killRows[i];
                d.Root.SetActive(s.Root.activeSelf);
                d.Killer.SetText(s.Killer.Logical); d.Killer.Target.color = s.Killer.Target.color;
                d.Victim.SetText(s.Victim.Logical); d.Victim.Target.color = s.Victim.Target.color;
                d.WeaponIcon.sprite = s.WeaponIcon.sprite; d.WeaponIcon.color = s.WeaponIcon.color; d.WeaponIcon.enabled = s.WeaponIcon.enabled;
                _killRows[i] = d;
            }
            var top = _killRows[0];
            top.Root.SetActive(true);
            top.Killer.SetText(killer);
            top.Killer.Target.color = killerIsDef ? TacticalHudArt.GoldSoft : TacticalHudArt.AllyBlue;
            top.Victim.SetText(victim);
            top.Victim.Target.color = victimIsDef ? TacticalHudArt.GoldSoft : TacticalHudArt.AllyBlue;
            var sp = !string.IsNullOrEmpty(weaponIcon) ? UiKit.LoadSprite("Adventure3D/Weapons/" + weaponIcon) : null;
            top.WeaponIcon.sprite = sp;
            top.WeaponIcon.enabled = sp != null;
            top.WeaponIcon.color = weaponIcon == "knife" ? new Color32(0xA4, 0xB2, 0xBE, 0xF0) : TacticalHudArt.Gold;
            _killRows[0] = top;
        }

        void SeedInitialKillFeed()
        {
            PushKillEvent("طارق", false, "hegrenade", "سامر", true);
            PushKillEvent("أبو العز", true, "awp", "قنّاص_الساحل", false);
            PushKillEvent("رامي", true, "knife", "مجهول_123", false);
            PushKillEvent("أسد", true, "ak47", "معتز", false);
        }

        /// <summary>The purchase is sent to the authority (offline / host / server), which checks the money
        /// and gives the item; the HUD then shows the real result through the next state update.</summary>
        void OnTeamAndCharacterSelected(int team, string charId)
        {
            if (_session != null && _session.LocalAvatar != null && (team == 0 || team == 1))
                _session.LocalAvatar.Combat.Team = (Team)team;
            ApplyCursor();
        }

        void OnWeaponBought(WeaponSpec w)
        {
            string item = w.Category == 6 ? "ammo_primary" : w.Category == 7 ? "ammo_secondary" : w.Icon;
            _session.RequestBuy(item);
            ApplyCursor();
        }

        void RefreshVitalsDisplay()
        {
            if (_hpText != null)
            {
                _hpText.text = _health.ToString();
                _hpText.color = _health <= 25 ? TacticalHudArt.RadarEnemy : (_health <= 50 ? TacticalHudArt.GoldBright : TacticalHudArt.Cream);
            }
            if (_armorText != null) _armorText.text = _armor.ToString();
        }

        void RefreshWeaponDisplay()
        {
            if (_weaponNameText != null) _weaponNameText.SetText(_weaponDisplayName);
            if (_weaponIconImg != null)
            {
                var sp = UiKit.LoadSprite("Adventure3D/Weapons/" + _weaponIconId);
                _weaponIconImg.sprite = sp;
                _weaponIconImg.enabled = sp != null;
            }
            if (_ammoClipText != null) _ammoClipText.text = _ammo.ToString();
            if (_ammoReserveText != null) _ammoReserveText.text = "/ " + _reserve;
        }

        void UpdateRadar()
        {
            var local = _session != null ? _session.LocalAvatar : null;
            float yaw = local != null ? local.transform.eulerAngles.y : 0f;
            if (_radarFloorRt != null) _radarFloorRt.localRotation = Quaternion.Euler(0f, 0f, yaw);

            const float rMax = 78f;
            int idx = 0;
            if (_customRadarContacts != null)
            {
                for (int i = 0; i < _customRadarContacts.Count && idx < MaxRadarBlips; i++)
                    PlaceBlip(idx++, _customRadarContacts[i].LocalOffset * rMax, _customRadarContacts[i].IsAlly);
            }
            else if (_session != null && local != null && _session.PlayerCount > 1)
            {
                Vector3 origin = local.transform.position;
                float rad = -yaw * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
                foreach (var p in _session.World.Players)
                {
                    if (p == null || p == local || idx >= MaxRadarBlips) continue;
                    Vector3 d = p.transform.position - origin;
                    Vector2 pos = Vector2.ClampMagnitude(new Vector2((d.x * cos - d.z * sin) / 36f, (d.x * sin + d.z * cos) / 36f), 0.92f) * rMax;
                    if (!p.Alive) continue;
                    PlaceBlip(idx++, pos, p.Combat.Team == local.Combat.Team);
                }
            }

            for (int i = idx; i < MaxRadarBlips; i++) _radarBlips[i].gameObject.SetActive(false);
        }

        void PlaceBlip(int slot, Vector2 pos, bool isAlly)
        {
            var b = _radarBlips[slot];
            b.gameObject.SetActive(true);
            b.rectTransform.anchoredPosition = pos;
            b.color = isAlly ? TacticalHudArt.RadarAlly : TacticalHudArt.RadarEnemy;
        }

        void OnDestroy()
        {
            if (_input != null) _input.MenuPressed -= TogglePause;
            if (_session != null) _session.StatusChanged -= OnStatusChanged;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnStatusChanged(SessionStatus status, SessionEndReason reason)
        {
            if (status != SessionStatus.Ended || _ended) return;
            _ended = true;
            if (reason == SessionEndReason.UserLeft) return;
            _pause.gameObject.SetActive(false);
            if (_input != null) _input.InputEnabled = false;
            ApplyCursor(forceUnlock: true);
            string title = _session.Mode == SessionMode.Client && reason != SessionEndReason.ServerClosed && reason != SessionEndReason.ConnectionLost
                ? Strings.Get("error.title") : Strings.Get("error.ended_title");
            _dialog.Show(title, EndReasonText.Message(reason), null, () => _leaveToMenu?.Invoke());
        }

        public void TogglePause()
        {
            if (_ended || _dialog.IsOpen) return;
            if (_buyMenu != null && _buyMenu.IsOpen) { _buyMenu.Hide(); ApplyCursor(); return; }
            if (_teamSelect != null && _teamSelect.IsOpen) { _teamSelect.Hide(); ApplyCursor(); return; }
            SetPaused(!PauseOpen);
        }

        public void SetPaused(bool paused)
        {
            if (_ended) return;
            _pause.gameObject.SetActive(paused);
            if (paused) _pause.SetAsLastSibling(); // the pause menu covers every other HUD layer (death panel, banners, tags)
            bool offline = _session.Mode == SessionMode.Offline;
            _pauseTitle.SetText(Strings.Get(offline ? "pause.title" : "pause.title_network"));
            if (offline) _session.Paused = paused;
            _session.LocalInputSuspended = paused;
            if (_input != null) _input.InputEnabled = !paused;
            if (paused) UiFocus.PushDialog(_resume);
            else UiFocus.Clear();
            ApplyCursor();
        }

        void Leave() { _ended = true; _leaveToMenu?.Invoke(); }

        bool AnyModalOpen => PauseOpen || (_dialog != null && _dialog.IsOpen)
            || (_motd != null && _motd.IsOpen) || (_teamSelect != null && _teamSelect.IsOpen)
            || (_buyMenu != null && _buyMenu.IsOpen) || (_console != null && _console.IsOpen)
            || (_settings != null && _settings.IsOpen);

        void ApplyCursor(bool forceUnlock = false)
        {
            bool lockIt = !forceUnlock && PlatformInfo.UsesMouseLook && !AnyModalOpen && !_ended;
            Cursor.lockState = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockIt;
            if (_input != null) _input.MouseLookActive = lockIt || Application.isBatchMode;
        }

        void RefreshListen()
        {
            if (_listenAddresses == null || _session.ListenPort == 0 || _session.ListenPort == _listenPortShown) return;
            _listenPortShown = _session.ListenPort;
            var sb = new StringBuilder();
            foreach (var ip in _listenAddresses) sb.Append(ip).Append(':').Append(_listenPortShown).Append('\n');
            _listen.text = sb.ToString().TrimEnd('\n');
        }

        void Update()
        {
            if (_session == null) return;
            RefreshListen();
            UpdateRadar();

            bool connecting = _session.Status == SessionStatus.Connecting || _session.Status == SessionStatus.Handshaking;
            if (_loading != null)
            {
                if (connecting && !_loading.IsOpen) _loading.Show();
                else if (!connecting && _loading.IsOpen) _loading.Hide();
            }

            _roundTime = _session.MatchTimeRemaining; // real match clock (authority) or server clock (client)
            int mins = Mathf.FloorToInt(_roundTime / 60f), secs = Mathf.FloorToInt(_roundTime % 60f);
            if (_timerText != null)
            {
                _timerText.text = $"{mins:00}:{secs:00}";
                bool urgent = _roundTime <= 15f && _roundTime > 0f;
                _timerText.color = urgent ? TacticalHudArt.RadarEnemy : TacticalHudArt.Cream;
                if (_timerRingImg != null)
                    _timerRingImg.color = urgent ? Color.Lerp(Color.white, TacticalHudArt.RadarEnemy, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : Color.white;
            }

            if (_hitmarkerTimer > 0f)
            {
                _hitmarkerTimer -= Time.unscaledDeltaTime;
                if (_hitmarkerTimer <= 0f && _hitmarkerTicks != null)
                    foreach (var t in _hitmarkerTicks) if (t != null) t.color = Color.clear;
            }

            if (_netGraphText != null)
            {
                bool show = TacticalSettings.Current.NetGraph;
                if (_netGraphText.gameObject.activeSelf != show) _netGraphText.gameObject.SetActive(show);
                if (show)
                {
                    if (Time.unscaledDeltaTime > 0.0001f) _fpsSmooth = Mathf.Lerp(_fpsSmooth, 1f / Time.unscaledDeltaTime, 0.1f);
                    _netGraphText.text = $"FPS: {Mathf.RoundToInt(_fpsSmooth)}";
                }
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && !_ended && !PauseOpen)
            {
                if (kb.backquoteKey.wasPressedThisFrame && _console != null) { _console.Toggle(); ApplyCursor(); }
                else if (!_console.IsOpen && kb.bKey.wasPressedThisFrame && _buyMenu != null)
                {
                    if (_buyMenu.IsOpen) _buyMenu.Hide(); else _buyMenu.Show(_money);
                    ApplyCursor();
                }
                else if (!_console.IsOpen && kb.mKey.wasPressedThisFrame && _teamSelect != null)
                {
                    if (_teamSelect.IsOpen) _teamSelect.Hide(); else _teamSelect.Show();
                    ApplyCursor();
                }
                if (_scoreboard != null)
                {
                    bool tab = kb.tabKey.isPressed;
                    if (tab && !_scoreboard.IsOpen) { _scoreboard.Refresh(_session); _scoreboard.gameObject.SetActive(true); }
                    else if (!tab && _scoreboard.IsOpen) _scoreboard.gameObject.SetActive(false);
                }
            }

            if (PauseOpen) UiFocus.KeepFocus(_resume);
            if (Cursor.lockState != CursorLockMode.Locked && PlatformInfo.UsesMouseLook && !AnyModalOpen && !_ended
                && AppFocus.HasFocus && UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                ApplyCursor();
        }
    }
}
