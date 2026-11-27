using System.Collections.Generic;
using System.IO;
using System.Linq;
using Adventure3D.Bootstrap;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Identity;
using Adventure3D.Input;
using Adventure3D.Networking;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Adventure3D.Tests
{
    public class PlayerCommandTests
    {
        [Test]
        public void SanitizeClampsMoveAndRejectsNaN()
        {
            var c = new PlayerCommand { Move = new Vector2(3f, 4f), Yaw = 725f, Pitch = 200f, Buttons = (CommandButtons)0xFF }.Sanitized();
            Assert.AreEqual(1f, c.Move.magnitude, 1e-5f);
            Assert.AreEqual(5f, c.Yaw, 1e-4f);
            Assert.AreEqual(89f, c.Pitch);
            Assert.AreEqual((CommandButtons)0xFF, c.Buttons, "all eight buttons are valid (bit 7 = crouch)");

            var n = new PlayerCommand { Move = new Vector2(float.NaN, 1f), Yaw = float.PositiveInfinity }.Sanitized();
            Assert.AreEqual(Vector2.zero, n.Move);
            Assert.AreEqual(0f, n.Yaw);
        }

        [Test]
        public void JumpProfileDerivesGravityAndVelocity()
        {
            var air = ScriptableObject.CreateInstance<AirMovementProfile>();
            air.JumpHeight = 1.2f;
            air.TimeToApex = 0.4f;
            Assert.AreEqual(15f, air.Gravity, 1e-4f);      // 2h/t²
            Assert.AreEqual(6f, air.JumpVelocity, 1e-4f);  // 2h/t
        }

        [Test]
        public void GameConfigAssetHasAllProfiles()
        {
            var cfg = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            Assert.IsNotNull(cfg, "GameConfig asset missing");
            Assert.IsNotNull(cfg.Movement);
            Assert.IsNotNull(cfg.Air);
            Assert.IsNotNull(cfg.Physics);
            Assert.IsNotNull(cfg.Camera);
            Assert.IsNotNull(cfg.Input);
            Assert.IsNotNull(cfg.Network);
        }

        [Test]
        public void RuntimeCloneDoesNotTouchSharedAssets()
        {
            var shared = GameConfig.Load();
            float before = shared.Movement.WalkSpeed;
            var clone = shared.CloneForRuntime();
            clone.Movement.WalkSpeed = 99f;
            Assert.AreEqual(before, shared.Movement.WalkSpeed);
        }
    }

    public class NetMessageTests
    {
        [Test]
        public void HelloRoundTripsWithArabicName()
        {
            var w = new DataStreamWriter(512, Allocator.Temp);
            NetMessages.WriteHello(ref w, new HelloMsg { ProtocolVersion = 7, ProfileId = "abc123", DisplayName = "ضيف-AB12", AuthToken = "" });
            var r = new DataStreamReader(w.AsNativeArray());
            Assert.AreEqual(MsgType.Hello, (MsgType)r.ReadByte());
            var h = NetMessages.ReadHello(ref r);
            Assert.AreEqual(7, h.ProtocolVersion);
            Assert.AreEqual("abc123", h.ProfileId);
            Assert.AreEqual("ضيف-AB12", h.DisplayName);
            Assert.IsFalse(r.HasFailedReads);
        }

        [Test]
        public void SnapshotRoundTripsFullState()
        {
            var state = new PlayerSimState
            {
                Position = new Vector3(1, 2, 3), Velocity = new Vector3(-1, 5.5f, 0.25f), Yaw = 90, Pitch = -10,
                Flags = MotorFlags.Grounded | MotorFlags.Sprinting, Events = MotorEvents.Landed,
                CoyoteTimer = 0.1f, JumpBufferTimer = 0.05f, AirTime = 0.3f, LandingSpeed = 7f,
            };
            var entries = new List<SnapshotEntry> { new SnapshotEntry { PlayerId = 3, AckSequence = 99, State = state } };
            var w = new DataStreamWriter(1400, Allocator.Temp);
            NetMessages.WriteSnapshot(ref w, 1234, 600, entries);
            Assert.Less(w.Length, 220);
            var r = new DataStreamReader(w.AsNativeArray());
            r.ReadByte();
            var outList = new List<SnapshotEntry>();
            Assert.AreEqual(1234u, NetMessages.ReadSnapshot(ref r, outList, out uint roundStart));
            Assert.AreEqual(600u, roundStart);
            Assert.AreEqual(1, outList.Count);
            Assert.AreEqual(99u, outList[0].AckSequence);
            Assert.AreEqual(state.Position, outList[0].State.Position);
            Assert.AreEqual(state.Velocity, outList[0].State.Velocity);
            Assert.AreEqual(state.Flags, outList[0].State.Flags);
            Assert.AreEqual(state.JumpBufferTimer, outList[0].State.JumpBufferTimer);
        }

        [Test]
        public void InputRoundTripsRedundantCommands()
        {
            var cmds = new List<PlayerCommand>();
            for (uint i = 1; i <= 6; i++) cmds.Add(new PlayerCommand { Sequence = i, Move = new Vector2(0, 1), Yaw = i, Buttons = CommandButtons.Sprint });
            var w = new DataStreamWriter(512, Allocator.Temp);
            NetMessages.WriteInput(ref w, cmds, 2, 4);
            var r = new DataStreamReader(w.AsNativeArray());
            r.ReadByte();
            var got = new List<PlayerCommand>();
            NetMessages.ReadInput(ref r, got);
            CollectionAssert.AreEqual(new uint[] { 3, 4, 5, 6 }, got.Select(c => c.Sequence).ToArray());
        }
    }

    public class AddressAndOptionsTests
    {
        [TestCase("127.0.0.1", AddressTools.Kind.IPv4)]
        [TestCase("192.168.1.20", AddressTools.Kind.IPv4)]
        [TestCase("::1", AddressTools.Kind.IPv6)]
        [TestCase("game.example.com", AddressTools.Kind.HostName)]
        [TestCase("localhost", AddressTools.Kind.HostName)]
        [TestCase("999.1.1", AddressTools.Kind.Invalid)]
        [TestCase("300.1.1.1", AddressTools.Kind.Invalid)]
        [TestCase("", AddressTools.Kind.Invalid)]
        [TestCase("bad host!", AddressTools.Kind.Invalid)]
        public void ClassifiesAddresses(string address, AddressTools.Kind expected)
        {
            Assert.AreEqual(expected, AddressTools.Classify(address));
        }

        [Test]
        public void ParsesPorts()
        {
            Assert.IsTrue(AddressTools.TryParsePort("7777", out var p));
            Assert.AreEqual(7777, p);
            Assert.IsFalse(AddressTools.TryParsePort("0", out _));
            Assert.IsFalse(AddressTools.TryParsePort("70000", out _));
            Assert.IsFalse(AddressTools.TryParsePort("abc", out _));
        }

        [Test]
        public void ParsesCommandLine()
        {
            var o = LaunchOptions.Parse(new[] { "game.exe", "-batchmode", "-mode", "server", "-port", "7800", "-simLatency", "120", "-tag", "s1", "-quitAfter", "12.5" });
            Assert.AreEqual(SessionMode.DedicatedServer, o.Mode);
            Assert.AreEqual(7800, o.Port);
            Assert.AreEqual(120, o.SimLatency);
            Assert.AreEqual("s1", o.Tag);
            Assert.AreEqual(12.5f, o.QuitAfter);

            var c = LaunchOptions.Parse(new[] { "-mode", "client", "-address", "10.0.0.5" });
            var cfg = GameConfig.CreateDefault();
            var req = c.ToRequest(cfg.Network);
            Assert.AreEqual(SessionMode.Client, req.Mode);
            Assert.AreEqual("10.0.0.5", req.Address);
            Assert.AreEqual(cfg.Network.DefaultPort, req.Port);

            Assert.IsNull(LaunchOptions.Parse(new[] { "game.exe" }).ToRequest(cfg.Network), "no -mode -> menu");
        }
    }

    public class GuestIdentityTests
    {
        string _dir;

        [SetUp] public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "a3d-identity-" + System.Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { try { Directory.Delete(_dir, true); } catch { } }

        [Test]
        public void GuestIdSurvivesRestart()
        {
            string first;
            using (var p = new GuestIdentityProvider(_dir)) first = p.SignIn().LocalProfileId;
            string second;
            using (var p = new GuestIdentityProvider(_dir)) second = p.SignIn().LocalProfileId;
            Assert.IsNotEmpty(first);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void TwoRunningCopiesGetDifferentPersistentIds()
        {
            using var a = new GuestIdentityProvider(_dir);
            using var b = new GuestIdentityProvider(_dir);
            var ia = a.SignIn();
            var ib = b.SignIn();
            Assert.AreNotEqual(ia.LocalProfileId, ib.LocalProfileId);
            Assert.AreEqual(0, a.Slot);
            Assert.AreEqual(1, b.Slot);
        }

        [Test]
        public void GuestHasNoAccountAndAccountsAreHonestlyUnavailable()
        {
            using var p = new GuestIdentityProvider(_dir, "tester");
            var id = p.SignIn();
            Assert.IsTrue(id.IsGuest);
            Assert.IsNull(id.AccountId);
            var accounts = new UnavailableAccountProvider();
            Assert.IsFalse(accounts.IsAvailable);
            var result = accounts.SignInAsync(new AccountCredentials { Login = "x", Secret = "y" }).Result;
            Assert.IsFalse(result.Success);
            Assert.AreEqual(AuthError.NotConfigured, result.Error);
        }
    }

    public class TouchModelTests
    {
        static readonly Rect Screen = new Rect(0, 0, 1920, 1080);

        [Test]
        public void MoveLookAndJumpWorkTogether()
        {
            var m = new TouchControlsModel { StickRadius = 0.09f, StickDeadzone = 0.1f };
            m.Process(new[]
            {
                new TouchSample(1, new Vector2(300, 300), TouchPhaseLite.Began),
                new TouchSample(2, new Vector2(1300, 600), TouchPhaseLite.Began),
            }, Screen);
            m.Process(new[]
            {
                new TouchSample(1, new Vector2(400, 300), TouchPhaseLite.Moved),     // stick right
                new TouchSample(2, new Vector2(1350, 600), TouchPhaseLite.Moved),    // look right
                new TouchSample(3, new Vector2(1730, 150), TouchPhaseLite.Began),    // jump button
            }, Screen);
            Assert.Greater(m.Move.x, 0.5f);
            Assert.Greater(m.LookDelta.x, 0f);
            Assert.IsTrue(m.JumpPressedThisFrame);
            Assert.AreEqual(3, m.ActiveFingerCount);
        }

        [Test]
        public void SprintButtonAndResetOnFocusLoss()
        {
            var m = new TouchControlsModel();
            m.Process(new[] { new TouchSample(5, new Vector2(1460, 150), TouchPhaseLite.Began) }, Screen);
            Assert.IsTrue(m.SprintHeld);
            m.Reset();
            Assert.IsFalse(m.SprintHeld);
            Assert.AreEqual(0, m.ActiveFingerCount);
        }

        [Test]
        public void FingerThatDisappearsIsReleased()
        {
            var m = new TouchControlsModel();
            m.Process(new[] { new TouchSample(1, new Vector2(300, 300), TouchPhaseLite.Began) }, Screen);
            m.Process(new[] { new TouchSample(1, new Vector2(450, 300), TouchPhaseLite.Moved) }, Screen);
            Assert.AreNotEqual(Vector2.zero, m.Move);
            m.Process(new TouchSample[0], Screen);
            Assert.AreEqual(Vector2.zero, m.Move);
        }

        [Test]
        public void GamepadDeadzoneAndCurve()
        {
            Assert.AreEqual(Vector2.zero, LocalPlayerInput.ApplyDeadzone(new Vector2(0.1f, 0f), 0.15f, 1f));
            Assert.AreEqual(1f, LocalPlayerInput.ApplyDeadzone(new Vector2(1f, 0f), 0.15f, 2f).x, 1e-4f);
            float half = LocalPlayerInput.ApplyDeadzone(new Vector2(0.575f, 0f), 0.15f, 2f).x;
            Assert.AreEqual(0.25f, half, 0.01f); // (0.5)^2 after rescale
        }
    }
}
