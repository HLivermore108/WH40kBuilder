using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ArmyForge
{
    public static class LocalStore
    {
        public static string Folder => Application.isEditor && Application.isBatchMode
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/ArmyForgeTests"))
            : Application.persistentDataPath;
        public static T Load<T>(string name) where T : class
        {
            string path = Path.Combine(Folder, name);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
            foreach (var candidate in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    var value = JsonUtility.FromJson<T>(File.ReadAllText(candidate));
                    if (value != null) return value;
                }
                catch (Exception e) { Debug.LogWarning("Could not read " + candidate + ": " + e.Message); }
            }
            throw new IOException("Saved data could not be read. The original files are preserved in " + Folder);
        }
        public static void Save(string name, object value)
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, name), temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(value, true), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        public static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        public static Army ImportArmy(string path)
        {
            var army = JsonUtility.FromJson<Army>(File.ReadAllText(path));
            if (army == null || string.IsNullOrEmpty(army.factionId) || string.IsNullOrWhiteSpace(army.name) || army.units == null || army.detachments == null || army.limit <= 0 || army.units.Count > 500)
                throw new FormatException("This is not a valid Army Forge roster JSON file.");
            foreach (var u in army.units)
            {
                if (u?.sheet == null || u.sheet.sizes == null || u.sizeIndex < 0 || u.sizeIndex >= u.sheet.sizes.Count || u.Size.models < 1 || u.Size.models > 200 || u.Size.points < 0 || u.sheet.profiles == null || u.sheet.profiles.Count == 0 || u.sheet.profiles.Any(x => x.wounds < 1 || x.wounds > 999) || u.paidOptions == null || u.sheet.keywords == null || u.sheet.bodyguards == null || u.sheet.enhancementIds == null || u.sheet.weapons == null || u.sheet.abilities == null)
                    throw new FormatException("The roster contains invalid or incomplete unit data.");
                if (string.IsNullOrEmpty(u.id) || string.IsNullOrEmpty(u.sheet.name) || u.sheet.sizes.Any(x => x.models < 1 || x.models > 200 || x.points < 0) || u.paidOptions.Any(x => x.quantity < 0 || x.quantity > 2000 || x.points < 0) || (u.enhancement != null && u.enhancement.points < 0))
                    throw new FormatException("The roster contains invalid model counts or point costs.");
            }
            foreach (var u in army.units)
            {
                if (u.loadouts == null) u.loadouts = new System.Collections.Generic.List<ModelLoadout>();
                if (u.loadouts.Count > 0 && (u.loadouts.Count != u.Size.models || u.loadouts.Any(m => m == null || m.profileIndex < 0 || m.profileIndex >= u.sheet.profiles.Count || m.weapons == null || m.weapons.Any(w => w == null || w.quantity < 0 || w.quantity > 10 || !LoadoutRules.Choices(u.sheet).Contains(w.name)))))
                    throw new FormatException("The roster contains invalid model loadouts.");
            }
            army.id = Guid.NewGuid().ToString("N");
            return army;
        }
        public static string ExportArmy(Army army)
        {
            string folder = Path.Combine(Folder, "Exports");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "army-" + army.id + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(army, true), Encoding.UTF8);
            var text = new StringBuilder().AppendLine(army.name).AppendLine(army.factionName).AppendLine($"{army.Points} / {army.limit} points").AppendLine("Data: " + army.dataVersion);
            foreach (var detachment in army.detachments) text.AppendLine(detachment.name + " • " + detachment.points + " DP");
            foreach (var u in army.units)
            {
                text.AppendLine().AppendLine($"{u.sheet.name} — {u.Size.models} models — {army.Cost(u)} pts" + (u.warlord ? " — WARLORD" : ""));
                if (u.enhancement != null) text.AppendLine(u.enhancement.name);
                foreach (var option in u.paidOptions.Where(x => x.quantity > 0)) text.AppendLine($"{option.quantity} × {option.name}");
                if (!string.IsNullOrEmpty(u.attachedTo)) text.AppendLine("Attached to: " + army.units.Find(x => x.id == u.attachedTo)?.sheet.name);
                text.AppendLine(u.notes);
                text.AppendLine(LoadoutRules.Summary(u.sheet, u.loadouts));
            }
            File.WriteAllText(Path.ChangeExtension(path, ".txt"), text.ToString(), Encoding.UTF8);
            return path;
        }
    }
}
