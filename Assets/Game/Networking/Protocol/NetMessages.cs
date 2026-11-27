using System.Collections.Generic;
using System.Text;
using Adventure3D.Core;
using Unity.Collections;
using UnityEngine;

namespace Adventure3D.Networking
{
    public enum MsgType : byte
    {
        Hello = 1,      // client -> server, reliable
        Welcome = 2,    // server -> client, reliable
        Reject = 3,     // server -> client, reliable, followed by disconnect
        Input = 4,      // client -> server, unreliable, redundant last N commands
        Snapshot = 5,   // server -> client, unreliable, full authoritative state
        Shutdown = 6,   // server -> client, reliable, host is closing
        Events = 7,     // server -> client, reliable, combat events (shots, damage, kills, respawns)
        Roster = 8,     // server -> client, reliable, player names / bots
        Buy = 9,        // client -> server, reliable, buy request (server validates money)
    }

    public enum RejectReason : byte
    {
        IncompatibleVersion = 1,
        ServerFull = 2,
        ReplacedByNewConnection = 3,
    }

    public struct HelloMsg
    {
        public ushort ProtocolVersion;
        public string ProfileId;
        public string DisplayName;
        /// <summary>Empty for guests. A future account service token is validated server-side here.</summary>
        public string AuthToken;
    }

    public struct WelcomeMsg
    {
        public ushort ProtocolVersion;
        public ushort PlayerId;
        public ushort TickRate;
        public uint ServerTick;
    }

    public struct RejectMsg
    {
        public RejectReason Reason;
        public ushort ServerProtocolVersion;
    }

    public struct SnapshotEntry
    {
        public ushort PlayerId;
        /// <summary>Last command sequence of this player the server has simulated.</summary>
        public uint AckSequence;
        public PlayerSimState State;
        public PlayerCombatState Combat;
    }

    public struct RosterEntry
    {
        public ushort PlayerId;
        public string Name;
        public bool IsBot;
    }

    /// <summary>Wire format. Little-endian, hand written so every byte is accounted for.</summary>
    public static class NetMessages
    {
        public const int MaxStringBytes = 200;

        public static void WriteHello(ref DataStreamWriter w, in HelloMsg m)
        {
            w.WriteByte((byte)MsgType.Hello);
            w.WriteUShort(m.ProtocolVersion);
            WriteString(ref w, m.ProfileId);
            WriteString(ref w, m.DisplayName);
            WriteString(ref w, m.AuthToken);
        }

        public static HelloMsg ReadHello(ref DataStreamReader r) => new HelloMsg
        {
            ProtocolVersion = r.ReadUShort(),
            ProfileId = ReadString(ref r),
            DisplayName = ReadString(ref r),
            AuthToken = ReadString(ref r),
        };

        public static void WriteWelcome(ref DataStreamWriter w, in WelcomeMsg m)
        {
            w.WriteByte((byte)MsgType.Welcome);
            w.WriteUShort(m.ProtocolVersion);
            w.WriteUShort(m.PlayerId);
            w.WriteUShort(m.TickRate);
            w.WriteUInt(m.ServerTick);
        }

        public static WelcomeMsg ReadWelcome(ref DataStreamReader r) => new WelcomeMsg
        {
            ProtocolVersion = r.ReadUShort(),
            PlayerId = r.ReadUShort(),
            TickRate = r.ReadUShort(),
            ServerTick = r.ReadUInt(),
        };

        public static void WriteReject(ref DataStreamWriter w, in RejectMsg m)
        {
            w.WriteByte((byte)MsgType.Reject);
            w.WriteByte((byte)m.Reason);
            w.WriteUShort(m.ServerProtocolVersion);
        }

        public static RejectMsg ReadReject(ref DataStreamReader r) => new RejectMsg
        {
            Reason = (RejectReason)r.ReadByte(),
            ServerProtocolVersion = r.ReadUShort(),
        };

        public static void WriteShutdown(ref DataStreamWriter w) => w.WriteByte((byte)MsgType.Shutdown);

        public static void WriteInput(ref DataStreamWriter w, IReadOnlyList<PlayerCommand> commands, int start, int count)
        {
            w.WriteByte((byte)MsgType.Input);
            w.WriteByte((byte)count);
            for (int i = start; i < start + count; i++)
            {
                var c = commands[i];
                w.WriteUInt(c.Sequence);
                w.WriteFloat(c.Move.x);
                w.WriteFloat(c.Move.y);
                w.WriteFloat(c.Yaw);
                w.WriteFloat(c.Pitch);
                w.WriteByte((byte)c.Buttons);
                w.WriteByte(c.SlotRequest);
            }
        }

        public static int ReadInput(ref DataStreamReader r, List<PlayerCommand> into)
        {
            into.Clear();
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var c = new PlayerCommand
                {
                    Sequence = r.ReadUInt(),
                    Move = new Vector2(r.ReadFloat(), r.ReadFloat()),
                    Yaw = r.ReadFloat(),
                    Pitch = r.ReadFloat(),
                    Buttons = (CommandButtons)r.ReadByte(),
                    SlotRequest = r.ReadByte(),
                };
                if (r.HasFailedReads) break;
                into.Add(c);
            }
            return into.Count;
        }

        public static void WriteSnapshot(ref DataStreamWriter w, uint serverTick, uint roundStartTick, IReadOnlyList<SnapshotEntry> entries)
        {
            w.WriteByte((byte)MsgType.Snapshot);
            w.WriteUInt(serverTick);
            w.WriteUInt(roundStartTick);
            w.WriteByte((byte)entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var s = e.State;
                w.WriteUShort(e.PlayerId);
                w.WriteUInt(e.AckSequence);
                w.WriteFloat(s.Position.x); w.WriteFloat(s.Position.y); w.WriteFloat(s.Position.z);
                w.WriteFloat(s.Velocity.x); w.WriteFloat(s.Velocity.y); w.WriteFloat(s.Velocity.z);
                w.WriteFloat(s.Yaw);
                w.WriteFloat(s.Pitch);
                w.WriteByte((byte)s.Flags);
                w.WriteByte((byte)s.Events);
                w.WriteFloat(s.CoyoteTimer);
                w.WriteFloat(s.JumpBufferTimer);
                w.WriteFloat(s.AirTime);
                w.WriteFloat(s.LandingSpeed);
                WriteCombat(ref w, e.Combat);
            }
        }

        public static uint ReadSnapshot(ref DataStreamReader r, List<SnapshotEntry> into, out uint roundStartTick)
        {
            into.Clear();
            uint tick = r.ReadUInt();
            roundStartTick = r.ReadUInt();
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var e = new SnapshotEntry { PlayerId = r.ReadUShort(), AckSequence = r.ReadUInt() };
                var s = new PlayerSimState
                {
                    Position = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat()),
                    Velocity = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat()),
                    Yaw = r.ReadFloat(),
                    Pitch = r.ReadFloat(),
                    Flags = (MotorFlags)r.ReadByte(),
                    Events = (MotorEvents)r.ReadByte(),
                    CoyoteTimer = r.ReadFloat(),
                    JumpBufferTimer = r.ReadFloat(),
                    AirTime = r.ReadFloat(),
                    LandingSpeed = r.ReadFloat(),
                };
                e.Combat = ReadCombat(ref r);
                if (r.HasFailedReads) break;
                e.State = s;
                into.Add(e);
            }
            return tick;
        }

        static void WriteCombat(ref DataStreamWriter w, in PlayerCombatState c)
        {
            w.WriteByte(c.Health);
            w.WriteByte(c.Armor);
            w.WriteByte((byte)c.Team);
            w.WriteByte((byte)c.Flags);
            w.WriteByte((byte)c.ActiveSlot);
            w.WriteByte(c.PrimaryWeapon);
            w.WriteByte(c.SecondaryWeapon);
            w.WriteByte(c.KnifeWeapon);
            w.WriteByte(c.PrimaryClip);
            w.WriteUShort(c.PrimaryReserve);
            w.WriteByte(c.SecondaryClip);
            w.WriteUShort(c.SecondaryReserve);
            w.WriteUShort(c.Kills);
            w.WriteUShort(c.Deaths);
            w.WriteInt(c.Money);
            w.WriteFloat(c.RespawnTimer);
            w.WriteUShort(c.LastAttacker);
        }

        static PlayerCombatState ReadCombat(ref DataStreamReader r) => new PlayerCombatState
        {
            Health = r.ReadByte(),
            Armor = r.ReadByte(),
            Team = (Team)r.ReadByte(),
            Flags = (CombatFlags)r.ReadByte(),
            ActiveSlot = (WeaponSlot)r.ReadByte(),
            PrimaryWeapon = r.ReadByte(),
            SecondaryWeapon = r.ReadByte(),
            KnifeWeapon = r.ReadByte(),
            PrimaryClip = r.ReadByte(),
            PrimaryReserve = r.ReadUShort(),
            SecondaryClip = r.ReadByte(),
            SecondaryReserve = r.ReadUShort(),
            Kills = r.ReadUShort(),
            Deaths = r.ReadUShort(),
            Money = r.ReadInt(),
            RespawnTimer = r.ReadFloat(),
            LastAttacker = r.ReadUShort(),
        };

        public const int MaxEventsPerMessage = 24;

        public static void WriteEvents(ref DataStreamWriter w, IReadOnlyList<CombatEvent> events, int start, int count)
        {
            w.WriteByte((byte)MsgType.Events);
            w.WriteByte((byte)count);
            for (int i = start; i < start + count; i++)
            {
                var e = events[i];
                w.WriteByte((byte)e.Type);
                w.WriteUShort(e.Actor);
                w.WriteUShort(e.Target);
                w.WriteByte(e.Weapon);
                WriteV3(ref w, e.Origin);
                WriteV3(ref w, e.Point);
                WriteV3(ref w, e.Normal);
                w.WriteShort(e.Amount);
                w.WriteByte((byte)((e.Headshot ? 1 : 0) | (e.HitPlayer ? 2 : 0)));
            }
        }

        public static void ReadEvents(ref DataStreamReader r, List<CombatEvent> into)
        {
            into.Clear();
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var e = new CombatEvent
                {
                    Type = (CombatEventType)r.ReadByte(),
                    Actor = r.ReadUShort(),
                    Target = r.ReadUShort(),
                    Weapon = r.ReadByte(),
                    Origin = ReadV3(ref r),
                    Point = ReadV3(ref r),
                    Normal = ReadV3(ref r),
                    Amount = r.ReadShort(),
                };
                byte f = r.ReadByte();
                e.Headshot = (f & 1) != 0;
                e.HitPlayer = (f & 2) != 0;
                if (r.HasFailedReads) break;
                into.Add(e);
            }
        }

        public static void WriteRoster(ref DataStreamWriter w, IReadOnlyList<RosterEntry> roster)
        {
            w.WriteByte((byte)MsgType.Roster);
            w.WriteByte((byte)roster.Count);
            foreach (var e in roster)
            {
                w.WriteUShort(e.PlayerId);
                w.WriteByte((byte)(e.IsBot ? 1 : 0));
                WriteString(ref w, e.Name);
            }
        }

        public static void ReadRoster(ref DataStreamReader r, List<RosterEntry> into)
        {
            into.Clear();
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var e = new RosterEntry { PlayerId = r.ReadUShort(), IsBot = r.ReadByte() != 0, Name = ReadString(ref r) };
                if (r.HasFailedReads) break;
                into.Add(e);
            }
        }

        public static void WriteBuy(ref DataStreamWriter w, string item)
        {
            w.WriteByte((byte)MsgType.Buy);
            WriteString(ref w, item);
        }

        static void WriteV3(ref DataStreamWriter w, Vector3 v) { w.WriteFloat(v.x); w.WriteFloat(v.y); w.WriteFloat(v.z); }
        static Vector3 ReadV3(ref DataStreamReader r) => new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());

        public static void WriteString(ref DataStreamWriter w, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? "");
            int len = Mathf.Min(bytes.Length, MaxStringBytes);
            w.WriteByte((byte)len);
            for (int i = 0; i < len; i++) w.WriteByte(bytes[i]);
        }

        public static string ReadString(ref DataStreamReader r)
        {
            int len = r.ReadByte();
            if (len == 0) return "";
            var bytes = new byte[len];
            for (int i = 0; i < len; i++) bytes[i] = r.ReadByte();
            return r.HasFailedReads ? "" : Encoding.UTF8.GetString(bytes);
        }
    }
}
