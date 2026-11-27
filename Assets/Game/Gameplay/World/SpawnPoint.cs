using UnityEngine;

namespace Adventure3D.Gameplay
{
    /// <summary>Marks where players appear. Lower Index is used first.</summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        public int Index;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.up * 1.6f, transform.position + Vector3.up * 1.6f + transform.forward);
        }
    }
}
