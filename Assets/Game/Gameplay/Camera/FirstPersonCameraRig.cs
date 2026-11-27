using Adventure3D.Configuration;
using Adventure3D.Core;
using UnityEngine;

namespace Adventure3D.Gameplay
{
    /// <summary>Contract for any camera that follows the local player (first person now, third person later).</summary>
    public interface ICameraRig
    {
        void Bind(PlayerAvatar target, ICommandSource look, CameraProfile profile);
        void Unbind();
    }

    /// <summary>
    /// Temporary first-person test camera. Reads look angles every rendered frame (not per tick) so aim
    /// stays responsive at any frame rate, and follows the interpolated camera anchor of the local
    /// player. Created only for processes that have a local player, never on the dedicated server.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Camera))]
    public sealed class FirstPersonCameraRig : MonoBehaviour, ICameraRig
    {
        PlayerAvatar _target;
        CameraProfile _profile;

        /// <summary>Extra rotation (recoil kick, damage shake) added by presentation, in degrees.</summary>
        public Vector3 KickEuler { get; set; }
        /// <summary>Field of view requested by presentation (aiming / scope); -1 = profile value.</summary>
        public float FovOverride { get; set; } = -1f;
        public PlayerAvatar Target => _target;
        ICommandSource _look;
        Camera _camera;

        public Camera Camera => _camera != null ? _camera : (_camera = GetComponent<Camera>());

        public static FirstPersonCameraRig Create(string name = "LocalPlayerCamera")
        {
            var go = new GameObject(name);
            go.tag = "MainCamera";
            go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            return go.AddComponent<FirstPersonCameraRig>();
        }

        public void Bind(PlayerAvatar target, ICommandSource look, CameraProfile profile)
        {
            _target = target;
            _look = look;
            _profile = profile;
            Camera.fieldOfView = profile.FieldOfView;
            Camera.nearClipPlane = profile.NearClip;
            Camera.farClipPlane = profile.FarClip;
            if (_target != null) _target.SetBodyVisible(false);
            LateUpdate();
        }

        public void Unbind()
        {
            if (_target != null) _target.SetBodyVisible(true);
            _target = null;
            _look = null;
        }

        void LateUpdate()
        {
            if (_target == null) return;
            transform.position = _target.CameraAnchor.position;
            float yaw = _look != null ? _look.LookYaw : _target.State.Yaw;
            float pitch = _look != null ? _look.LookPitch : _target.State.Pitch;
            transform.rotation = Quaternion.Euler(pitch + KickEuler.x, yaw + KickEuler.y, KickEuler.z);
            if (_profile != null)
            {
                float fov = FovOverride > 0f ? FovOverride : _profile.FieldOfView;
                Camera.fieldOfView = Mathf.Lerp(Camera.fieldOfView, fov, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            }
        }
    }
}
