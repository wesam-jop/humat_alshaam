using Adventure3D.Gameplay;
using NUnit.Framework;

namespace Adventure3D.Tests
{
    /// <summary>
    /// Weapon numbers checked against the Counter-Strike 1.6 reference (damage, range modifier, max
    /// speed / 250) and the damage formula: base x body part x rangeModifier^(distance / 12.7 m).
    /// </summary>
    public class WeaponBalanceTests
    {
        // id, damage, rangeModifier, speed multiplier (CS maxspeed / 250)
        static readonly object[] Reference =
        {
            new object[] { "usp45", 34, 0.79f, 1f },
            new object[] { "glock18", 25, 0.75f, 1f },
            new object[] { "deagle", 54, 0.81f, 1f },
            new object[] { "mp5", 26, 0.84f, 1f },
            new object[] { "p90", 21, 0.885f, 0.98f },
            new object[] { "ak47", 36, 0.98f, 0.884f },
            new object[] { "m4a1", 33, 0.97f, 0.92f },
            new object[] { "awp", 115, 0.99f, 0.84f },
            new object[] { "scout", 75, 0.98f, 1.04f },
            new object[] { "m3", 20, 0.7f, 0.92f },
            new object[] { "m249", 32, 0.97f, 0.88f },
        };

        [TestCaseSource(nameof(Reference))]
        public void MatchesReference(string id, int damage, float range, float speed)
        {
            Assert.IsTrue(WeaponCatalog.TryIndexOf(id, out var i), id);
            var w = WeaponCatalog.Get(i);
            Assert.AreEqual(damage, w.Damage, id + " damage");
            Assert.AreEqual(range, w.RangeModifier, 1e-4f, id + " range modifier");
            Assert.AreEqual(speed, w.MoveSpeedMultiplier, 1e-3f, id + " weight / speed");
        }

        [Test]
        public void BodyPartMultipliers()
        {
            var ak = WeaponCatalog.Get(WeaponCatalog.IndexOf("ak47"));
            Assert.AreEqual(36f * 4f, CombatRules.RawDamage(ak, HitZone.Head, 0f), 1e-3f);
            Assert.AreEqual(36f, CombatRules.RawDamage(ak, HitZone.Chest, 0f), 1e-3f);
            Assert.AreEqual(45f, CombatRules.RawDamage(ak, HitZone.Stomach, 0f), 1e-3f);
            Assert.AreEqual(27f, CombatRules.RawDamage(ak, HitZone.Legs, 0f), 1e-3f);
        }

        [Test]
        public void DamageFallsOffWithDistance()
        {
            var ak = WeaponCatalog.Get(WeaponCatalog.IndexOf("ak47"));
            var usp = WeaponCatalog.Get(WeaponCatalog.IndexOf("usp45"));
            var awp = WeaponCatalog.Get(WeaponCatalog.IndexOf("awp"));
            Assert.AreEqual(36f * 0.98f * 0.98f, CombatRules.RawDamage(ak, HitZone.Chest, 25.4f), 1e-3f, "AK at 25.4 m");
            Assert.AreEqual(34f * 0.79f, CombatRules.RawDamage(usp, HitZone.Chest, 12.7f), 1e-3f, "USP at 12.7 m");
            Assert.Greater(CombatRules.RawDamage(awp, HitZone.Chest, 100f), 100f, "AWP barely loses damage");
            Assert.Less(CombatRules.RawDamage(usp, HitZone.Chest, 40f), 17f, "pistols are weak at range");
        }

        [Test]
        public void KnifeIsTheFastestAndHeavyWeaponsAreSlower()
        {
            float knife = WeaponCatalog.Get(WeaponCatalog.IndexOf("knife")).MoveSpeedMultiplier;
            foreach (var w in WeaponCatalog.All)
                if (w.Id != "scout") Assert.LessOrEqual(w.MoveSpeedMultiplier, knife, w.Id);
            Assert.Less(WeaponCatalog.Get(WeaponCatalog.IndexOf("awp")).ScopedSpeedMultiplier, 1f, "scoped AWP is slower");
        }
    }
}
