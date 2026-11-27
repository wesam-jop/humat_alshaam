using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Adventure3D.Presentation
{
    /// <summary>Screen 24b: Full CS 1.6 Developer Console (~ / Backquote) with live ConVars, physics, bots, and history.</summary>
    public sealed class TacticalConsole : MonoBehaviour
    {
        static readonly string[] KnownCommands =
        {
            "help", "clear", "status", "maps", "changelevel", "disconnect", "quit",
            "sv_gravity", "sv_maxspeed", "sv_accelerate", "sv_airaccelerate", "sv_friction",
            "sv_stepsize", "sv_cheats", "cs_physics", "sv_restart", "sv_restartround",
            "mp_roundtime", "mp_freezetime", "mp_buytime", "mp_startmoney", "mp_friendlyfire",
            "mp_timelimit", "mp_winlimit",
            "bot_add", "bot_add_t", "bot_add_ct", "bot_kick", "bot_kill", "bot_difficulty", "bot_quota",
            "sensitivity", "fps_max", "net_graph", "volume",
            "cl_righthand", "cl_dynamiccrosshair", "cl_crosshair_size", "cl_crosshair_color",
            "name", "impulse"
        };

        readonly StringBuilder _log = new StringBuilder();
        readonly List<string> _history = new List<string>();
        int _historyIndex = -1;
        Text _output;
        InputField _input;
        GameSession _session;
        Action _onDisconnect;

        public bool IsOpen => gameObject.activeSelf;

        public static TacticalConsole Create(Transform parent, GameSession session, Action onDisconnect)
        {
            var root = UiKit.NewRect("TacticalConsole", parent);
            UiKit.Stretch(root);
            root.gameObject.AddComponent<Image>().color = MenuStyle.BgOverlay;
            var console = root.gameObject.AddComponent<TacticalConsole>();
            console._session = session;
            console._onDisconnect = onDisconnect;

            var panel = UiKit.Panel("Panel", root, new Vector2(1060, 580));
            UiKit.Column(panel, 10, 22);

            var title = UiKit.Label(panel, Strings.Get("ingame.console.title"), 26, TextAnchor.MiddleRight, 36);
            title.Target.color = MenuStyle.Gold;
            title.Target.fontStyle = FontStyle.Bold;

            var scroll = UiKit.ScrollList(panel, "Log", 4f, 10);
            console._output = UiKit.LtrLabel(scroll, "", 18, TextAnchor.UpperLeft);
            console._output.color = MenuStyle.TextWhite;

            var bottom = UiKit.Row(panel, "CmdRow", 54f, 10f);
            console._input = UiKit.LtrInput(bottom, "", "", InputField.ContentType.Standard);
            console._input.onSubmit.AddListener(_ => console.Execute());
            var send = UiKit.Button(bottom, Strings.Get("ingame.console.send"), console.Execute, 52f);
            var sLe = send.gameObject.AddComponent<LayoutElement>();
            sLe.minWidth = 140f;
            sLe.preferredWidth = 140f;
            sLe.flexibleWidth = 0f;

            console.Append(Strings.Get("ingame.console.hint"));
            console.Append("Type 'help' for CS 1.6 ConVars (sv_gravity, sv_maxspeed, bot_add, mp_startmoney, impulse 101...).");
            root.gameObject.SetActive(false);
            return console;
        }

        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
            if (gameObject.activeSelf)
            {
                transform.SetAsLastSibling();
                _input.ActivateInputField();
            }
        }

        void Update()
        {
            if (!IsOpen || _input == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.upArrowKey.wasPressedThisFrame && _history.Count > 0)
            {
                _historyIndex = _historyIndex < 0 ? _history.Count - 1 : Mathf.Max(0, _historyIndex - 1);
                _input.text = _history[_historyIndex];
                _input.caretPosition = _input.text.Length;
            }
            else if (kb.downArrowKey.wasPressedThisFrame && _history.Count > 0)
            {
                if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
                {
                    _historyIndex++;
                    _input.text = _history[_historyIndex];
                    _input.caretPosition = _input.text.Length;
                }
                else
                {
                    _historyIndex = -1;
                    _input.text = "";
                }
            }
            else if (kb.tabKey.wasPressedThisFrame)
            {
                string cur = (_input.text ?? "").Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(cur) && !cur.Contains(" "))
                {
                    var match = KnownCommands.FirstOrDefault(c => c.StartsWith(cur, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        _input.text = match + " ";
                        _input.caretPosition = _input.text.Length;
                    }
                }
            }
        }

        void Execute()
        {
            string cmd = (_input.text ?? "").Trim();
            _input.text = "";
            if (string.IsNullOrEmpty(cmd)) return;

            _history.Add(cmd);
            _historyIndex = -1;
            Append("] " + cmd);

            string[] parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string op = parts[0].ToLowerInvariant();
            string arg = parts.Length > 1 ? parts[1] : null;

            switch (op)
            {
                case "help":
                    if (string.IsNullOrEmpty(arg))
                    {
                        Append("Physics: sv_gravity, sv_maxspeed, sv_accelerate, sv_airaccelerate, sv_friction, sv_stepsize, sv_cheats, cs_physics");
                        Append("Rules:   mp_roundtime, mp_freezetime, mp_buytime, mp_startmoney, mp_friendlyfire, sv_restart, maps, changelevel");
                        Append("Bots:    bot_add, bot_add_t, bot_add_ct, bot_kick, bot_kill, bot_difficulty <0-3>, bot_quota <n>");
                        Append("Client:  sensitivity, fps_max, net_graph, volume, cl_righthand, cl_dynamiccrosshair, cl_crosshair_size, cl_crosshair_color, name, impulse 101, status, clear, disconnect");
                    }
                    else
                    {
                        var matches = KnownCommands.Where(c => c.Contains(arg.ToLowerInvariant())).ToArray();
                        Append(matches.Length > 0 ? string.Join(", ", matches) : "No matching commands.");
                    }
                    break;

                case "clear":
                    _log.Clear();
                    _output.text = "";
                    break;

                case "status":
                    if (_session != null)
                    {
                        Append($"mode: {_session.Mode} | players: {_session.World.Count} | tickrate: {_session.World.Config.Physics.TickRate}Hz");
                        Append($"sv_gravity: {Cs16ConVars.SvGravity} | sv_maxspeed: {Cs16ConVars.SvMaxSpeed} | bot_difficulty: {Cs16ConVars.BotDifficulty}");
                        foreach (var p in _session.World.Players)
                            Append($"  #{p.PlayerId} {p.DisplayName} | team={p.Combat.Team} | hp={p.Combat.Health} | ${p.Combat.Money} | bot={p.Combat.IsBot}");
                    }
                    break;

                case "maps":
                    Append("Available maps: " + string.Join(", ", TacticalCatalog.Maps));
                    break;

                case "changelevel":
                    if (!string.IsNullOrEmpty(arg))
                        Append($"Map '{arg}' queued. Use Create Server or Restart to switch arena.");
                    else
                        Append("Usage: changelevel <mapname>");
                    break;

                case "sv_gravity":
                    HandleFloatConVar("sv_gravity", arg, ref Cs16ConVars.SvGravity, 50f, 3000f, ApplyPhysics);
                    break;
                case "sv_maxspeed":
                    HandleFloatConVar("sv_maxspeed", arg, ref Cs16ConVars.SvMaxSpeed, 50f, 1000f, ApplyPhysics);
                    break;
                case "sv_accelerate":
                    HandleFloatConVar("sv_accelerate", arg, ref Cs16ConVars.SvAccelerate, 1f, 50f, ApplyPhysics);
                    break;
                case "sv_airaccelerate":
                    HandleFloatConVar("sv_airaccelerate", arg, ref Cs16ConVars.SvAirAccelerate, 0f, 100f, ApplyPhysics);
                    break;
                case "sv_friction":
                    HandleFloatConVar("sv_friction", arg, ref Cs16ConVars.SvFriction, 0.5f, 20f, ApplyPhysics);
                    break;
                case "sv_stepsize":
                    HandleFloatConVar("sv_stepsize", arg, ref Cs16ConVars.SvStepSize, 0f, 64f, ApplyPhysics);
                    break;
                case "sv_cheats":
                    HandleBoolConVar("sv_cheats", arg, ref Cs16ConVars.SvCheats);
                    break;
                case "cs_physics":
                    HandleBoolConVar("cs_physics", arg, ref Cs16ConVars.UseCs16Physics, () =>
                    {
                        if (Cs16ConVars.UseCs16Physics) ApplyPhysics();
                    });
                    break;

                case "mp_roundtime":
                    HandleFloatConVar("mp_roundtime", arg, ref Cs16ConVars.MpRoundTime, 1f, 10f);
                    break;
                case "mp_freezetime":
                    HandleIntConVar("mp_freezetime", arg, ref Cs16ConVars.MpFreezeTime, 0, 30);
                    break;
                case "mp_buytime":
                    HandleIntConVar("mp_buytime", arg, ref Cs16ConVars.MpBuyTime, 5, 180);
                    break;
                case "mp_startmoney":
                    HandleIntConVar("mp_startmoney", arg, ref Cs16ConVars.MpStartMoney, 800, 16000);
                    break;
                case "mp_friendlyfire":
                    HandleBoolConVar("mp_friendlyfire", arg, ref Cs16ConVars.MpFriendlyFire);
                    break;
                case "mp_timelimit":
                    HandleIntConVar("mp_timelimit", arg, ref Cs16ConVars.MpTimeLimit, 0, 120);
                    break;
                case "mp_winlimit":
                    HandleIntConVar("mp_winlimit", arg, ref Cs16ConVars.MpWinLimit, 1, 50);
                    break;

                case "sv_restart":
                case "sv_restartround":
                    _session?.RestartRound();
                    Append("The game will restart in 1 SECOND");
                    break;

                case "bot_add":
                    var b = _session?.AddBot();
                    Append(b != null ? $"Added bot: {b.DisplayName} ({b.Combat.Team})" : "Cannot add bot (not authority).");
                    break;
                case "bot_add_t":
                    var bt = _session?.AddBot(Team.Attackers);
                    Append(bt != null ? $"Added T bot: {bt.DisplayName}" : "Cannot add bot (not authority).");
                    break;
                case "bot_add_ct":
                    var bct = _session?.AddBot(Team.Defenders);
                    Append(bct != null ? $"Added CT bot: {bct.DisplayName}" : "Cannot add bot (not authority).");
                    break;
                case "bot_kick":
                    Append($"Kicked {_session?.KickBots() ?? 0} bots.");
                    break;
                case "bot_kill":
                    Append($"Killed {_session?.KillBots() ?? 0} bots.");
                    break;
                case "bot_difficulty":
                    HandleIntConVar("bot_difficulty", arg, ref Cs16ConVars.BotDifficulty, 0, 3, () =>
                    {
                        _session?.SetBotSkill(Cs16ConVars.BotSkillFromDifficulty(Cs16ConVars.BotDifficulty));
                    });
                    break;
                case "bot_quota":
                    HandleIntConVar("bot_quota", arg, ref Cs16ConVars.BotQuota, 0, 31);
                    break;

                case "impulse":
                    if (arg == "101")
                    {
                        if (!Cs16ConVars.SvCheats) Append("sv_cheats must be 1 to use impulse 101.");
                        else { _session?.GiveLocalMoney(16000); Append("Granted $16,000."); }
                    }
                    else Append("Usage: impulse 101");
                    break;

                case "net_graph":
                    if (arg != null && int.TryParse(arg, out int ng))
                    {
                        TacticalSettings.Current.NetGraph = ng != 0;
                        TacticalSettings.Current.SaveAndApply();
                    }
                    Append("net_graph = " + (TacticalSettings.Current.NetGraph ? "1" : "0"));
                    break;

                case "sensitivity":
                    if (arg != null && float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float sens))
                    {
                        TacticalSettings.Current.MouseSensitivity = Mathf.Clamp(sens, 0.1f, 20f);
                        TacticalSettings.Current.SaveAndApply();
                    }
                    Append("sensitivity = " + TacticalSettings.Current.MouseSensitivity.ToString("0.00", CultureInfo.InvariantCulture));
                    break;

                case "fps_max":
                    if (arg != null && int.TryParse(arg, out int fps))
                    {
                        TacticalSettings.Current.MaxFps = Mathf.Clamp(fps, 30, 300);
                        TacticalSettings.Current.VSync = false;
                        TacticalSettings.Current.SaveAndApply();
                    }
                    Append("fps_max = " + TacticalSettings.Current.MaxFps);
                    break;

                case "volume":
                    if (arg != null && float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float vol))
                    {
                        TacticalSettings.Current.MasterVolume = Mathf.Clamp01(vol);
                        TacticalSettings.Current.SaveAndApply();
                    }
                    Append("volume = " + TacticalSettings.Current.MasterVolume.ToString("0.00", CultureInfo.InvariantCulture));
                    break;

                case "cl_righthand":
                    if (arg != null && int.TryParse(arg, out int rh))
                    {
                        TacticalSettings.Current.RightHandWeapon = rh != 0;
                        TacticalSettings.Current.SaveAndApply();
                    }
                    Append("cl_righthand = " + (TacticalSettings.Current.RightHandWeapon ? "1" : "0"));
                    break;

                case "cl_dynamiccrosshair":
                    if (arg != null && int.TryParse(arg, out int dc))
                    {
                        TacticalSettings.Current.CrosshairDynamic = dc != 0;
                        TacticalSettings.Current.SaveAndApply();
                    }
                    Append("cl_dynamiccrosshair = " + (TacticalSettings.Current.CrosshairDynamic ? "1" : "0"));
                    break;

                case "cl_crosshair_size":
                    if (arg != null && int.TryParse(arg, out int cs))
                    {
                        TacticalSettings.Current.CrosshairSize = Mathf.Clamp(cs, 0, 3);
                        TacticalSettings.Current.SaveAndApply();
                    }
                    Append("cl_crosshair_size = " + TacticalSettings.Current.CrosshairSize);
                    break;

                case "cl_crosshair_color":
                    if (arg != null && int.TryParse(arg, out int cc))
                    {
                        TacticalSettings.Current.CrosshairColor = Mathf.Clamp(cc, 0, 3);
                        TacticalSettings.Current.SaveAndApply();
                    }
                    Append("cl_crosshair_color = " + TacticalSettings.Current.CrosshairColor);
                    break;

                case "name":
                    if (parts.Length > 1)
                    {
                        string newName = string.Join(" ", parts.Skip(1));
                        TacticalSettings.Current.PlayerName = newName;
                        TacticalSettings.Current.SaveAndApply();
                        if (_session?.LocalAvatar != null) _session.LocalAvatar.DisplayName = newName;
                    }
                    Append("name = \"" + TacticalSettings.Current.PlayerName + "\"");
                    break;

                case "disconnect":
                    gameObject.SetActive(false);
                    _onDisconnect?.Invoke();
                    break;

                case "quit":
                    Application.Quit();
                    break;

                default:
                    Append("Unknown command: " + op);
                    break;
            }

            _input.ActivateInputField();
        }

        void ApplyPhysics()
        {
            if (_session?.World == null) return;
            Cs16ConVars.ApplyPhysicsTo(_session.World.Config);
            _session.World.RefreshMotorSettings();
        }

        void HandleFloatConVar(string name, string arg, ref float target, float min, float max, Action onChanged = null)
        {
            if (arg != null && float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                target = Mathf.Clamp(v, min, max);
                onChanged?.Invoke();
            }
            Append($"{name} = {target.ToString("0.##", CultureInfo.InvariantCulture)}");
        }

        void HandleIntConVar(string name, string arg, ref int target, int min, int max, Action onChanged = null)
        {
            if (arg != null && int.TryParse(arg, out int v))
            {
                target = Mathf.Clamp(v, min, max);
                onChanged?.Invoke();
            }
            Append($"{name} = {target}");
        }

        void HandleBoolConVar(string name, string arg, ref bool target, Action onChanged = null)
        {
            if (arg != null && int.TryParse(arg, out int v))
            {
                target = v != 0;
                onChanged?.Invoke();
            }
            Append($"{name} = {(target ? "1" : "0")}");
        }

        void Append(string line)
        {
            _log.AppendLine(line);
            if (_output != null) _output.text = _log.ToString();
        }
    }
}
