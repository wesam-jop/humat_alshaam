using System;
using Adventure3D.Configuration;
using Adventure3D.Core;
using UnityEngine;

namespace Adventure3D.Gameplay
{
    /// <summary>
    /// A player entity. The root (this object) is the simulation root: it owns the CharacterController
    /// capsule and is moved only by <see cref="PlayerMotor"/> or by authoritative state.
    /// <see cref="ViewRoot"/> is a separate presentation root that is interpolated between ticks; the
    /// model, an Animator, or a third-person camera attach under it without touching the simulation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerAvatar : MonoBehaviour
    {
        [SerializeField] CharacterController controller;
        [Tooltip("Presentation root. Put the character model and Animator under this.")]
        [SerializeField] Transform viewRoot;
        [Tooltip("First-person camera anchor (eye height). Third-person rigs can follow ViewRoot instead.")]
        [SerializeField] Transform cameraAnchor;
        [SerializeField] Renderer[] bodyRenderers = Array.Empty<Renderer>();

        /// <summary>Live avatar count, used by tests to detect duplicated players.</summary>
        public static int LiveCount { get; private set; }

        public ushort PlayerId { get; private set; }
        public bool IsLocal { get; private set; }
        public string DisplayName { get; set; } = "";
        public PlayerSimState State;
        /// <summary>Health, weapons and score. Written by the authority, replicated to clients.</summary>
        public PlayerCombatState Combat;
        public bool Alive => Combat.Alive;

        /// <summary>Raised when the alive state changes (death / respawn), for presentation.</summary>
        public event Action<PlayerAvatar, bool> AliveChanged;

        public CharacterController Controller => controller;
        public Transform ViewRoot => viewRoot;
        public Transform CameraAnchor => cameraAnchor;

        /// <summary>Raised after every simulated tick that produced events (Jumped, Landed, ...).</summary>
        public event Action<PlayerAvatar, MotorEvents> MotorEventsRaised;

        Vector3 _prevTickPosition;
        Vector3 _currTickPosition;
        Vector3 _correctionOffset;
        float _correctionTime;
        float _remoteYaw;
        bool _useRemoteYaw;

        void Awake()
        {
            LiveCount++;
            if (controller == null) controller = GetComponent<CharacterController>();
            if (viewRoot == null)
            {
                var v = transform.Find("View");
                viewRoot = v != null ? v : transform;
            }
            if (cameraAnchor == null)
            {
                var a = viewRoot.Find("CameraAnchor");
                cameraAnchor = a != null ? a : viewRoot;
            }
        }

        void OnDestroy() => LiveCount--;

        public void Initialize(ushort playerId, bool isLocal, PhysicsProfile physics, Vector3 position, float yaw)
        {
            PlayerId = playerId;
            IsLocal = isLocal;
            name = (isLocal ? "Player_Local_" : "Player_") + playerId;

            controller.enabled = false;
            controller.height = physics.CapsuleHeight;
            controller.radius = physics.CapsuleRadius;
            controller.center = Vector3.up * (physics.CapsuleHeight * 0.5f);
            controller.skinWidth = physics.SkinWidth;
            controller.minMoveDistance = 0f;
            controller.enabled = true;
            cameraAnchor.localPosition = Vector3.up * physics.EyeHeight;
            _standHeight = physics.CapsuleHeight;
            _standEye = physics.EyeHeight;
            _crouchEye = physics.CrouchEyeHeight;
            _crouchCamTime = physics.CrouchCameraTime;
            _eyeCurrent = physics.EyeHeight;
            CaptureBodyRest();

            State = new PlayerSimState { Position = position, Yaw = yaw };
            Teleport(position);
            transform.rotation = Quaternion.identity; // yaw lives in state + view, the capsule never rotates
        }

        /// <summary>Applies the motor settings that depend on profiles (slope and step limits).</summary>
        public void ApplyMotorLimits(MovementProfile movement)
        {
            controller.slopeLimit = movement.MaxWalkableSlope;
            controller.stepOffset = Mathf.Min(movement.StepHeight, controller.height * 0.5f);
            _crouchHeight = movement.CrouchHeight;
            _stepHeight = movement.StepHeight;
        }

        /// <summary>Runs exactly one simulation tick.</summary>
        public void Simulate(PlayerCommand command, in MotorSettings settings, float dt)
        {
            if (!controller.enabled) return; // dead players do not move
            _prevTickPosition = _currTickPosition;
            PlayerMotor.Step(controller, ref State, command, settings, dt, MovementSpeedScale);
            _currTickPosition = State.Position;
            if (State.Events != MotorEvents.None) MotorEventsRaised?.Invoke(this, State.Events);
        }

        /// <summary>Hard-sets the simulation state (spawn, reconciliation).</summary>
        public void SetState(PlayerSimState state, bool resetInterpolation)
        {
            State = state;
            ApplyCapsuleShape(state.Crouching);
            Teleport(state.Position);
            if (resetInterpolation)
            {
                _prevTickPosition = state.Position;
                _currTickPosition = state.Position;
            }
            else
            {
                _currTickPosition = state.Position;
            }
        }

        /// <summary>Remote players on a client: placed directly from interpolated snapshots.</summary>
        public void SetRemotePose(Vector3 position, float yaw, PlayerSimState latest)
        {
            State = latest;
            State.Position = position;
            ApplyCapsuleShape(latest.Crouching);
            transform.position = position;
            _prevTickPosition = position;
            _currTickPosition = position;
            _remoteYaw = yaw;
            _useRemoteYaw = true;
        }

        public void Teleport(Vector3 position)
        {
            transform.position = position;
            Physics.SyncTransforms();
            _prevTickPosition = position;
            _currTickPosition = position;
        }

        /// <summary>Visual-only offset that fades out, hides small prediction corrections.</summary>
        public void AddCorrectionOffset(Vector3 offset, float smoothTime)
        {
            if (smoothTime <= 0f) return;
            _correctionOffset += offset;
            _correctionTime = smoothTime;
        }

        /// <summary>Positions the presentation root. alpha = fraction of the current tick elapsed.</summary>
        public void UpdateView(float alpha, bool interpolate, float deltaTime)
        {
            Vector3 p = interpolate ? Vector3.Lerp(_prevTickPosition, _currTickPosition, alpha) : _currTickPosition;
            if (_correctionOffset.sqrMagnitude > 1e-8f && _correctionTime > 0f)
            {
                float k = Mathf.Clamp01(deltaTime / _correctionTime);
                _correctionOffset = Vector3.Lerp(_correctionOffset, Vector3.zero, k * 3f);
                p += _correctionOffset;
            }
            else
            {
                _correctionOffset = Vector3.zero;
            }
            viewRoot.position = p;
            UpdateCrouchVisuals(deltaTime);
            UpdateCharacterAnimation();
            viewRoot.rotation = Quaternion.Euler(0f, _useRemoteYaw ? _remoteYaw : State.Yaw, 0f);
        }

        // ------------------------------------------------------------------ crouch and weapon weight

        float _standHeight = 1.8f, _crouchHeight = 1.2f, _stepHeight = 0.35f;
        float _standEye = 1.62f, _crouchEye = 1.02f, _crouchCamTime = 0.12f, _eyeCurrent = 1.62f;
        Vector3[] _bodyRestPos, _bodyRestScale;
        Animation _characterAnim;
        Transform _spineBone;
        string _currentAnimClip;

        /// <summary>
        /// Ground speed factor from the carried weapon's weight (and scoped aiming). Deterministic from
        /// the replicated combat state, so server and predicting client compute the same movement.
        /// </summary>
        public float MovementSpeedScale
        {
            get
            {
                if (!Combat.Alive) return 1f;
                var w = WeaponCatalog.Get(Combat.ActiveWeapon);
                float s = w.MoveSpeedMultiplier;
                if (Combat.Aiming && w.HasScope) s *= w.ScopedSpeedMultiplier;
                return s;
            }
        }

        /// <summary>Eye height used by the simulation (shots), standing or crouched.</summary>
        public float SimEyeHeight => State.Crouching ? _crouchEye : _standEye;
        public float EyeHeightFor(bool crouched) => crouched ? _crouchEye : _standEye;

        public void ApplyCapsuleShape(bool crouched)
        {
            float h = crouched ? _crouchHeight : _standHeight;
            if (Mathf.Approximately(controller.height, h)) return;
            controller.height = h;
            controller.center = Vector3.up * (h * 0.5f);
            controller.stepOffset = Mathf.Min(_stepHeight, h * 0.5f);
        }

        void CaptureBodyRest()
        {
            _bodyRestPos = new Vector3[bodyRenderers.Length];
            _bodyRestScale = new Vector3[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] == null) continue;
                _bodyRestPos[i] = bodyRenderers[i].transform.localPosition;
                _bodyRestScale[i] = bodyRenderers[i].transform.localScale;
            }
        }

        /// <summary>Smooth camera height and squashed placeholder body while crouched.</summary>
        void UpdateCrouchVisuals(float deltaTime)
        {
            float targetEye = State.Crouching ? _crouchEye : _standEye;
            float k = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.01f, _crouchCamTime) * 3f);
            _eyeCurrent = Mathf.Lerp(_eyeCurrent, targetEye, k);
            cameraAnchor.localPosition = Vector3.up * _eyeCurrent;

            if (_bodyRestPos == null) return;
            float ratio = _eyeCurrent / Mathf.Max(0.01f, _standEye);
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                var r = bodyRenderers[i];
                if (r == null) continue;
                var t = r.transform;
                t.localPosition = new Vector3(_bodyRestPos[i].x, _bodyRestPos[i].y * ratio, _bodyRestPos[i].z);
                t.localScale = new Vector3(_bodyRestScale[i].x, _bodyRestScale[i].y * ratio, _bodyRestScale[i].z);
            }
        }

        /// <summary>
        /// Dead players have no collision (bullets and other players pass through) and no visible body.
        /// </summary>
        public void SetAlive(bool alive)
        {
            bool wasAlive = controller.enabled;
            Combat.SetFlag(CombatFlags.Alive, alive);
            if (wasAlive == alive) return;
            controller.enabled = alive;
            if (viewRoot != transform) viewRoot.gameObject.SetActive(alive || IsLocal);
            if (!IsLocal) SetBodyVisible(alive);
            AliveChanged?.Invoke(this, alive);
        }

        /// <summary>
        /// Replaces the placeholder capsule with a character model (any prefab, e.g. an imported GLB/FBX).
        /// The model is scaled to <paramref name="height"/> metres, placed with its feet on the ground under
        /// the view root, and its renderers become the body renderers (hidden in first person, squashed when
        /// crouching). Simulation, collision and networking are not affected.
        /// </summary>
        public GameObject ReplaceBody(GameObject modelPrefab, float height, float yawOffset = 0f)
        {
            if (modelPrefab == null) return null;
            foreach (var r in bodyRenderers) if (r != null) r.gameObject.SetActive(false);

            var existing = viewRoot.Find("CharacterModel");
            if (existing != null) Destroy(existing.gameObject);

            var model = Instantiate(modelPrefab, viewRoot, false);
            model.name = "CharacterModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f);
            foreach (var col in model.GetComponentsInChildren<Collider>(true)) Destroy(col);

            // Fit to the requested height using the mesh bounds.
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds b = default;
            bool has = false;
            foreach (var r in renderers)
            {
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            float sourceHeight = has ? Mathf.Max(0.01f, b.size.y) : 1f;
            float scale = height / sourceHeight;
            model.transform.localScale = model.transform.localScale * scale;
            // Put the lowest point of the mesh on the feet.
            float bottom = has ? (b.min.y - viewRoot.position.y) * scale : 0f;
            model.transform.localPosition = new Vector3(0f, -bottom, 0f);

            _characterAnim = model.GetComponentInChildren<Animation>(true);
            _spineBone = FindChildRecursive(model.transform, "Bip01 Spine1");
            _currentAnimClip = null;
            UpdateCharacterAnimation();

            bodyRenderers = renderers;
            CaptureBodyRest();
            if (IsLocal) SetBodyVisible(false);
            return model;
        }

        static Transform FindChildRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var found = FindChildRecursive(c, name);
                if (found != null) return found;
            }
            return null;
        }

        void UpdateCharacterAnimation()
        {
            if (_spineBone != null && Alive)
                _spineBone.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(-State.Pitch * 0.4f, -25f, 25f));

            if (_characterAnim == null) return;
            float horizSpeedSq = State.Velocity.x * State.Velocity.x + State.Velocity.z * State.Velocity.z;
            string desired = !State.Grounded ? "jump"
                : State.Crouching ? (horizSpeedSq > 0.25f ? "crouchrun" : "crouch_idle")
                : horizSpeedSq > 9f ? "run"
                : horizSpeedSq > 0.25f ? "walk"
                : "idle1";

            if (_currentAnimClip == desired) return;
            var clip = _characterAnim.GetClip(desired);
            if (clip == null && desired != "idle1")
            {
                desired = "idle1";
                clip = _characterAnim.GetClip(desired);
            }
            if (clip != null)
            {
                clip.wrapMode = desired == "jump" ? WrapMode.ClampForever : WrapMode.Loop;
                _characterAnim.CrossFade(desired, 0.12f);
                _currentAnimClip = desired;
            }
        }

        public void SetBodyVisible(bool visible)
        {
            foreach (var r in bodyRenderers)
                if (r != null) r.enabled = visible;
        }

        public void SetBodyColor(Color color)
        {
            foreach (var r in bodyRenderers)
            {
                if (r == null) continue;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
