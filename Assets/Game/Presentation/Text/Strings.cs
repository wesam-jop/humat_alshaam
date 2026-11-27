using System.Collections.Generic;
using UnityEngine;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Central string table. All visible text comes from Resources/Adventure3D/Strings/ar.txt so it can
    /// be reviewed and edited in one place. Missing keys render as the key in brackets (and are caught
    /// by the Edit Mode tests).
    /// </summary>
    public static class Strings
    {
        public const string ResourcePath = "Adventure3D/Strings/ar";
        static Dictionary<string, string> _table;

        public static IReadOnlyDictionary<string, string> Table
        {
            get
            {
                if (_table == null) Load();
                return _table;
            }
        }

        public static void Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            _table = Parse(asset != null ? asset.text : "");
            if (asset == null) Debug.LogError("[A3D] Strings resource missing: " + ResourcePath);
        }

        public static Dictionary<string, string> Parse(string text)
        {
            var d = new Dictionary<string, string>();
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
            }
            return d;
        }

        public static bool Has(string key) => Table.ContainsKey(key);

        /// <summary>Logical (unshaped) text.</summary>
        public static string Get(string key) => Table.TryGetValue(key, out var v) ? v : "[" + key + "]";

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);
    }
}
