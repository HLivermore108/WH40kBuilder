using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyForge
{
    [Serializable] public class Faction { public string id, name, link; }
    [Serializable] public class Profile
    {
        public string name, movement, toughness, save, invulnerable, leadership, control;
        public int wounds;
    }
    [Serializable] public class UnitSize { public string label; public int models, points, from = 1, to = 999; }
    [Serializable] public class PaidOption { public string name; public int points, quantity; }
    [Serializable] public class Weapon { public string name, range, attacks, skill, strength, ap, damage, rules; }
    [Serializable] public class Rule { public string name, text; }
    [Serializable] public class Detachment { public string id, factionId, name, type, disposition; public int points; public List<Rule> rules = new List<Rule>(); }
    [Serializable] public class Enhancement { public string id, factionId, detachmentId, name, text; public int points; public bool upgrade; }
    [Serializable] public class UnitDefinition
    {
        public string id, factionId, name, source, sourceEdition, link, loadout, composition, options, transport, damaged, leaderNotes;
        public bool virtualUnit, support;
        public List<string> keywords = new List<string>();
        public List<string> bodyguards = new List<string>();
        public List<string> enhancementIds = new List<string>();
        public List<Profile> profiles = new List<Profile>();
        public List<UnitSize> sizes = new List<UnitSize>();
        public List<PaidOption> paidOptions = new List<PaidOption>();
        public List<Weapon> weapons = new List<Weapon>();
        public List<Rule> abilities = new List<Rule>();
        public bool Has(string keyword) => keywords.Any(x => string.Equals(x, keyword, StringComparison.OrdinalIgnoreCase));
    }
    [Serializable] public class Catalog
    {
        public string edition = "11", updated;
        public List<Faction> factions = new List<Faction>();
        public List<UnitDefinition> units = new List<UnitDefinition>();
        public List<Detachment> detachments = new List<Detachment>();
        public List<Enhancement> enhancements = new List<Enhancement>();
    }
    [Serializable] public class RosterUnit
    {
        public string id = Guid.NewGuid().ToString("N");
        public UnitDefinition sheet;
        public int sizeIndex;
        public bool warlord;
        public string notes = "", attachedTo = "";
        public Enhancement enhancement;
        public List<PaidOption> paidOptions = new List<PaidOption>();
        public UnitSize Size => sheet.sizes[sizeIndex];
        public List<ModelLoadout> loadouts = new List<ModelLoadout>();
        public int Extras => paidOptions.Sum(x => x.points * x.quantity) + (enhancement == null ? 0 : enhancement.points);
    }
    [Serializable] public class Army
    {
        public string id = Guid.NewGuid().ToString("N"), name = "New army", factionId, factionName, dataVersion;
        public int limit = 2000;
        public List<Detachment> detachments = new List<Detachment>();
        public List<RosterUnit> units = new List<RosterUnit>();
        public int Cost(RosterUnit unit)
        {
            int ordinal = units.TakeWhile(x => x != unit).Count(x => x.sheet.name == unit.sheet.name) + 1;
            var price = unit.sheet.sizes.LastOrDefault(x => x.models == unit.Size.models && ordinal >= x.from && ordinal <= x.to);
            return (price ?? unit.Size).points + unit.Extras;
        }
        public int Points => units.Sum(Cost);
    }
    [Serializable] public class ModelState
    {
        public string name;
        public int maxWounds, wounds;
        public void Change(int delta) => wounds = Math.Max(0, Math.Min(maxWounds, wounds + delta));
    }
    [Serializable] public class BattleUnit
    {
        public string name, rosterUnitId, notes;
        public UnitDefinition sheet;
        public bool battleShocked;
        public List<ModelLoadout> loadouts = new List<ModelLoadout>();
        public List<ModelState> models = new List<ModelState>();
    }
    [Serializable] public class Score
    {
        public string name;
        public int primary, secondary, command;
        public int Total => primary + secondary;
    }
    [Serializable] public class Battle
    {
        public string armyName, armyId;
        public int round = 1, turn, phase;
        public bool active;
        public BattleSetup setup;
        public SecondaryDeck playerDeck, opponentDeck;
        public Score player = new Score { name = "You" }, opponent = new Score { name = "Opponent" };
        public List<BattleUnit> units = new List<BattleUnit>();
        public static readonly string[] Phases = { "Command", "Movement", "Shooting", "Charge", "Fight" };
        public bool EnsureDecks()
        {
            bool changed = playerDeck == null || opponentDeck == null;
            if (playerDeck == null) playerDeck = SecondaryDeck.Create();
            if (opponentDeck == null) opponentDeck = SecondaryDeck.Create();
            return changed;
        }
        public bool ReplaceSecondary(bool forOpponent, string id)
        {
            EnsureDecks();
            var deck = forOpponent ? opponentDeck : playerDeck;
            var score = forOpponent ? opponent : player;
            if (turn != (forOpponent ? 1 : 0) || phase != 0 || deck.commandRedrawUsed || score.command < 1) return false;
            if (!deck.Redraw(id, false)) return false;
            score.command--; deck.commandRedrawUsed = true; return true;
        }
        public void NextPhase()
        {
            if (++phase < Phases.Length) return;
            phase = 0;
            if (turn == 0) turn = 1;
            else { turn = 0; round++; }
        }
        public static Battle Start(Army army)
        {
            var battle = new Battle { active = true, armyId = army.id, armyName = army.name };
            battle.EnsureDecks();
            foreach (var entry in army.units)
            {
                LoadoutRules.Resize(entry);
                var notes = new List<string>();
                if (entry.warlord) notes.Add("WARLORD");
                if (!string.IsNullOrWhiteSpace(entry.notes)) notes.Add(entry.notes);
                notes.AddRange(entry.paidOptions.Where(x => x.quantity > 0).Select(x => x.quantity + " × " + x.name));
                if (entry.enhancement != null) notes.Add(entry.enhancement.name + ": " + entry.enhancement.text);
                if (!string.IsNullOrEmpty(entry.attachedTo)) notes.Add("Attached to: " + army.units.Find(x => x.id == entry.attachedTo)?.sheet.name);
                var unit = new BattleUnit { name = entry.sheet.name, rosterUnitId = entry.id, sheet = entry.sheet, notes = string.Join("\n", notes), loadouts = entry.loadouts.Select(x => x.Copy()).ToList() };
                for (int i = 0; i < entry.Size.models; i++)
                {
                    var profile = entry.sheet.profiles[unit.loadouts[i].profileIndex];
                    unit.models.Add(new ModelState { name = profile.name + " " + (i + 1), wounds = profile.wounds, maxWounds = profile.wounds });
                }
                battle.units.Add(unit);
            }
            return battle;
        }
    }
    [Serializable] public class SaveData
    {
        public int version = 1;
        public bool extraLargeText;
        public List<Army> armies = new List<Army>();
        public Battle battle;
    }
    public static class ArmyRules
    {
        // Basic 11th-edition checks. Faction exceptions remain visible in source rules.
        public static List<string> Validate(Army army)
        {
            var issues = new List<string>();
            if (army.units.Count == 0) issues.Add("Add at least one unit.");
            if (army.Points > army.limit) issues.Add($"Over the points limit by {army.Points - army.limit}.");
            if (army.units.Count(x => x.warlord) != 1) issues.Add("Select exactly one CHARACTER unit as your Warlord.");
            if (army.units.Any(x => x.warlord && !x.sheet.Has("Character"))) issues.Add("Your Warlord must be a CHARACTER.");
            int unitLimit = army.limit <= 1000 ? 2 : 3;
            foreach (var group in army.units.GroupBy(x => x.sheet.name))
            {
                var sheet = group.First().sheet;
                int cap = sheet.Has("Epic Hero") ? 1 : (sheet.Has("Battleline") || sheet.Has("Dedicated Transport") ? unitLimit * 2 : unitLimit);
                if (group.Count() > cap) issues.Add($"{sheet.name}: {group.Count()} selected; basic limit is {cap}.");
            }
            int dp = army.limit <= 1000 ? 2 : 3;
            if (army.detachments.Count == 0) issues.Add("Select a detachment.");
            if (army.detachments.Sum(x => x.points) > dp && !(army.limit <= 1000 && army.detachments.Count == 1 && army.detachments[0].points == 3))
                issues.Add($"Detachments exceed the {dp} DP allowance.");
            var enhanced = army.units.Where(x => x.enhancement != null).ToList();
            int count = enhanced.Where(x => !x.enhancement.upgrade).Count() + enhanced.Where(x => x.enhancement.upgrade).Select(x => x.enhancement.id).Distinct().Count();
            if (count > (army.limit <= 1000 ? 2 : 4)) issues.Add("Too many enhancements for this battle size.");
            foreach (var group in enhanced.GroupBy(x => x.enhancement.id))
                if (group.Count() > (group.First().enhancement.upgrade ? 3 : 1)) issues.Add("Duplicate enhancement: " + group.First().enhancement.name);
            foreach (var unit in army.units)
            {
                issues.AddRange(LoadoutRules.Validate(unit));
                if (unit.sheet.factionId != army.factionId) issues.Add(unit.sheet.name + ": faction exception requires review.");
                if (unit.enhancement != null)
                {
                    if (unit.sheet.Has("Epic Hero") || (!unit.sheet.Has("Character") && !unit.enhancement.upgrade)) issues.Add(unit.sheet.name + ": enhancement eligibility requires review.");
                    if (!army.detachments.Any(x => x.id == unit.enhancement.detachmentId)) issues.Add(unit.sheet.name + ": enhancement's detachment is not selected.");
                }
                if (unit.sheet.support && string.IsNullOrEmpty(unit.attachedTo)) issues.Add(unit.sheet.name + ": attach this support unit to a bodyguard.");
                if (!string.IsNullOrEmpty(unit.attachedTo))
                {
                    var target = army.units.Find(x => x.id == unit.attachedTo);
                    if (target == null || target == unit || !unit.sheet.bodyguards.Contains(target.sheet.id)) issues.Add(unit.sheet.name + ": invalid bodyguard attachment.");
                    if (target != null && target.enhancement != null && unit.enhancement != null) issues.Add(unit.sheet.name + ": attached unit has more than one enhancement.");
                }
            }
            foreach (var group in army.units.Where(x => !string.IsNullOrEmpty(x.attachedTo)).GroupBy(x => x.attachedTo))
                if (group.Count(x => x.sheet.support) > 1 || group.Count(x => !x.sheet.support) > 1) issues.Add("Multiple leaders or supports share a bodyguard; check attachment exceptions.");
            return issues;
        }
    }
}
