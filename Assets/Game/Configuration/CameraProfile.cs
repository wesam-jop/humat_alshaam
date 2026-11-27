using UnityEngine;

namespace Adventure3D.Configuration
{
    [CreateAssetMenu(menuName = "Adventure3D/Camera Profile", fileName = "CameraProfile")]
    public sealed class CameraProfile : ScriptableObject
    {
        [Unit("deg", 40f, 110f)] public float FieldOfView = 75f;
        [Unit("m", Min = 0.01f)] public float NearClip = 0.05f;
        [Unit("m", Min = 1f)] public float FarClip = 500f;
        [Unit("deg", -89f, 0f)] public float MinPitch = -85f;
        [Unit("deg", 0f, 89f)] public float MaxPitch = 85f;
        [Tooltip("Interpolate the view between simulation ticks for smooth motion at any frame rate.")]
        public bool InterpolateView = true;
    }
}
