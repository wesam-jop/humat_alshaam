using System.Collections.Generic;
using Adventure3D.Core;
using UnityEngine;

namespace Adventure3D.Networking
{
    /// <summary>
    /// Smooths remote players on a client: snapshots are buffered by server time and the player is
    /// drawn <c>delay</c> seconds in the past, interpolating between the two surrounding snapshots.
    /// </summary>
    public sealed class RemoteInterpolation
    {
        struct Sample
        {
            public double Time;
            public PlayerSimState State;
        }

        readonly Dictionary<ushort, List<Sample>> _tracks = new Dictionary<ushort, List<Sample>>();
        readonly float _tickDelta;
        readonly float _delay;
        double _serverTime;
        bool _hasTime;

        public RemoteInterpolation(float tickDelta, float delay)
        {
            _tickDelta = tickDelta;
            _delay = delay;
        }

        public double RenderTime => _serverTime - _delay;

        public void Push(ushort playerId, uint serverTick, PlayerSimState state)
        {
            if (!_tracks.TryGetValue(playerId, out var list)) _tracks[playerId] = list = new List<Sample>(32);
            double t = serverTick * (double)_tickDelta;
            if (list.Count > 0 && list[list.Count - 1].Time >= t) return; // out of order
            list.Add(new Sample { Time = t, State = state });
            if (list.Count > 32) list.RemoveAt(0);
        }

        public void OnSnapshotTime(uint serverTick)
        {
            double t = serverTick * (double)_tickDelta;
            if (!_hasTime || System.Math.Abs(t - _serverTime) > 0.5)
            {
                _serverTime = t;
                _hasTime = true;
            }
            else
            {
                // Gently converge toward the newest snapshot time to absorb clock drift.
                _serverTime += (t - _serverTime) * 0.1;
            }
        }

        public void Advance(float deltaTime)
        {
            if (_hasTime) _serverTime += deltaTime;
        }

        public void Remove(ushort playerId) => _tracks.Remove(playerId);

        public bool TrySample(ushort playerId, out Vector3 position, out float yaw, out PlayerSimState latest)
        {
            position = default;
            yaw = 0f;
            latest = default;
            if (!_tracks.TryGetValue(playerId, out var list) || list.Count == 0) return false;

            latest = list[list.Count - 1].State;
            double rt = RenderTime;
            if (list.Count == 1 || rt <= list[0].Time)
            {
                position = list[0].State.Position;
                yaw = list[0].State.Yaw;
                return true;
            }
            for (int i = list.Count - 1; i > 0; i--)
            {
                var a = list[i - 1];
                var b = list[i];
                if (rt >= a.Time)
                {
                    if (rt >= b.Time)
                    {
                        position = b.State.Position; // newest is in the past: hold, never extrapolate far
                        yaw = b.State.Yaw;
                    }
                    else
                    {
                        float k = (float)((rt - a.Time) / (b.Time - a.Time));
                        position = Vector3.Lerp(a.State.Position, b.State.Position, k);
                        yaw = Mathf.LerpAngle(a.State.Yaw, b.State.Yaw, k);
                    }
                    // Drop samples that are no longer needed.
                    if (i - 1 > 0) list.RemoveRange(0, i - 1);
                    return true;
                }
            }
            position = list[0].State.Position;
            yaw = list[0].State.Yaw;
            return true;
        }
    }
}
