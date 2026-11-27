using UnityEngine;

namespace Adventure3D.Configuration
{
    /// <summary>
    /// Jump, gravity, fall and in-air control. Gravity and jump velocity are derived from the designer
    /// friendly JumpHeight and TimeToApex so the two stay consistent:
    ///   gravity = 2h / t²,  jumpVelocity = 2h / t
    /// </summary>
    [CreateAssetMenu(menuName = "Adventure3D/Air Movement Profile", fileName = "AirMovementProfile")]
    public sealed class AirMovementProfile : ScriptableObject
    {
        [Header("Jump")]
        [Unit("m", Min = 0.01f)] public float JumpHeight = 1.2f;
        [Unit("s", Min = 0.05f)] public float TimeToApex = 0.4f;

        [Header("Fall")]
        [Tooltip("Gravity multiplier while moving down. 1 = symmetric arc; >1 = heavier landing.")]
        [Unit("x", Min = 0.1f)] public float FallGravityMultiplier = 1.6f;
        [Unit("m/s", Min = 0.1f)] public float MaxFallSpeed = 30f;

        [Header("Air control")]
        [Unit("m/s²", Min = 0f)] public float AirAcceleration = 12f;
        [Tooltip("Horizontal slow-down in the air when there is no input.")]
        [Unit("m/s²", Min = 0f)] public float AirDeceleration = 2f;

        [Header("Assists")]
        public bool CoyoteTimeEnabled = true;
        [Tooltip("Jump is still allowed this long after walking off an edge. Never grants a second jump.")]
        [Unit("s", Min = 0f)] public float CoyoteTime = 0.12f;
        public bool JumpBufferEnabled = true;
        [Tooltip("A jump pressed this long before landing is executed on landing.")]
        [Unit("s", Min = 0f)] public float JumpBufferTime = 0.12f;

        [Header("Events")]
        [Tooltip("Minimum downward speed for a landing to raise the Landed event.")]
        [Unit("m/s", Min = 0f)] public float LandedEventMinSpeed = 0.5f;

        public float Gravity => 2f * JumpHeight / (TimeToApex * TimeToApex);
        public float JumpVelocity => 2f * JumpHeight / TimeToApex;
    }
}
