using System.Collections.Generic;
using Adventure3D.Core;
using Adventure3D.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Adventure3D.Tests
{
    /// <summary>
    /// Measurable movement rules on the engineering arena (Battlefield-like weight translated to numbers:
    /// walk 4.5 m/s, sprint 7 m/s, ~0.15 s to full speed, 1.2 m jump with 0.4 s to apex, heavier fall).
    /// Every assertion also records target vs actual in Reports/movement-measurements.json.
    /// </summary>
    public class MovementTests
    {
        MovementHarness _h;

        [TearDown]
        public void TearDown()
        {
            _h?.Dispose();
            _h = null;
        }

        static readonly Vector2 Fwd = new Vector2(0f, 1f);
        float Tick => _h.Dt;

        [Test]
        public void WalkReachesTargetSpeedWithGradualAcceleration()
        {
            _h = new MovementHarness();
            _h.Spawn(new Vector3(0f, 0.05f, -10f));
            float t90 = -1f;
            _h.Run(1.5f, Fwd, stopWhen: () =>
            {
                if (t90 < 0f && _h.Player.State.HorizontalSpeed >= 0.9f * _h.Config.Movement.WalkSpeed) t90 = _h.Time;
                return false;
            });
            float speed = _h.Player.State.HorizontalSpeed;
            float expectedT90 = 0.9f * _h.Config.Movement.WalkSpeed / _h.Config.Movement.GroundAcceleration;
            Assert.IsTrue(Measurements.Record("walk.speed", _h.Config.Movement.WalkSpeed, speed, 0.01, "m/s"));
            Assert.IsTrue(Measurements.Record("walk.timeTo90Percent", expectedT90, t90, Tick, "s", "gradual acceleration"));
            Assert.Greater(t90, 0.05f, "acceleration must not be instant");
        }

        [Test]
        public void CorridorOfKnownLengthIsCrossedInPredictedTime()
        {
            _h = new MovementHarness();
            _h.Spawn(TestArenaLayout.CorridorStart + new Vector3(0f, 0.05f, 0f));
            float startZ = _h.Player.State.Position.z;
            _h.Run(10f, Fwd, stopWhen: () => _h.Player.State.Position.z - startZ >= TestArenaLayout.CorridorLength);
            var m = _h.Config.Movement;
            float expected = TestArenaLayout.CorridorLength / m.WalkSpeed + m.WalkSpeed / (2f * m.GroundAcceleration);
            Assert.IsTrue(Measurements.Record("corridor.20m.walkTime", expected, _h.Time, 2 * Tick, "s", "20 m marked corridor from standstill"));
            Assert.Less(Mathf.Abs(_h.Player.State.Position.x), 0.01f, "no sideways drift");
        }

        [Test]
        public void SprintSpeedAndForwardOnlyRule()
        {
            _h = new MovementHarness();
            _h.Spawn(new Vector3(4f, 0.05f, -20f));
            _h.Run(1.5f, Fwd, sprint: true);
            Assert.IsTrue(Measurements.Record("sprint.speed", _h.Config.Movement.SprintSpeed, _h.Player.State.HorizontalSpeed, 0.01, "m/s"));
            Assert.IsTrue(_h.Player.State.Sprinting);

            _h.Run(1.0f, new Vector2(1f, 0f), sprint: true); // strafing does not sprint
            Assert.IsTrue(Measurements.Record("sprint.strafeSpeed", _h.Config.Movement.WalkSpeed, _h.Player.State.HorizontalSpeed, 0.01, "m/s", "sprint only forward"));
            Assert.IsFalse(_h.Player.State.Sprinting);
        }

        [Test]
        public void DiagonalSpeedEqualsForwardSpeed()
        {
            _h = new MovementHarness();
            _h.Spawn(new Vector3(-4f, 0.05f, -20f));
            _h.Run(1.5f, new Vector2(1f, 1f));
            Assert.IsTrue(Measurements.Record("walk.diagonalSpeed", _h.Config.Movement.WalkSpeed, _h.Player.State.HorizontalSpeed, 0.01, "m/s"));
        }

        [Test]
        public void DecelerationStopsWithinPredictedDistance()
        {
            _h = new MovementHarness();
            _h.Spawn(new Vector3(-10f, 0.05f, -20f));
            _h.Run(1.0f, Fwd);
            float z0 = _h.Player.State.Position.z;
            _h.Time = 0f;
            _h.Run(1.0f, Vector2.zero, stopWhen: () => _h.Player.State.HorizontalSpeed < 1e-4f);
            var m = _h.Config.Movement;
            Assert.IsTrue(Measurements.Record("walk.stopTime", m.WalkSpeed / m.GroundDeceleration, _h.Time, Tick, "s"));
            Assert.IsTrue(Measurements.Record("walk.stopDistance", m.WalkSpeed * m.WalkSpeed / (2 * m.GroundDeceleration), _h.Player.State.Position.z - z0, 0.03, "m"));
        }

        [Test]
        public void JumpHeightApexTimeAndAirTime()
        {
            _h = new MovementHarness();
            _h.Spawn(new Vector3(10f, 0.05f, -20f));
            float y0 = _h.Player.State.Position.y;
            float maxY = y0, apexT = 0f, landT = -1f;
            float minVy = 0f;
            _h.Run(1.5f, Vector2.zero, jumpOnTick: i => i == 0, stopWhen: () =>
            {
                var s = _h.Player.State;
                if (s.Position.y > maxY) { maxY = s.Position.y; apexT = _h.Time; }
                minVy = Mathf.Min(minVy, s.Velocity.y);
                if (_h.Time > 0.05f && s.Landed && landT < 0f) landT = _h.Time;
                return landT > 0f;
            });
            var a = _h.Config.Air;
            float expectedAir = a.TimeToApex + Mathf.Sqrt(2f * a.JumpHeight / (a.Gravity * a.FallGravityMultiplier));
            Assert.IsTrue(Measurements.Record("jump.height", a.JumpHeight, maxY - y0, 0.02, "m"));
            Assert.IsTrue(Measurements.Record("jump.timeToApex", a.TimeToApex, apexT, Tick, "s"));
            Assert.IsTrue(Measurements.Record("jump.airTime", expectedAir, landT, 2 * Tick, "s", "heavier fall (x" + a.FallGravityMultiplier + ")"));
            Assert.IsTrue(Measurements.Record("jump.landingSpeed", a.Gravity * a.FallGravityMultiplier * (expectedAir - a.TimeToApex), _h.Player.State.LandingSpeed, 0.3, "m/s"));
            Assert.AreEqual(1, _h.Count(MotorEvents.Jumped));
            Assert.AreEqual(1, _h.Count(MotorEvents.Landed));
        }

        [Test]
        public void RepeatedJumpPressesInTheAirDoNotJumpAgain()
        {
            _h = new MovementHarness();
            _h.Spawn(new Vector3(12f, 0.05f, -20f));
            float y0 = _h.Player.State.Position.y, maxY = y0;
            _h.Run(0.6f, Vector2.zero, jumpOnTick: _ => true, stopWhen: () => { maxY = Mathf.Max(maxY, _h.Player.State.Position.y); return false; });
            Measurements.Record("jump.airJumpsWhileSpamming", 1, _h.Count(MotorEvents.Jumped), 0, "jumps");
            Assert.AreEqual(1, _h.Count(MotorEvents.Jumped));
            Assert.AreEqual(_h.Config.Air.JumpHeight, maxY - y0, 0.02f);
        }

        PlayerAvatar WalkOffLedge(out int ticksAirborne, int waitTicks, bool pressAfterWait)
        {
            _h.Spawn(new Vector3(TestArenaLayout.LedgeEdgeX - 1.0f, TestArenaLayout.LedgeHeight + 0.05f, TestArenaLayout.LedgeCenterTop.z), 90f);
            ticksAirborne = -1;
            var move = new Vector2(0f, 1f);
            for (int i = 0; i < 300; i++)
            {
                bool press = ticksAirborne == waitTicks && pressAfterWait;
                _h.Step(new PlayerCommand { Move = move, Yaw = 90f, Buttons = press ? CommandButtons.JumpPressed : CommandButtons.None });
                if (ticksAirborne < 0 && !_h.Player.State.Grounded) ticksAirborne = 0;
                else if (ticksAirborne >= 0) ticksAirborne++;
                if (ticksAirborne > waitTicks + 5) break;
            }
            return _h.Player;
        }

        [Test]
        public void CoyoteTimeAllowsLateJumpButNeverAnExtraJump()
        {
            _h = new MovementHarness();
            WalkOffLedge(out _, 3, true); // 0.05 s after leaving the edge
            Assert.AreEqual(1, _h.Count(MotorEvents.Jumped), "jump inside coyote window");
            Measurements.Record("coyote.jumpAt0.05s", 1, _h.Count(MotorEvents.Jumped), 0, "jumps", "window " + _h.Config.Air.CoyoteTime + " s");

            // another press right after the coyote jump must not jump again
            _h.Run(0.2f, Vector2.zero, jumpOnTick: _ => true);
            Assert.AreEqual(1, _h.Count(MotorEvents.Jumped), "coyote must not grant a second jump");
            _h.Dispose();

            _h = new MovementHarness();
            WalkOffLedge(out _, 12, true); // 0.2 s: too late
            Measurements.Record("coyote.jumpAt0.20s", 0, _h.Count(MotorEvents.Jumped), 0, "jumps");
            Assert.AreEqual(0, _h.Count(MotorEvents.Jumped), "jump after coyote window must fail");
        }

        [Test]
        public void CoyoteTimeCanBeDisabled()
        {
            _h = new MovementHarness(c => c.Air.CoyoteTimeEnabled = false);
            WalkOffLedge(out _, 1, true);
            Measurements.Record("coyote.disabled.jumpAt0.017s", 0, _h.Count(MotorEvents.Jumped), 0, "jumps");
            Assert.AreEqual(0, _h.Count(MotorEvents.Jumped));
        }

        int JumpThenPressBeforeLanding(MovementHarness h)
        {
            h.Spawn(new Vector3(14f, 0.05f, -20f));
            float y0 = h.Player.State.Position.y;
            bool pressed = false;
            for (int i = 0; i < 120; i++)
            {
                var s = h.Player.State;
                bool press = i == 0;
                if (!pressed && i > 5 && s.Velocity.y < 0f && s.Position.y - y0 < 0.25f) { press = true; pressed = true; }
                h.Step(new PlayerCommand { Buttons = press ? CommandButtons.JumpPressed : CommandButtons.None });
            }
            Assert.IsTrue(pressed);
            return h.Count(MotorEvents.Jumped);
        }

        [Test]
        public void JumpBufferExecutesEarlyPressOnLanding()
        {
            _h = new MovementHarness();
            int jumps = JumpThenPressBeforeLanding(_h);
            Measurements.Record("buffer.enabled.jumps", 2, jumps, 0, "jumps", "press < 0.12 s before landing");
            Assert.AreEqual(2, jumps);
            _h.Dispose();

            _h = new MovementHarness(c => c.Air.JumpBufferEnabled = false);
            jumps = JumpThenPressBeforeLanding(_h);
            Measurements.Record("buffer.disabled.jumps", 1, jumps, 0, "jumps");
            Assert.AreEqual(1, jumps);
        }

        [Test]
        public void LowCeilingStopsTheJump()
        {
            _h = new MovementHarness();
            _h.Spawn(TestArenaLayout.CeilingCenter + new Vector3(0f, 0.05f, 0f));
            float y0 = _h.Player.State.Position.y, maxY = y0;
            bool landed = false;
            _h.Run(1.5f, Vector2.zero, jumpOnTick: i => i == 0, stopWhen: () =>
            {
                maxY = Mathf.Max(maxY, _h.Player.State.Position.y);
                if (_h.Time > 0.1f && _h.Player.State.Grounded) landed = true;
                return landed;
            });
            float expected = TestArenaLayout.CeilingClearance - _h.Config.Physics.CapsuleHeight;
            Assert.IsTrue(Measurements.Record("ceiling.maxRise", expected, maxY - y0, 0.07, "m", "clearance " + TestArenaLayout.CeilingClearance + " m"));
            Assert.GreaterOrEqual(_h.Count(MotorEvents.HitCeiling), 1);
            Assert.IsTrue(landed, "player falls back down after hitting the ceiling");
        }

        [Test]
        public void TunnelLowerThanCapsuleBlocksThePlayer()
        {
            _h = new MovementHarness();
            _h.Spawn(TestArenaLayout.TunnelEntrance + new Vector3(0f, 0.05f, -2f));
            _h.Run(3f, Fwd);
            float limit = TestArenaLayout.TunnelEntrance.z - _h.Config.Physics.CapsuleRadius + 0.05f;
            Measurements.RecordBound("tunnel.maxZ", limit, _h.Player.State.Position.z, true, "m", "clearance " + TestArenaLayout.TunnelClearance + " m");
            Assert.Less(_h.Player.State.Position.z, limit);
            Assert.Less(_h.Player.State.Position.y, 0.1f);
        }

        [Test]
        public void WalkableRampIsClimbed()
        {
            _h = new MovementHarness();
            _h.Spawn(TestArenaLayout.WalkableRampBase + new Vector3(0f, 0.05f, -1f));
            int grounded = 0, total = 0;
            float maxY = 0f;
            _h.Run(1.2f, Fwd, stopWhen: () =>
            {
                total++;
                if (_h.Player.State.Grounded) grounded++;
                maxY = Mathf.Max(maxY, _h.Player.State.Position.y);
                return false;
            });
            Measurements.RecordBound("ramp40.heightAfter3s", 2.0, maxY, false, "m", TestArenaLayout.WalkableRampAngle + " deg (limit " + _h.Config.Movement.MaxWalkableSlope + ")");
            Assert.Greater(maxY, 2.0f);
            Assert.Greater(grounded / (float)total, 0.9f, "stays grounded on a walkable slope");
        }

        [Test]
        public void SteepRampIsNotClimbed()
        {
            _h = new MovementHarness();
            _h.Spawn(TestArenaLayout.SteepRampBase + new Vector3(0f, 0.05f, -1f));
            float maxY = 0f;
            _h.Run(3f, Fwd, stopWhen: () => { maxY = Mathf.Max(maxY, _h.Player.State.Position.y); return false; });
            Measurements.RecordBound("ramp50.maxHeight", 0.6, maxY, true, "m", TestArenaLayout.SteepRampAngle + " deg");
            Assert.Less(maxY, 0.6f);
        }

        [Test]
        public void StairsAreClimbedToTheTop()
        {
            _h = new MovementHarness();
            _h.Spawn(TestArenaLayout.StairsBase + new Vector3(0f, 0.05f, -1f));
            int steps = 0;
            _h.Run(1.4f, Fwd, stopWhen: () => { if ((_h.Player.State.Events & MotorEvents.SteppedUp) != 0) steps++; return false; });
            Measurements.RecordBound("stairs.motorStepUps", 0, steps, false, "steps", "fallback step-ups by PlayerMotor (0 = PhysX stepOffset climbed every step)");
            Assert.IsTrue(Measurements.Record("stairs.finalHeight", TestArenaLayout.StairsTopHeight, _h.Player.State.Position.y, 0.06, "m",
                TestArenaLayout.StairCount + " steps of " + TestArenaLayout.StairRise + " m"));
            Assert.IsTrue(_h.Player.State.Grounded);
        }

        [Test]
        public void FallingFromLedgeLandsWithExpectedSpeed()
        {
            _h = new MovementHarness();
            WalkOffLedge(out _, 0, false);
            _h.Run(2f, Vector2.zero, stopWhen: () => _h.Player.State.Grounded);
            var a = _h.Config.Air;
            // Fall height measured from where the capsule actually left the edge (its rounded bottom rolls off slightly lower).
            float drop = _h.LeaveGroundY - _h.Player.State.Position.y;
            Measurements.Record("ledge.dropHeight", TestArenaLayout.LedgeHeight, drop, 0.3, "m");
            float expected = Mathf.Sqrt(2f * a.Gravity * a.FallGravityMultiplier * drop);
            Assert.IsTrue(Measurements.Record("ledge.3m.landingSpeed", expected, _h.Player.State.LandingSpeed, 0.35, "m/s"));
            Assert.GreaterOrEqual(_h.Count(MotorEvents.Landed), 1);
            Assert.GreaterOrEqual(_h.Count(MotorEvents.LeftGround), 1);
        }

        [Test]
        public void FallSpeedIsCapped()
        {
            _h = new MovementHarness();
            _h.Spawn(new Vector3(-20f, 60f, -20f));
            float minVy = 0f;
            _h.Run(5f, Vector2.zero, stopWhen: () => { minVy = Mathf.Min(minVy, _h.Player.State.Velocity.y); return _h.Player.State.Grounded; });
            Assert.IsTrue(Measurements.Record("fall.maxSpeed", -_h.Config.Air.MaxFallSpeed, minVy, 0.001, "m/s"));
        }

        [Test]
        public void AirControlIsIndependentAndPartial()
        {
            _h = new MovementHarness();
            _h.Spawn(new Vector3(16f, 0.05f, -20f));
            _h.Run(0.5f, Vector2.zero, jumpOnTick: i => i == 0, stopWhen: () => false);
            float vx = Mathf.Abs(_h.Player.State.Velocity.x);
            Assert.AreEqual(0f, vx, 1e-4f);
            _h.Run(0.15f, new Vector2(1f, 0f));
            float gained = _h.Player.State.Velocity.x;
            Assert.IsTrue(Measurements.Record("air.speedGainedIn0.15s", _h.Config.Air.AirAcceleration * 0.15f, gained, 0.05, "m/s", "air acceleration"));
            Assert.IsTrue(_h.Player.State.Airborne);
        }
    
        [Test]
        public void CrouchLowersCapsuleSlowsDownAndPassesTheLowTunnel()
        {
            _h = new MovementHarness();
            _h.Spawn(TestArenaLayout.TunnelEntrance + new Vector3(0f, 0.05f, -2f));
            var crouchFwd = new PlayerCommand { Move = Fwd, Buttons = CommandButtons.Crouch };
            for (int i = 0; i < 60; i++) _h.Step(crouchFwd);
            Assert.IsTrue(_h.Player.State.Crouching);
            Assert.AreEqual(_h.Config.Movement.CrouchHeight, _h.Player.Controller.height, 1e-4f);
            float expected = _h.Config.Movement.WalkSpeed * _h.Config.Movement.CrouchSpeedMultiplier;
            Assert.IsTrue(Measurements.Record("crouch.speed", expected, _h.Player.State.HorizontalSpeed, 0.01, "m/s"));

            // 1.5 m tunnel: blocked standing (see TunnelLowerThanCapsuleBlocksThePlayer), passable crouched.
            for (int i = 0; i < 60; i++) _h.Step(crouchFwd);
            float inside = _h.Player.State.Position.z;
            Measurements.RecordBound("crouch.tunnelDepthReached", TestArenaLayout.TunnelEntrance.z + 1.5f, inside, false, "m");
            Assert.Greater(inside, TestArenaLayout.TunnelEntrance.z + 1.5f, "crouched player fits under the 1.5 m roof");

            // Releasing crouch under the roof keeps the player crouched (no head room).
            for (int i = 0; i < 10; i++) _h.Step(new PlayerCommand());
            Assert.IsTrue(_h.Player.State.Crouching, "cannot stand up under the roof");

            // Walk back out, then standing up works.
            for (int i = 0; i < 200; i++) _h.Step(new PlayerCommand { Move = new Vector2(0f, -1f) });
            for (int i = 0; i < 5; i++) _h.Step(new PlayerCommand());
            Assert.IsFalse(_h.Player.State.Crouching, "stands up again in the open");
            Assert.AreEqual(_h.Config.Physics.CapsuleHeight, _h.Player.Controller.height, 1e-4f);
        }

        [Test]
        public void WeaponWeightChangesRunSpeed()
        {
            _h = new MovementHarness();
            _h.Spawn(new Vector3(20f, 0.05f, -20f));
            var p = _h.Player;
            p.Combat.SetFlag(CombatFlags.Alive, true);
            p.Combat.ActiveSlot = WeaponSlot.Primary;
            foreach (var id in new[] { "knife", "m4a1", "ak47", "awp" })
            {
                var idx = WeaponCatalog.IndexOf(id);
                if (id == "knife") { p.Combat.ActiveSlot = WeaponSlot.Knife; p.Combat.KnifeWeapon = idx; }
                else { p.Combat.ActiveSlot = WeaponSlot.Primary; p.Combat.PrimaryWeapon = idx; }
                _h.Run(1.2f, Fwd);
                float expected = _h.Config.Movement.WalkSpeed * WeaponCatalog.Get(idx).MoveSpeedMultiplier;
                Assert.IsTrue(Measurements.Record("weight.walkSpeed." + id, expected, p.State.HorizontalSpeed, 0.01, "m/s"), id);
                _h.Run(0.5f, Vector2.zero);
            }
        }
    }
}
