using UnityEditor;
using UnityEngine;

namespace Adventure3D.EditorTools
{
    /// <summary>
    /// Ensures all imported CS 1.6 GLB characters and weapon viewmodels use Legacy Animation
    /// so Animation.GetClip("idle1" / "shoot1" / "reload" / "draw") works at runtime.
    /// </summary>
    public static class GlbImportSetup
    {
        [MenuItem("Adventure3D/Fix GLB Animations (Legacy)")]
        public static void FixAllGlb()
        {
            string[] guids = AssetDatabase.FindAssets("", new[] { "Assets/Game/Presentation/Resources/Adventure3D" });
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[A3D] Reimported {count} GLB assets with Legacy animation support.");
        }
    }
}
