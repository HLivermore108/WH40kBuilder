using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ArmyForge
{
    public static class PipeCsv
    {
        public static List<Dictionary<string, string>> Read(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            text = (text ?? "").TrimStart('\uFEFF');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (quoted || field.Length == 0))
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (!quoted && (c == '|' || c == '\n' || c == '\r'))
                {
                    row.Add(field.ToString()); field.Clear();
                    if (c != '|')
                    {
                        if (row.Any(x => x.Length > 0)) rows.Add(row);
                        row = new List<string>();
                        if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    }
                }
                else field.Append(c);
            }
            if (quoted) throw new FormatException("Unclosed quoted field in export.");
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
            if (rows.Count == 0) throw new FormatException("Empty export.");
            var headers = rows[0];
            if (headers.Count < 2 && headers[0] != "last_update") throw new FormatException("Expected a pipe-delimited Wahapedia export.");
            return rows.Skip(1).Select(values =>
            {
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Count; i++) if (headers[i].Length > 0) result[headers[i]] = i < values.Count ? values[i] : "";
                return result;
            }).ToList();
        }
    }
    public static class WahapediaImport
    {
        public const string BaseUrl = "https://wahapedia.ru/wh40k11ed/";
        public static readonly string[] Files = { "Factions", "Source", "Datasheets", "Datasheets_abilities", "Datasheets_keywords", "Datasheets_models", "Datasheets_options", "Datasheets_wargear", "Datasheets_unit_composition", "Datasheets_models_cost", "Datasheets_enhancements", "Datasheets_leader", "Abilities", "Enhancements", "Detachment_abilities", "Detachments", "Last_update" };
        public static string Get(this Dictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value : "";
        public static string Plain(string html)
        {
            html = Regex.Replace(html ?? "", @"<(br\s*/?|/p|/div|/li|/tr)\s*>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<[^>]*>", "");
            return Regex.Replace(WebUtility.HtmlDecode(html).Replace('\u00a0', ' '), @"[ \t]+", " ").Trim();
        }
        static int Number(string text, int fallback = 0) => int.TryParse(Plain(text), out int n) ? n : fallback;
        public static Catalog Build(Func<string, string> read)
        {
            var tables = Files.ToDictionary(x => x, x => PipeCsv.Read(read(x)));
            var catalog = new Catalog { updated = tables["Last_update"].FirstOrDefault()?.Get("last_update") ?? "Unknown" };
            foreach (var r in tables["Factions"]) catalog.factions.Add(new Faction { id = r.Get("id"), name = Plain(r.Get("name")), link = r.Get("link") });
            var sources = tables["Source"].ToDictionary(x => x.Get("id"));
            foreach (var r in tables["Datasheets"])
            {
                sources.TryGetValue(r.Get("source_id"), out var source);
                catalog.units.Add(new UnitDefinition {
                    id = r.Get("id"), factionId = r.Get("faction_id"), name = Plain(r.Get("name")), link = r.Get("link"),
                    source = source == null ? "Unknown" : Plain(source.Get("name")), sourceEdition = source?.Get("edition"),
                    virtualUnit = r.Get("virtual") == "true", support = r.Get("is_support") == "true",
                    loadout = Plain(r.Get("loadout")), transport = Plain(r.Get("transport")), damaged = Plain(r.Get("damaged_w") + " " + r.Get("damaged_description")),
                    leaderNotes = Plain(r.Get("leader_head") + "\n" + r.Get("leader_footer"))
                });
            }
            var units = catalog.units.ToDictionary(x => x.id);
            void Each(string table, Action<UnitDefinition, Dictionary<string, string>> action)
            { foreach (var row in tables[table]) if (units.TryGetValue(row.Get("datasheet_id"), out var u)) action(u, row); }
            Each("Datasheets_keywords", (u, r) => u.keywords.Add(Plain(r.Get("keyword"))));
            Each("Datasheets_models", (u, r) => u.profiles.Add(new Profile { name = Plain(r.Get("name")), movement = Plain(r.Get("M")), toughness = r.Get("T"), save = r.Get("Sv"), invulnerable = r.Get("inv_sv"), wounds = Number(r.Get("W"), 1), leadership = r.Get("Ld"), control = r.Get("OC") }));
            var priceBands = new Dictionary<string, Tuple<int, int>>();
            Each("Datasheets_models_cost", (u, r) => {
                var label = Plain(r.Get("description"));
                if (label.StartsWith("YOUR"))
                {
                    var numbers = Regex.Matches(label, @"\d+").Cast<Match>().Select(x => int.Parse(x.Value)).ToArray();
                    int from = numbers.Length > 0 ? numbers[0] : 1;
                    int to = numbers.Length > 1 ? numbers[1] : (label.Contains("+") || numbers.Length == 0 ? 999 : from);
                    priceBands[u.id] = Tuple.Create(from, to);
                }
                var match = Regex.Match(label, @"^(\d+)\s+model", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(r.Get("cost"), out int cost))
                {
                    var band = priceBands.TryGetValue(u.id, out var b) ? b : Tuple.Create(1, 999);
                    u.sizes.Add(new UnitSize { label = label, models = int.Parse(match.Groups[1].Value), points = cost, from = band.Item1, to = band.Item2 });
                }
                else if (label.StartsWith("per ") && int.TryParse(r.Get("cost"), out int optionCost))
                    u.paidOptions.Add(new PaidOption { name = label.Substring(4), points = optionCost });
            });
            Each("Datasheets_wargear", (u, r) => u.weapons.Add(new Weapon { name = Plain(r.Get("name")), range = Plain(r.Get("range")), attacks = r.Get("A"), skill = r.Get("BS_WS"), strength = r.Get("S"), ap = r.Get("AP"), damage = r.Get("D"), rules = Plain(r.Get("description")) }));
            Each("Datasheets_options", (u, r) => u.options += Plain(r.Get("description")) + "\n");
            Each("Datasheets_unit_composition", (u, r) => u.composition += Plain(r.Get("description")) + "\n");
            // Shared abilities may repeat the same ID for different factions in the export.
            var abilities = tables["Abilities"].GroupBy(x => x.Get("id")).ToDictionary(x => x.Key, x => x.ToList());
            Each("Datasheets_abilities", (u, r) => {
                var source = abilities.TryGetValue(r.Get("ability_id"), out var linked) ? linked.FirstOrDefault(x => x.Get("faction_id") == u.factionId) ?? linked[0] : r;
                string qualifier = string.Join(" · ", new[] { Plain(r.Get("model")), Plain(r.Get("parameter")) }.Where(x => !string.IsNullOrEmpty(x)));
                u.abilities.Add(new Rule { name = Plain(source.Get("name")) + (qualifier.Length == 0 ? "" : " (" + qualifier + ")"), text = Plain(source.Get("description")) });
            });
            Each("Datasheets_enhancements", (u, r) => u.enhancementIds.Add(r.Get("enhancement_id")));
            foreach (var r in tables["Datasheets_leader"]) if (units.TryGetValue(r.Get("leader_id"), out var leader)) leader.bodyguards.Add(r.Get("attached_id"));
            foreach (var r in tables["Detachments"]) catalog.detachments.Add(new Detachment { id = r.Get("id"), factionId = r.Get("faction_id"), name = Plain(r.Get("name")), type = r.Get("type"), points = Number(r.Get("dp")), disposition = r.Get("force_disposition") });
            foreach (var r in tables["Detachment_abilities"])
                catalog.detachments.Find(x => x.id == r.Get("detachment_id"))?.rules.Add(new Rule { name = Plain(r.Get("name")), text = Plain(r.Get("description")) });
            foreach (var r in tables["Enhancements"]) catalog.enhancements.Add(new Enhancement { id = r.Get("id"), factionId = r.Get("faction_id"), detachmentId = r.Get("detachment_id"), name = Plain(r.Get("name")), text = Plain(r.Get("description")), points = Number(r.Get("cost")), upgrade = r.Get("upgrade") == "true" });
            catalog.factions = catalog.factions.OrderBy(x => x.name).ToList();
            catalog.units = catalog.units.OrderBy(x => x.name).ToList();
            if (catalog.factions.Count == 0 || catalog.units.Count == 0 || !catalog.units.Any(x => x.sizes.Count > 0)) throw new FormatException("Export is missing factions, datasheets, or unit prices. Previous data has been kept.");
            return catalog;
        }
    }
}
