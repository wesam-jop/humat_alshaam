using UnityEngine;

namespace Adventure3D.Configuration
{
    /// <summary>Collision shape and simulation clock. Changing TickRate changes the network protocol cadence.</summary>
    [CreateAssetMenu(menuName = "Adventure3D/Physics Profile", fileName = "PhysicsProfile")]
    public sealed class PhysicsProfile : ScriptableObject
    {
        [Header("Capsule")]
        [Unit("m", Min = 0.5f)] public float CapsuleHeight = 1.8f;
        [Unit("m", Min = 0.1f)] public float CapsuleRadius = 0.35f;
        [Unit("m", Min = 0.001f)] public float SkinWidth = 0.03f;
        [Tooltip("Camera anchor height above the feet.")]
        [Unit("m", Min = 0.1f)] public float EyeHeight = 1.62f;
        [Tooltip("Camera anchor height above the feet while crouched.")]
        [Unit("m", Min = 0.1f)] public float CrouchEyeHeight = 1.02f;
        [Tooltip("Time for the camera to move between standing and crouched eye height.")]
        [Unit("s", Min = 0.01f)] public float CrouchCameraTime = 0.12f;

        [Header("Simulation clock")]
        [Tooltip("Fixed simulation rate. Rendering and input run at any frame rate.")]
        [Unit("Hz", 10, 128)] public int TickRate = 60;
        [Tooltip("Upper bound of catch-up ticks per rendered frame (avoids a spiral of death).")]
        [Unit("ticks", 1, 30)] public int MaxTicksPerFrame = 8;

        public float TickDelta => 1f / TickRate;
    }
}
