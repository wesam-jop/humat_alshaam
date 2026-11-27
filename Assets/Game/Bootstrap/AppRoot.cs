using System.IO;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Identity;
using Adventure3D.Platform;
using Adventure3D.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Adventure3D.Bootstrap
{
    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string MainMenu = "MainMenu";
        public const string TestArena = "TestArena";
    }

    /// <summary>
    /// Application composition root. Exactly one instance lives for the whole process
    /// (DontDestroyOnLoad); it owns configuration, launch options and identity, and moves between the
    /// menu and the arena. Sessions are not owned here: they belong to the arena scene and die with it.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour, IMenuServices
    {
        public static AppRoot Instance { get; private set; }
        public static int LiveInstances { get; private set; }

        LaunchRequest _pending;
        SessionEndReason _lastEnd;

        public GameConfig Config { get; private set; }
        public LaunchOptions Options { get; private set; }
        public IdentityService IdentityService { get; private set; }
        public PlayerIdentity Identity => IdentityService?.Current;
        public int SessionsStarted { get; private set; }

        public static AppRoot EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AppRoot");
            DontDestroyOnLoad(go);
            var root = go.AddComponent<AppRoot>();
            root.Initialize();
            return root;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // never two application roots
                return;
            }
            Instance = this;
            LiveInstances++;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            LiveInstances--;
            Instance = null;
            IdentityService?.Dispose();
            if (ReferenceEquals(MenuServices.Current, this)) MenuServices.Current = null;
        }

        void Initialize()
        {
            Options = LaunchOptions.FromCommandLine();
            if (!string.IsNullOrEmpty(Options.Tag)) GameLog.ProcessTag = Options.Tag;
            PlatformInfo.IsHeadlessServer = Options.IsDedicatedServer;
            PlatformInfo.ForceTouchUi = Options.TouchUi;

            Config = GameConfig.Load().CloneForRuntime();
            Options.ApplyTo(Config);

            if (Options.TargetFps > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = Options.TargetFps;
            }
            else if (Options.IsDedicatedServer)
            {
                Application.targetFrameRate = Config.Physics.TickRate; // do not spin a core at 1000 fps
            }
            Application.runInBackground = true;

            var guest = new GuestIdentityProvider(Path.Combine(Application.persistentDataPath, "identity"), Options.Profile);
            IdentityService = new IdentityService(guest, new UnavailableAccountProvider());
            var identity = IdentityService.SignInGuest();
            MenuServices.Current = this;

            GameLog.Info($"Adventure3D {ProtocolInfo.GameVersion} protocol {ProtocolInfo.ProtocolVersion} on {PlatformInfo.Describe()}");
            GameLog.Event("identity", "\"profileId\":" + GameLog.Q(identity.LocalProfileId) + ",\"name\":" + GameLog.Q(identity.DisplayName) +
                                      ",\"guest\":" + (identity.IsGuest ? "true" : "false") + ",\"file\":" + GameLog.Q(IdentityService.GuestRecordPath));

            if (!string.IsNullOrEmpty(Options.Agent))
                gameObject.AddComponent<DiagnosticsAgent>().Configure(this, Options);
            if (Options.QuitAfter > 0f && string.IsNullOrEmpty(Options.Agent))
                Invoke(nameof(QuitGame), Options.QuitAfter);
        }

        // ------------------------------------------------------------------ flow

        public void StartSession(LaunchRequest request)
        {
            _pending = request;
            SessionsStarted++;
            GameLog.Event("load_arena", "\"mode\":\"" + request.Mode + "\",\"n\":" + SessionsStarted);
            SceneManager.LoadScene(SceneNames.TestArena);
        }

        public LaunchRequest TakePendingRequest()
        {
            var r = _pending;
            _pending = null;
            return r;
        }

        public void ReturnToMenu(SessionEndReason reasonToShow)
        {
            if (Options.IsDedicatedServer)
            {
                QuitGame();
                return;
            }
            _lastEnd = reasonToShow;
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        public SessionEndReason ConsumeLastEndReason()
        {
            var r = _lastEnd;
            _lastEnd = SessionEndReason.None;
            return r;
        }

        public void QuitGame() => Quit(0);

        public void Quit(int code)
        {
            GameLog.Event("quit", "\"code\":" + code);
#if UNITY_EDITOR
            if (!Application.isBatchMode) { UnityEditor.EditorApplication.isPlaying = false; return; }
#endif
            Application.Quit(code);
        }
    }
}
