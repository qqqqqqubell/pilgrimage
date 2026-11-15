using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Pilgrimage.Tests
{
    internal static class BalanceTestSupport
    {
        internal static GameSession Game(bool funded = true, FactionId faction = FactionId.Poverty, TimedEffectPolicy policy = null)
        {
            var game = new GameSession(new DemoRuleAssumptions { shuffleInitialDeck = false, timedEffects = policy ?? new TimedEffectPolicy() }, humanFaction: faction);
            if (funded) for (int i = 0; i < 4; i++) Fund(game, i);
            return game;
        }
        internal static void Fund(GameSession game, int owner = 0, int offering = 200, int faith = 50, int doctrine = 50, int property = 20)
            => GameDebugTools.SetResources(game, owner, offering, faith, doctrine, property);
        internal static CardInstance Give(GameSession game, CardId id, int owner = 0, CardPile pile = CardPile.Hand)
            => GameDebugTools.GiveCard(game, owner, id, pile);
        internal static CardInstance Play(GameSession game, CardId id, int owner = 0, EffectTarget target = default)
        {
            var card = Give(game, id, owner);
            Assert.That(game.TryPlay(owner, card.InstanceId, out string message, target), Is.True, id + ": " + message);
            Assert.That(game.FindCard(card.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
            return card;
        }
        internal static CardInstance Follower(GameSession game, int owner = 0)
            => game.Players[owner].Deck.First(c => c.Definition.CountsAsFollower && !c.Definition.ProtectedFromCardEffects);
        internal static void NextRound(GameSession game)
        {
            int before = game.Round;
            for (int i = 0; i < 8 && game.Round == before && game.Phase != GamePhase.Finished; i++)
                if (game.Phase == GamePhase.RewardChoice) Assert.That(game.AcknowledgeRewards(), Is.True);
                else Assert.That(game.EndActionTurn(game.ActivePlayerIndex), Is.True);
            Assert.That(game.Round != before || game.Phase == GamePhase.Finished, Is.True);
        }
        internal static void HumanTurn(GameSession game)
        { for (int i = 0; i < 4 && game.ActivePlayerIndex != 0; i++) game.EndActionTurn(game.ActivePlayerIndex); Assert.That(game.ActivePlayerIndex, Is.Zero); }
        internal static CardInstance[] Cards(GameSession game)
            => game.Players.SelectMany(p => p.Hand.Concat(p.Deck).Concat(p.Discard).Concat(p.Removed)).ToArray();
        internal static void Conserved(GameSession game, HashSet<Guid> known = null)
        {
            var ids = Cards(game).Select(c => c.InstanceId).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            if (known != null) { Assert.That(known.IsSubsetOf(ids), Is.True); known.UnionWith(ids); }
            Assert.That(game.PopulationIsConserved, Is.True, "Population: " + game.PopulationTotal + " expected " + (120 + game.DebugPopulationDelta));
        }
    }
}
