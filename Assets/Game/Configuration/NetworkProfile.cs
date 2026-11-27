using UnityEngine;

namespace Adventure3D.Configuration
{
    /// <summary>
    /// Network defaults. Every value can be overridden at runtime by a settings file
    /// (adventure3d-settings.json) or by command line arguments, see Docs/Networking.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Adventure3D/Network Profile", fileName = "NetworkProfile")]
    public sealed class NetworkProfile : ScriptableObject
    {
        [Header("Endpoints")]
        public string DefaultLanAddress = "127.0.0.1";
        [Tooltip("Online server address (IP or host name). Empty = ask the player.")]
        public string DefaultOnlineAddress = "127.0.0.1";
        [Range(1, 65535)] public int DefaultPort = 7777;
        [Range(1, 64)] public int MaxPlayers = 8;

        [Header("Replication")]
        [Tooltip("Server sends a snapshot every N simulation ticks.")]
        [Unit("ticks", 1, 10)] public int SnapshotIntervalTicks = 2;
        [Tooltip("Each input packet repeats the last N commands to survive packet loss.")]
        [Unit("cmds", 1, 16)] public int InputRedundancy = 4;
        [Unit("cmds", 2, 64)] public int MaxQueuedCommands = 10;
        [Tooltip("Remote players are rendered this far in the past to interpolate between snapshots.")]
        [Unit("s", 0f, 0.5f)] public float InterpolationDelay = 0.1f;
        [Tooltip("Local prediction is corrected when it differs from the server by more than this.")]
        [Unit("m", Min = 0.0001f)] public float ReconciliationThreshold = 0.02f;
        [Tooltip("Visual smoothing applied to the local view after a correction.")]
        [Unit("s", 0f, 0.5f)] public float CorrectionSmoothingTime = 0.1f;

        [Header("Timeouts")]
        [Unit("ms", Min = 100)] public int ConnectTimeoutMs = 1000;
        [Range(1, 30)] public int MaxConnectAttempts = 5;
        [Unit("ms", Min = 500)] public int DisconnectTimeoutMs = 4000;
        [Unit("ms", Min = 100)] public int HeartbeatTimeoutMs = 500;
        [Unit("s", Min = 1f)] public float HandshakeTimeout = 5f;

        [Header("Artificial conditions (testing)")]
        [Unit("ms", Min = 0)] public int SimulatedLatencyMs = 0;
        [Unit("ms", Min = 0)] public int SimulatedJitterMs = 0;
        [Unit("%", 0, 50)] public int SimulatedPacketLossPercent = 0;
    }
}
