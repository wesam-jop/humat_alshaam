using System;
using System.Collections.Generic;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Platform;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Adventure3D.Input
{
    public enum InputDeviceKind { None, KeyboardMouse, Gamepad, Touch }

    /// <summary>
    /// Converts keyboard + mouse, gamepad and touch into the shared player commands
    /// (Move, Look, Jump, Sprint, Menu). Devices are read every rendered frame; the simulation pulls a
    /// <see cref="PlayerCommand"/> once per fixed tick through <see cref="SampleCommand"/>, so reading
    /// input and moving the camera never depend on the simulation rate.
    /// </summary>
    public sealed class LocalPlayerInput : MonoBehaviour, ICommandSource
    {
        InputProfile _profile;
        CameraProfile _camera;
        readonly TouchControlsModel _touch = new TouchControlsModel();
        readonly List<TouchSample> _touchSamples = new List<TouchSample>(10);

        float _yaw;
        float _pitch;
        Vector2 _moveSmoothed;
        bool _jumpLatched;
        bool _jumpHeld;
        bool _sprintHeld;
        bool _sprintToggled;
        bool _inputEnabled = true;
        bool _fireHeld, _aimHeld, _reloadLatched, _switchLatched;
        bool _crouchHeld, _crouchToggled;
        public bool CrouchActive => _crouchHeld || _crouchToggled;
        byte _slotLatched;

        /// <summary>Look sensitivity multiplier while aiming down sights (set per weapon by presentation).</summary>
        public float AimSensitivityScale { get; set; } = 0.6f;
        /// <summary>Look rotation applied this frame in degrees (x yaw, y pitch), for weapon sway.</summary>
        public Vector2 LastLookDelta { get; private set; }
        public bool FireHeld => _fireHeld;
        public bool AimHeld => _aimHeld;

        /// <summary>When false (pause menu), the player stands still but the session keeps running.</summary>
        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                if (_inputEnabled == value) return;
                _inputEnabled = value;
                ResetTransientInput();
            }
        }

        /// <summary>Mouse look is applied only while this is true (cursor locked on desktop).</summary>
        public bool MouseLookActive { get; set; } = true;

        public InputDeviceKind LastDevice { get; private set; }

        /// <summary>Tests: fixed touch layout area instead of Screen.safeArea.</summary>
        public Rect? SafeAreaOverride { get; set; }
        public TouchControlsModel Touch => _touch;
        public Vector2 CurrentMove => _moveSmoothed;
        public bool SprintActive => _sprintHeld || _sprintToggled;

        public float LookYaw => _yaw;
        public float LookPitch => _pitch;

        /// <summary>Menu / pause command (Esc, gamepad Start, touch pause button).</summary>
        public event Action MenuPressed;

        public void Configure(InputProfile profile, CameraProfile camera)
        {
            _profile = profile;
            _camera = camera;
            _touch.StickRadius = profile.TouchStickRadius;
            _touch.StickDeadzone = profile.TouchStickDeadzone;
        }

        void OnEnable() => AppFocus.FocusChanged += OnFocusChanged;
        void OnDisable() => AppFocus.FocusChanged -= OnFocusChanged;

        void OnFocusChanged(bool focus)
        {
            if (!focus) ResetTransientInput();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) ResetTransientInput();
        }

        public void SetLook(float yaw, float pitch)
        {
            _yaw = Mathf.Repeat(yaw, 360f);
            _pitch = pitch;
        }

        public void ResetTransientInput()
        {
            _touch.Reset();
            _moveSmoothed = Vector2.zero;
            _jumpLatched = false;
            _jumpHeld = false;
            _sprintHeld = false;
            _sprintToggled = false;
            _fireHeld = false;
            _aimHeld = false;
            _reloadLatched = false;
            _switchLatched = false;
            _slotLatched = 0;
            _crouchHeld = false;
        }

        public PlayerCommand SampleCommand()
        {
            var buttons = CommandButtons.None;
            if (_inputEnabled)
            {
                if (_jumpLatched) buttons |= CommandButtons.JumpPressed;
                if (_jumpHeld) buttons |= CommandButtons.JumpHeld;
                if (SprintActive) buttons |= CommandButtons.Sprint;
                if (_fireHeld) buttons |= CommandButtons.Fire;
                if (_aimHeld) buttons |= CommandButtons.Aim;
                if (_reloadLatched) buttons |= CommandButtons.Reload;
                if (_switchLatched) buttons |= CommandButtons.SwitchWeapon;
                if (CrouchActive) buttons |= CommandButtons.Crouch;
            }
            byte slot = _inputEnabled ? _slotLatched : (byte)0;
            _jumpLatched = false;
            _reloadLatched = false;
            _switchLatched = false;
            _slotLatched = 0;
            return new PlayerCommand
            {
                Move = _inputEnabled ? _moveSmoothed : Vector2.zero,
                Yaw = _yaw,
                Pitch = _pitch,
                Buttons = buttons,
                SlotRequest = slot,
            };
        }

        void Update()
        {
            if (_profile == null) return;
            float dt = Time.unscaledDeltaTime;

            Vector2 rawMove = Vector2.zero;
            Vector2 lookDeg = Vector2.zero;
            bool jumpPressed = false, jumpHeld = false, sprintHeld = false, sprintPressed = false, menuPressed = false;
            bool fire = false, aim = false, reload = false, swap = false, crouchHeld = false, crouchToggle = false;
            byte slotPick = 0;
            InputDeviceKind moveDevice = InputDeviceKind.None;

            // ---- Keyboard & mouse
            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector2 k = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) k.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) k.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) k.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) k.x -= 1f;
                if (k != Vector2.zero) { rawMove = k; moveDevice = InputDeviceKind.KeyboardMouse; }
                jumpPressed |= kb.spaceKey.wasPressedThisFrame;
                jumpHeld |= kb.spaceKey.isPressed;
                sprintHeld |= kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                sprintPressed |= kb.leftShiftKey.wasPressedThisFrame;
                menuPressed |= kb.escapeKey.wasPressedThisFrame;
                reload |= kb.rKey.wasPressedThisFrame;
                swap |= kb.qKey.wasPressedThisFrame;
                crouchHeld |= kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                crouchToggle |= kb.cKey.wasPressedThisFrame;
                if (kb.digit1Key.wasPressedThisFrame) slotPick = 1;
                if (kb.digit2Key.wasPressedThisFrame) slotPick = 2;
                if (kb.digit3Key.wasPressedThisFrame) slotPick = 3;
                if (kb.anyKey.wasPressedThisFrame) LastDevice = InputDeviceKind.KeyboardMouse;
            }
            var mouse = Mouse.current;
            if (mouse != null && MouseLookActive && PlatformInfo.UsesMouseLook)
            {
                fire |= mouse.leftButton.isPressed;
                aim |= mouse.rightButton.isPressed;
                if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f) swap = true;
                Vector2 d = mouse.delta.ReadValue();
                if (d != Vector2.zero)
                {
                    lookDeg += d * _profile.MouseSensitivity;
                    LastDevice = InputDeviceKind.KeyboardMouse;
                }
            }

            // ---- Gamepad
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 ls = ApplyDeadzone(pad.leftStick.ReadValue(), _profile.GamepadMoveDeadzone, 1f);
                if (ls != Vector2.zero && moveDevice == InputDeviceKind.None) { rawMove = ls; moveDevice = InputDeviceKind.Gamepad; }
                Vector2 rs = ApplyDeadzone(pad.rightStick.ReadValue(), _profile.GamepadLookDeadzone, _profile.GamepadLookExponent);
                lookDeg += new Vector2(rs.x * _profile.GamepadYawSpeed, rs.y * _profile.GamepadPitchSpeed) * dt;
                jumpPressed |= pad.buttonSouth.wasPressedThisFrame;
                jumpHeld |= pad.buttonSouth.isPressed;
                sprintHeld |= pad.leftStickButton.isPressed || pad.leftShoulder.isPressed;
                sprintPressed |= pad.leftStickButton.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame;
                menuPressed |= pad.startButton.wasPressedThisFrame;
                fire |= pad.rightTrigger.ReadValue() > 0.4f;
                aim |= pad.leftTrigger.ReadValue() > 0.4f;
                reload |= pad.buttonWest.wasPressedThisFrame;
                swap |= pad.buttonNorth.wasPressedThisFrame;
                crouchToggle |= pad.buttonEast.wasPressedThisFrame || pad.rightStickButton.wasPressedThisFrame;
                if (ls != Vector2.zero || rs != Vector2.zero || pad.buttonSouth.wasPressedThisFrame) LastDevice = InputDeviceKind.Gamepad;
            }

            // ---- Touch (multi-touch: move, look and buttons at the same time)
            var ts = Touchscreen.current;
            if (ts != null)
            {
                _touchSamples.Clear();
                foreach (TouchControl t in ts.touches)
                {
                    var phase = t.phase.ReadValue();
                    if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;
                    var lite = phase == UnityEngine.InputSystem.TouchPhase.Began ? TouchPhaseLite.Began
                        : phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled ? TouchPhaseLite.Ended
                        : phase == UnityEngine.InputSystem.TouchPhase.Stationary ? TouchPhaseLite.Stationary
                        : TouchPhaseLite.Moved;
                    _touchSamples.Add(new TouchSample(t.touchId.ReadValue(), t.position.ReadValue(), lite));
                }
                ProcessTouch(_touchSamples, SafeAreaOverride ?? SafeArea.Current);
            }
            if (_touch.ActiveFingerCount > 0)
            {
                if (_touch.Move != Vector2.zero && moveDevice == InputDeviceKind.None) { rawMove = _touch.Move; moveDevice = InputDeviceKind.Touch; }
                lookDeg += _touch.LookDelta * _profile.TouchLookSensitivity;
                jumpPressed |= _touch.JumpPressedThisFrame;
                jumpHeld |= _touch.JumpHeld;
                sprintHeld |= _touch.SprintHeld;
                fire |= _touch.FireHeld;
                reload |= _touch.ReloadPressedThisFrame;
                swap |= _touch.SwitchPressedThisFrame;
                crouchToggle |= _touch.CrouchPressedThisFrame;
                LastDevice = InputDeviceKind.Touch;
            }
            menuPressed |= _touch.PausePressedThisFrame;

            if (menuPressed) MenuPressed?.Invoke();

            if (!_inputEnabled)
            {
                _fireHeld = false;
                _aimHeld = false;
                LastLookDelta = Vector2.zero;
                _moveSmoothed = Vector2.zero;
                return;
            }

            // ---- Look (applied every frame, independent of the simulation tick)
            _fireHeld = fire;
            _aimHeld = aim;
            if (reload) _reloadLatched = true;
            if (swap) _switchLatched = true;
            if (slotPick != 0) _slotLatched = slotPick;
            _crouchHeld = crouchHeld;
            if (crouchToggle) _crouchToggled = !_crouchToggled;
            if (sprintHeld && _crouchToggled && rawMove.y > 0.5f) _crouchToggled = false; // sprinting stands you up
            if (aim) lookDeg *= AimSensitivityScale;
            LastLookDelta = lookDeg;

            float invert = _profile.InvertY ? -1f : 1f;
            _yaw = Mathf.Repeat(_yaw + lookDeg.x, 360f);
            float minP = _camera != null ? _camera.MinPitch : -85f;
            float maxP = _camera != null ? _camera.MaxPitch : 85f;
            _pitch = Mathf.Clamp(_pitch - lookDeg.y * invert, minP, maxP);

            // ---- Move with per-device response time
            if (rawMove.sqrMagnitude > 1f) rawMove.Normalize();
            float response = moveDevice switch
            {
                InputDeviceKind.KeyboardMouse => _profile.KeyboardMoveResponseTime,
                InputDeviceKind.Gamepad => _profile.GamepadMoveResponseTime,
                InputDeviceKind.Touch => _profile.TouchMoveResponseTime,
                _ => _profile.KeyboardMoveResponseTime,
            };
            _moveSmoothed = response <= 0f ? rawMove : Vector2.MoveTowards(_moveSmoothed, rawMove, dt / response);

            // ---- Buttons
            if (jumpPressed) _jumpLatched = true;
            _jumpHeld = jumpHeld;
            if (_profile.SprintToggle)
            {
                if (sprintPressed) _sprintToggled = !_sprintToggled;
                if (rawMove.y <= 0.1f) _sprintToggled = false;
                _sprintHeld = false;
            }
            else
            {
                _sprintHeld = sprintHeld;
            }
        }

        /// <summary>Exposed for tests: feed touch samples directly.</summary>
        public void ProcessTouch(IReadOnlyList<TouchSample> samples, Rect safeArea) => _touch.Process(samples, safeArea);

        public static Vector2 ApplyDeadzone(Vector2 v, float deadzone, float exponent)
        {
            float mag = v.magnitude;
            if (mag <= deadzone) return Vector2.zero;
            float scaled = Mathf.InverseLerp(deadzone, 1f, Mathf.Min(mag, 1f));
            scaled = Mathf.Pow(scaled, exponent);
            return v / mag * scaled;
        }
    }
}
