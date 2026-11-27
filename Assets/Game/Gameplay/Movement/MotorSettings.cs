using Adventure3D.Configuration;

namespace Adventure3D.Gameplay
{
    /// <summary>Flattened, read-only copy of the movement profiles used inside the simulation step.</summary>
    public readonly struct MotorSettings
    {
        public readonly float WalkSpeed;
        public readonly float SprintSpeed;
        public readonly float SprintForwardThreshold;
        public readonly float GroundAcceleration;
        public readonly float GroundDeceleration;
        public readonly float MaxWalkableSlope;
        public readonly float GroundProbeDistance;
        public readonly float GroundSnapDistance;
        public readonly float StepHeight;
        public readonly float CrouchHeight;
        public readonly float CrouchSpeedMultiplier;

        public readonly float Gravity;
        public readonly float JumpVelocity;
        public readonly float FallGravityMultiplier;
        public readonly float MaxFallSpeed;
        public readonly float AirAcceleration;
        public readonly float AirDeceleration;
        public readonly bool CoyoteEnabled;
        public readonly float CoyoteTime;
        public readonly bool JumpBufferEnabled;
        public readonly float JumpBufferTime;
        public readonly float LandedEventMinSpeed;

        public readonly float CapsuleRadius;
        public readonly float CapsuleHeight;
        public readonly float SkinWidth;

        public MotorSettings(MovementProfile g, AirMovementProfile a, PhysicsProfile p)
        {
            WalkSpeed = g.WalkSpeed;
            SprintSpeed = g.SprintSpeed;
            SprintForwardThreshold = g.SprintForwardThreshold;
            GroundAcceleration = g.GroundAcceleration;
            GroundDeceleration = g.GroundDeceleration;
            MaxWalkableSlope = g.MaxWalkableSlope;
            GroundProbeDistance = g.GroundProbeDistance;
            GroundSnapDistance = g.GroundSnapDistance;
            StepHeight = g.StepHeight;
            CrouchHeight = g.CrouchHeight;
            CrouchSpeedMultiplier = g.CrouchSpeedMultiplier;

            Gravity = a.Gravity;
            JumpVelocity = a.JumpVelocity;
            FallGravityMultiplier = a.FallGravityMultiplier;
            MaxFallSpeed = a.MaxFallSpeed;
            AirAcceleration = a.AirAcceleration;
            AirDeceleration = a.AirDeceleration;
            CoyoteEnabled = a.CoyoteTimeEnabled && a.CoyoteTime > 0f;
            CoyoteTime = a.CoyoteTime;
            JumpBufferEnabled = a.JumpBufferEnabled && a.JumpBufferTime > 0f;
            JumpBufferTime = a.JumpBufferTime;
            LandedEventMinSpeed = a.LandedEventMinSpeed;

            CapsuleRadius = p.CapsuleRadius;
            CapsuleHeight = p.CapsuleHeight;
            SkinWidth = p.SkinWidth;
        }

        public static MotorSettings From(GameConfig c) => new MotorSettings(c.Movement, c.Air, c.Physics);
    }
}
