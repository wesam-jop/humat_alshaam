using System;
using UnityEngine;

namespace Adventure3D.Core
{
    [Flags]
    public enum MotorFlags : byte
    {
        None = 0,
        Grounded = 1 << 0,
        Sprinting = 1 << 1,
        /// <summary>A jump was used since the player last stood on walkable ground.</summary>
        JumpConsumed = 1 << 2,
        OnSteepSlope = 1 << 3,
        /// <summary>Capsule is in the crouched (short) shape.</summary>
        Crouching = 1 << 4,
    }

    /// <summary>One-tick events, for future animation and audio hooks.</summary>
    [Flags]
    public enum MotorEvents : byte
    {
        None = 0,
        Jumped = 1 << 0,
        Landed = 1 << 1,
        HitCeiling = 1 << 2,
        LeftGround = 1 << 3,
        SteppedUp = 1 << 4,
    }

    /// <summary>
    /// Complete per-player runtime movement state. Shared profiles hold tuning; this struct holds
    /// what changes per player and is what the server sends in snapshots.
    /// </summary>
    [Serializable]
    public struct PlayerSimState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
        public float Pitch;
        public MotorFlags Flags;
        public MotorEvents Events;
        public float CoyoteTimer;
        public float JumpBufferTimer;
        /// <summary>Downward speed at the moment of the last landing (m/s, positive).</summary>
        public float LandingSpeed;
        /// <summary>Seconds since leaving the ground (0 while grounded).</summary>
        public float AirTime;

        public bool Grounded => (Flags & MotorFlags.Grounded) != 0;
        public bool Airborne => !Grounded;
        public bool Sprinting => (Flags & MotorFlags.Sprinting) != 0;
        public bool Crouching => (Flags & MotorFlags.Crouching) != 0;
        public bool Landed => (Events & MotorEvents.Landed) != 0;
        public bool Jumped => (Events & MotorEvents.Jumped) != 0;
        public float HorizontalSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;
        public float VerticalSpeed => Velocity.y;

        public void SetFlag(MotorFlags flag, bool on)
        {
            if (on) Flags |= flag; else Flags &= ~flag;
        }
    }
}
