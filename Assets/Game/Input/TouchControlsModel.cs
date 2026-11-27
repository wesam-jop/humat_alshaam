using System.Collections.Generic;
using UnityEngine;

namespace Adventure3D.Input
{
    public enum TouchPhaseLite { Began, Moved, Stationary, Ended }

    public struct TouchSample
    {
        public int Id;
        public Vector2 Position;
        public TouchPhaseLite Phase;

        public TouchSample(int id, Vector2 position, TouchPhaseLite phase)
        {
            Id = id;
            Position = position;
            Phase = phase;
        }
    }

    /// <summary>
    /// Device-independent touch layout logic: a floating move stick on the left, a look drag area on
    /// the right, and Jump / Sprint / Pause buttons. Every finger is tracked by id, so moving, looking
    /// and jumping work at the same time. All rectangles are normalized to the safe area (0..1).
    /// </summary>
    public sealed class TouchControlsModel
    {
        enum Role { None, Stick, Look, Jump, Sprint, Pause, Fire, Reload, Switch, Crouch }

        struct Finger
        {
            public Role Role;
            public Vector2 Origin;
            public Vector2 Last;
        }

        public static readonly Rect StickZone = new Rect(0f, 0f, 0.45f, 0.75f);
        public static readonly Rect LookZone = new Rect(0.45f, 0f, 0.55f, 1f);
        public static readonly Rect JumpButton = new Rect(0.84f, 0.08f, 0.12f, 0.2f);
        public static readonly Rect SprintButton = new Rect(0.70f, 0.08f, 0.12f, 0.2f);
        public static readonly Rect PauseButton = new Rect(0.02f, 0.86f, 0.08f, 0.12f);
        /// <summary>Fire button; dragging the finger on it also aims (like mobile shooters).</summary>
        public static readonly Rect FireButton = new Rect(0.84f, 0.32f, 0.12f, 0.22f);
        public static readonly Rect ReloadButton = new Rect(0.72f, 0.32f, 0.10f, 0.16f);
        public static readonly Rect SwitchButton = new Rect(0.86f, 0.84f, 0.10f, 0.12f);
        public static readonly Rect CrouchButton = new Rect(0.72f, 0.52f, 0.10f, 0.14f);

        readonly Dictionary<int, Finger> _fingers = new Dictionary<int, Finger>();

        public float StickRadius = 0.09f;   // fraction of safe-area height
        public float StickDeadzone = 0.1f;

        public Vector2 Move { get; private set; }
        /// <summary>Look delta this frame as a fraction of the safe-area height.</summary>
        public Vector2 LookDelta { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool PausePressedThisFrame { get; private set; }
        public bool FireHeld { get; private set; }
        public bool ReloadPressedThisFrame { get; private set; }
        public bool SwitchPressedThisFrame { get; private set; }
        public bool CrouchPressedThisFrame { get; private set; }
        public bool StickActive { get; private set; }
        public Vector2 StickOriginNormalized { get; private set; }
        public Vector2 StickKnobNormalized { get; private set; }
        public int ActiveFingerCount => _fingers.Count;

        public void Process(IReadOnlyList<TouchSample> touches, Rect safeArea)
        {
            JumpPressedThisFrame = false;
            PausePressedThisFrame = false;
            ReloadPressedThisFrame = false;
            SwitchPressedThisFrame = false;
            CrouchPressedThisFrame = false;
            LookDelta = Vector2.zero;
            float h = Mathf.Max(1f, safeArea.height);

            var seen = new HashSet<int>();
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                Vector2 n = Normalize(t.Position, safeArea);
                if (t.Phase == TouchPhaseLite.Ended)
                {
                    _fingers.Remove(t.Id);
                    continue;
                }
                seen.Add(t.Id);

                if (!_fingers.TryGetValue(t.Id, out var f))
                {
                    f = new Finger { Role = Classify(n), Origin = t.Position, Last = t.Position };
                    if (f.Role == Role.Jump) JumpPressedThisFrame = true;
                    if (f.Role == Role.Pause) PausePressedThisFrame = true;
                    if (f.Role == Role.Reload) ReloadPressedThisFrame = true;
                    if (f.Role == Role.Switch) SwitchPressedThisFrame = true;
                    if (f.Role == Role.Crouch) CrouchPressedThisFrame = true;
                    _fingers[t.Id] = f;
                }

                if (f.Role == Role.Look || f.Role == Role.Fire)
                {
                    Vector2 d = t.Position - f.Last;
                    LookDelta += d / h;
                }
                f.Last = t.Position;
                _fingers[t.Id] = f;
            }

            // Fingers that vanished without an Ended phase (focus loss, device reset) are released.
            var stale = new List<int>();
            foreach (var kv in _fingers) if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var id in stale) _fingers.Remove(id);

            Move = Vector2.zero;
            StickActive = false;
            JumpHeld = false;
            SprintHeld = false;
            FireHeld = false;
            foreach (var f in _fingers.Values)
            {
                switch (f.Role)
                {
                    case Role.Stick:
                        StickActive = true;
                        Vector2 offset = (f.Last - f.Origin) / (h * StickRadius);
                        if (offset.sqrMagnitude > 1f) offset.Normalize();
                        float mag = offset.magnitude;
                        Move = mag < StickDeadzone ? Vector2.zero : offset * Mathf.InverseLerp(StickDeadzone, 1f, mag) / Mathf.Max(mag, 1e-5f);
                        StickOriginNormalized = Normalize(f.Origin, safeArea);
                        StickKnobNormalized = Normalize(f.Origin + offset * h * StickRadius, safeArea);
                        break;
                    case Role.Jump: JumpHeld = true; break;
                    case Role.Sprint: SprintHeld = true; break;
                    case Role.Fire: FireHeld = true; break;
                }
            }
        }

        /// <summary>Releases every finger (focus loss, pause, returning from background).</summary>
        public void Reset()
        {
            _fingers.Clear();
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
            JumpHeld = false;
            SprintHeld = false;
            FireHeld = false;
            StickActive = false;
            JumpPressedThisFrame = false;
            PausePressedThisFrame = false;
        }

        static Role Classify(Vector2 n)
        {
            if (PauseButton.Contains(n)) return Role.Pause;
            if (FireButton.Contains(n)) return Role.Fire;
            if (ReloadButton.Contains(n)) return Role.Reload;
            if (SwitchButton.Contains(n)) return Role.Switch;
            if (CrouchButton.Contains(n)) return Role.Crouch;
            if (JumpButton.Contains(n)) return Role.Jump;
            if (SprintButton.Contains(n)) return Role.Sprint;
            if (StickZone.Contains(n)) return Role.Stick;
            if (LookZone.Contains(n)) return Role.Look;
            return Role.None;
        }

        static Vector2 Normalize(Vector2 p, Rect safe) =>
            new Vector2((p.x - safe.x) / Mathf.Max(1f, safe.width), (p.y - safe.y) / Mathf.Max(1f, safe.height));
    }
}
