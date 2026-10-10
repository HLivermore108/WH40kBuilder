using System;
using System.Linq;
using NUnit.Framework;

namespace ArmyForge.Tests
{
    public class SecondaryMissionTests
    {
        [Test] public void DrawsAreUniqueAndExhaustionDoesNotRecycleDiscards()
        {
            var deck = SecondaryDeck.Create(new Random(1));
            Assert.That(deck.drawPile.Count, Is.EqualTo(18));
            Assert.That(deck.drawPile.Distinct().Count(), Is.EqualTo(18));
            var first = deck.Draw(2); deck.Discard(first[0]); deck.Draw(50);
            Assert.That(deck.drawPile.Count, Is.Zero);
            Assert.That(deck.active.Count, Is.EqualTo(17));
            Assert.That(deck.Draw(2), Is.Empty);
            Assert.That(deck.active.Concat(deck.discarded).Distinct().Count(), Is.EqualTo(18));
            Assert.That(deck.Redraw(first[1], false), Is.False);
            deck.ReshuffleDiscarded(new Random(2));
            Assert.That(deck.Draw(2).Single(), Is.EqualTo(first[0]));
        }
        [Test] public void CommandDrawHappensOncePerRoundAndRetainsPreviousHand()
        {
            var deck = SecondaryDeck.Create(new Random(1));
            Assert.That(deck.DrawCommandCards(1), Is.True);
            Assert.That(deck.DrawCommandCards(1), Is.False);
            Assert.That(deck.DrawCommandCards(2), Is.True);
            Assert.That(deck.active.Count, Is.EqualTo(4));
            Assert.That(deck.DrawCommandCards(1), Is.False);
        }
        [Test] public void ReplacementSpendsOneCpOnlyWhenEligibleAndOnlyOnce()
        {
            var battle = new Battle(); battle.EnsureDecks(); battle.playerDeck.Draw(2);
            string first = battle.playerDeck.active[0];
            Assert.That(battle.ReplaceSecondary(false, first), Is.False);
            battle.player.command = 2; battle.phase = 1;
            Assert.That(battle.ReplaceSecondary(false, first), Is.False);
            battle.phase = 0;
            Assert.That(battle.ReplaceSecondary(false, first), Is.True);
            Assert.That(battle.player.command, Is.EqualTo(1));
            Assert.That(battle.playerDeck.active.Count, Is.EqualTo(2));
            Assert.That(battle.playerDeck.discarded, Does.Contain(first));
            Assert.That(battle.ReplaceSecondary(false, battle.playerDeck.active[0]), Is.False);
            Assert.That(battle.player.command, Is.EqualTo(1));
        }
        [Test] public void CardRedrawCannotImmediatelyRedrawSameCardAndRoundTrips()
        {
            var battle = new Battle(); battle.EnsureDecks();
            var deck = battle.playerDeck;
            string first = deck.Draw(1).Single();
            Assert.That(deck.Redraw(first, true, new Random(1)), Is.True);
            Assert.That(deck.active, Does.Not.Contain(first));
            Assert.That(deck.drawPile, Does.Contain(first));
            var copy = LocalStore.Copy(battle);
            Assert.That(copy.playerDeck.drawPile, Is.EqualTo(deck.drawPile));
            Assert.That(copy.playerDeck.active, Is.EqualTo(deck.active));
            Assert.That(copy.opponentDeck.drawPile.Count, Is.EqualTo(18));
            Assert.That(copy.EnsureDecks(), Is.False);
        }
    }
}
