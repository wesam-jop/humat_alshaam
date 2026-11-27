using Adventure3D.Core;
using UnityEngine;

namespace Adventure3D.Gameplay
{
    /// <summary>
    /// Simple combat AI that drives a PlayerAvatar through the same PlayerCommand path as a human:
    /// it looks for the nearest visible enemy, turns with a limited speed and some aim error, fires in
    /// bursts, strafes while engaging and otherwise patrols between spawn points. Runs only on the
    /// authority (offline training).
    /// </summary>
    public sealed class BotBrain
    {
        public readonly ushort PlayerId;
        readonly System.Random _rng;
        readonly float _skill; // 0..1

        float _yaw, _pitch;
        ushort _target;
        float _reaction;
        float _burst, _pause;
        float _strafeTimer;
        float _strafeDir = 1f;
        Vector3 _goal;
        bool _hasGoal;
        float _stuckTimer;
        Vector3 _lastPos;
        float _jumpCooldown;
        Vector2 _aimError;
        bool _crouchToShoot;

        static readonly RaycastHit[] Hits = new RaycastHit[16];

        public BotBrain(ushort playerId, int seed, float skill)
        {
            PlayerId = playerId;
            _rng = new System.Random(seed);
            _skill = Mathf.Clamp01(skill);
        }

        float R() => (float)_rng.NextDouble();

        public PlayerCommand Think(PlayerAvatar self, WorldSimulation world, float dt, float eyeHeight)
        {
            if (!self.Alive)
            {
                _target = 0;
                _hasGoal = false;
                _yaw = self.State.Yaw;
                return PlayerCommand.Neutral(_yaw, 0f);
            }

            Vector3 eye = self.State.Position + Vector3.up * self.SimEyeHeight;
            PlayerAvatar enemy = FindTarget(self, world, eye);
            var cmd = new PlayerCommand();
            _jumpCooldown -= dt;

            if (enemy != null)
            {
                if (enemy.PlayerId != _target)
                {
                    _target = enemy.PlayerId;
                    _reaction = Mathf.Lerp(0.55f, 0.18f, _skill);
                    _aimError = new Vector2(R() - 0.5f, R() - 0.5f) * Mathf.Lerp(14f, 4f, _skill);
                    _crouchToShoot = R() < 0.45f;
                }
                // Decide the stance first, then aim from the eye height the shot will really start at.
                float distToEnemy = (enemy.State.Position - self.State.Position).magnitude;
                bool crouch = _crouchToShoot && distToEnemy > 12f;
                if (crouch) cmd.Buttons |= CommandButtons.Crouch;
                eye = self.State.Position + Vector3.up * self.EyeHeightFor(crouch);
                Vector3 aimPoint = enemy.State.Position + Vector3.up * (R() < 0.15f + 0.25f * _skill ? 1.6f : 1.15f);
                Vector3 to = aimPoint - eye;
                float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + _aimError.x;
                float wantPitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg + _aimError.y;
                float turn = Mathf.Lerp(220f, 520f, _skill) * dt;
                _yaw = Mathf.MoveTowardsAngle(_yaw, wantYaw, turn);
                _pitch = Mathf.MoveTowards(_pitch, wantPitch, turn);
                _aimError = Vector2.MoveTowards(_aimError, Vector2.zero, Mathf.Lerp(4f, 12f, _skill) * dt);

                _reaction -= dt;
                float off = Mathf.Abs(Mathf.DeltaAngle(_yaw, wantYaw - _aimError.x)) + Mathf.Abs(_pitch - (wantPitch - _aimError.y));
                bool onTarget = off < 4f;

                var weapon = WeaponCatalog.Get(self.Combat.ActiveWeapon);
                if (self.Combat.ActiveClip == 0 && self.Combat.ActiveReserve > 0) cmd.Buttons |= CommandButtons.Reload;
                if (_reaction <= 0f && onTarget && self.Combat.ProtectedTimer <= 0f)
                {
                    if (_pause > 0f) _pause -= dt;
                    else
                    {
                        bool fire = weapon.Automatic ? true : R() < 0.35f;
                        if (fire) cmd.Buttons |= CommandButtons.Fire;
                        _burst += dt;
                        if (_burst > Mathf.Lerp(0.35f, 0.7f, _skill)) { _burst = 0f; _pause = Mathf.Lerp(0.5f, 0.25f, _skill); }
                    }
                }

                // Strafe while fighting, keep some distance.
                _strafeTimer -= dt;
                if (_strafeTimer <= 0f) { _strafeDir = R() < 0.5f ? -1f : 1f; _strafeTimer = 0.6f + R() * 0.9f; }
                float dist = to.magnitude;
                float fwd = dist > 18f ? 0.8f : dist < 6f ? -0.6f : 0f;
                cmd.Move = new Vector2(_strafeDir * 0.9f, fwd);
                if (_pause > 0f && R() < 0.01f && _jumpCooldown <= 0f) { cmd.Buttons |= CommandButtons.JumpPressed; _jumpCooldown = 2f; }
            }
            else
            {
                _target = 0;
                if (!_hasGoal || Flat(_goal - self.State.Position).magnitude < 1.5f)
                {
                    var pose = world.NextSpawnPose();
                    _goal = pose.position + new Vector3(R() * 16f - 8f, 0f, R() * 16f - 8f);
                    _hasGoal = true;
                }
                Vector3 to = _goal - self.State.Position;
                float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                _yaw = Mathf.MoveTowardsAngle(_yaw, wantYaw, 180f * dt);
                _pitch = Mathf.MoveTowards(_pitch, 0f, 90f * dt);
                cmd.Move = new Vector2(0f, Mathf.Abs(Mathf.DeltaAngle(_yaw, wantYaw)) < 45f ? 1f : 0.3f);
                if (self.Combat.ActiveClip < WeaponCatalog.Get(self.Combat.ActiveWeapon).ClipSize / 2 && self.Combat.ActiveReserve > 0)
                    cmd.Buttons |= CommandButtons.Reload;
            }

            // Unstick: jump or pick a new goal when not moving.
            float moved = Flat(self.State.Position - _lastPos).magnitude;
            _lastPos = self.State.Position;
            if (cmd.Move.sqrMagnitude > 0.1f && moved < 0.01f) _stuckTimer += dt; else _stuckTimer = 0f;
            if (_stuckTimer > 0.4f)
            {
                if (_jumpCooldown <= 0f) { cmd.Buttons |= CommandButtons.JumpPressed; _jumpCooldown = 1f; }
                if (_stuckTimer > 1.2f) { _hasGoal = false; _stuckTimer = 0f; _strafeDir = -_strafeDir; }
            }

            cmd.Yaw = _yaw;
            cmd.Pitch = _pitch;
            return cmd;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        PlayerAvatar FindTarget(PlayerAvatar self, WorldSimulation world, Vector3 eye)
        {
            PlayerAvatar best = null;
            float bestScore = float.MaxValue;
            foreach (var other in world.Players)
            {
                if (other == self || !other.Alive || other.Combat.Team == self.Combat.Team) continue;
                Vector3 chest = other.State.Position + Vector3.up * 1.2f;
                Vector3 d = chest - eye;
                float dist = d.magnitude;
                if (dist > 60f) continue;
                // Field of view 160 degrees, but the current target stays tracked.
                float ang = Mathf.Abs(Mathf.DeltaAngle(_yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg));
                if (ang > 80f && other.PlayerId != _target) continue;
                if (!Visible(self, other, eye, d / dist, dist)) continue;
                float score = dist + (other.PlayerId == _target ? -10f : 0f);
                if (score < bestScore) { bestScore = score; best = other; }
            }
            return best;
        }

        static bool Visible(PlayerAvatar self, PlayerAvatar other, Vector3 eye, Vector3 dir, float dist)
        {
            int n = Physics.RaycastNonAlloc(eye, dir, Hits, dist + 0.5f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Collider first = null;
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].collider == self.Controller) continue;
                if (Hits[i].distance < nearest) { nearest = Hits[i].distance; first = Hits[i].collider; }
            }
            return first == null || first == other.Controller;
        }
    }
}
