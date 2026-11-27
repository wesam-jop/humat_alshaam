using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Adventure3D.Configuration;
using Adventure3D.Core;
using UnityEngine;

namespace Adventure3D.Bootstrap
{
    /// <summary>
    /// Command line options. Examples:
    ///   Adventure3D.exe -mode offline
    ///   Adventure3D.exe -mode host -port 7777
    ///   Adventure3D.exe -mode client -address 192.168.1.20 -port 7777
    ///   Adventure3D.exe -batchmode -nographics -mode server -port 7777 -logFile server.log
    /// Testing: -agent &lt;scenario&gt; -quitAfter &lt;s&gt; -tag &lt;name&gt; -fps &lt;n&gt; -profile &lt;name&gt;
    ///          -simLatency &lt;ms&gt; -simJitter &lt;ms&gt; -simLoss &lt;%&gt; -protocol &lt;n&gt; -touchui -shots &lt;dir&gt;
    /// </summary>
    public sealed class LaunchOptions
    {
        public SessionMode? Mode;
        public string Address;
        public int? Port;
        public string Agent;
        public float QuitAfter = -1f;
        public string Tag;
        public int TargetFps = -1;
        public string Profile;
        public int? SimLatency, SimJitter, SimLoss;
        public ushort? ProtocolOverride;
        public bool TouchUi;
        public string ScreenshotDir;
        public string SettingsFile;
        public int? Bots;

        public bool IsDedicatedServer => Mode == SessionMode.DedicatedServer;

        public static LaunchOptions FromCommandLine() => Parse(Environment.GetCommandLineArgs());

        public static LaunchOptions Parse(IList<string> args)
        {
            var o = new LaunchOptions();
            for (int i = 0; i < args.Count; i++)
            {
                string a = args[i].ToLowerInvariant();
                string next = i + 1 < args.Count ? args[i + 1] : null;
                switch (a)
                {
                    case "-mode":
                        o.Mode = ParseMode(next);
                        i++;
                        break;
                    case "-server":
                        o.Mode = SessionMode.DedicatedServer;
                        break;
                    case "-address": o.Address = next; i++; break;
                    case "-port": if (int.TryParse(next, out var p)) o.Port = p; i++; break;
                    case "-agent": o.Agent = next; i++; break;
                    case "-quitafter": if (TryFloat(next, out var q)) o.QuitAfter = q; i++; break;
                    case "-tag": o.Tag = next; i++; break;
                    case "-fps": if (int.TryParse(next, out var f)) o.TargetFps = f; i++; break;
                    case "-profile": o.Profile = next; i++; break;
                    case "-simlatency": if (int.TryParse(next, out var l)) o.SimLatency = l; i++; break;
                    case "-simjitter": if (int.TryParse(next, out var j)) o.SimJitter = j; i++; break;
                    case "-simloss": if (int.TryParse(next, out var s)) o.SimLoss = s; i++; break;
                    case "-protocol": if (ushort.TryParse(next, out var pv)) o.ProtocolOverride = pv; i++; break;
                    case "-touchui": o.TouchUi = true; break;
                    case "-shots": o.ScreenshotDir = next; i++; break;
                    case "-settings": o.SettingsFile = next; i++; break;
                    case "-bots": if (int.TryParse(next, out var b)) o.Bots = b; i++; break;
                }
            }
            return o;
        }

        static SessionMode? ParseMode(string s)
        {
            switch ((s ?? "").ToLowerInvariant())
            {
                case "offline": return SessionMode.Offline;
                case "host": return SessionMode.Host;
                case "client": case "join": return SessionMode.Client;
                case "server": case "dedicated": return SessionMode.DedicatedServer;
                default: return null;
            }
        }

        static bool TryFloat(string s, out float v) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        /// <summary>Request built from the command line, or null when the menu should open.</summary>
        public LaunchRequest ToRequest(NetworkProfile net)
        {
            if (Mode == null) return null;
            return new LaunchRequest
            {
                Mode = Mode.Value,
                Address = string.IsNullOrEmpty(Address) ? net.DefaultOnlineAddress : Address,
                Port = (ushort)Mathf.Clamp(Port ?? net.DefaultPort, 1, 65535),
            };
        }

        /// <summary>Applies the optional settings file and then command line overrides to the runtime config.</summary>
        public void ApplyTo(GameConfig config)
        {
            RuntimeSettingsFile.Apply(config, SettingsFile);
            var net = config.Network;
            if (SimLatency.HasValue) net.SimulatedLatencyMs = SimLatency.Value;
            if (SimJitter.HasValue) net.SimulatedJitterMs = SimJitter.Value;
            if (SimLoss.HasValue) net.SimulatedPacketLossPercent = SimLoss.Value;
            if (Port.HasValue) net.DefaultPort = Port.Value;
        }
    }

    /// <summary>
    /// Optional JSON settings file "adventure3d-settings.json", looked up next to the executable and
    /// then in persistentDataPath. Lets a server operator change addresses without rebuilding.
    /// </summary>
    public static class RuntimeSettingsFile
    {
        public const string FileName = "adventure3d-settings.json";

        [Serializable]
        sealed class Data
        {
            public string onlineServerAddress;
            public string lanAddress;
            public int port;
            public int maxPlayers;
            public int simulatedLatencyMs = -1;
        }

        public static string LoadedFrom { get; private set; }

        public static void Apply(GameConfig config, string explicitPath)
        {
            LoadedFrom = null;
            foreach (var path in Candidates(explicitPath))
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                try
                {
                    var d = JsonUtility.FromJson<Data>(File.ReadAllText(path));
                    var n = config.Network;
                    if (!string.IsNullOrWhiteSpace(d.onlineServerAddress)) n.DefaultOnlineAddress = d.onlineServerAddress.Trim();
                    if (!string.IsNullOrWhiteSpace(d.lanAddress)) n.DefaultLanAddress = d.lanAddress.Trim();
                    if (d.port > 0 && d.port <= 65535) n.DefaultPort = d.port;
                    if (d.maxPlayers > 0) n.MaxPlayers = d.maxPlayers;
                    if (d.simulatedLatencyMs >= 0) n.SimulatedLatencyMs = d.simulatedLatencyMs;
                    LoadedFrom = path;
                    GameLog.Info("Settings loaded from " + path);
                    return;
                }
                catch (Exception e)
                {
                    GameLog.Warn("Ignoring invalid settings file " + path + ": " + e.Message);
                }
            }
        }

        static IEnumerable<string> Candidates(string explicitPath)
        {
            if (!string.IsNullOrEmpty(explicitPath)) yield return explicitPath;
            string exeDir = null;
            try { exeDir = Path.GetDirectoryName(Application.dataPath); } catch (Exception) { }
            if (exeDir != null) yield return Path.Combine(exeDir, FileName);
            yield return Path.Combine(Application.persistentDataPath, FileName);
        }
    }
}
