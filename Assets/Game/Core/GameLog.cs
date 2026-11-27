using System.Globalization;
using UnityEngine;

namespace Adventure3D.Core
{
    /// <summary>
    /// Log helper. Lines start with "[A3D]" so the multi-process test scripts can grep player and
    /// server logs. Machine-readable lines use "[A3D:EVT] {json}". Numbers are always written with
    /// the invariant culture so logs parse the same on any system locale.
    /// </summary>
    public static class GameLog
    {
        public static string ProcessTag = "app";

        public static void Info(string message) => Debug.Log($"[A3D][{ProcessTag}] {message}");
        public static void Warn(string message) => Debug.LogWarning($"[A3D][{ProcessTag}] {message}");
        public static void Error(string message) => Debug.LogError($"[A3D][{ProcessTag}] {message}");

        /// <summary>Emit a structured event: [A3D:EVT] {"tag":"..","evt":"..",...}.</summary>
        public static void Event(string evt, string jsonFields = null)
        {
            var body = string.IsNullOrEmpty(jsonFields) ? "" : "," + jsonFields;
            Debug.Log("[A3D:EVT] {\"tag\":\"" + ProcessTag + "\",\"evt\":\"" + evt + "\",\"t\":" +
                      F(Time.realtimeSinceStartup) + ",\"wall\":" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + body + "}");
        }

        public static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        public static string V3(Vector3 v) => "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";
        public static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") + "\"";
    }
}
