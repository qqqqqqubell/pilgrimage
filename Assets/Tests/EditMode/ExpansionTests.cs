using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Pilgrimage.Tests
{
    public sealed class ExpansionTests
    {
        private static GameSession Create(TimedEffectPolicy policy = null, FactionId faction = FactionId.Poverty)
        {
            var game = new GameSession(new DemoRuleAssumptions { shuffleInitialDeck = false,
                timedEffects = policy ?? new TimedEffectPolicy() }, humanFaction: faction);
            GameDebugTools.SetResources(game, 0, 100, 50, 50, 20);
            return game;
        }
        private static CardInstance Play(GameSession game, CardId id, int actor = 0, EffectTarget target = default)
        {
            var source = GameDebugTools.GiveCard(game, actor, id);
            Assert.That(game.CanPlay(actor, source.InstanceId, out string reason, target), Is.True, id + ": " + reason);
            Assert.That(game.TryPlay(actor, source.InstanceId, out reason, target), Is.True, reason);
            Assert.That(game.FindCard(source.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
            return source;
        }
        private static void Rewards(GameSession game)
        { while (game.Phase == GamePhase.RewardChoice) Assert.That(game.ChooseReward(game.RewardPlayerIndex, game.RewardOptions()[0].Id), Is.True); }
        private static void NextRound(GameSession game)
        {
            int round = game.Round;
            for (int i = 0; i < 9 && game.Round == round && game.Phase != GamePhase.Finished; i++)
                if (game.Phase == GamePhase.RewardChoice) Rewards(game);
                else Assert.That(game.EndActionTurn(game.ActivePlayerIndex), Is.True);
            Assert.That(game.Round != round || game.Phase == GamePhase.Finished, Is.True);
        }
        private static void HumanTurn(GameSession game)
        {
            for (int i = 0; i < 4 && game.ActivePlayerIndex != 0; i++) Assert.That(game.EndActionTurn(game.ActivePlayerIndex), Is.True);
            Assert.That(game.ActivePlayerIndex, Is.Zero);
        }
        private static CardInstance[] Cards(GameSession game) => game.Players.SelectMany(p => p.Hand.Concat(p.Deck).Concat(p.Discard).Concat(p.Removed)).ToArray();
        private static void Conserved(GameSession game, HashSet<Guid> known)
        {
            var ids = Cards(game).Select(c => c.InstanceId).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length)); Assert.That(known.IsSubsetOf(ids), Is.True);
            known.UnionWith(ids);
        }

        [Test] public void EveryFactionOccupiesHumanSeatWithThreeDistinctOpponentsAndUnchangedInitialDecks()
        {
            foreach (FactionId faction in new[] { FactionId.Poverty, FactionId.Gold, FactionId.Discipline, FactionId.Fur })
            {
                var game = new GameSession(humanFaction: faction);
                Assert.That(game.HumanFaction, Is.EqualTo(faction)); Assert.That(game.Players.Select(p => p.Faction).Distinct().Count(), Is.EqualTo(4));
                foreach (var player in game.Players)
                {
                    Assert.That(player.Name, Is.EqualTo(FactionSetup.Name(player.Faction)));
                    Assert.That(player.Offering, Is.EqualTo(5)); Assert.That(player.Faith, Is.EqualTo(50)); Assert.That(player.Doctrine, Is.EqualTo(50));
                    Assert.That(player.Hand.Count, Is.EqualTo(5)); Assert.That(player.TotalCardCount, Is.EqualTo(15));
                    var cards = player.Hand.Concat(player.Deck).ToArray();
                    Assert.That(cards.Count(c => c.Definition.Id == CardId.Follower), Is.EqualTo(5));
                    Assert.That(cards.Count(c => c.Definition.Id == CardId.Evangelism), Is.EqualTo(4));
                    Assert.That(cards.Count(c => c.Definition.Id == CardId.RequestOffering), Is.EqualTo(3));
                    Assert.That(cards.Count(c => c.Definition.Id == CardId.RegularWorship), Is.EqualTo(3));
                }
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameSession(humanFaction: FactionId.None));
        }

        [Test] public void PenanceResolvesAfterFiveFutureSettlementsExactlyOnce()
        {
            var game = Create(); var source = Play(game, CardId.Penance); var player = game.Players[0];
            Assert.That(player.Offering, Is.EqualTo(97)); Assert.That(player.Faith, Is.EqualTo(50));
            for (int i = 1; i <= 4; i++) { NextRound(game); Assert.That(player.Offering, Is.EqualTo(97 + 5 * i)); Assert.That(player.Faith, Is.EqualTo(50)); }
            NextRound(game);
            Assert.That(player.Offering, Is.EqualTo(128)); Assert.That(player.Faith, Is.EqualTo(60)); Assert.That(player.Doctrine, Is.EqualTo(60));
            Assert.That(player.TimedEffects, Is.Empty); Assert.That(game.FindCard(source.InstanceId).Card, Is.SameAs(source));
            NextRound(game); Assert.That(player.Offering, Is.EqualTo(133));
        }

        [Test] public void InvestmentResolvesAfterTwoSettlementsWithSeededInclusiveZeroToFiveRange()
        {
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 40; seed++)
            {
                var game = new GameSession(new DemoRuleAssumptions { shuffleInitialDeck = false, randomSeed = seed });
                Play(game, CardId.CaravanInvestment); NextRound(game);
                Assert.That(game.Players[0].Offering, Is.EqualTo(9));
                int reward = new Random(seed ^ 0x51A7).Next(0, 6); seen.Add(reward);
                NextRound(game); Assert.That(game.Players[0].Offering, Is.EqualTo(14 + reward));
                Assert.That(game.Players[0].TimedEffects, Is.Empty);
                NextRound(game); Assert.That(game.Players[0].Offering, Is.EqualTo(19 + reward));
            }
            Assert.That(seen, Is.EquivalentTo(Enumerable.Range(0, 6)));
        }

        [Test] public void ReflectionHalvesOnlyNextRoundIncomeAndFloorsRemainder()
        {
            var game = Create(); Play(game, CardId.SolitaryReflection); var player = game.Players[0];
            Assert.That(player.Faith, Is.EqualTo(60)); Assert.That(player.Doctrine, Is.EqualTo(60));
            Assert.That(game.PreviewRoundIncome(0), Is.EqualTo(2)); NextRound(game);
            Assert.That(player.Offering, Is.EqualTo(101)); Assert.That(player.TimedEffects, Is.Empty);
            NextRound(game); Assert.That(player.Offering, Is.EqualTo(106));
        }

        [Test] public void HumbleTempleRaisesBothStatsAndReducesExactlyTwoFutureIncomePayments()
        {
            var game = Create(); Play(game, CardId.HumbleTemple); var player = game.Players[0];
            Assert.That(player.Offering, Is.EqualTo(98)); Assert.That(player.Faith, Is.EqualTo(65)); Assert.That(player.Doctrine, Is.EqualTo(65));
            NextRound(game); Assert.That(player.Offering, Is.EqualTo(100));
            Assert.That(player.TimedEffects.Single().RemainingRoundStarts, Is.EqualTo(1));
            NextRound(game); Assert.That(player.Offering, Is.EqualTo(102)); Assert.That(player.TimedEffects, Is.Empty);
            NextRound(game); Assert.That(player.Offering, Is.EqualTo(107));
        }

        [Test] public void RemodelingDoublesIncomeAndCardDecreasesThenBothEffectsExpire()
        {
            var game = Create(); Play(game, CardId.TempleRemodeling); var player = game.Players[0];
            Assert.That(player.ChurchProperty, Is.EqualTo(16)); Assert.That(game.PreviewRoundIncome(0), Is.EqualTo(10));
            Play(game, CardId.RequestOffering); Assert.That(player.Faith, Is.EqualTo(40)); Assert.That(player.Doctrine, Is.EqualTo(40));
            Play(game, CardId.RegularWorship); Assert.That(player.Faith, Is.EqualTo(45));
            NextRound(game); Assert.That(player.Offering, Is.EqualTo(108));
            NextRound(game); Assert.That(player.Offering, Is.EqualTo(118)); Assert.That(player.TimedEffects, Is.Empty);
            HumanTurn(game); Play(game, CardId.RequestOffering); Assert.That(player.Faith, Is.EqualTo(40));
        }

        [Test] public void ArchbishopBlocksOnlyDecreasesIncludingEnemyEffectsAndOverridesRemodeling()
        {
            var game = Create(); var player = game.Players[0];
            GameDebugTools.SetResources(game, 0, 100, 20, 20, 20);
            Play(game, CardId.TempleRemodeling); Play(game, CardId.Archbishop);
            Play(game, CardId.RequestOffering); Assert.That(player.Faith, Is.EqualTo(20));
            Play(game, CardId.RegularWorship); Assert.That(player.Faith, Is.EqualTo(25)); Assert.That(player.Doctrine, Is.EqualTo(25));
            game.EndActionTurn(0); GameDebugTools.SetResources(game, 1, 100, 50, 50);
            Play(game, CardId.FalseRevelation, 1, new EffectTarget { PlayerIndex = 0 });
            Assert.That(player.Faith, Is.EqualTo(25)); Assert.That(game.Players[1].Offering, Is.EqualTo(98), "No refund when protection prevented the decrease.");
            NextRound(game); Assert.That(player.TimedEffects.Any(e => e.Kind == TimedEffectKind.PreventDecrease), Is.True);
            NextRound(game); Assert.That(player.TimedEffects.Count, Is.EqualTo(1));
            NextRound(game); Assert.That(player.TimedEffects, Is.Empty);
            HumanTurn(game); Play(game, CardId.RequestOffering); Assert.That(player.Faith, Is.EqualTo(20));
        }

        [Test] public void WitchHuntUsesConfiguredOwnPilePreservesGuidAndProtectsFaithfulFollowers()
        {
            foreach (var source in new[] { WitchHuntSource.OwnDeckTop, WitchHuntSource.OwnHandFirst })
            {
                var game = Create(new TimedEffectPolicy { witchHuntSource = source });
                var faithful = GameDebugTools.GiveCard(game, 0, CardId.FaithfulFollower, CardPile.Deck);
                var first = source == WitchHuntSource.OwnDeckTop ? GameDebugTools.GiveCard(game, 0, CardId.Follower, CardPile.Deck) : game.Players[0].Hand[0];
                var second = GameDebugTools.GiveCard(game, 0, CardId.Follower, CardPile.Deck);
                int followers = game.Players[0].FollowerCount, total = game.Players[0].TotalCardCount;
                Play(game, CardId.WitchHunt);
                Assert.That(first.Definition.Id, Is.EqualTo(CardId.Heretic)); Assert.That(game.FindCard(first.InstanceId).Card, Is.SameAs(first));
                Assert.That(second.Definition.Id, Is.EqualTo(CardId.Follower)); Assert.That(faithful.Definition.Id, Is.EqualTo(CardId.FaithfulFollower));
                Assert.That(game.Players[0].Offering, Is.EqualTo(103)); Assert.That(game.Players[0].FollowerCount, Is.EqualTo(followers - 1));
                Assert.That(game.Players[0].TotalCardCount, Is.EqualTo(total + 1));
            }
            var empty = Create(); var card = GameDebugTools.GiveCard(empty, 0, CardId.WitchHunt);
            Assert.That(empty.TryPlay(0, card.InstanceId, out _), Is.False); Assert.That(empty.Players[0].Offering, Is.EqualTo(100));
        }

        [Test] public void StackingOrderDurationAndPropertyPoliciesAreConfigurableAndCopied()
        {
            foreach (var stacking in new[] { EffectStacking.Independent, EffectStacking.RefreshSameCard })
            {
                var policy = new TimedEffectPolicy { stacking = stacking }; var game = Create(policy);
                policy.stacking = EffectStacking.Independent; // Editing settings must not mutate an existing game.
                Play(game, CardId.HumbleTemple); Play(game, CardId.HumbleTemple);
                Assert.That(game.PreviewRoundIncome(0), Is.EqualTo(stacking == EffectStacking.Independent ? 0 : 2));
                Assert.That(game.Players[0].TimedEffects.Count, Is.EqualTo(stacking == EffectStacking.Independent ? 2 : 1));
            }
            foreach (var order in new[] { IncomeOrder.AddThenMultiply, IncomeOrder.MultiplyThenAdd })
            {
                var game = Create(new TimedEffectPolicy { incomeOrder = order });
                Play(game, CardId.HumbleTemple); Play(game, CardId.TempleRemodeling);
                Assert.That(game.PreviewRoundIncome(0), Is.EqualTo(order == IncomeOrder.AddThenMultiply ? 4 : 7));
            }
            var shorter = Create(new TimedEffectPolicy { durationIncludesActivationRound = true });
            Play(shorter, CardId.HumbleTemple); NextRound(shorter); Assert.That(shorter.Players[0].TimedEffects, Is.Empty);
            var strict = Create(new TimedEffectPolicy { remodelingRequiresProperty = true }); GameDebugTools.SetResources(strict, 0, 100, 50, 50, 3);
            var remodel = GameDebugTools.GiveCard(strict, 0, CardId.TempleRemodeling);
            Assert.That(strict.TryPlay(0, remodel.InstanceId, out _), Is.False); Assert.That(strict.Players[0].Offering, Is.EqualTo(100));
            var lenient = Create(); GameDebugTools.SetResources(lenient, 0, 100, 50, 50, 0);
            Play(lenient, CardId.TempleRemodeling); Assert.That(lenient.Players[0].ChurchProperty, Is.EqualTo(-4));
        }

        [Test] public void AllThirtySixActionCardsExecuteThroughSharedRulesAndPreserveExistingGuids()
        {
            foreach (var def in CardCatalog.All.Where(c => c.IsPlayable))
            {
                var game = GameDebugTools.CreateCardLab(); GameDebugTools.SetResources(game, 0, def.Id == CardId.GlimmerOfHope ? 0 : 100, 70, 50, 20);
                for (int i = 0; i < 13; i++) GameDebugTools.GiveCard(game, 0, CardId.Follower, CardPile.Deck);
                var source = GameDebugTools.GiveCard(game, 0, def.Id);
                var known = new HashSet<Guid>(Cards(game).Select(c => c.InstanceId));
                var target = new EffectTarget { PlayerIndex = def.Id == CardId.Edict ? 0 : 1, CardInstanceId = game.Players[0].Deck[0].InstanceId };
                Assert.That(game.CanPlay(0, source.InstanceId, out string reason, target), Is.True, def.Name + ": " + reason);
                Assert.That(game.TryPlay(0, source.InstanceId, out reason, target), Is.True, reason);
                Assert.That(game.TryPlay(0, source.InstanceId, out _, target), Is.False); Conserved(game, known);
            }
            Assert.That(CardCatalog.All.Count(c => c.IsPlayable), Is.EqualTo(36));
        }

        [Test] public void AllSevenNewEffectsAreAiCandidatesAndExecutableWithConfiguredStrategy()
        {
            foreach (var id in new[] { CardId.Penance, CardId.SolitaryReflection, CardId.CaravanInvestment, CardId.WitchHunt,
                CardId.HumbleTemple, CardId.TempleRemodeling, CardId.Archbishop })
            {
                var game = Create(); var card = GameDebugTools.GiveCard(game, 0, id);
                GameDebugTools.GiveCard(game, 0, CardId.Follower, CardPile.Deck);
                var settings = new AiSettings { randomSeed = 1, similarScoreWindow = 0, factions = new FactionAiProfile[0],
                    defaultWeights = new AiWeights { offering = 1, cost = 0, followers = 0, faith = 5, doctrine = 5,
                        factionAbility = id == CardId.TempleRemodeling ? 0 : 10 } };
                var ai = new RuleBasedAi(settings); Assert.That(ai.EvaluateActions(new AiObservation(game, 0)).Any(c => c.CardInstanceId == card.InstanceId), Is.True);
                var turn = new AiTurn(game, 0, ai, settings); var choice = turn.Next(game);
                Assert.That(choice, Is.Not.Null, id.ToString()); Assert.That(choice.CardInstanceId, Is.EqualTo(card.InstanceId));
                Assert.That(game.TryPlay(0, choice.CardInstanceId, out _, choice.Target), Is.True); Assert.That(turn.Next(game), Is.Null);
            }
        }

        [Test] public void EveryHumanFactionReceivesOwnRoundEightRewardThenNaturallyDrawsAndUsesIt()
        {
            foreach (var faction in new[] { FactionId.Poverty, FactionId.Gold, FactionId.Discipline, FactionId.Fur })
            {
                var game = Create(faction: faction);
                for (int round = 1; round < 8; round++) NextRound(game);
                for (int turn = 0; turn < 4; turn++) game.EndActionTurn(game.ActivePlayerIndex);
                while (game.RewardPlayerIndex != 0) game.ChooseReward(game.RewardPlayerIndex, game.RewardOptions()[0].Id);
                Assert.That(game.CurrentRewardKind, Is.EqualTo(RewardKind.Faction));
                Assert.That(game.RewardOptions().Count, Is.EqualTo(4)); Assert.That(game.RewardOptions().All(c => c.Faction == faction), Is.True);
                var id = faction == FactionId.Gold ? CardId.Indulgence : faction == FactionId.Poverty ? CardId.HumbleTemple : faction == FactionId.Discipline ? CardId.Archbishop : CardId.MoltingRitual;
                var existing = new HashSet<Guid>(Cards(game).Select(c => c.InstanceId));
                Assert.That(game.ChooseReward(0, id), Is.True);
                var reward = game.Players[0].Discard.Single(c => !existing.Contains(c.InstanceId));
                Assert.That(reward.Definition.Id, Is.EqualTo(id)); Rewards(game);
                for (int n = 0; n < 12 && !game.Players[0].Hand.Contains(reward); n++) NextRound(game);
                Assert.That(game.Players[0].Hand.Contains(reward), Is.True, faction.ToString()); HumanTurn(game);
                GameDebugTools.SetResources(game, 0, 50, 50, 50, 20);
                GameDebugTools.GiveCard(game, 0, CardId.Follower, CardPile.Deck); GameDebugTools.GiveCard(game, 0, CardId.Follower, CardPile.Deck);
                int before = game.Players[0].FollowerCount;
                Assert.That(game.TryPlay(0, reward.InstanceId, out _), Is.True);
                Assert.That(game.FindCard(reward.InstanceId).Card, Is.SameAs(reward)); Assert.That(game.FindCard(reward.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
                if (faction == FactionId.Gold)
                {
                    Assert.That(game.Players[0].Offering, Is.EqualTo(56)); Assert.That(game.Players[0].ChurchProperty, Is.EqualTo(26));
                    Assert.That(game.Players[0].FollowerCount, Is.EqualTo(before - 2)); Assert.That(game.Players[0].Faith, Is.EqualTo(40));
                }
                Assert.That(game.EndActionTurn(0), Is.True); Assert.That(game.ActivePlayerIndex, Is.Not.Zero);
            }
        }

        [Test] public void FourFactionSelectionsCompleteFortyRoundAiGamesWithTimedCardsAndNoIdentityLoss()
        {
            foreach (var faction in new[] { FactionId.Poverty, FactionId.Gold, FactionId.Discipline, FactionId.Fur })
            {
                var game = new GameSession(humanFaction: faction); var settings = new AiSettings { randomSeed = 7, actionSafetyLimit = 40 };
                var agents = Enumerable.Range(0, 4).Select(i => new RuleBasedAi(settings, i)).ToArray();
                for (int owner = 0; owner < 4; owner++)
                {
                    GameDebugTools.SetResources(game, owner, 50, 40, 40, 20);
                    foreach (var id in new[] { CardId.Penance, CardId.SolitaryReflection, CardId.CaravanInvestment, CardId.WitchHunt }) GameDebugTools.GiveCard(game, owner, id);
                    GameDebugTools.GiveCard(game, owner, CardId.Follower, CardPile.Deck);
                }
                var known = new HashSet<Guid>(); Conserved(game, known); int steps = 0;
                while (game.Phase != GamePhase.Finished && steps++ < 250)
                {
                    if (game.Phase == GamePhase.RewardChoice) { int owner = game.RewardPlayerIndex; var reward = agents[owner].ChooseReward(new AiObservation(game, owner), game.RewardOptions()); Assert.That(game.ChooseReward(owner, reward.Id), Is.True); }
                    else
                    {
                        int owner = game.ActivePlayerIndex; var turn = new AiTurn(game, owner, agents[owner], settings);
                        for (var decision = turn.Next(game); decision != null; decision = turn.Next(game))
                        { Assert.That(game.TryPlay(owner, decision.CardInstanceId, out _, decision.Target), Is.True); Conserved(game, known); }
                        Assert.That(turn.Attempts, Is.LessThanOrEqualTo(40)); Assert.That(game.EndActionTurn(owner), Is.True);
                    }
                    Conserved(game, known);
                    foreach (var player in game.Players) { Assert.That(player.Faith, Is.InRange(0, 100)); Assert.That(player.Doctrine, Is.InRange(0, 100)); Assert.That(player.Offering, Is.GreaterThanOrEqualTo(0)); }
                }
                Assert.That(game.Phase, Is.EqualTo(GamePhase.Finished), faction.ToString()); Assert.That(game.Round, Is.EqualTo(40));
            }
        }
    }
}
