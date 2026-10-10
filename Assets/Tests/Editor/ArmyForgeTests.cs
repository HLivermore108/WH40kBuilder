using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ArmyForge.Tests
{
    public class ArmyForgeTests
    {
        Catalog catalog;
        [OneTimeSetUp] public void Import() => catalog = WahapediaImport.Build(name => Resources.Load<TextAsset>("ArmyData/" + name).text);

        [Test] public void BundledExportContainsAllFactionsAndPlayableUnits()
        {
            Assert.That(catalog.factions.Count, Is.EqualTo(25));
            Assert.That(catalog.units.Count, Is.GreaterThan(1000));
            foreach (var faction in catalog.factions.Where(x => x.id != "UA"))
                Assert.That(catalog.units.Any(x => x.factionId == faction.id), Is.True, faction.name);
            Assert.That(catalog.units.Count(x => x.factionId == "UA"), Is.Zero, "The provider currently lists Unbound Adversaries without datasheets.");
            Assert.That(catalog.units.Where(x => x.sizes.Count > 0).All(x => x.profiles.Count > 0), Is.True);
            Assert.That(catalog.units.SelectMany(x => x.profiles).All(x => x.wounds > 0), Is.True);
            Debug.Log($"IMPORT VERIFIED: {catalog.factions.Count} factions, {catalog.units.Count} datasheets, {catalog.units.Count(x => x.sizes.Count > 0)} priced units, {catalog.detachments.Count} detachments.");
        }
        [Test] public void CsvPreservesIdsEscapesAndMultilineFields()
        {
            var rows = PipeCsv.Read("\uFEFFid|name|description|\r\n0001|\"a|b\"|\"line 1\nline \"\"2\"\"\"|\r\n");
            Assert.That(rows[0]["id"], Is.EqualTo("0001"));
            Assert.That(rows[0]["name"], Is.EqualTo("a|b"));
            Assert.That(rows[0]["description"], Is.EqualTo("line 1\nline \"2\""));
            Assert.Throws<FormatException>(() => PipeCsv.Read("id|name\n1|\"unterminated"));
            Assert.Throws<FormatException>(() => PipeCsv.Read("<html>Server error</html>"));
        }
        [Test] public void DuplicateUnitPricingChangesAndRevertsWhenRemovingUnit()
        {
            var unit = catalog.units.Single(x => x.id == "000000002");
            var army = new Army();
            for (int i = 0; i < 3; i++) army.units.Add(new RosterUnit { sheet = unit });
            Assert.That(army.Cost(army.units[0]), Is.EqualTo(115));
            Assert.That(army.Cost(army.units[2]), Is.EqualTo(130));
            Assert.That(army.Points, Is.EqualTo(360));
            army.units.RemoveAt(0);
            Assert.That(army.Points, Is.EqualTo(230));
            army.units[0].paidOptions.Add(new PaidOption { points = 5, quantity = 2 });
            Assert.That(army.Points, Is.EqualTo(240));
        }
        [Test] public void WoundsClampAndBattleStateIsIndependentOfRoster()
        {
            var unit = catalog.units.First(x => x.profiles.Count > 0 && x.sizes.Count > 0);
            var army = new Army(); army.units.Add(new RosterUnit { sheet = unit });
            var battle = Battle.Start(army);
            var model = battle.units[0].models[0];
            model.Change(-9999); Assert.That(model.wounds, Is.Zero);
            model.Change(9999); Assert.That(model.wounds, Is.EqualTo(model.maxWounds));
            Assert.That(unit.profiles[0].wounds, Is.EqualTo(model.maxWounds));
            for (int i = 0; i < 10; i++) battle.NextPhase();
            Assert.That(battle.round, Is.EqualTo(2)); Assert.That(battle.turn, Is.Zero); Assert.That(battle.phase, Is.Zero);
            Assert.That(battle.player.command, Is.Zero);
        }
        [Test] public void SavesRoundTripArmySnapshotsEnhancementsAndBattle()
        {
            var army = new Army { factionId = "ORK", name = "Test army" };
            army.units.Add(new RosterUnit { sheet = catalog.units.Single(x => x.id == "000000002"), warlord = true });
            army.units[0].enhancement = new Enhancement { name = "Test enhancement", text = "Selected rule", points = 10 };
            var save = new SaveData(); save.armies.Add(army); save.battle = Battle.Start(army);
            save.battle.units[0].models[0].Change(-2); save.battle.opponent.primary = 15;
            var clone = LocalStore.Copy(save);
            Assert.That(clone.armies[0].Points, Is.EqualTo(army.Points));
            Assert.That(clone.armies[0].units[0].sheet.weapons.Count, Is.GreaterThan(0));
            Assert.That(clone.battle.units[0].models[0].wounds, Is.EqualTo(save.battle.units[0].models[0].wounds));
            Assert.That(clone.battle.opponent.Total, Is.EqualTo(15));
            Assert.That(clone.battle.units[0].notes, Does.Contain("Selected rule"));
        }
        [Test] public void AtomicSaveRecoversPreviousVersionFromBackup()
        {
            string name = "verification-" + Guid.NewGuid().ToString("N") + ".json";
            string path = Path.Combine(LocalStore.Folder, name);
            try
            {
                var state = new SaveData(); state.armies.Add(new Army { name = "Before" });
                LocalStore.Save(name, state);
                state.armies[0].name = "After"; LocalStore.Save(name, state);
                Assert.That(LocalStore.Load<SaveData>(name).armies[0].name, Is.EqualTo("After"));
                File.WriteAllText(path, "{broken");
                Assert.That(LocalStore.Load<SaveData>(name).armies[0].name, Is.EqualTo("Before"));
            }
            finally { foreach (var suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }
        }
        [Test] public void ValidationFindsOverspendAndAllowsSingleThreeDpIncursion()
        {
            var unit = catalog.units.Single(x => x.id == "000000002");
            var army = new Army { factionId = unit.factionId, limit = 1000 };
            army.units.Add(new RosterUnit { sheet = unit, warlord = true });
            army.detachments.Add(new Detachment { points = 3 });
            Assert.That(ArmyRules.Validate(army), Is.Empty);
            army.units[0].paidOptions.Add(new PaidOption { quantity = 1, points = 1000 });
            Assert.That(ArmyRules.Validate(army).Any(x => x.Contains("Over the points")), Is.True);
            army.detachments.Add(new Detachment { points = 1 });
            Assert.That(ArmyRules.Validate(army).Any(x => x.Contains("DP allowance")), Is.True);
        }
    }
}
