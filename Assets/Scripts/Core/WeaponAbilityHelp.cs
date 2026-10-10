using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyForge
{
    public static class WeaponAbilityHelp
    {
        public const string Source = "https://wahapedia.ru/wh40k11ed/the-rules/core-rules/#Core-Abilities";
        // Concise reminders checked against 11th-edition core rules, 2026-10-10.
        // Parameter values and target qualifiers remain on the clickable label.
        static readonly Dictionary<string, string> Reminders = new Dictionary<string, string> {
            { "ANTI", "Against the specified target keyword, an unmodified wound roll meeting the listed threshold is critical." },
            { "ASSAULT", "Enables assault shooting after advancing, while unengaged." },
            { "BLAST", "Add X attacks per complete five target models counted when selecting targets. Without a number, X is 1. Cannot target engaged units. Example: Blast 2 against 15 models adds 6 attacks." },
            { "CLEAVE", "If all this weapon’s attacks target one unit, add X attacks per complete five target models." },
            { "CLOSE-QUARTERS", "Enables close-quarters shooting. For ordinary shooting, non-MONSTER/VEHICLE models choose these or other ranged weapons." },
            { "PISTOL", "Uses the Close-quarters rules." },
            { "DEVASTATING WOUNDS", "Critical wounds cause damage as mortal wounds after normal damage; excess cannot spill between models." },
            { "EXTRA ATTACKS", "Fight with these alongside your chosen melee weapon." },
            { "HAZARDOUS", "After attacking, make one hazard roll per selected hazardous weapon." },
            { "HEAVY", "Shooting: +1 hit if unengaged, not newly deployed, and every model moved 3″ or less." },
            { "IGNORES COVER", "The target receives no cover benefit." },
            { "INDIRECT FIRE", "Enables indirect shooting; its targeting and accuracy restrictions apply." },
            { "LANCE", "Charging this turn grants +1 wound." },
            { "LETHAL HITS", "Critical hits may wound automatically, without a wound roll." },
            { "MELTA", "Within half range, add X damage." },
            { "ONE SHOT", "Each weapon fires once per battle." },
            { "PRECISION", "You may prioritise a visible CHARACTER’s allocation group." },
            { "PSYCHIC", "May ignore BS/WS and hit-roll modifiers." },
            { "RAPID FIRE", "Within half range, add X attacks." },
            { "SUSTAINED HITS", "Each critical hit generates X extra hits. Sustained Hits 1 therefore produces two hits in total; Sustained Hits 2 produces three." },
            { "TORRENT", "Hits automatically. Cannot have Indirect Fire or Precision." },
            { "TWIN-LINKED", "You may reroll wound rolls." }
        };
        public static List<string> Split(string rules) => (rules ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().Trim('[', ']')).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        public static string Explain(string label, UnitDefinition sheet)
        {
            string key = label.Trim().ToUpperInvariant();
            var entry = Reminders.OrderByDescending(x => x.Key.Length).FirstOrDefault(x => key == x.Key || key.StartsWith(x.Key + " ") || key.StartsWith(x.Key + ":") || (x.Key == "ANTI" && key.StartsWith("ANTI-")));
            if (entry.Key != null)
            {
                var parts = label.Split(':');
                return entry.Value + (parts.Length > 1 ? "\n\nTarget restriction: " + parts[1].Trim() + "." : "");
            }
            var rule = sheet?.abilities.FirstOrDefault(x => x.name.Equals(label, StringComparison.OrdinalIgnoreCase) || x.name.StartsWith(label + " (", StringComparison.OrdinalIgnoreCase));
            return rule?.text ?? "This is a datasheet-specific weapon ability. Open this unit’s source for its rule.";
        }
    }
}
