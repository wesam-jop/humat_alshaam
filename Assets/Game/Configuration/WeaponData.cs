using UnityEngine;

namespace Adventure3D.Configuration
{
    public enum WeaponCategory
    {
        Pistol,
        Shotgun,
        SMG,
        Rifle,
        Sniper,
        Heavy,
        Melee,
        Equipment
    }

    public enum TeamRestriction
    {
        Both,
        HumatAlSham,
        OpposingTeam
    }

    [CreateAssetMenu(fileName = "NewWeapon", menuName = "HumatAlSham/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("الهوية والواجهة (UI)")]
        public string weaponId;
        public string displayNameAr;
        public WeaponCategory category;
        public TeamRestriction team;
        public Sprite iconSprite;
        public int price;

        [Header("الذخيرة والأداء (Stats)")]
        public int magazineSize;
        public int maxReserveAmmo;
        public float damage;
        public float fireRate;
        public float reloadTime;
        public float movementSpeedMult = 1f;

        [Header("ميزات خاصة (CS 1.5)")]
        public bool hasSilencer;
        public bool hasScope;
        public int zoomLevels = 0;

        [Header("المجسمات والأصوات (3D & Audio)")]
        public GameObject viewModelPrefab;
        public GameObject worldModelPrefab;
        public AudioClip fireSound;
        public AudioClip reloadSound;
    }
}
