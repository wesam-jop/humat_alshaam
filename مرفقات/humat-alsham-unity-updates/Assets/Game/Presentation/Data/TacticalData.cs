using System;
using System.Collections.Generic;
using UnityEngine;

namespace Adventure3D.Presentation
{
    public enum TacticalMapId { de_aleppo, cs_damascus, cs_palmyra }
    public enum GameModeType { Defusal, Hostage, Tdm }
    public enum BotDifficulty { Easy, Normal, Hard, Expert }
    public enum BotRadioMode { Voice, Text, Off }

    [Serializable]
    public sealed class CreateServerConfig
    {
        public string MapName = "de_aleppo";
        public string ServerName = "خادم حماة الشام #1";
        public GameModeType Mode = GameModeType.Defusal;
        public int MaxPlayers = 16;
        public bool IsOnline;
        public string Password = "";

        public int TimeLimitMinutes = 30;
        public int WinLimitRounds = 16;
        public float RoundTimeMinutes = 2.5f;
        public int FreezeTimeSeconds = 5;
        public int BuyTimeSeconds = 45;
        public int StartingMoney = 800;

        public bool FriendlyFire;
        public bool AutoTeamBalance = true;
        public bool AllowFlashlight = true;
        public bool Footsteps = true;

        public bool IncludeBots = true;
        public int BotCount = 8;
        public BotDifficulty BotDifficulty = BotDifficulty.Normal;
        public bool BotPistols = true;
        public bool BotRifles = true;
        public bool BotSnipers = true;
        public bool BotGrenades = true;
        public bool BotArmor = true;
        public BotRadioMode BotRadio = BotRadioMode.Voice;
    }

    public sealed class ServerEntry
    {
        public string Name;
        public string Map;
        public string Address;
        public ushort Port;
        public int Players;
        public int MaxPlayers;
        public int Ping;
        public bool HasPassword;
        public bool IsLan;
        public bool IsFavorite;
        public bool IsHistory;
        public string[] ActivePlayers;
    }

    public sealed class KeybindEntry
    {
        public string ActionName;
        public string PrimaryKey;
        public string SecondaryKey;
    }

    [Serializable]
    public sealed class TacticalSettings
    {
        public float MouseSensitivity = 2.2f;
        public bool ReverseMouse;
        public bool MouseFilter;
        public bool RawInput = true;

        public int ResolutionIndex;
        public int AspectRatioIndex = 1; // 0: 4:3, 1: 16:9
        public bool Fullscreen = true;
        public float Brightness = 1.0f;
        public float Gamma = 2.2f;
        public int QualityIndex = 2;
        public bool VSync = true;
        public int MaxFps = 144;

        public float MasterVolume = 0.85f;
        public float SfxVolume = 0.9f;
        public float MusicVolume = 0.45f;
        public float MicVolume = 0.8f;
        public bool EnableVoice = true;

        public string PlayerName = "مقاتل الشام";
        public int SprayIndex;
        public int CrosshairSize = 1;  // 0 Auto, 1 Small, 2 Medium, 3 Large
        public int CrosshairColor;     // 0 Green, 1 Red, 2 Yellow, 3 Cyan
        public bool CrosshairTranslucent = true;
        public bool CrosshairDynamic = true;

        public bool RightHandWeapon = true;
        public bool AutoSwitchWeapon = true;
        public bool ShowCenterId = true;
        public bool NetGraph = true;

        public int SelectedWeaponIndex;
        public int SelectedSoldierSkin;

        const string PrefKey = "a3d.tactical.settings";
        static TacticalSettings _current;
        public static TacticalSettings Current => _current ??= Load();

        public static TacticalSettings Load()
        {
            string json = PlayerPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { return JsonUtility.FromJson<TacticalSettings>(json) ?? new TacticalSettings(); }
                catch (Exception) { }
            }
            return new TacticalSettings();
        }

        public void SaveAndApply()
        {
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
            AudioListener.volume = Mathf.Clamp01(MasterVolume);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : Mathf.Clamp(MaxFps, 30, 300);
        }

        public void ResetDefaults()
        {
            var def = new TacticalSettings();
            MouseSensitivity = def.MouseSensitivity;
            ReverseMouse = def.ReverseMouse;
            MouseFilter = def.MouseFilter;
            RawInput = def.RawInput;
            Brightness = def.Brightness;
            Gamma = def.Gamma;
            VSync = def.VSync;
            MaxFps = def.MaxFps;
            MasterVolume = def.MasterVolume;
            SfxVolume = def.SfxVolume;
            MusicVolume = def.MusicVolume;
            MicVolume = def.MicVolume;
            EnableVoice = def.EnableVoice;
            CrosshairSize = def.CrosshairSize;
            CrosshairColor = def.CrosshairColor;
            CrosshairTranslucent = def.CrosshairTranslucent;
            CrosshairDynamic = def.CrosshairDynamic;
            RightHandWeapon = def.RightHandWeapon;
            AutoSwitchWeapon = def.AutoSwitchWeapon;
            ShowCenterId = def.ShowCenterId;
            NetGraph = def.NetGraph;
            SaveAndApply();
        }

        public Color GetCrosshairColor() => CrosshairColor switch
        {
            1 => MenuStyle.Danger,
            2 => MenuStyle.Gold,
            3 => new Color32(0x38, 0xBD, 0xF8, 0xFF),
            _ => MenuStyle.Success,
        };
    }

    public sealed class WeaponSpec
    {
        public int Category; // 1..8 (CS 1.5 Buy categories)
        public string Icon;
        public string Name;
        public int Price;
        public int Damage;
        public int Accuracy;
        public int FireRate;
    }

    public static class TacticalCatalog
    {
        public static readonly string[] Maps = { "de_aleppo", "cs_damascus", "cs_palmyra" };

        public static string MapDescription(string map) => map switch
        {
            "de_aleppo" => "تفكيك القنبلة — قلعة حلب التاريخية",
            "cs_damascus" => "إنقاذ الرهائن — أحياء دمشق القديمة",
            "cs_palmyra" => "معركة الفرق — آثار تدمر الصحراوية",
            _ => map,
        };

        public static readonly WeaponSpec[] Weapons =
        {
            new WeaponSpec { Category = 1, Icon = "usp45", Name = "USP .45 التكتيكي", Price = 500, Damage = 34, Accuracy = 86, FireRate = 65 },
            new WeaponSpec { Category = 1, Icon = "glock18", Name = "Glock 18", Price = 400, Damage = 28, Accuracy = 78, FireRate = 75 },
            new WeaponSpec { Category = 1, Icon = "p228", Name = "P228", Price = 600, Damage = 32, Accuracy = 80, FireRate = 68 },
            new WeaponSpec { Category = 1, Icon = "deagle", Name = "مسدس ديزرت إيغل", Price = 650, Damage = 63, Accuracy = 82, FireRate = 45 },
            new WeaponSpec { Category = 1, Icon = "elites", Name = "Dual Berettas", Price = 800, Damage = 36, Accuracy = 72, FireRate = 84 },
            new WeaponSpec { Category = 1, Icon = "fiveseven", Name = "Five-SeveN", Price = 750, Damage = 30, Accuracy = 84, FireRate = 72 },
            new WeaponSpec { Category = 2, Icon = "m3", Name = "M3 Super 90", Price = 1700, Damage = 88, Accuracy = 55, FireRate = 32 },
            new WeaponSpec { Category = 2, Icon = "xm1014", Name = "XM1014", Price = 3000, Damage = 82, Accuracy = 58, FireRate = 55 },
            new WeaponSpec { Category = 3, Icon = "tmp", Name = "TMP", Price = 1250, Damage = 26, Accuracy = 74, FireRate = 92 },
            new WeaponSpec { Category = 3, Icon = "mac10", Name = "MAC-10", Price = 1400, Damage = 29, Accuracy = 68, FireRate = 95 },
            new WeaponSpec { Category = 3, Icon = "mp5", Name = "رشاش MP5", Price = 1500, Damage = 36, Accuracy = 80, FireRate = 88 },
            new WeaponSpec { Category = 3, Icon = "ump45", Name = "UMP .45", Price = 1700, Damage = 40, Accuracy = 78, FireRate = 76 },
            new WeaponSpec { Category = 3, Icon = "p90", Name = "P90 التكتيكي", Price = 2350, Damage = 32, Accuracy = 76, FireRate = 94 },
            new WeaponSpec { Category = 4, Icon = "ak47", Name = "كلاشنكوف AK-47", Price = 2700, Damage = 46, Accuracy = 74, FireRate = 78 },
            new WeaponSpec { Category = 4, Icon = "m4a1", Name = "M4A1 هجومية", Price = 3100, Damage = 42, Accuracy = 88, FireRate = 82 },
            new WeaponSpec { Category = 4, Icon = "sg552", Name = "SG 552 Commando", Price = 3500, Damage = 44, Accuracy = 85, FireRate = 80 },
            new WeaponSpec { Category = 4, Icon = "aug", Name = "Steyr AUG", Price = 3500, Damage = 43, Accuracy = 86, FireRate = 78 },
            new WeaponSpec { Category = 4, Icon = "scout", Name = "Steyr Scout", Price = 2750, Damage = 75, Accuracy = 95, FireRate = 40 },
            new WeaponSpec { Category = 4, Icon = "awp", Name = "قناصة AWP", Price = 4750, Damage = 100, Accuracy = 98, FireRate = 22 },
            new WeaponSpec { Category = 4, Icon = "g3sg1", Name = "G3SG1", Price = 5000, Damage = 80, Accuracy = 92, FireRate = 55 },
            new WeaponSpec { Category = 4, Icon = "sg550", Name = "SG 550", Price = 4200, Damage = 74, Accuracy = 93, FireRate = 58 },
            new WeaponSpec { Category = 5, Icon = "m249", Name = "M249 Para", Price = 5750, Damage = 45, Accuracy = 64, FireRate = 90 },
            new WeaponSpec { Category = 6, Icon = "ak47", Name = "ذخيرة أساسية كاملة", Price = 180, Damage = 0, Accuracy = 100, FireRate = 100 },
            new WeaponSpec { Category = 7, Icon = "usp45", Name = "ذخيرة ثانوية كاملة", Price = 80, Damage = 0, Accuracy = 100, FireRate = 100 },
            new WeaponSpec { Category = 8, Icon = "kevlar", Name = "سترة واقية (Kevlar)", Price = 650, Damage = 0, Accuracy = 100, FireRate = 100 },
            new WeaponSpec { Category = 8, Icon = "helmet", Name = "درع + خوذة", Price = 1000, Damage = 0, Accuracy = 100, FireRate = 100 },
            new WeaponSpec { Category = 8, Icon = "hegrenade", Name = "قنبلة يدوية", Price = 300, Damage = 92, Accuracy = 70, FireRate = 50 },
            new WeaponSpec { Category = 8, Icon = "flashbang", Name = "قنبلة ضوئية", Price = 200, Damage = 0, Accuracy = 85, FireRate = 60 },
            new WeaponSpec { Category = 8, Icon = "smoke", Name = "قنبلة دخانية", Price = 200, Damage = 0, Accuracy = 85, FireRate = 60 },
            new WeaponSpec { Category = 8, Icon = "defuser", Name = "طقم تفكيك", Price = 400, Damage = 0, Accuracy = 100, FireRate = 100 },
        };

        public static List<KeybindEntry> DefaultKeybinds() => new List<KeybindEntry>
        {
            new KeybindEntry { ActionName = "الحركة للأمام", PrimaryKey = "W", SecondaryKey = "UpArrow" },
            new KeybindEntry { ActionName = "الحركة للخلف", PrimaryKey = "S", SecondaryKey = "DownArrow" },
            new KeybindEntry { ActionName = "الحركة لليسار", PrimaryKey = "A", SecondaryKey = "LeftArrow" },
            new KeybindEntry { ActionName = "الحركة لليمين", PrimaryKey = "D", SecondaryKey = "RightArrow" },
            new KeybindEntry { ActionName = "القفز", PrimaryKey = "Space", SecondaryKey = "-" },
            new KeybindEntry { ActionName = "الركض", PrimaryKey = "LeftShift", SecondaryKey = "-" },
            new KeybindEntry { ActionName = "الانحناء", PrimaryKey = "LeftCtrl", SecondaryKey = "C" },
            new KeybindEntry { ActionName = "إطلاق النار", PrimaryKey = "Mouse0", SecondaryKey = "-" },
            new KeybindEntry { ActionName = "إعادة التلقيم", PrimaryKey = "R", SecondaryKey = "-" },
            new KeybindEntry { ActionName = "قائمة الشراء", PrimaryKey = "B", SecondaryKey = "-" },
            new KeybindEntry { ActionName = "اختيار الفريق", PrimaryKey = "M", SecondaryKey = "-" },
            new KeybindEntry { ActionName = "لوحة النتائج", PrimaryKey = "Tab", SecondaryKey = "-" },
            new KeybindEntry { ActionName = "الدردشة", PrimaryKey = "Y", SecondaryKey = "U" },
            new KeybindEntry { ActionName = "الراديو", PrimaryKey = "Z", SecondaryKey = "X" },
            new KeybindEntry { ActionName = "وحدة الأوامر", PrimaryKey = "BackQuote", SecondaryKey = "-" },
        };

        public static List<ServerEntry> SampleServers() => new List<ServerEntry>
        {
            new ServerEntry { Name = "خادم حماة الشام الرسمي #1", Map = "de_aleppo", Address = "127.0.0.1", Port = 7777, Players = 14, MaxPlayers = 32, Ping = 24, IsLan = true, IsFavorite = true, ActivePlayers = new[] { "أبو عمر — 18 قتل", "النسر_السوري — 12 قتل", "صقر_حلب — 9 قتل" } },
            new ServerEntry { Name = "سيرفر دمشق التكتيكي (LAN)", Map = "cs_damascus", Address = "192.168.1.10", Port = 7777, Players = 10, MaxPlayers = 16, Ping = 12, IsLan = true, IsHistory = true, ActivePlayers = new[] { "فارس_الشام — 14 قتل", "رامي — 8 قتل" } },
            new ServerEntry { Name = "بطولة تدمر للفرق #2", Map = "cs_palmyra", Address = "185.10.22.40", Port = 7778, Players = 28, MaxPlayers = 32, Ping = 48, HasPassword = true, IsFavorite = true, ActivePlayers = new[] { "قائد_الفرقة — 22 قتل", "شبح_تدمر — 19 قتل" } },
            new ServerEntry { Name = "تدريب المحترفين 24/7", Map = "de_aleppo", Address = "185.10.22.41", Port = 7779, Players = 6, MaxPlayers = 20, Ping = 72, IsHistory = true, ActivePlayers = new[] { "خالد — 5 قتل" } },
        };
    }
}
