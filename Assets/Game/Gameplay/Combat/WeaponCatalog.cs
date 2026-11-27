using System.Collections.Generic;

namespace Adventure3D.Gameplay
{
    public enum WeaponClass : byte { Knife, Pistol, SMG, Rifle, Sniper, Shotgun, Heavy }

    /// <summary>
    /// Ballistic and handling numbers of one weapon. Units: damage in HP, distances in meters, times in
    /// seconds, spread / recoil in degrees. The Id matches the HUD icon in Resources/Adventure3D/Weapons.
    /// </summary>
    public sealed class WeaponDefinition
    {
        public string Id;
        public string NameAr;
        public WeaponClass Class;
        public int Price;
        public int Damage;
        public int Pellets = 1;
        public float Range = 120f;
        public float FireInterval;
        public bool Automatic;
        public int ClipSize;
        public int ReserveMax;
        public float ReloadTime;
        public float DrawTime = 0.5f;
        public float BaseSpread;
        public float MoveSpread;
        public float AirSpread = 8f;
        public float AimSpreadMultiplier = 0.6f;
        public float RecoilPerShot;
        public float RecoilRecovery = 10f;
        public float MaxRecoil = 6f;
        public float HeadshotMultiplier = 4f;
        /// <summary>Fraction of damage that goes through armor to health (CS style armor ratio).</summary>
        public float ArmorPenetration = 0.5f;
        public int KillReward = 300;
        /// <summary>Visual camera kick per shot (presentation only).</summary>
        public float ViewKick = 1.5f;
        public bool HasScope;
        /// <summary>Ground speed multiplier from the weapon's weight (CS max speed / 250).</summary>
        public float MoveSpeedMultiplier = 1f;
        /// <summary>Extra speed multiplier while looking through a scope.</summary>
        public float ScopedSpeedMultiplier = 1f;
        /// <summary>Damage kept per 12.7 m of distance (CS range modifier per 500 units).</summary>
        public float RangeModifier = 1f;

        public bool IsKnife => Class == WeaponClass.Knife;
        public bool UsesAmmo => !IsKnife;
    }

    /// <summary>Server-side weapon table. Index in <see cref="All"/> is the weapon number sent on the wire.</summary>
    public static class WeaponCatalog
    {
        public static readonly WeaponDefinition[] All =
        {
            new WeaponDefinition { Id = "knife", MoveSpeedMultiplier = 1f, RangeModifier = 1f, NameAr = "سكين قتالية", Class = WeaponClass.Knife, Damage = 55, Range = 1.9f, FireInterval = 0.45f, Automatic = true, DrawTime = 0.35f, HeadshotMultiplier = 1f, ArmorPenetration = 0.85f, KillReward = 1500, ViewKick = 0f },
            new WeaponDefinition { Id = "usp45", MoveSpeedMultiplier = 1f, RangeModifier = 0.79f, NameAr = "USP .45 التكتيكي", Class = WeaponClass.Pistol, Price = 500, Damage = 34, FireInterval = 0.15f, ClipSize = 12, ReserveMax = 100, ReloadTime = 2.2f, BaseSpread = 0.7f, MoveSpread = 1.8f, RecoilPerShot = 1.3f, ArmorPenetration = 0.5f, ViewKick = 1.6f },
            new WeaponDefinition { Id = "glock18", MoveSpeedMultiplier = 1f, RangeModifier = 0.75f, NameAr = "Glock 18", Class = WeaponClass.Pistol, Price = 400, Damage = 25, FireInterval = 0.12f, ClipSize = 20, ReserveMax = 120, ReloadTime = 2.2f, BaseSpread = 0.9f, MoveSpread = 2f, RecoilPerShot = 1.1f, ArmorPenetration = 0.47f, ViewKick = 1.3f },
            new WeaponDefinition { Id = "deagle", MoveSpeedMultiplier = 1f, RangeModifier = 0.81f, NameAr = "مسدس ديزرت إيغل", Class = WeaponClass.Pistol, Price = 650, Damage = 54, FireInterval = 0.25f, ClipSize = 7, ReserveMax = 35, ReloadTime = 2.2f, BaseSpread = 0.6f, MoveSpread = 3f, RecoilPerShot = 3.5f, MaxRecoil = 8f, ArmorPenetration = 0.93f, ViewKick = 4f },
            new WeaponDefinition { Id = "mp5", MoveSpeedMultiplier = 1f, RangeModifier = 0.84f, NameAr = "رشاش MP5", Class = WeaponClass.SMG, Price = 1500, Damage = 26, FireInterval = 0.08f, Automatic = true, ClipSize = 30, ReserveMax = 120, ReloadTime = 2.6f, BaseSpread = 0.9f, MoveSpread = 1.6f, RecoilPerShot = 0.45f, ArmorPenetration = 0.5f, KillReward = 600, ViewKick = 0.8f },
            new WeaponDefinition { Id = "p90", MoveSpeedMultiplier = 0.98f, RangeModifier = 0.885f, NameAr = "P90 التكتيكي", Class = WeaponClass.SMG, Price = 2350, Damage = 21, FireInterval = 0.066f, Automatic = true, ClipSize = 50, ReserveMax = 100, ReloadTime = 3.3f, BaseSpread = 1f, MoveSpread = 1.8f, RecoilPerShot = 0.4f, ArmorPenetration = 0.69f, ViewKick = 0.7f },
            new WeaponDefinition { Id = "ak47", MoveSpeedMultiplier = 0.884f, RangeModifier = 0.98f, NameAr = "كلاشنكوف AK-47", Class = WeaponClass.Rifle, Price = 2700, Damage = 36, FireInterval = 0.1f, Automatic = true, ClipSize = 30, ReserveMax = 90, ReloadTime = 2.5f, BaseSpread = 0.4f, MoveSpread = 4f, RecoilPerShot = 0.9f, ArmorPenetration = 0.78f, ViewKick = 1.8f },
            new WeaponDefinition { Id = "m4a1", MoveSpeedMultiplier = 0.92f, RangeModifier = 0.97f, NameAr = "M4A1 هجومية", Class = WeaponClass.Rifle, Price = 3100, Damage = 33, FireInterval = 0.09f, Automatic = true, ClipSize = 30, ReserveMax = 90, ReloadTime = 3.1f, BaseSpread = 0.35f, MoveSpread = 3.5f, RecoilPerShot = 0.75f, ArmorPenetration = 0.7f, ViewKick = 1.5f },
            new WeaponDefinition { Id = "awp", MoveSpeedMultiplier = 0.84f, ScopedSpeedMultiplier = 0.714f, RangeModifier = 0.99f, NameAr = "قناصة AWP", Class = WeaponClass.Sniper, Price = 4750, Damage = 115, FireInterval = 1.45f, ClipSize = 10, ReserveMax = 30, ReloadTime = 3.7f, Range = 300f, BaseSpread = 5f, MoveSpread = 8f, AimSpreadMultiplier = 0.01f, RecoilPerShot = 0f, HeadshotMultiplier = 2f, ArmorPenetration = 0.97f, KillReward = 100, ViewKick = 6f, HasScope = true },
            new WeaponDefinition { Id = "scout", MoveSpeedMultiplier = 1.04f, ScopedSpeedMultiplier = 0.846f, RangeModifier = 0.98f, NameAr = "Steyr Scout", Class = WeaponClass.Sniper, Price = 2750, Damage = 75, FireInterval = 1.25f, ClipSize = 10, ReserveMax = 90, ReloadTime = 2f, Range = 300f, BaseSpread = 3f, MoveSpread = 5f, AimSpreadMultiplier = 0.02f, RecoilPerShot = 0f, HeadshotMultiplier = 3f, ArmorPenetration = 0.85f, ViewKick = 4f, HasScope = true },
            new WeaponDefinition { Id = "m3", MoveSpeedMultiplier = 0.92f, RangeModifier = 0.7f, NameAr = "M3 Super 90", Class = WeaponClass.Shotgun, Price = 1700, Damage = 20, Pellets = 9, FireInterval = 0.88f, ClipSize = 8, ReserveMax = 32, ReloadTime = 3.2f, Range = 35f, BaseSpread = 3.8f, MoveSpread = 1f, AimSpreadMultiplier = 0.85f, RecoilPerShot = 0f, HeadshotMultiplier = 2f, ArmorPenetration = 0.5f, KillReward = 900, ViewKick = 5f },
            new WeaponDefinition { Id = "m249", MoveSpeedMultiplier = 0.88f, RangeModifier = 0.97f, NameAr = "M249 Para", Class = WeaponClass.Heavy, Price = 5750, Damage = 32, FireInterval = 0.08f, Automatic = true, ClipSize = 100, ReserveMax = 200, ReloadTime = 5f, BaseSpread = 1.1f, MoveSpread = 4f, RecoilPerShot = 0.6f, ArmorPenetration = 0.8f, ViewKick = 1.4f },
            new WeaponDefinition { Id = "p228", MoveSpeedMultiplier = 1f, RangeModifier = 0.8f, NameAr = "P228 Compact", Class = WeaponClass.Pistol, Price = 600, Damage = 32, FireInterval = 0.14f, ClipSize = 13, ReserveMax = 52, ReloadTime = 2.7f, BaseSpread = 0.75f, MoveSpread = 1.9f, RecoilPerShot = 1.2f, ArmorPenetration = 0.8f, ViewKick = 1.4f },
            new WeaponDefinition { Id = "fiveseven", MoveSpeedMultiplier = 1f, RangeModifier = 0.885f, NameAr = "Five-SeveN", Class = WeaponClass.Pistol, Price = 750, Damage = 20, FireInterval = 0.13f, ClipSize = 20, ReserveMax = 100, ReloadTime = 3.2f, BaseSpread = 0.65f, MoveSpread = 1.7f, RecoilPerShot = 1.0f, ArmorPenetration = 0.911f, ViewKick = 1.2f },
            new WeaponDefinition { Id = "elites", MoveSpeedMultiplier = 1f, RangeModifier = 0.75f, NameAr = "Dual Berettas", Class = WeaponClass.Pistol, Price = 800, Damage = 36, FireInterval = 0.11f, ClipSize = 30, ReserveMax = 120, ReloadTime = 4.6f, BaseSpread = 0.95f, MoveSpread = 2.2f, RecoilPerShot = 1.35f, ArmorPenetration = 0.525f, ViewKick = 1.5f },
            new WeaponDefinition { Id = "xm1014", MoveSpeedMultiplier = 0.96f, RangeModifier = 0.7f, NameAr = "XM1014 Auto", Class = WeaponClass.Shotgun, Price = 3000, Damage = 22, Pellets = 6, FireInterval = 0.25f, Automatic = true, ClipSize = 7, ReserveMax = 32, ReloadTime = 3.0f, Range = 35f, BaseSpread = 3.5f, MoveSpread = 1.2f, AimSpreadMultiplier = 0.85f, RecoilPerShot = 0.4f, HeadshotMultiplier = 2f, ArmorPenetration = 0.5f, KillReward = 900, ViewKick = 4.2f },
            new WeaponDefinition { Id = "tmp", MoveSpeedMultiplier = 1f, RangeModifier = 0.84f, NameAr = "TMP التكتيكي", Class = WeaponClass.SMG, Price = 1250, Damage = 20, FireInterval = 0.07f, Automatic = true, ClipSize = 30, ReserveMax = 120, ReloadTime = 2.1f, BaseSpread = 0.85f, MoveSpread = 1.5f, RecoilPerShot = 0.38f, ArmorPenetration = 0.425f, KillReward = 600, ViewKick = 0.65f },
            new WeaponDefinition { Id = "mac10", MoveSpeedMultiplier = 1f, RangeModifier = 0.82f, NameAr = "MAC-10", Class = WeaponClass.SMG, Price = 1400, Damage = 29, FireInterval = 0.07f, Automatic = true, ClipSize = 30, ReserveMax = 100, ReloadTime = 3.2f, BaseSpread = 1.15f, MoveSpread = 1.9f, RecoilPerShot = 0.5f, ArmorPenetration = 0.475f, KillReward = 600, ViewKick = 0.85f },
            new WeaponDefinition { Id = "ump45", MoveSpeedMultiplier = 1f, RangeModifier = 0.82f, NameAr = "UMP .45", Class = WeaponClass.SMG, Price = 1700, Damage = 30, FireInterval = 0.105f, Automatic = true, ClipSize = 25, ReserveMax = 100, ReloadTime = 3.5f, BaseSpread = 0.85f, MoveSpread = 1.6f, RecoilPerShot = 0.55f, ArmorPenetration = 0.5f, KillReward = 600, ViewKick = 0.95f },
            new WeaponDefinition { Id = "galil", MoveSpeedMultiplier = 0.96f, RangeModifier = 0.98f, NameAr = "Galil الهجومية", Class = WeaponClass.Rifle, Price = 2000, Damage = 30, FireInterval = 0.088f, Automatic = true, ClipSize = 35, ReserveMax = 90, ReloadTime = 2.4f, BaseSpread = 0.5f, MoveSpread = 3.6f, RecoilPerShot = 0.8f, ArmorPenetration = 0.775f, ViewKick = 1.6f },
            new WeaponDefinition { Id = "famas", MoveSpeedMultiplier = 0.96f, RangeModifier = 0.96f, NameAr = "FAMAS 5.56", Class = WeaponClass.Rifle, Price = 2250, Damage = 30, FireInterval = 0.085f, Automatic = true, ClipSize = 25, ReserveMax = 90, ReloadTime = 3.3f, BaseSpread = 0.42f, MoveSpread = 3.2f, RecoilPerShot = 0.75f, ArmorPenetration = 0.7f, ViewKick = 1.45f },
            new WeaponDefinition { Id = "sg552", MoveSpeedMultiplier = 0.94f, ScopedSpeedMultiplier = 0.85f, RangeModifier = 0.955f, NameAr = "SG 552 Commando", Class = WeaponClass.Rifle, Price = 3500, Damage = 33, FireInterval = 0.088f, Automatic = true, ClipSize = 30, ReserveMax = 90, ReloadTime = 2.8f, BaseSpread = 0.35f, MoveSpread = 3.4f, RecoilPerShot = 0.75f, ArmorPenetration = 0.7f, ViewKick = 1.5f, HasScope = true },
            new WeaponDefinition { Id = "aug", MoveSpeedMultiplier = 0.96f, ScopedSpeedMultiplier = 0.85f, RangeModifier = 0.96f, NameAr = "Steyr AUG", Class = WeaponClass.Rifle, Price = 3500, Damage = 32, FireInterval = 0.088f, Automatic = true, ClipSize = 30, ReserveMax = 90, ReloadTime = 3.3f, BaseSpread = 0.35f, MoveSpread = 3.4f, RecoilPerShot = 0.72f, ArmorPenetration = 0.7f, ViewKick = 1.45f, HasScope = true },
            new WeaponDefinition { Id = "g3sg1", MoveSpeedMultiplier = 0.84f, ScopedSpeedMultiplier = 0.714f, RangeModifier = 0.98f, NameAr = "G3SG1 الآلية", Class = WeaponClass.Sniper, Price = 5000, Damage = 80, FireInterval = 0.25f, Automatic = true, ClipSize = 20, ReserveMax = 90, ReloadTime = 4.7f, Range = 300f, BaseSpread = 3.5f, MoveSpread = 7f, AimSpreadMultiplier = 0.03f, RecoilPerShot = 1.8f, HeadshotMultiplier = 2f, ArmorPenetration = 0.825f, ViewKick = 4.2f, HasScope = true },
            new WeaponDefinition { Id = "sg550", MoveSpeedMultiplier = 0.84f, ScopedSpeedMultiplier = 0.714f, RangeModifier = 0.98f, NameAr = "SG 550 الآلية", Class = WeaponClass.Sniper, Price = 4200, Damage = 70, FireInterval = 0.25f, Automatic = true, ClipSize = 30, ReserveMax = 90, ReloadTime = 3.8f, Range = 300f, BaseSpread = 3.2f, MoveSpread = 6.5f, AimSpreadMultiplier = 0.03f, RecoilPerShot = 1.5f, HeadshotMultiplier = 2f, ArmorPenetration = 0.825f, ViewKick = 3.6f, HasScope = true },
        };

        static Dictionary<string, byte> _byId;

        public static WeaponDefinition Get(byte index) => index < All.Length ? All[index] : All[0];

        public static bool TryIndexOf(string id, out byte index)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, byte>();
                for (int i = 0; i < All.Length; i++) _byId[All[i].Id] = (byte)i;
            }
            return _byId.TryGetValue(id ?? "", out index);
        }

        public static byte IndexOf(string id) => TryIndexOf(id, out var i) ? i : (byte)0;

        public static bool IsPrimary(WeaponDefinition w) =>
            w.Class == WeaponClass.SMG || w.Class == WeaponClass.Rifle || w.Class == WeaponClass.Sniper
            || w.Class == WeaponClass.Shotgun || w.Class == WeaponClass.Heavy;
    }
}
