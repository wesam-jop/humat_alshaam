using System;
using System.Collections.Generic;
using System.Linq;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Gameplay;
using Adventure3D.Networking;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Adventure3D.Tests
{
    /// <summary>Authoritative combat rules exercised through a real offline GameSession.</summary>
    public class CombatTests
    {
        sealed class FuncSource : ICommandSource
        {
            public Func<PlayerCommand> Next = () => default;
            public PlayerCommand SampleCommand() => Next();
            public float LookYaw => 0f;
            public float LookPitch => 0f;
            public void ResetTransientInput() { }
            public void SetLook(float yaw, float pitch) { }
        }

        Transform _arena;
        GameSession _session;
        readonly List<CombatEvent> _events = new List<CombatEvent>();

        [SetUp]
        public void SetUp()
        {
            _arena = TestArenaLayout.Build();
            var cfg = GameConfig.Load().CloneForRuntime();
            _session = new GameSession(LaunchRequest.Offline(), cfg, null, new WorldSimulation(cfg)) { OfflineBots = 0 };
            _session.CombatEventRaised += e => _events.Add(e);
            _events.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _session.Dispose();
            foreach (var a in Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None)) Object.DestroyImmediate(a.gameObject);
            var players = GameObject.Find("Players");
            if (players != null) Object.DestroyImmediate(players);
            Object.DestroyImmediate(_arena.gameObject);
        }

        void Run(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds * 60f);
            for (int i = 0; i < ticks; i++) _session.Update(1f / 60f);
        }

        PlayerAvatar AddTarget(Vector3 position, Team team)
        {
            var a = _session.World.Spawn(500, false, new Pose(position, Quaternion.identity));
            a.Combat.Team = team;
            a.Combat.ProtectedTimer = 0f;
            a.SetState(new PlayerSimState { Position = position }, true);
            return a;
        }

        static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        [Test]
        public void ShootingAnEnemyKillsScoresAndRespawns()
        {
            var src = new FuncSource();
            _session.LocalCommands = src;
            Assert.IsTrue(_session.Start());
            var me = _session.LocalAvatar;
            me.SetState(new PlayerSimState { Position = new Vector3(0f, 0.05f, -20f) }, true);
            var target = AddTarget(new Vector3(0f, 0.05f, -10f), Team.Attackers);
            Run(0.2f);
            Assert.AreEqual(Team.Defenders, me.Combat.Team);
            src.Next = () => new PlayerCommand { Yaw = YawTo(me.State.Position, target.State.Position), Pitch = 3f, Buttons = CommandButtons.Fire };
            Run(1.5f);

            var kill = _events.FirstOrDefault(e => e.Type == CombatEventType.Kill);
            Assert.AreEqual(CombatEventType.Kill, kill.Type, "enemy was killed");
            Assert.AreEqual(me.PlayerId, kill.Actor);
            Assert.AreEqual(target.PlayerId, kill.Target);
            Assert.AreEqual(1, me.Combat.Kills);
            Assert.AreEqual(1, target.Combat.Deaths);
            Assert.IsFalse(target.Alive);
            Assert.IsFalse(target.Controller.enabled, "dead players have no collision");
            Assert.Greater(me.Combat.Money, CombatRules.StartMoney, "kill reward");
            Assert.Less(me.Combat.PrimaryClip, 30, "ammo was spent");

            src.Next = () => default;
            Run(CombatRules.RespawnDelay + 0.2f);
            Assert.IsTrue(target.Alive, "respawned");
            Assert.AreEqual(100, target.Combat.Health);
            Assert.IsTrue(_events.Any(e => e.Type == CombatEventType.Respawn && e.Actor == target.PlayerId));
        }

        [Test]
        public void ArmorAndHeadshotDamageFollowTheWeaponNumbers()
        {
            Assert.IsTrue(_session.Start());
            var me = _session.LocalAvatar;
            var target = AddTarget(new Vector3(5f, 0.05f, -20f), Team.Attackers);
            target.Combat.ProtectedTimer = 0f;
            var ak = WeaponCatalog.Get(WeaponCatalog.IndexOf("ak47"));
            var ev = new List<CombatEvent>();

            CombatRules.ApplyDamage(target, me, ak, WeaponCatalog.IndexOf("ak47"), false, ev);
            int expectedHp = 100 - Mathf.RoundToInt(36 * 0.78f);
            Assert.AreEqual(expectedHp, target.Combat.Health, "body shot through armor");
            Assert.Less(target.Combat.Armor, 100, "armor absorbed part");
            Measurements.Record("combat.ak47.bodyShotThroughArmor", 100 - expectedHp, 100 - target.Combat.Health, 0, "HP");

            CombatRules.ApplyDamage(target, me, ak, WeaponCatalog.IndexOf("ak47"), true, ev);
            Assert.IsFalse(target.Alive, "AK headshot after a body shot is lethal");
            Assert.IsTrue(ev.Any(e => e.Type == CombatEventType.Kill && e.Headshot));
        }

        [Test]
        public void TeammatesCannotBeDamaged()
        {
            var src = new FuncSource();
            _session.LocalCommands = src;
            Assert.IsTrue(_session.Start());
            var me = _session.LocalAvatar;
            me.SetState(new PlayerSimState { Position = new Vector3(0f, 0.05f, -20f) }, true);
            var mate = AddTarget(new Vector3(0f, 0.05f, -12f), Team.Defenders);
            Run(0.2f);
            src.Next = () => new PlayerCommand { Yaw = YawTo(me.State.Position, mate.State.Position), Pitch = 3f, Buttons = CommandButtons.Fire };
            Run(1f);
            Assert.AreEqual(100, mate.Combat.Health);
            Assert.IsTrue(_events.Any(e => e.Type == CombatEventType.Shot && e.HitPlayer && e.Target == mate.PlayerId), "the bullets did reach the teammate");
        }

        [Test]
        public void ReloadMovesAmmoFromReserveAndSemiAutoNeedsNewPresses()
        {
            var src = new FuncSource();
            _session.LocalCommands = src;
            Assert.IsTrue(_session.Start());
            var me = _session.LocalAvatar;
            me.SetState(new PlayerSimState { Position = new Vector3(-30f, 0.05f, -30f) }, true);
            Run(0.2f);

            // 5 rifle shots
            int ticks = 0;
            src.Next = () => ++ticks <= 30 ? new PlayerCommand { Yaw = 180f, Buttons = CommandButtons.Fire } : new PlayerCommand { Yaw = 180f };
            Run(0.6f);
            int fired = 30 - me.Combat.PrimaryClip;
            Assert.AreEqual(5, fired, "0.5 s at 0.09 s per shot (first shot immediate)");

            bool sentReload = false;
            src.Next = () => { var c = new PlayerCommand { Yaw = 180f, Buttons = sentReload ? 0 : CommandButtons.Reload }; sentReload = true; return c; };
            Run(0.1f);
            Assert.IsTrue(me.Combat.Reloading);
            Run(WeaponCatalog.Get(me.Combat.PrimaryWeapon).ReloadTime + 0.1f);
            Assert.AreEqual(30, me.Combat.PrimaryClip);
            Assert.AreEqual(90 - fired, me.Combat.PrimaryReserve);

            // Pistol: holding the trigger fires exactly once.
            src.Next = () => new PlayerCommand { Yaw = 180f, SlotRequest = 2 };
            Run(0.1f);
            src.Next = () => new PlayerCommand { Yaw = 180f };
            Run(0.6f);
            Assert.AreEqual(WeaponSlot.Secondary, me.Combat.ActiveSlot);
            int before = me.Combat.SecondaryClip;
            src.Next = () => new PlayerCommand { Yaw = 180f, Buttons = CommandButtons.Fire };
            Run(1f);
            Assert.AreEqual(before - 1, me.Combat.SecondaryClip, "semi-automatic");
        }

        [Test]
        public void BuyingNeedsMoneyAndGivesTheWeapon()
        {
            Assert.IsTrue(_session.Start());
            var me = _session.LocalAvatar;
            _session.RequestBuy("ak47");
            Assert.AreEqual("ak47", WeaponCatalog.Get(me.Combat.PrimaryWeapon).Id);
            Assert.AreEqual(CombatRules.StartMoney - 2700, me.Combat.Money);
            _session.RequestBuy("awp"); // 4750 > remaining 1800
            Assert.AreEqual("ak47", WeaponCatalog.Get(me.Combat.PrimaryWeapon).Id, "not enough money");
            _session.RequestBuy("kevlar");
            Assert.AreEqual(CombatRules.StartMoney - 2700 - 650, me.Combat.Money);
        }

        [Test]
        public void BotsFightEachOther()
        {
            _session.OfflineBots = 6;
            Assert.IsTrue(_session.Start());
            _session.LocalAvatar.SetState(new PlayerSimState { Position = new Vector3(-36f, 0.05f, -36f) }, true); // keep the human out of the way
            Run(40f);
            int kills = _events.Count(e => e.Type == CombatEventType.Kill);
            int shots = _events.Count(e => e.Type == CombatEventType.Shot);
            Measurements.RecordBound("combat.bots.killsIn40s", 1, kills, false, "kills", "6 bots, offline, 40 s");
            Assert.Greater(shots, 20, "bots shoot");
            int damage = _events.Count(e => e.Type == CombatEventType.Damage);
            int hitPlayer = _events.Count(e => e.Type == CombatEventType.Shot && e.HitPlayer);
            var sh = _events.Where(e => e.Type == CombatEventType.Shot).ToList();
            Debug.Log($"[A3D-TEST] avgDist={sh.Average(e => (e.Point - e.Origin).magnitude):0.0} worldHits={sh.Count(e => !e.HitPlayer && e.Normal != Vector3.zero)} misses={sh.Count(e => e.Normal == Vector3.zero)} sample={sh[sh.Count/2].Origin}->{sh[sh.Count/2].Point}");
            Debug.Log($"[A3D-TEST] bots: shots={shots} hitPlayer={hitPlayer} damage={damage} kills={kills} alive={_session.World.Players.Count(p => p.Alive)} crouched={_session.World.Players.Count(p => p.State.Crouching)}");
            foreach (var p in _session.World.Players) Debug.Log($"[A3D-TEST] {p.PlayerId} team={p.Combat.Team} pos={p.State.Position} hp={p.Combat.Health} h={p.Controller.height}");
            Assert.GreaterOrEqual(kills, 1, "bots kill each other");
        }
    }
}
