using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyForge
{
    public class SecondaryCard
    {
        public string id, name, cue, redraw;
        public SecondaryCard(string id, string name, string cue, string redraw = "")
        { this.id = id; this.name = name; this.cue = cue; this.redraw = redraw; }
    }
    public static class SecondaryMissions
    {
        public const string DeckName = "Chapter Approved 2026–27 · Tactical";
        public const string SourceUrl = "https://wahapedia.ru/wh40k11ed/the-rules/warhammer-event-companion/#Secondary-Mission-deck";
        // Short reminders, not replacement card rules. Source checked 2026-10-10.
        public static readonly SecondaryCard[] Cards = {
            new SecondaryCard("no-prisoners", "No Prisoners", "Eliminate opposing units."),
            new SecondaryCard("overwhelming-force", "Overwhelming Force", "Destroy units that began on objectives."),
            new SecondaryCard("plunder", "Plunder", "Perform the Plunder action.", "shuffle"),
            new SecondaryCard("display-of-might", "Display of Might", "Outnumber enemies in No Man’s Land."),
            new SecondaryCard("outflank", "Outflank", "Establish qualifying positions near battlefield edges."),
            new SecondaryCard("beacon", "Beacon", "Nominate and position your beacon unit."),
            new SecondaryCard("cleanse", "Cleanse", "Perform Cleanse actions on objectives.", "shuffle"),
            new SecondaryCard("a-grievous-blow", "A Grievous Blow", "Destroy units with starting strength 13+.", "discard"),
            new SecondaryCard("defend-stronghold", "Defend Stronghold", "Defend your home objective.", "shuffle"),
            new SecondaryCard("engage-on-all-fronts", "Engage on All Fronts", "Establish qualifying presence across battlefield quarters."),
            new SecondaryCard("secure-no-mans-land", "Secure No Man’s Land", "Control qualifying No Man’s Land objectives."),
            new SecondaryCard("forward-position", "Forward Position", "Capture forward objectives.", "shuffle"),
            new SecondaryCard("centre-ground", "Centre Ground", "Secure the battlefield centre."),
            new SecondaryCard("assassination", "Assassination", "Eliminate enemy characters."),
            new SecondaryCard("a-tempting-target", "A Tempting Target", "Capture the objective your opponent nominates."),
            new SecondaryCard("behind-enemy-lines", "Behind Enemy Lines", "Position units in the enemy deployment zone.", "shuffle"),
            new SecondaryCard("bring-it-down", "Bring It Down", "Destroy enemy models with 10+ wounds.", "discard"),
            new SecondaryCard("burden-of-trust", "Burden of Trust", "Guard objectives with nominated units.")
        };
        public static SecondaryCard Find(string id) => Cards.First(x => x.id == id);
    }
    [Serializable] public class SecondaryDeck
    {
        public string version = SecondaryMissions.DeckName;
        public List<string> drawPile = new List<string>(), active = new List<string>(), discarded = new List<string>();
        public int lastDrawRound;
        public bool commandRedrawUsed;
        public static SecondaryDeck Create(Random random = null)
        {
            var deck = new SecondaryDeck { drawPile = SecondaryMissions.Cards.Select(x => x.id).ToList() };
            deck.Shuffle(random); return deck;
        }
        void Shuffle(Random random = null)
        {
            random = random ?? new Random(Guid.NewGuid().GetHashCode());
            for (int i = drawPile.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var temp = drawPile[i]; drawPile[i] = drawPile[j]; drawPile[j] = temp; }
        }
        public List<string> Draw(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var drawn = drawPile.Take(count).ToList();
            drawPile.RemoveRange(0, drawn.Count); active.AddRange(drawn); return drawn;
        }
        public bool DrawCommandCards(int round)
        {
            if (round < 1 || lastDrawRound >= round || drawPile.Count == 0) return false;
            Draw(2); lastDrawRound = round; return true;
        }
        public bool Discard(string id)
        {
            if (!active.Remove(id)) return false;
            discarded.Add(id); return true;
        }
        public bool Redraw(string id, bool shuffleBack, Random random = null)
        {
            if (!active.Contains(id) || drawPile.Count == 0) return false;
            active.Remove(id); Draw(1);
            if (shuffleBack) { drawPile.Add(id); Shuffle(random); }
            else discarded.Add(id);
            return true;
        }
        public void ReshuffleDiscarded(Random random = null)
        { drawPile.AddRange(discarded); discarded.Clear(); Shuffle(random); }
    }
}
