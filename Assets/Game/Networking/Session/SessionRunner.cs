using UnityEngine;

namespace Adventure3D.Networking
{
    /// <summary>
    /// Drives a <see cref="GameSession"/> from the Unity player loop and guarantees it is disposed when
    /// the arena scene unloads, so returning to the menu always closes sockets and removes players.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SessionRunner : MonoBehaviour
    {
        public static int LiveRunners { get; private set; }

        public GameSession Session { get; private set; }

        public static SessionRunner Create(GameSession session)
        {
            var go = new GameObject("SessionRunner");
            var runner = go.AddComponent<SessionRunner>();
            runner.Session = session;
            return runner;
        }

        void Awake() => LiveRunners++;

        void Update() => Session?.Update(Time.unscaledDeltaTime);

        void LateUpdate() => Session?.LateUpdate(Time.unscaledDeltaTime);

        void OnDestroy()
        {
            LiveRunners--;
            Session?.Dispose();
            Session = null;
        }

        void OnApplicationQuit()
        {
            Session?.Leave();
        }
    }
}
