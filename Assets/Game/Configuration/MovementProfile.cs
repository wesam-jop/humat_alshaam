using UnityEngine;

namespace Adventure3D.Configuration
{
    /// <summary>
    /// Ground movement tuning shared by every player. The "weight" of the character comes mostly from
    /// acceleration / deceleration: a heavy military feel is reached in roughly 0.15 s to full walk
    /// speed and a slightly faster stop, with no instant velocity changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Adventure3D/Movement Profile", fileName = "MovementProfile")]
    public sealed class MovementProfile : ScriptableObject
    {
        [Header("Speeds")]
        [Unit("m/s", Min = 0f)] public float WalkSpeed = 4.5f;
        [Unit("m/s", Min = 0f)] public float SprintSpeed = 7.0f;
        [Tooltip("Sprint only applies when forward input is at least this value (0..1).")]
        [Unit("0..1", 0f, 1f)] public float SprintForwardThreshold = 0.5f;

        [Header("Crouch")]
        [Tooltip("Capsule height while crouched.")]
        [Unit("m", Min = 0.8f)] public float CrouchHeight = 1.2f;
        [Tooltip("Ground speed multiplier while crouched.")]
        [Unit("x", 0.1f, 1f)] public float CrouchSpeedMultiplier = 0.45f;
        [Tooltip("Spread multiplier while crouched on the ground (steadier aim).")]
        [Unit("x", 0.2f, 1f)] public float CrouchSpreadMultiplier = 0.7f;

        [Header("Acceleration")]
        [Unit("m/s²", Min = 0.01f)] public float GroundAcceleration = 30f;
        [Tooltip("Used when there is no input, or the input points against the current velocity.")]
        [Unit("m/s²", Min = 0.01f)] public float GroundDeceleration = 36f;

        [Header("Ground")]
        [Unit("deg", 0f, 89f)] public float MaxWalkableSlope = 45f;
        [Unit("m", Min = 0f)] public float StepHeight = 0.35f;
        [Tooltip("Extra distance below the capsule that still counts as touching the ground.")]
        [Unit("m", Min = 0.001f)] public float GroundProbeDistance = 0.08f;
        [Tooltip("Max drop the player is snapped down while walking (stairs, ramps). Larger drops become a fall.")]
        [Unit("m", Min = 0f)] public float GroundSnapDistance = 0.45f;
    }
}
