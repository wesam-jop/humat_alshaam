using System.Collections.Generic;
using Adventure3D.Core;
using UnityEngine;

namespace Adventure3D.Gameplay
{
    /// <summary>
    /// Authoritative combat rules: firing, hitscan, damage, armor, death, respawn, reload, weapon switch,
    /// buying. Runs only where the session is the authority (offline, LAN host, dedicated server); clients
    /// receive the resulting state and events. Spread is deterministic (seeded by player and shot number).
    /// </summary>
    /// <summary>Body part hit, CS damage multipliers: head x HeadshotMultiplier, chest x1, stomach x1.25, legs x0.75.</summary>
    public enum HitZone : byte { Chest, Head, Stomach, Legs }

    public static class CombatRules
    {
        /// <summary>Spread multiplier while crouched on the ground (set from MovementProfile by the session).</summary>
        public static float CrouchSpreadMultiplier = 0.7f;
        /// <summary>Distance unit of the range modifier: 500 CS units = 12.7 m.</summary>
        public const float RangeUnit = 12.7f;
        public const float RespawnDelay = 3f;
        public const int StartMoney = 4500;
        public const int MaxMoney = 16000;
        public const float HeadHeight = 1.45f;
        public const float SpawnProtection = 1.5f;
        public const float FallOutHeight = -20f;

        /// <summary>A free spawn position in the team's area (falls back to the scene spawn points).</summary>
        public static Pose TeamSpawnPose(Team team, WorldSimulation world)
        {
            var list = team == Team.Defenders ? TestArenaLayout.DefenderSpawns : TestArenaLayout.AttackerSpawns;
            float yaw = team == Team.Defenders ? 0f : 180f;
            for (int attempt = 0; attempt < list.Length; attempt++)
            {
                var p = list[(world.TeamSpawnCursor++) % list.Length];
                bool free = true;
                foreach (var other in world.Players)
                    if (other.Alive && (other.State.Position - p).sqrMagnitude < 1.5f) { free = false; break; }
                if (free) return new Pose(p, Quaternion.Euler(0f, yaw, 0f));
            }
            return new Pose(list[0], Quaternion.Euler(0f, yaw, 0f));
        }

        public static void MoveToSpawn(PlayerAvatar a, WorldSimulation world)
        {
            var pose = TeamSpawnPose(a.Combat.Team, world);
            a.SetState(new PlayerSimState { Position = pose.position, Yaw = pose.rotation.eulerAngles.y }, true);
            PlayerMotor.RefreshGround(a.Controller, ref a.State, world.Motor);
        }

        static readonly RaycastHit[] Hits = new RaycastHit[32];
        static readonly HitComparer Comparer = new HitComparer();

        sealed class HitComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        // ------------------------------------------------------------------ setup

        public static void GiveDefaultLoadout(ref PlayerCombatState c)
        {
            bool defenders = c.Team == Team.Defenders;
            SetWeapon(ref c, WeaponCatalog.IndexOf(defenders ? "m4a1" : "ak47"));
            SetWeapon(ref c, WeaponCatalog.IndexOf(defenders ? "usp45" : "glock18"));
            c.KnifeWeapon = WeaponCatalog.IndexOf("knife");
            c.ActiveSlot = WeaponSlot.Primary;
        }

        /// <summary>Puts a weapon in its slot with full ammo.</summary>
        public static void SetWeapon(ref PlayerCombatState c, byte index)
        {
            var w = WeaponCatalog.Get(index);
            if (w.IsKnife) { c.KnifeWeapon = index; return; }
            if (WeaponCatalog.IsPrimary(w))
            {
                c.PrimaryWeapon = index;
                c.PrimaryClip = (byte)w.ClipSize;
                c.PrimaryReserve = (ushort)w.ReserveMax;
            }
            else
            {
                c.SecondaryWeapon = index;
                c.SecondaryClip = (byte)w.ClipSize;
                c.SecondaryReserve = (ushort)w.ReserveMax;
            }
        }

        public static void InitPlayer(PlayerAvatar a, Team team, bool bot, WorldSimulation world = null)
        {
            var c = new PlayerCombatState
            {
                Team = team,
                PrimaryWeapon = PlayerCombatState.NoWeapon,
                SecondaryWeapon = PlayerCombatState.NoWeapon,
                Money = StartMoney,
            };
            c.SetFlag(CombatFlags.Bot, bot);
            GiveDefaultLoadout(ref c);
            a.Combat = c;
            if (world != null) MoveToSpawn(a, world);
            Revive(a);
        }

        static void Revive(PlayerAvatar a)
        {
            ref var c = ref a.Combat;
            c.Health = 100;
            c.Armor = 100;
            c.SetFlag(CombatFlags.Helmet, true);
            c.FireCooldown = 0f;
            c.ReloadTimer = 0f;
            c.SwitchTimer = 0f;
            c.Recoil = 0f;
            c.RespawnTimer = 0f;
            c.ProtectedTimer = SpawnProtection;
            c.SetFlag(CombatFlags.Reloading, false);
            // Refill ammo of the weapons the player carries (deathmatch respawn).
            if (c.PrimaryWeapon != PlayerCombatState.NoWeapon) SetWeapon(ref c, c.PrimaryWeapon);
            if (c.SecondaryWeapon != PlayerCombatState.NoWeapon) SetWeapon(ref c, c.SecondaryWeapon);
            if (c.PrimaryWeapon == PlayerCombatState.NoWeapon) c.ActiveSlot = WeaponSlot.Secondary;
            a.SetAlive(true);
        }

        // ------------------------------------------------------------------ per tick

        /// <summary>One combat tick for one player, after movement.</summary>
        public static void Tick(PlayerAvatar a, PlayerCommand cmd, WorldSimulation world, float dt, float eyeHeight, List<CombatEvent> events)
        {
            ref var c = ref a.Combat;

            if (!c.Alive)
            {
                c.RespawnTimer -= dt;
                if (c.RespawnTimer <= 0f)
                {
                    MoveToSpawn(a, world);
                    Revive(a);
                    events.Add(new CombatEvent { Type = CombatEventType.Respawn, Actor = a.PlayerId, Point = a.State.Position });
                }
                return;
            }

            // Fell out of the world: counts as a death, respawn at the team spawn.
            if (a.State.Position.y < FallOutHeight)
            {
                Kill(a, a, c.ActiveWeapon, false, events);
                return;
            }

            c.FireCooldown = Mathf.Max(0f, c.FireCooldown - dt);
            c.ProtectedTimer = Mathf.Max(0f, c.ProtectedTimer - dt);
            c.SwitchTimer = Mathf.Max(0f, c.SwitchTimer - dt);
            c.SetFlag(CombatFlags.Switching, c.SwitchTimer > 0f);

            var weapon = WeaponCatalog.Get(c.ActiveWeapon);
            bool triggerDown = cmd.Fire;
            bool triggerPressed = triggerDown && !c.TriggerWasDown;
            c.TriggerWasDown = triggerDown;
            if (!triggerDown) c.Recoil = Mathf.Max(0f, c.Recoil - weapon.RecoilRecovery * dt);

            // ---- weapon switch
            WeaponSlot? wanted = null;
            if (cmd.SlotRequest >= 1 && cmd.SlotRequest <= 3) wanted = (WeaponSlot)(cmd.SlotRequest - 1);
            else if (cmd.SwitchWeapon) wanted = NextSlot(c);
            if (wanted.HasValue && wanted.Value != c.ActiveSlot && HasWeapon(c, wanted.Value))
            {
                c.ActiveSlot = wanted.Value;
                c.ReloadTimer = 0f;
                c.SetFlag(CombatFlags.Reloading, false);
                weapon = WeaponCatalog.Get(c.ActiveWeapon);
                c.SwitchTimer = weapon.DrawTime;
                c.Recoil = 0f;
                c.SetFlag(CombatFlags.Switching, true);
                return;
            }

            c.SetFlag(CombatFlags.Aiming, cmd.Aim && !c.Reloading && !weapon.IsKnife && c.SwitchTimer <= 0f);

            // ---- reload
            if (c.Reloading)
            {
                c.ReloadTimer -= dt;
                if (c.ReloadTimer <= 0f) FinishReload(ref c, weapon);
                return;
            }
            bool clipEmpty = weapon.UsesAmmo && c.ActiveClip == 0;
            if (weapon.UsesAmmo && c.ActiveClip < weapon.ClipSize && c.ActiveReserve > 0 && (cmd.Reload || (clipEmpty && triggerDown)))
            {
                c.ReloadTimer = weapon.ReloadTime;
                c.SetFlag(CombatFlags.Reloading, true);
                c.SetFlag(CombatFlags.Aiming, false);
                events.Add(new CombatEvent { Type = CombatEventType.ReloadStart, Actor = a.PlayerId, Weapon = c.ActiveWeapon });
                return;
            }

            // ---- fire
            if (!triggerDown || c.SwitchTimer > 0f || c.FireCooldown > 0f) return;
            if (!weapon.Automatic && !triggerPressed) return;
            if (clipEmpty)
            {
                if (triggerPressed) events.Add(new CombatEvent { Type = CombatEventType.DryFire, Actor = a.PlayerId, Weapon = c.ActiveWeapon });
                return;
            }

            if (weapon.UsesAmmo)
            {
                if (c.ActiveSlot == WeaponSlot.Primary) c.PrimaryClip--;
                else c.SecondaryClip--;
            }
            c.FireCooldown = weapon.FireInterval;
            c.ShotCounter++;

            Vector3 eye = a.State.Position + Vector3.up * a.SimEyeHeight;
            float spread = CurrentSpread(a, weapon);
            for (int p = 0; p < weapon.Pellets; p++)
            {
                Vector3 dir = SpreadDirection(a.State.Yaw, a.State.Pitch, spread, a.PlayerId, c.ShotCounter, p);
                FireRay(a, weapon, c.ActiveWeapon, eye, dir, world, events);
            }
            c.Recoil = Mathf.Min(weapon.MaxRecoil, c.Recoil + weapon.RecoilPerShot);
        }

        public static float CurrentSpread(PlayerAvatar a, WeaponDefinition w)
        {
            var c = a.Combat;
            float s = w.BaseSpread + c.Recoil;
            float speed01 = Mathf.Clamp01(a.State.HorizontalSpeed / 7f);
            s += w.MoveSpread * speed01;
            if (!a.State.Grounded) s += w.AirSpread;
            if (c.Aiming) s *= w.AimSpreadMultiplier;
            if (a.State.Crouching && a.State.Grounded) s *= CrouchSpreadMultiplier;
            return s;
        }

        static Vector3 SpreadDirection(float yaw, float pitch, float spreadDeg, ushort player, uint shot, int pellet)
        {
            uint h = Hash((uint)player * 73856093u ^ shot * 19349663u ^ (uint)pellet * 83492791u);
            float u = (h & 0xFFFF) / 65535f;
            float v = ((h >> 16) & 0xFFFF) / 65535f;
            float r = Mathf.Sqrt(u) * spreadDeg;          // uniform in the cone
            float ang = v * Mathf.PI * 2f;
            var rot = Quaternion.Euler(pitch + Mathf.Sin(ang) * r, yaw + Mathf.Cos(ang) * r, 0f);
            return rot * Vector3.forward;
        }

        static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
            return x;
        }

        static void FireRay(PlayerAvatar shooter, WeaponDefinition w, byte weaponIndex, Vector3 origin, Vector3 dir, WorldSimulation world, List<CombatEvent> events)
        {
            int count = Physics.RaycastNonAlloc(origin, dir, Hits, w.Range, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(Hits, 0, count, Comparer);
            var shot = new CombatEvent
            {
                Type = CombatEventType.Shot,
                Actor = shooter.PlayerId,
                Weapon = weaponIndex,
                Origin = origin,
                Point = origin + dir * w.Range,
            };
            for (int i = 0; i < count; i++)
            {
                var h = Hits[i];
                if (h.collider == shooter.Controller) continue;
                shot.Point = h.point;
                shot.Normal = h.normal;
                var victim = h.collider.GetComponent<PlayerAvatar>();
                if (victim != null && victim.Alive)
                {
                    shot.HitPlayer = true;
                    shot.Target = victim.PlayerId;
                    var zone = ZoneOf(victim, h.point);
                    shot.Headshot = zone == HitZone.Head;
                    if (victim.Combat.Team != shooter.Combat.Team)
                        ApplyDamage(victim, shooter, w, weaponIndex, zone, h.distance, events);
                }
                break;
            }
            events.Add(shot);
        }

        /// <summary>Body part from the hit height on the victim's current (standing or crouched) capsule.</summary>
        public static HitZone ZoneOf(PlayerAvatar victim, Vector3 point)
        {
            float h = victim.Controller.height;
            float y = point.y - victim.State.Position.y;
            if (y >= h - 0.33f * (h / 1.8f)) return HitZone.Head;
            if (y < 0.45f * h) return HitZone.Legs;
            if (y < 0.6f * h) return HitZone.Stomach;
            return HitZone.Chest;
        }

        public static float ZoneMultiplier(WeaponDefinition w, HitZone zone) => zone switch
        {
            HitZone.Head => w.HeadshotMultiplier,
            HitZone.Stomach => 1.25f,
            HitZone.Legs => 0.75f,
            _ => 1f,
        };

        /// <summary>Damage before armor: base x body part x range falloff.</summary>
        public static float RawDamage(WeaponDefinition w, HitZone zone, float distance) =>
            w.Damage * ZoneMultiplier(w, zone) * Mathf.Pow(w.RangeModifier, Mathf.Max(0f, distance) / RangeUnit);

        /// <summary>Compatibility overload: chest or head at point blank.</summary>
        public static void ApplyDamage(PlayerAvatar victim, PlayerAvatar attacker, WeaponDefinition w, byte weaponIndex, bool head, List<CombatEvent> events) =>
            ApplyDamage(victim, attacker, w, weaponIndex, head ? HitZone.Head : HitZone.Chest, 0f, events);

        public static void ApplyDamage(PlayerAvatar victim, PlayerAvatar attacker, WeaponDefinition w, byte weaponIndex, HitZone zone, float distance, List<CombatEvent> events)
        {
            ref var v = ref victim.Combat;
            if (v.ProtectedTimer > 0f) return; // spawn protection
            bool head = zone == HitZone.Head;
            float dmg = RawDamage(w, zone, distance);
            // Kevlar covers chest and stomach, the helmet covers the head, legs are never armored.
            bool armorApplies = v.Armor > 0 && zone != HitZone.Legs && (!head || (v.Flags & CombatFlags.Helmet) != 0);
            if (armorApplies)
            {
                float toHealth = dmg * w.ArmorPenetration;
                float toArmor = (dmg - toHealth) * 0.5f;
                v.Armor = (byte)Mathf.Max(0, v.Armor - Mathf.CeilToInt(toArmor));
                dmg = toHealth;
            }
            int damage = Mathf.Max(1, Mathf.RoundToInt(dmg));
            int hp = v.Health - damage;
            v.LastAttacker = attacker.PlayerId;
            events.Add(new CombatEvent
            {
                Type = CombatEventType.Damage, Actor = attacker.PlayerId, Target = victim.PlayerId, Weapon = weaponIndex,
                Origin = attacker.State.Position, Point = victim.State.Position, Amount = (short)damage, Headshot = head,
            });
            if (hp > 0)
            {
                v.Health = (byte)hp;
                return;
            }

            Kill(victim, attacker, weaponIndex, head, events);
        }

        static void Kill(PlayerAvatar victim, PlayerAvatar attacker, byte weaponIndex, bool head, List<CombatEvent> events)
        {
            ref var v = ref victim.Combat;
            v.Health = 0;
            v.Deaths++;
            v.RespawnTimer = RespawnDelay;
            v.SetFlag(CombatFlags.Reloading, false);
            v.SetFlag(CombatFlags.Aiming, false);
            victim.SetAlive(false);
            ref var k = ref attacker.Combat;
            if (attacker != victim)
            {
                k.Kills++;
                k.Money = Mathf.Min(MaxMoney, k.Money + WeaponCatalog.Get(weaponIndex).KillReward);
            }
            events.Add(new CombatEvent
            {
                Type = CombatEventType.Kill, Actor = attacker.PlayerId, Target = victim.PlayerId, Weapon = weaponIndex,
                Point = victim.State.Position, Headshot = head,
            });
        }

        static void FinishReload(ref PlayerCombatState c, WeaponDefinition w)
        {
            c.ReloadTimer = 0f;
            c.SetFlag(CombatFlags.Reloading, false);
            if (c.ActiveSlot == WeaponSlot.Primary)
            {
                int take = Mathf.Min(w.ClipSize - c.PrimaryClip, c.PrimaryReserve);
                c.PrimaryClip = (byte)(c.PrimaryClip + take);
                c.PrimaryReserve = (ushort)(c.PrimaryReserve - take);
            }
            else if (c.ActiveSlot == WeaponSlot.Secondary)
            {
                int take = Mathf.Min(w.ClipSize - c.SecondaryClip, c.SecondaryReserve);
                c.SecondaryClip = (byte)(c.SecondaryClip + take);
                c.SecondaryReserve = (ushort)(c.SecondaryReserve - take);
            }
        }

        static bool HasWeapon(in PlayerCombatState c, WeaponSlot slot) => slot switch
        {
            WeaponSlot.Primary => c.PrimaryWeapon != PlayerCombatState.NoWeapon,
            WeaponSlot.Secondary => c.SecondaryWeapon != PlayerCombatState.NoWeapon,
            _ => true,
        };

        static WeaponSlot NextSlot(in PlayerCombatState c)
        {
            var s = c.ActiveSlot;
            for (int i = 0; i < 3; i++)
            {
                s = (WeaponSlot)(((int)s + 1) % 3);
                if (HasWeapon(c, s)) return s;
            }
            return c.ActiveSlot;
        }

        // ------------------------------------------------------------------ economy

        public enum BuyResult { Ok, NotEnoughMoney, Dead, Unknown }

        /// <summary>Buys by HUD item id: weapons, "ammo_primary", "ammo_secondary", "kevlar", "helmet".</summary>
        public static int PriceOf(string itemId)
        {
            switch (itemId)
            {
                case "kevlar": return 650;
                case "helmet": return 1000;
                case "ammo_primary": return 180;
                case "ammo_secondary": return 80;
                default: return WeaponCatalog.TryIndexOf(itemId, out var i) ? WeaponCatalog.Get(i).Price : -1;
            }
        }

        public static BuyResult Buy(PlayerAvatar a, string itemId, List<CombatEvent> events)
        {
            int price = PriceOf(itemId);
            if (price < 0) return BuyResult.Unknown;
            ref var c = ref a.Combat;
            if (!c.Alive) return BuyResult.Dead;
            if (price > c.Money) return BuyResult.NotEnoughMoney;
            switch (itemId)
            {
                case "kevlar":
                    c.Armor = 100;
                    break;
                case "helmet":
                    c.Armor = 100;
                    c.SetFlag(CombatFlags.Helmet, true);
                    break;
                case "ammo_primary":
                    if (c.PrimaryWeapon == PlayerCombatState.NoWeapon) return BuyResult.Unknown;
                    c.PrimaryReserve = (ushort)WeaponCatalog.Get(c.PrimaryWeapon).ReserveMax;
                    break;
                case "ammo_secondary":
                    if (c.SecondaryWeapon == PlayerCombatState.NoWeapon) return BuyResult.Unknown;
                    c.SecondaryReserve = (ushort)WeaponCatalog.Get(c.SecondaryWeapon).ReserveMax;
                    break;
                default:
                    if (!WeaponCatalog.TryIndexOf(itemId, out var idx)) return BuyResult.Unknown;
                    var w = WeaponCatalog.Get(idx);
                    SetWeapon(ref c, idx);
                    c.ActiveSlot = w.IsKnife ? WeaponSlot.Knife : WeaponCatalog.IsPrimary(w) ? WeaponSlot.Primary : WeaponSlot.Secondary;
                    c.SwitchTimer = w.DrawTime;
                    c.ReloadTimer = 0f;
                    c.SetFlag(CombatFlags.Reloading, false);
                    events.Add(new CombatEvent { Type = CombatEventType.Equip, Actor = a.PlayerId, Weapon = idx });
                    break;
            }
            c.Money -= price;
            return BuyResult.Ok;
        }

        /// <summary>End of the match clock: reset scores and money, respawn everyone.</summary>
        public static void RestartRound(WorldSimulation world, List<CombatEvent> events)
        {
            foreach (var a in world.Players)
            {
                a.Combat.Kills = 0;
                a.Combat.Deaths = 0;
                a.Combat.Money = StartMoney;
                a.Combat.RespawnTimer = 0f;
                if (a.Alive) a.SetAlive(false);
                a.Combat.Flags &= ~CombatFlags.Alive;
            }
            events.Add(new CombatEvent { Type = CombatEventType.RoundRestart });
        }
    }
}
