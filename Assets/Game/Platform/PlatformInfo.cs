using UnityEngine;

namespace Adventure3D.Platform
{
    public enum PlatformFamily { Desktop, Mobile, Console, Server, Editor }

    /// <summary>
    /// Single place for per-platform differences. Device specific integrations (PlayStation SDK, store
    /// services, haptics) must live behind #if blocks in this assembly so every other platform keeps
    /// compiling without the licensed SDKs.
    /// </summary>
    public static class PlatformInfo
    {
        /// <summary>Set by the bootstrapper when running the headless dedicated server.</summary>
        public static bool IsHeadlessServer { get; set; }

        /// <summary>Forces the touch layout on desktop for testing (-touchui).</summary>
        public static bool ForceTouchUi { get; set; }

        public static PlatformFamily Family
        {
            get
            {
                if (IsHeadlessServer) return PlatformFamily.Server;
#if UNITY_EDITOR
                return PlatformFamily.Editor;
#elif UNITY_ANDROID || UNITY_IOS
                return PlatformFamily.Mobile;
#elif UNITY_PS5 || UNITY_PS4
                return PlatformFamily.Console;
#else
                return PlatformFamily.Desktop;
#endif
            }
        }

        public static bool IsMobile => Family == PlatformFamily.Mobile;
        public static bool IsConsole => Family == PlatformFamily.Console;

        /// <summary>Whether on-screen touch controls should be shown.</summary>
        public static bool UseTouchUi => !IsHeadlessServer && (ForceTouchUi || IsMobile);

        /// <summary>Whether the mouse cursor should be locked while playing.</summary>
        public static bool UsesMouseLook => !IsHeadlessServer && !IsMobile && !IsConsole;

        public static string Describe() =>
            $"{Application.platform} family={Family} batch={Application.isBatchMode} gfx={SystemInfo.graphicsDeviceType}";
    }
}
