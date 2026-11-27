using System.Collections.Generic;
using Adventure3D.Core;
using UnityEngine;

namespace Adventure3D.Gameplay
{
    /// <summary>
    /// Deterministic command source driven by a list of timed segments. Used by automated tests and by
    /// the in-build diagnostics agent so measurements use the same command path as real input.
    /// </summary>
    public sealed class ScriptedCommandSource : ICommandSource
    {
        public struct Segment
        {
            public float Duration;
            public Vector2 Move;
            public bool Sprint;
            public bool JumpAtStart;
            public bool HoldJump;
            public float YawDelta; // degrees added over the segment
        }

        readonly List<Segment> _segments = new List<Segment>();
        readonly float _tickDelta;
        int _index;
        float _segmentTime;
        bool _segmentStarted;
        float _yaw;
        float _pitch;
        uint _ticks;

        public bool Loop;
        /// <summary>Added to every command's yaw (kept when the session re-aims the player at spawn).</summary>
        public float YawOffset;
        public bool Finished => !Loop && _index >= _segments.Count;
        public uint TicksSampled => _ticks;
        public float LookYaw => _yaw + YawOffset;
        public float LookPitch => _pitch;

        public ScriptedCommandSource(float tickDelta, float startYaw = 0f)
        {
            _tickDelta = tickDelta;
            _yaw = startYaw;
        }

        public ScriptedCommandSource Add(float duration, Vector2 move, bool sprint = false, bool jumpAtStart = false, float yawDelta = 0f, bool holdJump = false)
        {
            _segments.Add(new Segment { Duration = duration, Move = move, Sprint = sprint, JumpAtStart = jumpAtStart, YawDelta = yawDelta, HoldJump = holdJump });
            return this;
        }

        public ScriptedCommandSource Wait(float duration) => Add(duration, Vector2.zero);

        public PlayerCommand SampleCommand()
        {
            _ticks++;
            if (_index >= _segments.Count)
            {
                if (!Loop || _segments.Count == 0) return PlayerCommand.Neutral(_yaw + YawOffset, _pitch);
                _index = 0;
            }

            var seg = _segments[_index];
            var buttons = CommandButtons.None;
            if (!_segmentStarted)
            {
                _segmentStarted = true;
                if (seg.JumpAtStart) buttons |= CommandButtons.JumpPressed | CommandButtons.JumpHeld;
            }
            if (seg.HoldJump) buttons |= CommandButtons.JumpHeld;
            if (seg.Sprint) buttons |= CommandButtons.Sprint;
            if (seg.Duration > 0f) _yaw += seg.YawDelta * (_tickDelta / seg.Duration);

            var cmd = new PlayerCommand { Move = seg.Move, Yaw = _yaw + YawOffset, Pitch = _pitch, Buttons = buttons };

            _segmentTime += _tickDelta;
            if (_segmentTime >= seg.Duration - 1e-5f)
            {
                _index++;
                _segmentTime = 0f;
                _segmentStarted = false;
            }
            return cmd;
        }

        public void ResetTransientInput() { }

        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = pitch;
        }
    }
}
