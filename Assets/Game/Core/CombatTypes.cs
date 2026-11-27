using System;
using UnityEngine;

namespace Adventure3D.Core
{
    public enum Team : byte
    {
        /// <summary>حماة الشام</summary>
        Defenders = 0,
        /// <summary>الفريق المقابل</summary>
        Attackers = 1,
    }

    [Flags]
    public enum CombatFlags : byte
    {
        None = 0,
        Alive = 1 << 0,
        Reloading = 1 << 1,
        Aiming = 1 << 2,
        Switching = 1 << 3,
        Helmet = 1 << 4,
        Bot = 1 << 5,
    }

    /// <summary>Weapon slots carried by every player.</summary>
    public enum WeaponSlot : byte { Primary = 0, Secondary = 1, Knife = 2 }

    /// <summary>
    /// Per-player combat state. Owned by the authority (offline, host, dedicated server) and copied to
    /// clients in snapshots. Weapon numbers are indices into the gameplay WeaponCatalog; 255 = empty slot.
    /// </summary>
    [Serializable]
    public struct PlayerCombatState
    {
        public const byte NoWeapon = 255;

        public byte Health;
        public byte Armor;
        public Team Team;
        public CombatFlags Flags;
        public WeaponSlot ActiveSlot;
        public byte PrimaryWeapon;
        public byte SecondaryWeapon;
        public byte KnifeWeapon;
        public byte PrimaryClip;
        public ushort PrimaryReserve;
        public byte SecondaryClip;
        public ushort SecondaryReserve;
        public ushort Kills;
        public ushort Deaths;
        public int Money;

        // Timers (seconds). Not all are replicated; clients only need the flags.
        public float FireCooldown;
        public float ReloadTimer;
        public float SwitchTimer;
        public float RespawnTimer;
        /// <summary>Seconds of spawn protection left (no damage taken).</summary>
        public float ProtectedTimer;
        /// <summary>Accumulated recoil spread in degrees, recovers over time.</summary>
        public float Recoil;
        /// <summary>Counts shots, seeds deterministic spread.</summary>
        public uint ShotCounter;
        public bool TriggerWasDown;
        public ushort LastAttacker;

        public bool Alive => (Flags & CombatFlags.Alive) != 0;
        public bool Reloading => (Flags & CombatFlags.Reloading) != 0;
        public bool Aiming => (Flags & CombatFlags.Aiming) != 0;
        public bool IsBot => (Flags & CombatFlags.Bot) != 0;

        public void SetFlag(CombatFlags flag, bool on)
        {
            if (on) Flags |= flag; else Flags &= ~flag;
        }

        public byte ActiveWeapon => ActiveSlot switch
        {
            WeaponSlot.Primary => PrimaryWeapon,
            WeaponSlot.Secondary => SecondaryWeapon,
            _ => KnifeWeapon,
        };

        public int ActiveClip => ActiveSlot switch
        {
            WeaponSlot.Primary => PrimaryClip,
            WeaponSlot.Secondary => SecondaryClip,
            _ => 0,
        };

        public int ActiveReserve => ActiveSlot switch
        {
            WeaponSlot.Primary => PrimaryReserve,
            WeaponSlot.Secondary => SecondaryReserve,
            _ => 0,
        };
    }

    public enum CombatEventType : byte
    {
        /// <summary>A weapon was fired (tracer from Origin to Point).</summary>
        Shot = 1,
        /// <summary>A player took damage.</summary>
        Damage = 2,
        /// <summary>A player died.</summary>
        Kill = 3,
        Respawn = 4,
        ReloadStart = 5,
        /// <summary>Weapon bought / given.</summary>
        Equip = 6,
        /// <summary>Match clock ran out: scores reset, everyone respawns.</summary>
        RoundRestart = 7,
        DryFire = 8,
    }

    /// <summary>Something that happened in combat, for HUD, effects and sounds (replicated to clients).</summary>
    public struct CombatEvent
    {
        public CombatEventType Type;
        public ushort Actor;   // shooter / killer
        public ushort Target;  // victim (0 = world)
        public byte Weapon;
        public Vector3 Origin;
        public Vector3 Point;
        public Vector3 Normal;
        public short Amount;   // damage
        public bool Headshot;
        public bool HitPlayer;
    }
}
