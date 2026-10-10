using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArmyForge
{
    [Serializable] public class EquippedWeapon
    {
        public string name;
        public int quantity;
    }
    [Serializable] public class ModelLoadout
    {
        public int profileIndex;
        public bool customised;
        public string equipment = "";
        public List<EquippedWeapon> weapons = new List<EquippedWeapon>();
        public ModelLoadout Copy() => new ModelLoadout { profileIndex = profileIndex, customised = customised, equipment = equipment,
            weapons = (weapons ?? new List<EquippedWeapon>()).Select(x => new EquippedWeapon { name = x.name, quantity = x.quantity }).ToList() };
    }
    public static class LoadoutRules
    {
        // Alternative firing profiles belong to one physical weapon, not separate equipment choices.
        public static string WeaponName(Weapon weapon) => Regex.Split(weapon.name ?? "", @"\s+[–—-]\s+")[0].Trim();
        public static List<string> Choices(UnitDefinition sheet) => sheet.weapons.Select(WeaponName).Distinct().ToList();
        public static void Resize(RosterUnit unit)
        {
            if (unit.loadouts == null) unit.loadouts = new List<ModelLoadout>();
            while (unit.loadouts.Count < unit.Size.models) unit.loadouts.Add(new ModelLoadout());
            if (unit.loadouts.Count > unit.Size.models) unit.loadouts.RemoveRange(unit.Size.models, unit.loadouts.Count - unit.Size.models);
            foreach (var model in unit.loadouts) model.profileIndex = Math.Max(0, Math.Min(unit.sheet.profiles.Count - 1, model.profileIndex));
        }
        public static string Summary(UnitDefinition sheet, List<ModelLoadout> models)
        {
            if (models == null || models.Count == 0) return "Datasheet default loadout";
            return string.Join("\n", models.Select((m, i) => {
                string gear = m.customised ? string.Join(", ", m.weapons.Where(w => w.quantity > 0).Select(w => w.quantity + " × " + w.name)) : "Datasheet default";
                if (m.customised && gear.Length == 0) gear = "No weapons selected";
                return (i + 1) + ". " + sheet.profiles[Math.Max(0, Math.Min(m.profileIndex, sheet.profiles.Count - 1))].name + ": " + gear + (string.IsNullOrWhiteSpace(m.equipment) ? "" : " · " + m.equipment);
            }));
        }
        public static List<string> Validate(RosterUnit unit)
        {
            var issues = new List<string>();
            if (unit.loadouts == null || unit.loadouts.Count == 0) return issues; // Previous saves retain the default loadout.
            if (unit.loadouts.Count != unit.Size.models) issues.Add(unit.sheet.name + ": loadout model count does not match unit size.");
            var names = Choices(unit.sheet);
            foreach (var m in unit.loadouts)
            {
                if (m.customised && !m.weapons.Any(w => w.quantity > 0)) issues.Add(unit.sheet.name + ": a customised model has no weapons selected.");
                if (m.weapons.Any(w => !names.Contains(w.name) || w.quantity < 0 || w.quantity > 10)) issues.Add(unit.sheet.name + ": invalid weapon selection.");
            }
            return issues.Distinct().ToList();
        }
    }
}
