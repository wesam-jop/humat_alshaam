using System;
using Adventure3D.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Screens 4, 5, 6: Create Server modal matching the 2-column RTL tactical designs.
    /// </summary>
    public sealed class CreateServerModal : MonoBehaviour
    {
        readonly CreateServerConfig _cfg = new CreateServerConfig();
        Button[] _tabs;
        RectTransform[] _pages;
        ArabicText _mapDesc;
        InputField _serverName, _password;
        Selectable _returnFocus;
        Action<LaunchRequest> _onStart;

        public bool IsOpen => gameObject.activeSelf;

        public static CreateServerModal Create(Transform parent, Action<LaunchRequest> onStart)
        {
            var blocker = UiKit.NewRect("CreateServerModal", parent);
            UiKit.Stretch(blocker);
            blocker.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var modal = blocker.gameObject.AddComponent<CreateServerModal>();
            modal._onStart = onStart;
            modal.Build(blocker);
            blocker.gameObject.SetActive(false);
            return modal;
        }

        void Build(RectTransform blocker)
        {
            var panel = UiKit.Panel("Panel", blocker, new Vector2(1060, 680));
            UiKit.Column(panel, 12, 24);

            UiKit.ModalHeader(panel, Strings.Get("create.title"));

            // RTL tab order: Basic on the right, Bots on the left
            _tabs = UiKit.TabBar(panel, new[]
            {
                Strings.Get("create.tab.bots"),
                Strings.Get("create.tab.rules"),
                Strings.Get("create.tab.basic"),
            }, idx => ShowTab(2 - idx), 46f);

            var body = UiKit.NewRect("TabBody", panel);
            var bodyLe = body.gameObject.AddComponent<LayoutElement>();
            bodyLe.flexibleHeight = 1f;
            bodyLe.minHeight = 420f;

            _pages = new[]
            {
                BuildBasicTab(body),
                BuildRulesTab(body),
                BuildBotsTab(body),
            };

            var footer = UiKit.Row(panel, "Footer", 58f, 14f);
            UiKit.Button(footer, Strings.Get("common.cancel"), Close, 56f);
            var startBtn = UiKit.Button(footer, Strings.Get("host.start"), StartServer, 56f);
            startBtn.GetComponent<Image>().sprite = MenuStyle.ButtonSelected;

            ShowTab(0);
        }

        RectTransform BuildBasicTab(Transform parent)
        {
            var root = UiKit.NewRect("BasicTab", parent);
            UiKit.Stretch(root);
            UiKit.TwoColumns(root, out var left, out var right);

            // Right column: Map, Server Name, Mode
            UiKit.CycleRow(right, Strings.Get("create.map"), TacticalCatalog.Maps, 0, i =>
            {
                _cfg.MapName = TacticalCatalog.Maps[i];
                if (_mapDesc != null) _mapDesc.SetText(TacticalCatalog.MapDescription(_cfg.MapName));
            });
            _mapDesc = UiKit.Label(right, TacticalCatalog.MapDescription(_cfg.MapName), 20, TextAnchor.MiddleRight, 34);
            _mapDesc.Target.color = MenuStyle.Gold;
            _serverName = UiKit.LtrInput(right, Strings.Get("create.server_name"), _cfg.ServerName, InputField.ContentType.Standard);
            UiKit.CycleRow(right, Strings.Get("create.mode"), new[]
            {
                Strings.Get("create.mode.defusal"),
                Strings.Get("create.mode.hostage"),
                Strings.Get("create.mode.tdm"),
            }, 0, i => _cfg.Mode = (GameModeType)i);

            // Left column: Max players, Broadcast, Password
            UiKit.SliderRow(left, Strings.Get("create.max_players"), 2, 32, _cfg.MaxPlayers, true, "0", v => _cfg.MaxPlayers = Mathf.RoundToInt(v));
            UiKit.CycleRow(left, Strings.Get("create.broadcast"), new[]
            {
                Strings.Get("create.broadcast.lan"),
                Strings.Get("create.broadcast.online"),
            }, 0, i => _cfg.IsOnline = i == 1);
            _password = UiKit.LtrInput(left, Strings.Get("create.password"), "", InputField.ContentType.Password);
            return root;
        }

        RectTransform BuildRulesTab(Transform parent)
        {
            var root = UiKit.NewRect("RulesTab", parent);
            UiKit.Stretch(root);
            UiKit.TwoColumns(root, out var left, out var right);

            // Right column: Timers & Money
            UiKit.SliderRow(right, Strings.Get("create.round_time"), 1f, 5f, _cfg.RoundTimeMinutes, false, "0.0", v => _cfg.RoundTimeMinutes = v);
            UiKit.SliderRow(right, Strings.Get("create.buy_time"), 10, 90, _cfg.BuyTimeSeconds, true, "0", v => _cfg.BuyTimeSeconds = Mathf.RoundToInt(v));
            UiKit.SliderRow(right, Strings.Get("create.freeze_time"), 0, 15, _cfg.FreezeTimeSeconds, true, "0", v => _cfg.FreezeTimeSeconds = Mathf.RoundToInt(v));
            UiKit.SliderRow(right, Strings.Get("create.start_money"), 800, 16000, _cfg.StartingMoney, true, "0", v => _cfg.StartingMoney = Mathf.RoundToInt(v));

            // Left column: Limits & Rule Checkboxes
            UiKit.SliderRow(left, Strings.Get("create.win_limit"), 1, 30, _cfg.WinLimitRounds, true, "0", v => _cfg.WinLimitRounds = Mathf.RoundToInt(v));
            UiKit.SliderRow(left, Strings.Get("create.time_limit"), 5, 90, _cfg.TimeLimitMinutes, true, "0", v => _cfg.TimeLimitMinutes = Mathf.RoundToInt(v));
            UiKit.Divider(left);
            UiKit.Checkbox(left, Strings.Get("create.friendly_fire"), _cfg.FriendlyFire, v => _cfg.FriendlyFire = v);
            UiKit.Checkbox(left, Strings.Get("create.auto_balance"), _cfg.AutoTeamBalance, v => _cfg.AutoTeamBalance = v);
            UiKit.Checkbox(left, Strings.Get("create.flashlight"), _cfg.AllowFlashlight, v => _cfg.AllowFlashlight = v);
            UiKit.Checkbox(left, Strings.Get("create.footsteps"), _cfg.Footsteps, v => _cfg.Footsteps = v);
            return root;
        }

        RectTransform BuildBotsTab(Transform parent)
        {
            var root = UiKit.NewRect("BotsTab", parent);
            UiKit.Stretch(root);
            UiKit.TwoColumns(root, out var left, out var right);

            // Right column: Bot general settings
            UiKit.Checkbox(right, Strings.Get("create.bots_enable"), _cfg.IncludeBots, v => _cfg.IncludeBots = v);
            UiKit.SliderRow(right, Strings.Get("create.bots_count"), 0, 31, _cfg.BotCount, true, "0", v => _cfg.BotCount = Mathf.RoundToInt(v));
            UiKit.CycleRow(right, Strings.Get("create.bots_diff"), new[]
            {
                Strings.Get("create.diff.easy"),
                Strings.Get("create.diff.normal"),
                Strings.Get("create.diff.hard"),
                Strings.Get("create.diff.expert"),
            }, (int)_cfg.BotDifficulty, i => _cfg.BotDifficulty = (BotDifficulty)i);
            UiKit.CycleRow(right, Strings.Get("create.bots_radio"), new[]
            {
                Strings.Get("create.radio.voice"),
                Strings.Get("create.radio.text"),
                Strings.Get("create.radio.off"),
            }, (int)_cfg.BotRadio, i => _cfg.BotRadio = (BotRadioMode)i);

            // Left column: Bot weapon restrictions
            UiKit.Checkbox(left, Strings.Get("create.bots_pistols"), _cfg.BotPistols, v => _cfg.BotPistols = v);
            UiKit.Checkbox(left, Strings.Get("create.bots_rifles"), _cfg.BotRifles, v => _cfg.BotRifles = v);
            UiKit.Checkbox(left, Strings.Get("create.bots_snipers"), _cfg.BotSnipers, v => _cfg.BotSnipers = v);
            UiKit.Checkbox(left, Strings.Get("create.bots_grenades"), _cfg.BotGrenades, v => _cfg.BotGrenades = v);
            UiKit.Checkbox(left, Strings.Get("create.bots_armor"), _cfg.BotArmor, v => _cfg.BotArmor = v);
            return root;
        }

        void ShowTab(int logicalIdx)
        {
            UiKit.SetActiveTab(_tabs, 2 - logicalIdx);
            for (int i = 0; i < _pages.Length; i++)
                _pages[i].gameObject.SetActive(i == logicalIdx);
        }

        void StartServer()
        {
            _cfg.ServerName = _serverName.text;
            _cfg.Password = _password.text;
            Adventure3D.Configuration.Cs16ConVars.MpRoundTime = _cfg.RoundTimeMinutes;
            Adventure3D.Configuration.Cs16ConVars.MpFreezeTime = _cfg.FreezeTimeSeconds;
            Adventure3D.Configuration.Cs16ConVars.MpBuyTime = _cfg.BuyTimeSeconds;
            Adventure3D.Configuration.Cs16ConVars.MpStartMoney = _cfg.StartingMoney;
            Adventure3D.Configuration.Cs16ConVars.MpFriendlyFire = _cfg.FriendlyFire;
            Adventure3D.Configuration.Cs16ConVars.MpTimeLimit = _cfg.TimeLimitMinutes;
            Adventure3D.Configuration.Cs16ConVars.MpWinLimit = _cfg.WinLimitRounds;
            Adventure3D.Configuration.Cs16ConVars.BotQuota = _cfg.IncludeBots ? _cfg.BotCount : 0;
            Adventure3D.Configuration.Cs16ConVars.BotDifficulty = (int)_cfg.BotDifficulty;
            ushort port = (ushort)PlayerPrefs.GetInt("a3d.port", 7777);
            Close();
            _onStart?.Invoke(new LaunchRequest { Mode = SessionMode.Host, Port = port });
        }

        public void Open(Selectable returnFocus)
        {
            _returnFocus = returnFocus;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowTab(0);
            UiFocus.PushDialog(_tabs[2]);
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            UiFocus.PopDialog(_returnFocus);
        }
    }
}
