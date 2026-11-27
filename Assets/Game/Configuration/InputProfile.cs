using UnityEngine;

namespace Adventure3D.Configuration
{
    /// <summary>Per-device sensitivity and response. Stick, mouse and touch are tuned independently.</summary>
    [CreateAssetMenu(menuName = "Adventure3D/Input Profile", fileName = "InputProfile")]
    public sealed class InputProfile : ScriptableObject
    {
        [Header("Keyboard & mouse")]
        [Unit("deg/count", Min = 0.001f)] public float MouseSensitivity = 0.08f;
        [Tooltip("Time to ramp keyboard movement from 0 to full (digital keys feel less twitchy).")]
        [Unit("s", Min = 0f)] public float KeyboardMoveResponseTime = 0.05f;

        [Header("Gamepad")]
        [Unit("deg/s", Min = 1f)] public float GamepadYawSpeed = 200f;
        [Unit("deg/s", Min = 1f)] public float GamepadPitchSpeed = 140f;
        [Unit("0..1", 0f, 0.9f)] public float GamepadMoveDeadzone = 0.15f;
        [Unit("0..1", 0f, 0.9f)] public float GamepadLookDeadzone = 0.12f;
        [Tooltip("Response curve exponent for the look stick. 1 = linear, 2 = finer aim near center.")]
        [Unit("exp", 1f, 4f)] public float GamepadLookExponent = 2f;
        [Unit("s", Min = 0f)] public float GamepadMoveResponseTime = 0f;

        [Header("Touch")]
        [Tooltip("Degrees of rotation for a drag across the full screen height.")]
        [Unit("deg/screen", Min = 1f)] public float TouchLookSensitivity = 150f;
        [Tooltip("Virtual stick radius as a fraction of the safe area height.")]
        [Unit("0..1", 0.03f, 0.3f)] public float TouchStickRadius = 0.09f;
        [Unit("0..1", 0f, 0.9f)] public float TouchStickDeadzone = 0.1f;
        [Unit("s", Min = 0f)] public float TouchMoveResponseTime = 0.03f;

        [Header("General")]
        public bool InvertY = false;
        [Tooltip("When on, sprint toggles instead of being held (useful on touch and gamepad).")]
        public bool SprintToggle = false;
    }
}
