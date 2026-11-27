using System.Collections;
using System.Linq;
using Adventure3D.Bootstrap;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Gameplay;
using Adventure3D.Identity;
using Adventure3D.Networking;
using Adventure3D.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Adventure3D.Tests
{
    public class SessionTests
    {
        static void DestroyLeftovers()
        {
            foreach (var a in Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None)) Object.DestroyImmediate(a.gameObject);
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t != null && t.name == "Players" && t.parent == null).Select(t => t.gameObject).ToArray())
                Object.DestroyImmediate(go);
        }

        static Vector3 RunOffline(GameConfig cfg, System.Func<int, float> frameDt, uint ticks)
        {
            var world = new WorldSimulation(cfg);
            var session = new GameSession(LaunchRequest.Offline(), cfg, null, world) { OfflineBots = 0 };
            var script = new ScriptedCommandSource(cfg.Physics.TickDelta);
            script.Add(1.0f, new Vector2(0, 1)).Add(1.0f, new Vector2(0, 1), sprint: true)
                  .Add(0.1f, new Vector2(0, 1), sprint: true, jumpAtStart: true).Add(1.5f, new Vector2(1, 1)).Wait(5f);
            session.LocalCommands = script;
            Assert.IsTrue(session.Start());
            Vector3 pos = default;
            bool captured = false;
            session.Ticked += tick =>
            {
                if (tick == ticks) { pos = session.LocalAvatar.State.Position; captured = true; }
            };
            int frame = 0;
            while (!captured)
            {
                session.Update(frameDt(frame++));
                session.LateUpdate(0.016f);
            }
            session.Dispose();
            DestroyLeftovers();
            return pos;
        }

        [Test]
        public void SimulationIsFrameRateIndependent()
        {
            var arena = TestArenaLayout.Build();
            try
            {
                var cfg = GameConfig.Load().CloneForRuntime();
                cfg.Physics.MaxTicksPerFrame = 30;
                const uint ticks = 240;
                // Frame steps are integer multiples of the tick so runs stop exactly on tick 240.
                Vector3 p30 = RunOffline(cfg, _ => 2f / 60f, ticks);
                Vector3 p60 = RunOffline(cfg, _ => 1f / 60f, ticks);
                Vector3 p120 = RunOffline(cfg, _ => 1f / 120f + 1e-7f, ticks);
                var rng = new System.Random(4);
                Vector3 pJitter = RunOffline(cfg, _ => (float)(0.004 + rng.NextDouble() * 0.03), ticks);

                float d30 = Vector3.Distance(p30, p60), d120 = Vector3.Distance(p120, p60), dj = Vector3.Distance(pJitter, p60);
                Measurements.Record("framerate.drift.30fps_vs_60fps", 0, d30, 1e-4, "m", "tick 240 at fixed 60 Hz simulation");
                Measurements.Record("framerate.drift.120fps_vs_60fps", 0, d120, 1e-4, "m", "tick 240");
                Measurements.Record("framerate.drift.jitter_vs_60fps", 0, dj, 1e-4, "m", "random 4..34 ms frames");
                Assert.Less(d30, 1e-4f);
                Assert.Less(d120, 1e-4f);
                Assert.Less(dj, 1e-4f);
                Assert.Greater(p60.magnitude, 5f, "the scripted path actually moved");
            }
            finally
            {
                Object.DestroyImmediate(arena.gameObject);
            }
        }

        [Test]
        public void OfflineSessionsDoNotLeakPlayersOrSessions()
        {
            var arena = TestArenaLayout.Build();
            try
            {
                var cfg = GameConfig.Load().CloneForRuntime();
                for (int i = 0; i < 3; i++)
                {
                    var session = new GameSession(LaunchRequest.Offline(), cfg, null, new WorldSimulation(cfg)) { OfflineBots = 0 };
                    Assert.IsTrue(session.Start());
                    session.Update(0.1f);
                    Assert.AreEqual(1, session.PlayerCount);
                    Assert.AreEqual(1, GameSession.LiveSessions);
                    session.Dispose();
                    DestroyLeftovers();
                    Assert.AreEqual(0, GameSession.LiveSessions);
                    Assert.AreEqual(0, PlayerAvatar.LiveCount);
                }
            }
            finally
            {
                Object.DestroyImmediate(arena.gameObject);
            }
        }

        [Test]
        public void OfflinePauseFreezesSimulation()
        {
            var arena = TestArenaLayout.Build();
            var cfg = GameConfig.Load().CloneForRuntime();
            var session = new GameSession(LaunchRequest.Offline(), cfg, null, new WorldSimulation(cfg)) { OfflineBots = 0 };
            try
            {
                session.LocalCommands = new ScriptedCommandSource(cfg.Physics.TickDelta).Add(5f, new Vector2(0, 1));
                session.Start();
                session.Update(0.5f);
                uint t = session.Tick;
                var p = session.LocalAvatar.State.Position;
                session.Paused = true;
                for (int i = 0; i < 30; i++) session.Update(1f / 60f);
                Assert.AreEqual(t, session.Tick);
                Assert.AreEqual(p, session.LocalAvatar.State.Position);
                session.Paused = false;
                session.Update(0.1f);
                Assert.Greater(session.Tick, t);
            }
            finally
            {
                session.Dispose();
                DestroyLeftovers();
                Object.DestroyImmediate(arena.gameObject);
            }
        }

        // ------------------------------------------------------------------ network edge cases (in-process, localhost)

        static IEnumerator Pump(GameSession a, GameSession b, float timeout, System.Func<bool> done)
        {
            float t = 0f;
            while (t < timeout && !done())
            {
                float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                a?.Update(dt);
                b?.Update(dt);
                t += dt;
                yield return null;
            }
        }

        static PlayerIdentity TestIdentity(string id) => new PlayerIdentity { Kind = IdentityKind.Guest, LocalProfileId = id, DisplayName = "اختبار" };

        [UnityTest]
        public IEnumerator InvalidAddressFailsImmediately()
        {
            var cfg = GameConfig.Load().CloneForRuntime();
            var client = new GameSession(new LaunchRequest { Mode = SessionMode.Client, Address = "999.1.1", Port = 7777 }, cfg, TestIdentity("c"), new WorldSimulation(cfg));
            Assert.IsFalse(client.Start());
            Assert.AreEqual(SessionEndReason.InvalidAddress, client.EndReason);
            client.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConnectingToNothingTimesOut()
        {
            var cfg = GameConfig.Load().CloneForRuntime();
            cfg.Network.ConnectTimeoutMs = 250;
            cfg.Network.MaxConnectAttempts = 3;
            var client = new GameSession(new LaunchRequest { Mode = SessionMode.Client, Address = "127.0.0.1", Port = 7899 }, cfg, TestIdentity("c"), new WorldSimulation(cfg));
            Assert.IsTrue(client.Start());
            LogAssert.ignoreFailingMessages = true; // UTP logs ICMP "port unreachable" socket errors on localhost
            float start = Time.realtimeSinceStartup;
            yield return Pump(client, null, 8f, () => client.Status == SessionStatus.Ended);
            float took = Time.realtimeSinceStartup - start;
            LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(SessionEndReason.ConnectTimeout, client.EndReason);
            Measurements.RecordBound("net.connectTimeout.seconds", 3.0, took, true, "s", "250 ms x 3 attempts");
            client.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator IncompatibleProtocolIsRejectedWithReason()
        {
            var cfg = GameConfig.Load().CloneForRuntime();
            var server = new GameSession(new LaunchRequest { Mode = SessionMode.DedicatedServer, Port = 7841 }, cfg, null, new WorldSimulation(cfg));
            Assert.IsTrue(server.Start());
            var client = new GameSession(new LaunchRequest { Mode = SessionMode.Client, Address = "127.0.0.1", Port = 7841 }, cfg, TestIdentity("c1"), new WorldSimulation(cfg));
            client.ProtocolVersionOverride = 999;
            Assert.IsTrue(client.Start());
            yield return Pump(server, client, 6f, () => client.Status == SessionStatus.Ended);
            Assert.AreEqual(SessionEndReason.IncompatibleVersion, client.EndReason);
            Assert.AreEqual(0, server.PlayerCount, "rejected client never gets a player");
            client.Dispose();
            server.Dispose();
            yield return null;
            DestroyLeftovers();
        }

        [UnityTest]
        public IEnumerator FullServerRejectsClient()
        {
            var cfg = GameConfig.Load().CloneForRuntime();
            cfg.Network.MaxPlayers = 0;
            var server = new GameSession(new LaunchRequest { Mode = SessionMode.DedicatedServer, Port = 7842 }, cfg, null, new WorldSimulation(cfg));
            Assert.IsTrue(server.Start());
            var client = new GameSession(new LaunchRequest { Mode = SessionMode.Client, Address = "127.0.0.1", Port = 7842 }, cfg, TestIdentity("c2"), new WorldSimulation(cfg));
            Assert.IsTrue(client.Start());
            yield return Pump(server, client, 6f, () => client.Status == SessionStatus.Ended);
            Assert.AreEqual(SessionEndReason.ServerFull, client.EndReason);
            client.Dispose();
            server.Dispose();
            yield return null;
            DestroyLeftovers();
        }

        [UnityTest]
        public IEnumerator PortAlreadyInUseFailsToHost()
        {
            var cfg = GameConfig.Load().CloneForRuntime();
            var first = new GameSession(new LaunchRequest { Mode = SessionMode.DedicatedServer, Port = 7843 }, cfg, null, new WorldSimulation(cfg));
            Assert.IsTrue(first.Start());
            var second = new GameSession(new LaunchRequest { Mode = SessionMode.DedicatedServer, Port = 7843 }, cfg, null, new WorldSimulation(cfg));
            LogAssert.ignoreFailingMessages = true; // the transport logs the bind error
            bool started = second.Start();
            LogAssert.ignoreFailingMessages = false;
            Assert.IsFalse(started);
            Assert.AreEqual(SessionEndReason.BindFailed, second.EndReason);
            second.Dispose();
            first.Dispose();
            yield return null;
        }

        // ------------------------------------------------------------------ real entry point flow

        [UnityTest]
        public IEnumerator BootstrapMenuOfflineAndBackTwice()
        {
            SceneManager.LoadScene(SceneNames.Bootstrap);
            yield return WaitScene(SceneNames.MainMenu);
            Assert.AreEqual(1, AppRoot.LiveInstances);
            var app = AppRoot.Instance;
            Assert.IsNotNull(app.Identity);

            for (int round = 0; round < 2; round++)
            {
                MenuServices.Current.StartSession(LaunchRequest.Offline());
                yield return WaitScene(SceneNames.TestArena);
                for (int i = 0; i < 30; i++) yield return null;
                var arena = ArenaEntryPoint.Current;
                Assert.AreEqual(SessionStatus.Running, arena.Session.Status);
                Assert.AreEqual(arena.Session.PlayerCount, PlayerAvatar.LiveCount, "no duplicated or leftover players");
                Assert.AreEqual(1, arena.Session.World.Players.Count(p => p.IsLocal), "exactly one local player");
                Assert.AreEqual(1 + arena.Session.OfflineBots, arena.Session.PlayerCount, "local player + training bots");
                Assert.AreEqual(1, GameSession.LiveSessions);
                Assert.AreEqual(1, SessionRunner.LiveRunners);
                Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(2, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, "player camera + first-person weapon camera");
                arena.Hud.LeaveButton.onClick.Invoke();
                yield return WaitScene(SceneNames.MainMenu);
                yield return null;
                Assert.AreEqual(0, PlayerAvatar.LiveCount);
                Assert.AreEqual(0, GameSession.LiveSessions);
                Assert.AreEqual(1, AppRoot.LiveInstances);
            }

            // Loading Bootstrap again must not create a second AppRoot.
            SceneManager.LoadScene(SceneNames.Bootstrap);
            yield return WaitScene(SceneNames.MainMenu);
            Assert.AreEqual(1, AppRoot.LiveInstances);
            Object.Destroy(app.gameObject);
            yield return null;
        }

        static IEnumerator WaitScene(string name)
        {
            float t = 0f;
            while (SceneManager.GetActiveScene().name != name && t < 10f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            yield return null;
        }
    }
}
