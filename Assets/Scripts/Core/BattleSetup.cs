using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyForge
{
    [Serializable] public class BattleSetup
    {
        public string playerPrimary = "Battlefield Dominance", opponentPrimary = "Battlefield Dominance", deployment = "Dawn of War";
    }
    public static class MissionChoices
    {
        public const string Source = "https://wahapedia.ru/wh40k11ed/the-rules/warhammer-event-companion/";
        public static readonly string[] Primaries = {
            "Battlefield Dominance", "Immovable Object", "Determined Acquisition", "Purge and Secure", "Inescapable Dominion", "Unstoppable Force",
            "Meatgrinder", "Punishment", "Consecrate", "Destroyer's Wrath", "Death Trap", "Delaying Action", "Outmanoeuvre", "Smoke and Mirrors",
            "Locate and Deny", "Reconnaissance Sweep", "Triangulation", "Surveil the Foe", "Gather Intel", "Search and Scour", "Secure Asset", "Vital Link", "Extract Relic", "Vanguard Operation", "Sabotage"
        };
        public static readonly string[] Deployments = { "Tipping Point", "Sweeping Engagement", "Search and Destroy", "Hammer and Anvil", "Dawn of War", "Crucible of Battle" };
        // Each press deals a different choice, without changing the other selectors.
        public static string Shuffle(IEnumerable<string> choices, string current, Random random = null)
        {
            var pool = choices.Where(x => x != current).ToArray();
            return pool.Length == 0 ? current : pool[(random ?? new Random(Guid.NewGuid().GetHashCode())).Next(pool.Length)];
        }
    }
}
