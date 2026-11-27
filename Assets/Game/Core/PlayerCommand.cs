using System;
using UnityEngine;

namespace Adventure3D.Core
{
    [Flags]
    public enum CommandButtons : byte
    {
        None = 0,
        /// <summary>Jump was pressed at least once since the previous simulation tick (edge).</summary>
        JumpPressed = 1 << 0,
        JumpHeld = 1 << 1,
        Sprint = 1 << 2,
        /// <summary>Trigger held (automatic weapons) / pressed (the combat rules keep their own edge state).</summary>
        Fire = 1 << 3,
        /// <summary>Reload requested (edge).</summary>
        Reload = 1 << 4,
        /// <summary>Aim down sights held.</summary>
        Aim = 1 << 5,
        /// <summary>Cycle to the next weapon slot (edge).</summary>
        SwitchWeapon = 1 << 6,
        /// <summary>Crouch held (Ctrl) or toggled on (C / gamepad / touch).</summary>
        Crouch = 1 << 7,
    }

    /// <summary>
    /// One simulation tick worth of player intent. This single representation is used by
    /// offline play, the LAN host, the dedicated server and client-side prediction.
    /// </summary>
    [Serializable]
    public struct PlayerCommand
    {
        public uint Sequence;
        /// <summary>x = strafe (right +), y = forward (+). Magnitude is clamped to 1.</summary>
        public Vector2 Move;
        /// <summary>Absolute yaw in degrees.</summary>
        public float Yaw;
        /// <summary>Absolute pitch in degrees (positive looks down, Unity convention).</summary>
        public float Pitch;
        public CommandButtons Buttons;
        /// <summary>Direct weapon slot request: 0 none, 1 primary, 2 secondary, 3 knife, 4 drop, 5 HE, 6 Flash, 7 Smoke, 8 Use/C4.</summary>
        public byte SlotRequest;

        public bool JumpPressed => (Buttons & CommandButtons.JumpPressed) != 0;
        public bool JumpHeld => (Buttons & CommandButtons.JumpHeld) != 0;
        public bool Sprint => (Buttons & CommandButtons.Sprint) != 0;
        public bool Fire => (Buttons & CommandButtons.Fire) != 0;
        public bool Reload => (Buttons & CommandButtons.Reload) != 0;
        public bool Aim => (Buttons & CommandButtons.Aim) != 0;
        public bool SwitchWeapon => (Buttons & CommandButtons.SwitchWeapon) != 0;
        public bool Crouch => (Buttons & CommandButtons.Crouch) != 0;

        public static PlayerCommand Neutral(float yaw, float pitch) =>
            new PlayerCommand { Move = Vector2.zero, Yaw = yaw, Pitch = pitch, Buttons = CommandButtons.None };

        /// <summary>
        /// Makes a command safe to simulate. The server always calls this on remote input,
        /// so a modified client cannot move faster by sending |Move| &gt; 1 or NaN values.
        /// </summary>
        public PlayerCommand Sanitized()
        {
            var c = this;
            if (!IsFinite(c.Move.x) || !IsFinite(c.Move.y)) c.Move = Vector2.zero;
            if (c.Move.sqrMagnitude > 1f) c.Move = c.Move.normalized;
            if (!IsFinite(c.Yaw)) c.Yaw = 0f;
            if (!IsFinite(c.Pitch)) c.Pitch = 0f;
            c.Yaw = Mathf.Repeat(c.Yaw, 360f);
            c.Pitch = Mathf.Clamp(c.Pitch, -89f, 89f);
            c.Buttons &= CommandButtons.JumpPressed | CommandButtons.JumpHeld | CommandButtons.Sprint | CommandButtons.Fire
                         | CommandButtons.Reload | CommandButtons.Aim | CommandButtons.SwitchWeapon | CommandButtons.Crouch;
            if (c.SlotRequest > 8) c.SlotRequest = 0;
            return c;
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }

    /// <summary>Anything that can produce commands for the local player (devices, scripts, tests).</summary>
    public interface ICommandSource
    {
        /// <summary>Called once per simulation tick. Consumes edge-triggered buttons.</summary>
        PlayerCommand SampleCommand();

        /// <summary>Latest look angles, updated every rendered frame (used by the camera).</summary>
        float LookYaw { get; }
        float LookPitch { get; }

        /// <summary>Forget held buttons and touches (focus loss, pause, menu).</summary>
        void ResetTransientInput();

        void SetLook(float yaw, float pitch);
    }
}
