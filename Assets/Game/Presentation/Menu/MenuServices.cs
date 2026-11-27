using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Identity;

namespace Adventure3D.Presentation
{
    /// <summary>What the menu needs from the application (implemented by the Bootstrap composition root).</summary>
    public interface IMenuServices
    {
        GameConfig Config { get; }
        PlayerIdentity Identity { get; }
        /// <summary>Reason the previous session ended, shown once when the menu opens.</summary>
        SessionEndReason ConsumeLastEndReason();
        void StartSession(LaunchRequest request);
        void QuitGame();
    }

    public static class MenuServices
    {
        public static IMenuServices Current { get; set; }
    }

    public static class EndReasonText
    {
        public static bool IsError(SessionEndReason r) => r != SessionEndReason.None && r != SessionEndReason.UserLeft;
        public static string Message(SessionEndReason r) => Strings.Get("error." + r);
    }
}
