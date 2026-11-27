using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Adventure3D.Core;
using Adventure3D.Gameplay;
using Adventure3D.Networking;
using Adventure3D.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Adventure3D.Bootstrap
{
    /// <summary>
    /// Automated player used by the multi-process test scripts (Assets/Game/Tests/MultiProcess). It
    /// drives the real build through the same paths as a person: scripted commands go through
    /// GameSession, leaving uses the HUD/arena flow, rejoining uses the menu flow. It writes
    /// "[A3D:EVT]" lines that the scripts parse. Only active with -agent on the command line.
    ///
    /// Scenarios: walk, offline-cycle, reconnect, identity, fps, screenshots, idle.
    /// </summary>
    public sealed class DiagnosticsAgent : MonoBehaviour
    {
        AppRoot _app;
        LaunchOptions _options;
        ArenaEntryPoint _arena;
        ScriptedCommandSource _script;
        string Scenario => _options.Agent.ToLowerInvariant();

        public void Configure(AppRoot app, LaunchOptions options)
        {
            _app = app;
            _options = options;
            GameLog.Event("agent_start", "\"scenario\":" + GameLog.Q(options.Agent));
            StartCoroutine(Run());
            if (options.QuitAfter > 0f) StartCoroutine(Watchdog(options.QuitAfter));
        }

        IEnumerator Watchdog(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            LogState("final");
            _app.Quit(0);
        }

        /// <summary>Scripted movement replaces device input for scenarios that need a moving player.</summary>
        public ICommandSource CreateCommandSource(float tickDelta)
        {
            switch (Scenario)
            {
                case "walk":
                case "reconnect":
                {
                    // Each process walks a different square pattern so positions are distinguishable.
                    int sum = 0;
                    foreach (char ch in _options.Tag ?? "") sum += ch;
                    float yaw = sum % 2 * 90f; // relative to the spawn direction (towards the arena centre)
                    _script = new ScriptedCommandSource(tickDelta) { Loop = true, YawOffset = Mathf.Repeat(yaw, 360f) };
                    _script.Add(1.2f, new Vector2(0, 1)).Add(0.6f, new Vector2(0, 1), jumpAtStart: true)
                           .Add(1.2f, new Vector2(1, 0)).Add(1.2f, new Vector2(0, -1), sprint: false)
                           .Add(1.2f, new Vector2(-1, 0)).Wait(0.4f);
                    return _script;
                }
                case "combat":
                    _combat = new CombatAgentSource();
                    return _combat;
                case "offline-cycle":
                case "fps":
                    _script = new ScriptedCommandSource(tickDelta);
                    _script.Add(1.0f, new Vector2(0, 1)).Add(1.0f, new Vector2(0, 1), sprint: true)
                           .Add(0.1f, new Vector2(0, 1), sprint: true, jumpAtStart: true).Add(1.5f, new Vector2(1, 1))
                           .Wait(10f);
                    return _script;
                default:
                    return null;
            }
        }

        public void OnArenaReady(ArenaEntryPoint arena)
        {
            _arena = arena;
            arena.Session.StatusChanged += (s, r) =>
                GameLog.Event("agent_session_status", "\"status\":\"" + s + "\",\"reason\":\"" + r + "\"");
        }

        IEnumerator Run()
        {
            yield return null;
            switch (Scenario)
            {
                case "identity": yield return Identity(); break;
                case "walk": yield return Walk(); break;
                case "offline-cycle": yield return OfflineCycle(); break;
                case "reconnect": yield return Reconnect(); break;
                case "fps": yield return Fps(); break;
                case "screenshots": yield return Screenshots(); break;
                case "combat": yield return Combat(); break;
                case "character": yield return CharacterShowcase(); break;
                default: break; // idle: only the watchdog runs
            }
        }

        // ------------------------------------------------------------------ scenarios

        IEnumerator Identity()
        {
            yield return null;
            _app.Quit(0);
        }

        IEnumerator Walk()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                LogState("periodic");
                yield return HandleEndedSession();
            }
        }

        /// <summary>When the session ends by itself (server closed), press OK like a player and confirm the menu.</summary>
        IEnumerator HandleEndedSession()
        {
            var arena = ArenaEntryPoint.Current;
            if (arena == null || arena.Session == null || arena.Session.Status != SessionStatus.Ended) yield break;
            var reason = arena.Session.EndReason;
            var dialog = arena.Hud != null ? arena.Hud.Dialog : null;
            bool shown = dialog != null && dialog.IsOpen;
            GameLog.Event("agent_disconnect_seen", "\"reason\":\"" + reason + "\",\"dialog\":" + (shown ? "true" : "false") +
                                                   ",\"message\":" + GameLog.Q(shown ? dialog.BodyLogical : ""));
            yield return new WaitForSecondsRealtime(1f);
            if (dialog != null && dialog.IsOpen) dialog.Close(); // OK -> back to menu
            yield return WaitForScene(SceneNames.MainMenu, 10f);
            var menu = FindFirstObjectByType<MainMenuController>();
            GameLog.Event("agent_menu_after_disconnect", "\"menu\":" + (menu != null ? "true" : "false") +
                                                         ",\"selected\":" + GameLog.Q(EventSystem.current?.currentSelectedGameObject?.name));
            yield return new WaitForSecondsRealtime(1f);
            _app.Quit(0);
            yield return new WaitForSecondsRealtime(10f);
        }

        IEnumerator OfflineCycle()
        {
            for (int round = 1; round <= 2; round++)
            {
                if (round == 2)
                {
                    _app.StartSession(LaunchRequest.Offline());
                }
                yield return WaitForScene(SceneNames.TestArena, 10f);
                yield return new WaitForSecondsRealtime(3.5f);
                var arena = ArenaEntryPoint.Current;
                var local = arena.Session.LocalAvatar;
                GameLog.Event("offline_round", "\"round\":" + round + Counts() +
                                               ",\"pos\":" + GameLog.V3(local.State.Position) +
                                               ",\"grounded\":" + (local.State.Grounded ? "true" : "false") +
                                               ",\"traveled\":" + GameLog.F(new Vector2(local.State.Position.x, local.State.Position.z).magnitude));
                // Pause, then leave through the pause menu like a player.
                arena.Hud.SetPaused(true);
                uint tickBefore = arena.Session.Tick;
                yield return new WaitForSecondsRealtime(0.5f);
                GameLog.Event("offline_pause", "\"round\":" + round + ",\"ticksWhilePaused\":" + (arena.Session.Tick - tickBefore));
                arena.Hud.LeaveButton.onClick.Invoke();
                yield return WaitForScene(SceneNames.MainMenu, 10f);
                yield return null;
                yield return null;
                GameLog.Event("menu_after_round", "\"round\":" + round + Counts());
                _script = null;
            }
            _app.Quit(0);
        }

        IEnumerator Reconnect()
        {
            var request = new LaunchRequest { Mode = SessionMode.Client, Address = _options.Address ?? "127.0.0.1", Port = (ushort)(_options.Port ?? 7777) };
            for (int round = 1; round <= 2; round++)
            {
                if (round == 2) _app.StartSession(request);
                yield return WaitForScene(SceneNames.TestArena, 10f);
                float t = 0f;
                while (t < 4f)
                {
                    yield return new WaitForSecondsRealtime(0.1f);
                    t += 0.1f;
                    LogState("round" + round);
                }
                GameLog.Event("reconnect_round", "\"round\":" + round + ",\"player\":" + ArenaEntryPoint.Current.Session.LocalPlayerId + Counts());
                if (round == 1)
                {
                    ArenaEntryPoint.Current.LeaveToMenu();
                    yield return WaitForScene(SceneNames.MainMenu, 10f);
                    yield return new WaitForSecondsRealtime(1.5f);
                    GameLog.Event("menu_after_round", "\"round\":1" + Counts());
                }
            }
            yield return new WaitForSecondsRealtime(1f);
            _app.Quit(0);
        }

        IEnumerator Fps()
        {
            yield return WaitForScene(SceneNames.TestArena, 10f);
            var session = ArenaEntryPoint.Current.Session;
            uint[] marks = { 60, 120, 180, 240 };
            int next = 0;
            float frames = 0, time = 0;
            // Positions are recorded inside the tick callback, exactly at the tick, so they compare across frame rates.
            session.Ticked += tick =>
            {
                if (next >= marks.Length || tick != marks[next]) return;
                GameLog.Event("fps_mark", "\"targetFps\":" + _options.TargetFps + ",\"measuredFps\":" + GameLog.F(frames / Mathf.Max(time, 1e-3f)) +
                                          ",\"tick\":" + tick + ",\"mark\":" + marks[next] +
                                          ",\"pos\":" + GameLog.V3(session.LocalAvatar.State.Position));
                next++;
            };
            while (next < marks.Length)
            {
                yield return null;
                frames++;
                time += Time.unscaledDeltaTime;
            }
            _app.Quit(0);
        }

        IEnumerator Screenshots()
        {
            string dir = string.IsNullOrEmpty(_options.ScreenshotDir) ? Path.Combine(Application.persistentDataPath, "shots") : _options.ScreenshotDir;
            Directory.CreateDirectory(dir);
            yield return WaitForScene(SceneNames.MainMenu, 10f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot(Path.Combine(dir, "01-menu.png"));

            var menu = FindFirstObjectByType<MainMenuController>();
            menu.OpenJoinLan();
            menu.AddressField.text = "999.1.1";
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(Path.Combine(dir, "02-join.png"));
            menu.Connect(); // invalid address -> Arabic error dialog
            yield return new WaitForSecondsRealtime(0.5f);
            GameLog.Event("invalid_address_dialog", "\"open\":" + (menu.Dialog.IsOpen ? "true" : "false") + ",\"message\":" + GameLog.Q(menu.Dialog.BodyLogical) +
                                                    ",\"selected\":" + GameLog.Q(EventSystem.current?.currentSelectedGameObject?.name));
            yield return Shot(Path.Combine(dir, "03-error.png"));
            menu.Dialog.Close();
            yield return null;
            GameLog.Event("focus_after_dialog", "\"selected\":" + GameLog.Q(EventSystem.current?.currentSelectedGameObject?.name) +
                                                ",\"isConnect\":" + (EventSystem.current?.currentSelectedGameObject == menu.ConnectButton.gameObject ? "true" : "false"));

            _app.StartSession(new LaunchRequest { Mode = SessionMode.Host, Port = (ushort)(_options.Port ?? 7790) });
            yield return WaitForScene(SceneNames.TestArena, 10f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot(Path.Combine(dir, "04-hud-host.png"));
            GameLog.Event("host_listen_text", "\"text\":" + GameLog.Q(ArenaEntryPoint.Current.Hud.ListenText));
            ArenaEntryPoint.Current.Hud.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(Path.Combine(dir, "05-pause.png"));
            ArenaEntryPoint.Current.Hud.LeaveButton.onClick.Invoke();
            yield return WaitForScene(SceneNames.MainMenu, 10f);
            yield return new WaitForSecondsRealtime(0.5f);
            _app.Quit(0);
        }

        CombatAgentSource _combat;

        /// <summary>Scripted fighter: aims at the nearest visible enemy, fires, aims down sights, reloads.</summary>
        sealed class CombatAgentSource : ICommandSource
        {
            public GameSession Session;
            public bool Aim, Fire = true, ReloadNow;
            public byte Slot;
            float _yaw, _pitch;
            public float LookYaw => _yaw;
            public float LookPitch => _pitch;
            public void ResetTransientInput() { }
            public void SetLook(float yaw, float pitch) { _yaw = yaw; _pitch = pitch; }

            public PlayerCommand SampleCommand()
            {
                var cmd = new PlayerCommand();
                var me = Session?.LocalAvatar;
                if (me != null && me.Alive)
                {
                    PlayerAvatar best = null;
                    float bestD = float.MaxValue;
                    foreach (var p in Session.World.Players)
                    {
                        if (p == me || !p.Alive || p.Combat.Team == me.Combat.Team) continue;
                        float d = Vector3.Distance(p.State.Position, me.State.Position);
                        if (d < bestD) { bestD = d; best = p; }
                    }
                    if (best != null)
                    {
                        Vector3 to = best.State.Position + Vector3.up * 1.3f - (me.State.Position + Vector3.up * me.SimEyeHeight);
                        float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                        float wantPitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                        _yaw = Mathf.MoveTowardsAngle(_yaw, wantYaw, 6f);
                        _pitch = Mathf.MoveTowards(_pitch, wantPitch, 4f);
                        bool onTarget = Mathf.Abs(Mathf.DeltaAngle(_yaw, wantYaw)) < 3f;
                        if (Fire && onTarget) cmd.Buttons |= CommandButtons.Fire;
                        if (bestD > 14f) cmd.Move = new Vector2(0f, 0.6f);
                    }
                    if (Aim) cmd.Buttons |= CommandButtons.Aim;
                    if (ReloadNow) { cmd.Buttons |= CommandButtons.Reload; ReloadNow = false; }
                    cmd.SlotRequest = Slot;
                    Slot = 0;
                }
                cmd.Yaw = _yaw;
                cmd.Pitch = _pitch;
                return cmd;
            }
        }

        IEnumerator Combat()
        {
            string dir = string.IsNullOrEmpty(_options.ScreenshotDir) ? Path.Combine(Application.persistentDataPath, "shots") : _options.ScreenshotDir;
            Directory.CreateDirectory(dir);
            yield return WaitForScene(SceneNames.TestArena, 15f);
            var arena = ArenaEntryPoint.Current;
            _combat.Session = arena.Session;
            int kills = 0, shots = 0, deaths = 0, hits = 0;
            bool hitNow = false;
            arena.Session.CombatEventRaised += e =>
            {
                ushort me = arena.Session.LocalPlayerId;
                if (e.Type == CombatEventType.Shot && e.Actor == me) shots++;
                if (e.Type == CombatEventType.Damage && e.Actor == me) { hits++; hitNow = true; }
                if (e.Type == CombatEventType.Kill && e.Actor == me && e.Target != me) kills++;
                if (e.Type == CombatEventType.Kill && e.Target == me) deaths++;
                if (e.Type == CombatEventType.Kill)
                    GameLog.Event("combat_kill", "\"killer\":" + GameLog.Q(arena.Session.NameOf(e.Actor)) + ",\"victim\":" + GameLog.Q(arena.Session.NameOf(e.Target)) +
                                                 ",\"weapon\":" + GameLog.Q(Adventure3D.Gameplay.WeaponCatalog.Get(e.Weapon).Id) + ",\"headshot\":" + (e.Headshot ? "true" : "false"));
            };
            _combat.Fire = false;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot(Path.Combine(dir, "06-combat-idle.png"));
            _combat.Fire = true;
            float t = 0f;
            while (t < 1.2f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Shot(Path.Combine(dir, "07-combat-firing.png"));
            _combat.Aim = true;
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(Path.Combine(dir, "08-combat-aim.png"));
            _combat.Aim = false;
            _combat.Fire = false;
            _combat.ReloadNow = true;
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot(Path.Combine(dir, "09-combat-reload.png"));
            _combat.Fire = true;
            yield return new WaitForSecondsRealtime(8f);
            yield return Shot(Path.Combine(dir, "10-combat-killfeed.png"));
            hitNow = false;
            float waitHit = 0f;
            while (!hitNow && waitHit < 8f) { waitHit += Time.unscaledDeltaTime; yield return null; }
            yield return Shot(Path.Combine(dir, "12-combat-hit.png"));
            arena.Session.RequestBuy("awp");
            _combat.Slot = 1;
            yield return new WaitForSecondsRealtime(1.2f);
            _combat.Aim = true;
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot(Path.Combine(dir, "11-combat-scope.png"));
            _combat.Aim = false;
            yield return new WaitForSecondsRealtime(10f);
            // Pause while dead: the pause menu must cover the death panel and every HUD layer.
            _combat.Fire = false;
            float waitDeath = 0f;
            while (arena.Session.LocalAvatar.Alive && waitDeath < 15f) { waitDeath += Time.unscaledDeltaTime; yield return null; }
            arena.Hud.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot(Path.Combine(dir, "13-pause-over-death.png"));
            arena.Hud.SetPaused(false);
            var local = arena.Session.LocalAvatar;
            GameLog.Event("combat_summary", "\"shots\":" + shots + ",\"hits\":" + hits + ",\"kills\":" + kills + ",\"deaths\":" + deaths +
                                            ",\"players\":" + arena.Session.PlayerCount + ",\"health\":" + local.Combat.Health + ",\"money\":" + local.Combat.Money);
            _app.Quit(0);
        }

        /// <summary>Places another player in front of the camera to check the character model from several sides.</summary>
        IEnumerator CharacterShowcase()
        {
            string dir = string.IsNullOrEmpty(_options.ScreenshotDir) ? Path.Combine(Application.persistentDataPath, "shots") : _options.ScreenshotDir;
            Directory.CreateDirectory(dir);
            yield return WaitForScene(SceneNames.TestArena, 15f);
            yield return new WaitForSecondsRealtime(1f);
            var s = ArenaEntryPoint.Current.Session;
            var me = s.LocalAvatar;
            var other = s.World.Spawn(900, false);
            other.DisplayName = "عرض";
            other.Combat.Team = me.Combat.Team;
            Vector3 front = me.State.Position + Quaternion.Euler(0f, me.State.Yaw, 0f) * Vector3.forward * 3.2f;
            float[] yaws = { me.State.Yaw + 180f, me.State.Yaw + 90f, me.State.Yaw };
            string[] names = { "14-character-front.png", "15-character-side.png", "16-character-back.png" };
            for (int i = 0; i < yaws.Length; i++)
            {
                other.SetState(new PlayerSimState { Position = front, Yaw = yaws[i], Flags = MotorFlags.Grounded }, true);
                other.Combat.SetFlag(CombatFlags.Alive, true);
                yield return new WaitForSecondsRealtime(0.6f);
                other.SetState(new PlayerSimState { Position = front, Yaw = yaws[i], Flags = MotorFlags.Grounded }, true);
                yield return Shot(Path.Combine(dir, names[i]));
            }
            _app.Quit(0);
        }

        // ------------------------------------------------------------------ helpers

        IEnumerator Shot(string path)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            GameLog.Event("screenshot", "\"path\":" + GameLog.Q(path));
        }

        static IEnumerator WaitForScene(string name, float timeout)
        {
            float t = 0f;
            while (SceneManager.GetActiveScene().name != name && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
        }

        static string Counts() =>
            ",\"avatars\":" + PlayerAvatar.LiveCount +
            ",\"sessions\":" + GameSession.LiveSessions +
            ",\"runners\":" + SessionRunner.LiveRunners +
            ",\"appRoots\":" + AppRoot.LiveInstances +
            ",\"eventSystems\":" + FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length +
            ",\"cameras\":" + FindObjectsByType<Camera>(FindObjectsSortMode.None).Length +
            ",\"inputs\":" + FindObjectsByType<Adventure3D.Input.LocalPlayerInput>(FindObjectsSortMode.None).Length;

        void LogState(string label)
        {
            var arena = ArenaEntryPoint.Current;
            if (arena == null || arena.Session == null) return;
            var s = arena.Session;
            var sb = new StringBuilder();
            sb.Append("\"label\":").Append(GameLog.Q(label));
            sb.Append(",\"mode\":\"").Append(s.Mode).Append("\",\"status\":\"").Append(s.Status).Append('"');
            sb.Append(",\"local\":").Append(s.LocalPlayerId);
            var local = s.LocalAvatar;
            if (local != null) sb.Append(",\"pos\":").Append(GameLog.V3(local.State.Position)).Append(",\"grounded\":").Append(local.State.Grounded ? "true" : "false");
            sb.Append(",\"others\":[");
            bool first = true;
            foreach (var a in s.World.Players.Where(p => p.PlayerId != s.LocalPlayerId))
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"id\":").Append(a.PlayerId).Append(",\"pos\":").Append(GameLog.V3(a.transform.position)).Append('}');
            }
            sb.Append("],\"corrections\":").Append(s.Stats.Corrections)
              .Append(",\"maxCorrection\":").Append(GameLog.F(s.Stats.MaxCorrection))
              .Append(",\"snapshots\":").Append(s.Stats.SnapshotsReceived);
            GameLog.Event("agent_state", sb.ToString());
        }
    }
}
