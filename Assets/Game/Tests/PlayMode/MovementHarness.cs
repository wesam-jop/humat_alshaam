using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Adventure3D.Tests
{
    /// <summary>Builds the test arena and one player, and steps the real motor tick by tick.</summary>
    public sealed class MovementHarness : IDisposable
    {
        public readonly GameConfig Config;
        public readonly WorldSimulation World;
        public readonly Transform Arena;
        public PlayerAvatar Player;
        public float Dt => World.TickDelta;
        public float Time;
        public float LeaveGroundY;
        public readonly List<MotorEvents> EventLog = new List<MotorEvents>();

        public MovementHarness(Action<GameConfig> tweak = null)
        {
            Config = GameConfig.Load().CloneForRuntime();
            tweak?.Invoke(Config);
            Arena = TestArenaLayout.Build();
            World = new WorldSimulation(Config);
        }

        public PlayerAvatar Spawn(Vector3 position, float yaw = 0f)
        {
            Player = World.Spawn(1, true, new Pose(position, Quaternion.Euler(0f, yaw, 0f)));
            // settle onto the ground
            for (int i = 0; i < 5; i++) Step(PlayerCommand.Neutral(yaw, 0f));
            Time = 0f;
            EventLog.Clear();
            return Player;
        }

        public void Step(PlayerCommand cmd)
        {
            bool wasGrounded = Player.State.Grounded;
            World.Tick(_ => cmd);
            Time += Dt;
            if (wasGrounded && !Player.State.Grounded) LeaveGroundY = Player.State.Position.y;
            EventLog.Add(Player.State.Events);
        }

        public void Run(float seconds, Vector2 move, float yaw = 0f, bool sprint = false, Func<int, bool> jumpOnTick = null, Func<bool> stopWhen = null)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++)
            {
                var b = sprint ? CommandButtons.Sprint : CommandButtons.None;
                if (jumpOnTick != null && jumpOnTick(i)) b |= CommandButtons.JumpPressed | CommandButtons.JumpHeld;
                Step(new PlayerCommand { Move = move, Yaw = yaw, Buttons = b });
                if (stopWhen != null && stopWhen()) return;
            }
        }

        public int Count(MotorEvents e)
        {
            int n = 0;
            foreach (var ev in EventLog) if ((ev & e) != 0) n++;
            return n;
        }

        public void Dispose()
        {
            World.Dispose();
            foreach (var a in Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None)) Object.DestroyImmediate(a.gameObject);
            var players = GameObject.Find("Players");
            if (players != null) Object.DestroyImmediate(players);
            if (Arena != null) Object.DestroyImmediate(Arena.gameObject);
        }
    }

    /// <summary>Collects target vs measured values and writes Reports/movement-measurements.json.</summary>
    public static class Measurements
    {
        struct Entry
        {
            public string Name, Unit, Note;
            public double Target, Actual, Tolerance;
            public bool Pass;
        }

        static readonly List<Entry> Entries = new List<Entry>();

        public static bool Record(string name, double target, double actual, double tolerance, string unit, string note = "")
        {
            bool pass = Math.Abs(actual - target) <= tolerance;
            Entries.Add(new Entry { Name = name, Target = target, Actual = actual, Tolerance = tolerance, Unit = unit, Pass = pass, Note = note });
            Write();
            return pass;
        }

        public static void RecordBound(string name, double bound, double actual, bool mustBeBelow, string unit, string note = "")
        {
            bool pass = mustBeBelow ? actual <= bound : actual >= bound;
            Entries.Add(new Entry { Name = name, Target = bound, Actual = actual, Tolerance = double.NaN, Unit = unit, Pass = pass, Note = (mustBeBelow ? "<= " : ">= ") + bound + " " + note });
            Write();
        }

        static void Write()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\n  \"unity\": \"").Append(Application.unityVersion).Append("\",\n  \"tickRate\": 60,\n  \"conditions\": \"Editor Play Mode, fixed 60 Hz simulation stepped directly, Windows\",\n  \"entries\": [\n");
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                sb.Append("    {\"name\": \"").Append(e.Name).Append("\", \"target\": ").Append(e.Target.ToString("0.####", ci))
                  .Append(", \"actual\": ").Append(e.Actual.ToString("0.####", ci))
                  .Append(", \"tolerance\": ").Append(double.IsNaN(e.Tolerance) ? "null" : e.Tolerance.ToString("0.####", ci))
                  .Append(", \"unit\": \"").Append(e.Unit).Append("\", \"pass\": ").Append(e.Pass ? "true" : "false")
                  .Append(", \"note\": \"").Append(e.Note.Replace("\"", "'")).Append("\"}").Append(i < Entries.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            string dir = Path.Combine(Application.dataPath, "..", "Reports");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "movement-measurements.json"), sb.ToString(), new UTF8Encoding(false));
        }
    }
}
