using Adventure3D.Core;
using Adventure3D.Gameplay;
using Adventure3D.Input;
using Adventure3D.Networking;
using Adventure3D.Platform;
using Adventure3D.Presentation;
using UnityEngine;

namespace Adventure3D.Bootstrap
{
    /// <summary>
    /// Lives in TestArena.unity. Builds one session for the pending launch request. Everything it
    /// creates belongs to the arena scene, so leaving the scene destroys the session runner (which
    /// disposes the session, its players and its sockets), the input, the camera and the HUD.
    /// The dedicated server path creates no camera, no Canvas and no local input.
    /// </summary>
    public sealed class ArenaEntryPoint : MonoBehaviour
    {
        [SerializeField] PlayerAvatar playerPrefab;

        public static ArenaEntryPoint Current { get; private set; }

        public GameSession Session { get; private set; }
        public LocalPlayerInput Input { get; private set; }
        public ArenaHud Hud { get; private set; }
        public FirstPersonCameraRig CameraRig { get; private set; }
        public FirstPersonViewModel ViewModel { get; private set; }
        public CombatPresenter Combat { get; private set; }

        void Awake() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Start()
        {
            var app = AppRoot.EnsureExists();
            var request = app.TakePendingRequest() ?? app.Options.ToRequest(app.Config.Network) ?? LaunchRequest.Offline();
            bool headless = request.Mode == SessionMode.DedicatedServer;
            PlatformInfo.IsHeadlessServer = headless;

            // Combat cover is part of the level on every machine (server and clients share colliders).
            if (GameObject.Find(TestArenaLayout.CombatCoverName) == null)
            {
                var floor = GameObject.Find("Floor");
                var stairs = GameObject.Find("Stair_0");
                var ceiling = GameObject.Find("Ceiling_Slab");
                TestArenaLayout.BuildCombatCover(null,
                    stairs != null ? stairs.GetComponent<Renderer>().sharedMaterial : null,
                    ceiling != null ? ceiling.GetComponent<Renderer>().sharedMaterial : (floor != null ? floor.GetComponent<Renderer>().sharedMaterial : null));
                Physics.SyncTransforms();
            }
            var runtimeConfig = app.Config.CloneForRuntime();
            if (string.IsNullOrEmpty(app.Options.Agent))
                Adventure3D.Configuration.Cs16ConVars.ApplyPhysicsTo(runtimeConfig);
            var world = new WorldSimulation(runtimeConfig, playerPrefab);
            Session = new GameSession(request, runtimeConfig, app.Identity, world);
            if (app.Options.ProtocolOverride.HasValue) Session.ProtocolVersionOverride = app.Options.ProtocolOverride.Value;
            // Training bots in offline play; automated movement checks run without them unless asked.
            Session.BotSkill = Adventure3D.Configuration.Cs16ConVars.BotSkillFromDifficulty(Adventure3D.Configuration.Cs16ConVars.BotDifficulty);
            Session.OfflineBots = app.Options.Bots ?? (string.IsNullOrEmpty(app.Options.Agent) ? Adventure3D.Configuration.Cs16ConVars.BotQuota : 0);
            SessionRunner.Create(Session);

            if (!headless)
            {
                Input = new GameObject("LocalPlayerInput").AddComponent<LocalPlayerInput>();
                Input.Configure(app.Config.Input, app.Config.Camera);
                ICommandSource commands = Input;
                var agent = app.GetComponent<DiagnosticsAgent>();
                var scripted = agent != null ? agent.CreateCommandSource(app.Config.Physics.TickDelta) : null;
                if (scripted != null) commands = scripted;
                Session.LocalCommands = commands;

                CameraRig = FirstPersonCameraRig.Create();
                Session.LocalPlayerSpawned += avatar => CameraRig.Bind(avatar, commands, app.Config.Camera);
                Hud = ArenaHud.Create(Session, Input, LeaveToMenu);
                ViewModel = FirstPersonViewModel.Create(CameraRig, Session, Input);
                Combat = CombatPresenter.Create(Session, Hud, ViewModel, CameraRig.Camera);
                if (agent != null && Hud != null) agent.OnArenaReady(this);
            }
            else
            {
                var agent = app.GetComponent<DiagnosticsAgent>();
                if (agent != null) agent.OnArenaReady(this);
            }

            Session.StatusChanged += (status, reason) =>
            {
                if (status != SessionStatus.Ended) return;
                if (headless) app.Quit(reason == SessionEndReason.BindFailed ? 2 : 0);
            };

            if (!Session.Start())
                GameLog.Warn("Session failed to start: " + Session.EndReason);
        }

        /// <summary>Pause menu "back to menu" and the OK button of the session-ended dialog.</summary>
        public void LeaveToMenu()
        {
            if (Session != null && Session.Status != SessionStatus.Ended) Session.Leave();
            AppRoot.Instance.ReturnToMenu(SessionEndReason.None);
        }
    }
}
