using Adventure3D.Configuration;
using UnityEditor;
using UnityEngine;

namespace Adventure3D.Editor
{
    /// <summary>
    /// Generates WeaponData ScriptableObject assets in Resources/Adventure3D/WeaponsData
    /// with their extracted weapon sprites automatically linked.
    /// </summary>
    public static class WeaponDataGenerator
    {
        const string OutputFolder = "Assets/Game/Configuration/Resources/Adventure3D/WeaponsData";

        [MenuItem("HumatAlSham/Generate Default Weapon Assets")]
        public static void GenerateAll()
        {
            EnsureFolder("Assets/Game/Configuration/Resources");
            EnsureFolder("Assets/Game/Configuration/Resources/Adventure3D");
            EnsureFolder(OutputFolder);

            Create("glock18", "Glock 18", WeaponCategory.Pistol, TeamRestriction.OpposingTeam, 400, 20, 120, 28, 10, 2.2f, 0.98f);
            Create("usp45", "USP .45 التكتيكي", WeaponCategory.Pistol, TeamRestriction.Both, 500, 12, 100, 34, 8, 2.7f, 0.98f, silencer: true);
            Create("p228", "P228", WeaponCategory.Pistol, TeamRestriction.Both, 600, 13, 52, 32, 8.5f, 2.7f, 0.98f);
            Create("deagle", "Desert Eagle .50", WeaponCategory.Pistol, TeamRestriction.Both, 650, 7, 35, 63, 4.5f, 2.2f, 0.95f);
            Create("elites", "Dual Berettas", WeaponCategory.Pistol, TeamRestriction.OpposingTeam, 800, 30, 120, 36, 11, 4.6f, 0.96f);
            Create("fiveseven", "Five-SeveN", WeaponCategory.Pistol, TeamRestriction.HumatAlSham, 750, 20, 100, 30, 9, 2.7f, 0.98f);

            Create("m3", "M3 Super 90", WeaponCategory.Shotgun, TeamRestriction.Both, 1700, 8, 32, 88, 1.2f, 3.0f, 0.90f);
            Create("xm1014", "XM1014", WeaponCategory.Shotgun, TeamRestriction.Both, 3000, 7, 32, 82, 3.5f, 3.2f, 0.92f);

            Create("tmp", "TMP", WeaponCategory.SMG, TeamRestriction.HumatAlSham, 1250, 30, 120, 26, 14, 2.1f, 0.96f, silencer: true);
            Create("mac10", "MAC-10", WeaponCategory.SMG, TeamRestriction.OpposingTeam, 1400, 30, 100, 29, 14.5f, 3.1f, 0.96f);
            Create("mp5", "MP5 Navy", WeaponCategory.SMG, TeamRestriction.Both, 1500, 30, 120, 36, 13, 2.6f, 0.95f);
            Create("ump45", "UMP .45", WeaponCategory.SMG, TeamRestriction.Both, 1700, 25, 100, 40, 10, 3.5f, 0.94f);
            Create("p90", "P90 التكتيكي", WeaponCategory.SMG, TeamRestriction.Both, 2350, 50, 100, 32, 14.5f, 3.3f, 0.94f);

            Create("ak47", "كلاشنكوف AK-47", WeaponCategory.Rifle, TeamRestriction.OpposingTeam, 2500, 30, 90, 46, 10, 2.5f, 0.88f);
            Create("m4a1", "M4A1 Carbine", WeaponCategory.Rifle, TeamRestriction.HumatAlSham, 3100, 30, 90, 42, 11, 3.1f, 0.89f, silencer: true);
            Create("sg552", "SG 552 Commando", WeaponCategory.Rifle, TeamRestriction.OpposingTeam, 3500, 30, 90, 44, 10, 2.9f, 0.88f, scope: true, zoom: 1);
            Create("aug", "Steyr AUG", WeaponCategory.Rifle, TeamRestriction.HumatAlSham, 3500, 30, 90, 43, 10, 3.3f, 0.88f, scope: true, zoom: 1);

            Create("scout", "Steyr Scout", WeaponCategory.Sniper, TeamRestriction.Both, 2750, 10, 90, 75, 0.8f, 2.0f, 0.98f, scope: true, zoom: 2);
            Create("awp", "AWP Magnum", WeaponCategory.Sniper, TeamRestriction.Both, 4750, 10, 30, 115, 0.7f, 3.7f, 0.82f, scope: true, zoom: 2);
            Create("g3sg1", "G3SG1", WeaponCategory.Sniper, TeamRestriction.OpposingTeam, 5000, 20, 90, 80, 4.0f, 4.7f, 0.84f, scope: true, zoom: 2);
            Create("sg550", "SG 550", WeaponCategory.Sniper, TeamRestriction.HumatAlSham, 4200, 30, 90, 74, 4.2f, 3.8f, 0.85f, scope: true, zoom: 2);

            Create("m249", "M249 Para", WeaponCategory.Heavy, TeamRestriction.Both, 5750, 100, 200, 45, 12.5f, 5.7f, 0.78f);
            Create("knife", "سكين تكتيكي", WeaponCategory.Melee, TeamRestriction.Both, 0, 0, 0, 55, 2.0f, 0f, 1.0f);

            Create("kevlar", "سترة واقية (Kevlar)", WeaponCategory.Equipment, TeamRestriction.Both, 650, 0, 0, 0, 0, 0, 1f);
            Create("helmet", "سترة واقية + خوذة", WeaponCategory.Equipment, TeamRestriction.Both, 1000, 0, 0, 0, 0, 0, 1f);
            Create("hegrenade", "قنبلة يدوية (HE)", WeaponCategory.Equipment, TeamRestriction.Both, 300, 1, 0, 95, 1, 0, 0.98f);
            Create("flashbang", "قنبلة ضوئية (Flash)", WeaponCategory.Equipment, TeamRestriction.Both, 200, 2, 0, 0, 1, 0, 0.98f);
            Create("smoke", "قنبلة دخانية (Smoke)", WeaponCategory.Equipment, TeamRestriction.Both, 300, 1, 0, 0, 1, 0, 0.98f);
            Create("defuser", "عدّة تفكيك القنبلة", WeaponCategory.Equipment, TeamRestriction.HumatAlSham, 200, 0, 0, 0, 0, 0, 1f);
            Create("c4", "حقيبة متفجرات C4", WeaponCategory.Equipment, TeamRestriction.OpposingTeam, 0, 1, 0, 0, 0, 0, 0.96f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HumatAlSham] Generated 30 WeaponData assets in " + OutputFolder);
        }

        static void Create(string id, string nameAr, WeaponCategory cat, TeamRestriction team, int price, int mag, int reserve, float dmg, float rate, float reload, float spd, bool silencer = false, bool scope = false, int zoom = 0)
        {
            string path = $"{OutputFolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.weaponId = id;
            asset.displayNameAr = nameAr;
            asset.category = cat;
            asset.team = team;
            asset.price = price;
            asset.magazineSize = mag;
            asset.maxReserveAmmo = reserve;
            asset.damage = dmg;
            asset.fireRate = rate;
            asset.reloadTime = reload;
            asset.movementSpeedMult = spd;
            asset.hasSilencer = silencer;
            asset.hasScope = scope;
            asset.zoomLevels = zoom;

            string spritePath = $"Assets/Game/Presentation/Resources/Adventure3D/Weapons/{id}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null) asset.iconSprite = sprite;
            EditorUtility.SetDirty(asset);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
