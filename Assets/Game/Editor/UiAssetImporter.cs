using UnityEditor;
using UnityEngine;

namespace Adventure3D.Editor
{
    /// <summary>
    /// Ensures all UI and Weapon PNGs under Resources/Adventure3D/ are imported as crisp Sprites
    /// with alpha transparency and no mipmap blur.
    /// </summary>
    public sealed class UiAssetImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.EndsWith(".png")) return;
            if (!assetPath.Contains("Resources/Adventure3D/UI/") && !assetPath.Contains("Resources/Adventure3D/Weapons/"))
                return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.filterMode = FilterMode.Bilinear;
        }

        [MenuItem("HumatAlSham/Reimport UI & Weapon Sprites")]
        public static void ReimportAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[]
            {
                "Assets/Game/Presentation/Resources/Adventure3D/UI",
                "Assets/Game/Presentation/Resources/Adventure3D/Weapons"
            });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.EndsWith(".png"))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log($"[HumatAlSham] Reimported {guids.Length} UI & Weapon textures as Sprites.");
        }
    }
}
