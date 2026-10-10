using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ArmyForge.Tests
{
    public class EquipmentAndSetupTests
    {
        static RosterUnit Unit() => new RosterUnit { sheet = new UnitDefinition {
            name = "Test squad", profiles = new List<Profile> { new Profile { name = "Trooper", wounds = 2 }, new Profile { name = "Leader", wounds = 3 } },
            sizes = new List<UnitSize> { new UnitSize { models = 2 }, new UnitSize { models = 4 } },
            weapons = new List<Weapon> { new Weapon { name = "Plasma gun - standard" }, new Weapon { name = "Plasma gun - supercharge" }, new Weapon { name = "Sword" } }
        } };

        [Test] public void LoadoutsResizeWithoutLosingSurvivingModelsAndGroupFiringModes()
        {
            var u = Unit(); LoadoutRules.Resize(u);
            u.loadouts[0].customised = true; u.loadouts[0].profileIndex = 1;
            u.loadouts[0].weapons.Add(new EquippedWeapon { name = "Plasma gun", quantity = 1 });
            u.sizeIndex = 1; LoadoutRules.Resize(u);
            Assert.That(u.loadouts.Count, Is.EqualTo(4));
            Assert.That(u.loadouts[0].weapons.Single().name, Is.EqualTo("Plasma gun"));
            Assert.That(u.loadouts[3].customised, Is.False);
            Assert.That(LoadoutRules.Choices(u.sheet), Is.EquivalentTo(new[] { "Plasma gun", "Sword" }));
            u.sizeIndex = 0; LoadoutRules.Resize(u);
            Assert.That(u.loadouts.Count, Is.EqualTo(2));
            Assert.That(LoadoutRules.Validate(u), Is.Empty);
        }
        [Test] public void BattleAndSavePreserveEquipmentAndSelectedModelProfileIndependently()
        {
            var u = Unit(); LoadoutRules.Resize(u);
            u.loadouts[0].profileIndex = 1; u.loadouts[0].customised = true; u.loadouts[0].equipment = "Scanner";
            u.loadouts[0].weapons.Add(new EquippedWeapon { name = "Sword", quantity = 1 });
            var army = new Army(); army.units.Add(u);
            var battle = Battle.Start(army); battle.setup = new BattleSetup { playerPrimary = "Sabotage", opponentPrimary = "Meatgrinder", deployment = "Tipping Point" };
            u.loadouts[0].weapons[0].quantity = 2;
            Assert.That(battle.units[0].loadouts[0].weapons[0].quantity, Is.EqualTo(1));
            Assert.That(battle.units[0].models[0].maxWounds, Is.EqualTo(3));
            var saved = LocalStore.Copy(battle);
            Assert.That(saved.setup.playerPrimary, Is.EqualTo("Sabotage"));
            Assert.That(saved.setup.deployment, Is.EqualTo("Tipping Point"));
            Assert.That(saved.units[0].loadouts[0].equipment, Is.EqualTo("Scanner"));
            Assert.That(LoadoutRules.Summary(u.sheet, saved.units[0].loadouts), Does.Contain("Sword"));
        }
        [Test] public void AbilityTokensKeepParametersQualifiersAndResolveCustomRules()
        {
            var labels = WeaponAbilityHelp.Split("[BLAST 2], SUSTAINED HITS 1: non-MONSTER/VEHICLE,ignores cover,psychic");
            Assert.That(labels.Count, Is.EqualTo(4));
            Assert.That(WeaponAbilityHelp.Explain(labels[0], null), Does.Contain("five target models"));
            Assert.That(WeaponAbilityHelp.Explain(labels[1], null), Does.Contain("non-MONSTER/VEHICLE"));
            Assert.That(WeaponAbilityHelp.Explain(labels[1], null), Does.Contain("extra hits"));
            var sheet = new UnitDefinition(); sheet.abilities.Add(new Rule { name = "Hooked", text = "Custom hook rule" });
            Assert.That(WeaponAbilityHelp.Explain("hooked", sheet), Is.EqualTo("Custom hook rule"));
            Assert.That(WeaponAbilityHelp.Explain("unknown", sheet), Does.Contain("datasheet-specific"));
        }
        [Test] public void MissionShufflesStayWithinDeckAndDoNotRepeatCurrentChoice()
        {
            Assert.That(MissionChoices.Primaries.Distinct().Count(), Is.EqualTo(25));
            Assert.That(MissionChoices.Deployments.Distinct().Count(), Is.EqualTo(6));
            var rng = new Random(17); string value = MissionChoices.Primaries[0];
            for (int i = 0; i < 100; i++) { string next = MissionChoices.Shuffle(MissionChoices.Primaries, value, rng); Assert.That(next, Is.Not.EqualTo(value)); Assert.That(MissionChoices.Primaries, Does.Contain(next)); value = next; }
            Assert.That(MissionChoices.Shuffle(new[] { "Only" }, "Only", rng), Is.EqualTo("Only"));
        }
    }
}
