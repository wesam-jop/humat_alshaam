using Adventure3D.Core;
using UnityEngine;

namespace Adventure3D.Gameplay
{
    /// <summary>
    /// The one movement step used everywhere (offline, host, dedicated server, client prediction and
    /// replay). It is a pure function of (collider placement, state, command, settings, dt) plus the
    /// static level geometry, so the server and a predicting client produce the same result.
    /// </summary>
    public static class PlayerMotor
    {
        struct GroundHit
        {
            public bool Hit;
            public bool Walkable;
            public float Gap;
            public Vector3 Normal;
        }

        const float CastStartOffset = 0.1f;
        const float CastRadiusScale = 0.9f;
        static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <param name="speedScale">Per-player ground speed factor (weapon weight, scoped aim). 1 = profile speed.</param>
        public static void Step(CharacterController cc, ref PlayerSimState s, PlayerCommand cmd, in MotorSettings m, float dt, float speedScale = 1f)
        {
            cmd = cmd.Sanitized();
            s.Events = MotorEvents.None;
            s.Yaw = cmd.Yaw;
            s.Pitch = cmd.Pitch;
            bool wasGrounded = s.Grounded;

            // ---- Crouch: shorter capsule; standing up needs head room.
            bool crouching = s.Crouching;
            if (cmd.Crouch && !crouching) { SetCrouchShape(cc, true, m); crouching = true; }
            else if (!cmd.Crouch && crouching && CanStand(cc, m)) { SetCrouchShape(cc, false, m); crouching = false; }
            s.SetFlag(MotorFlags.Crouching, crouching);

            // ---- Intent (diagonal input is normalized, so diagonal speed == forward speed).
            Quaternion yawRot = Quaternion.Euler(0f, s.Yaw, 0f);
            Vector3 fwd = yawRot * Vector3.forward;
            Vector3 right = yawRot * Vector3.right;
            Vector2 move = cmd.Move;
            if (move.sqrMagnitude > 1f) move.Normalize();
            bool sprintIntent = cmd.Sprint && move.y >= m.SprintForwardThreshold && !crouching;
            float targetSpeed = (sprintIntent ? m.SprintSpeed : m.WalkSpeed) * speedScale * (crouching ? m.CrouchSpeedMultiplier : 1f);
            Vector3 wish = (fwd * move.y + right * move.x) * targetSpeed;
            bool hasInput = wish.sqrMagnitude > 1e-6f;

            Vector3 hv = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            Vector3 hvStart = hv;
            float vy = s.Velocity.y;

            // ---- Horizontal acceleration (gradual, never instant).
            if (wasGrounded)
            {
                float rate = hasInput ? m.GroundAcceleration : m.GroundDeceleration;
                if (hasInput && (Vector3.Dot(wish, hv) < 0f || hv.magnitude > targetSpeed + 0.01f))
                    rate = Mathf.Max(m.GroundAcceleration, m.GroundDeceleration);
                hv = Vector3.MoveTowards(hv, wish, rate * dt);
            }
            else
            {
                // Independent horizontal control in the air.
                hv = hasInput
                    ? Vector3.MoveTowards(hv, wish, m.AirAcceleration * dt)
                    : Vector3.MoveTowards(hv, Vector3.zero, m.AirDeceleration * dt);
            }

            // ---- Jump (buffer + coyote, both optional; neither can grant a second jump).
            if (cmd.JumpPressed) s.JumpBufferTimer = m.JumpBufferEnabled ? m.JumpBufferTime : 0f;
            bool jumpRequested = cmd.JumpPressed || (m.JumpBufferEnabled && s.JumpBufferTimer > 0f);
            bool jumpConsumed = (s.Flags & MotorFlags.JumpConsumed) != 0;
            bool coyoteOk = m.CoyoteEnabled && s.CoyoteTimer > 0f;
            bool jumped = false;
            if (jumpRequested && !jumpConsumed && (wasGrounded || coyoteOk))
            {
                vy = m.JumpVelocity;
                jumped = true;
                s.SetFlag(MotorFlags.JumpConsumed, true);
                s.CoyoteTimer = 0f;
                s.JumpBufferTimer = 0f;
                s.Events |= MotorEvents.Jumped;
            }
            else
            {
                s.JumpBufferTimer = Mathf.Max(0f, s.JumpBufferTimer - dt);
            }

            // ---- Gravity (heavier on the way down, capped fall speed).
            float vyStart = vy;
            if (!wasGrounded || jumped)
            {
                float g = m.Gravity * (vy <= 0f ? m.FallGravityMultiplier : 1f);
                vy -= g * dt;
                if (vy < -m.MaxFallSpeed) vy = -m.MaxFallSpeed;
            }
            else
            {
                vy = 0f;
            }

            // ---- Move. Average velocity integration makes the jump arc exact for constant gravity.
            Vector3 startPos = cc.transform.position;
            Vector3 delta = (hvStart + hv) * (0.5f * dt); // trapezoid: exact distances for constant acceleration
            if (wasGrounded && !jumped)
                delta.y = -m.SkinWidth; // keep contact on flat ground and gentle slopes
            else
                delta.y = (vyStart + vy) * 0.5f * dt;

            CollisionFlags flags = cc.Move(delta);
            Vector3 actual = cc.transform.position - startPos;

            if ((flags & CollisionFlags.Above) != 0 && vy > 0f)
            {
                vy = 0f;
                s.Events |= MotorEvents.HitCeiling;
            }
            // Step up stairs / small ledges when blocked while walking (deterministic, same on server and client).
            if (wasGrounded && !jumped && (flags & CollisionFlags.Sides) != 0 && m.StepHeight > 0f)
            {
                Vector3 intended = new Vector3(delta.x, 0f, delta.z);
                Vector3 achieved = new Vector3(actual.x, 0f, actual.z);
                if (achieved.magnitude < intended.magnitude * 0.5f && TryStepUp(cc, intended - achieved, m))
                {
                    flags &= ~CollisionFlags.Sides;
                    actual = cc.transform.position - startPos;
                    s.Events |= MotorEvents.SteppedUp;
                }
            }
            if ((flags & CollisionFlags.Sides) != 0)
            {
                // Do not keep pushing velocity into a wall.
                Vector3 actualH = new Vector3(actual.x, 0f, actual.z) / dt;
                if (actualH.sqrMagnitude < hv.sqrMagnitude) hv = actualH;
            }

            // ---- Ground detection from the capsule shape and the surface direction.
            GroundHit ground = ProbeGround(cc, m, m.SkinWidth + m.GroundProbeDistance);
            bool grounded = ground.Walkable && vy <= 0.001f && !jumped;

            // Snap down stairs and ramps while walking; a bigger drop becomes a real fall.
            if (!grounded && wasGrounded && !jumped)
            {
                GroundHit snap = ProbeGround(cc, m, m.GroundSnapDistance);
                if (snap.Walkable)
                {
                    cc.Move(Vector3.down * (snap.Gap + m.SkinWidth));
                    grounded = true;
                    ground = snap;
                }
            }

            s.SetFlag(MotorFlags.OnSteepSlope, ground.Hit && !ground.Walkable);
            if (!grounded && (flags & CollisionFlags.Below) != 0 && vy < 0f)
            {
                // Sliding on a steep surface: vertical speed is what actually happened, not free fall.
                vy = Mathf.Max(vy, actual.y / dt);
            }

            if (grounded)
            {
                if (!wasGrounded)
                {
                    s.LandingSpeed = Mathf.Max(0f, -vy);
                    if (s.LandingSpeed >= m.LandedEventMinSpeed) s.Events |= MotorEvents.Landed;
                }
                vy = 0f;
                s.SetFlag(MotorFlags.JumpConsumed, false);
                s.CoyoteTimer = m.CoyoteEnabled ? m.CoyoteTime : 0f;
                s.AirTime = 0f;
            }
            else
            {
                if (wasGrounded) s.Events |= MotorEvents.LeftGround;
                if (!jumped) s.CoyoteTimer = Mathf.Max(0f, s.CoyoteTimer - dt);
                s.AirTime += dt;
            }

            s.SetFlag(MotorFlags.Grounded, grounded);
            s.SetFlag(MotorFlags.Sprinting, grounded && sprintIntent && hv.sqrMagnitude > 0.01f);
            s.Position = cc.transform.position;
            s.Velocity = new Vector3(hv.x, vy, hv.z);
        }

        static bool TryStepUp(CharacterController cc, Vector3 remaining, in MotorSettings m)
        {
            Transform t = cc.transform;
            Vector3 before = t.position;
            cc.Move(Vector3.up * m.StepHeight);
            float raised = t.position.y - before.y;
            if (raised < 0.01f) { Revert(t, before); return false; }
            Vector3 dir = remaining.sqrMagnitude > 1e-8f ? remaining.normalized : Vector3.zero;
            cc.Move(remaining + dir * (cc.radius * 0.1f));
            cc.Move(Vector3.down * (raised + m.SkinWidth + 0.02f));
            Vector3 after = t.position;
            GroundHit g = ProbeGround(cc, m, m.SkinWidth + m.GroundProbeDistance);
            float rise = after.y - before.y;
            float gain = new Vector2(after.x - before.x, after.z - before.z).magnitude;
            if (g.Walkable && rise > 0.005f && rise <= m.StepHeight + 0.01f && gain > remaining.magnitude * 0.3f) return true;
            Revert(t, before);
            return false;
        }

        static void Revert(Transform t, Vector3 position)
        {
            t.position = position;
            Physics.SyncTransforms();
        }

        /// <summary>Applies the standing or crouched capsule (feet stay where they are).</summary>
        public static void SetCrouchShape(CharacterController cc, bool crouched, in MotorSettings m)
        {
            float h = crouched ? m.CrouchHeight : m.CapsuleHeight;
            if (Mathf.Approximately(cc.height, h)) return;
            cc.height = h;
            cc.center = Vector3.up * (h * 0.5f);
            cc.stepOffset = Mathf.Min(m.StepHeight, h * 0.5f);
        }

        static readonly Collider[] Overlaps = new Collider[16];

        /// <summary>True when there is room above a crouched player to stand up.</summary>
        public static bool CanStand(CharacterController cc, in MotorSettings m)
        {
            Vector3 feet = cc.transform.position;
            float r = cc.radius - 0.02f;
            Vector3 bottom = feet + Vector3.up * (cc.radius + 0.05f);
            Vector3 top = feet + Vector3.up * (m.CapsuleHeight - cc.radius);
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, r, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (Overlaps[i] != cc) return false;
            return true;
        }

        /// <summary>Re-evaluates ground contact without moving (used after spawning or teleporting).</summary>
        public static void RefreshGround(CharacterController cc, ref PlayerSimState s, in MotorSettings m)
        {
            GroundHit g = ProbeGround(cc, m, m.SkinWidth + m.GroundProbeDistance);
            s.SetFlag(MotorFlags.Grounded, g.Walkable);
            s.Position = cc.transform.position;
        }

        static GroundHit ProbeGround(CharacterController cc, in MotorSettings m, float maxGap)
        {
            Transform t = cc.transform;
            float radius = cc.radius * CastRadiusScale;
            Vector3 bottomCenter = t.position + cc.center + Vector3.down * (cc.height * 0.5f - cc.radius);
            Vector3 origin = bottomCenter + Vector3.up * CastStartOffset;
            float lift = CastStartOffset + (cc.radius - radius); // sphere bottom above capsule bottom
            float castDistance = lift + maxGap;

            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, Hits, castDistance, ~0, QueryTriggerInteraction.Ignore);
            var best = new GroundHit();
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = Hits[i];
                if (h.collider == cc) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue; // initial overlap, no contact info
                if (h.distance >= bestDistance) continue;

                Vector3 normal = h.normal;
                float angle = Vector3.Angle(normal, Vector3.up);
                if (angle > m.MaxWalkableSlope)
                {
                    // Sphere casts report a blended normal on step edges; ask the real surface.
                    if (h.collider.Raycast(new Ray(h.point + Vector3.up * 0.05f, Vector3.down), out RaycastHit surface, 0.1f))
                    {
                        normal = surface.normal;
                        angle = Vector3.Angle(normal, Vector3.up);
                    }
                }

                bestDistance = h.distance;
                best.Hit = true;
                best.Normal = normal;
                best.Walkable = angle <= m.MaxWalkableSlope + 0.01f;
                best.Gap = Mathf.Max(0f, h.distance - lift);
            }
            return best;
        }
    }
}
