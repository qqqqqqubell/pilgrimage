using System;
using System.Linq;
using NUnit.Framework;

namespace Pilgrimage.Tests
{
    public sealed class GameSessionTests
    {
        private static GameSession Create() => new GameSession(new DemoRuleAssumptions { shuffleInitialDeck = false });
        private static CardInstance Give(GameSession game, CardId id, int player = 0)
            => GameDebugTools.GiveCard(game, player, id);
        private static void EndRound(GameSession game)
        { for (int i = 0; i < 4; i++) Assert.That(game.EndActionTurn(game.ActivePlayerIndex), Is.True); }
        private static void ResolveRewards(GameSession game)
        {
            while (game.Phase == GamePhase.RewardChoice)
                Assert.That(game.ChooseReward(game.RewardPlayerIndex, game.RewardOptions()[0].Id), Is.True);
        }

        [Test] public void LatestCatalogHasTwentyTwoGeneralAndSixteenFactionCards()
        {
            Assert.That(CardCatalog.All.Count, Is.EqualTo(38));
            Assert.That(CardCatalog.All.Count(c => c.Faction == FactionId.None), Is.EqualTo(22));
            foreach (FactionId faction in new[] { FactionId.Poverty, FactionId.Gold, FactionId.Discipline, FactionId.Fur })
                Assert.That(CardCatalog.All.Count(c => c.Faction == faction), Is.EqualTo(4));
            Assert.That(CardCatalog.All.Any(c => c.Name == "신앙의 결속"), Is.True);
            Assert.That(CardCatalog.Get(CardId.RequestOffering).Cost, Is.Zero);
            Assert.That(CardCatalog.Get(CardId.ClergyTraining).Cost, Is.EqualTo(2));
            Assert.That(CardCatalog.Get(CardId.Confession).Cost, Is.EqualTo(5));
            Assert.That(CardCatalog.Get(CardId.Archbishop).Name, Is.EqualTo("대주교"));
            Assert.That(CardCatalog.Get(CardId.FaithfulFollower).Cost, Is.EqualTo(-1));
            Assert.That(CardCatalog.Get(CardId.TempleRemodeling).ChurchPropertyChange, Is.EqualTo(-4));
            Assert.That(CardCatalog.All.All(c => !string.IsNullOrEmpty(c.FlavorText)), Is.True);
        }

        [Test] public void InitialFourPlayersHaveConfirmedResourcesAndFifteenCards()
        {
            GameSession game = Create();
            Assert.That(game.Atheists, Is.EqualTo(100));
            foreach (PlayerState player in game.Players)
            {
                Assert.That(player.Offering, Is.EqualTo(5)); Assert.That(player.Faith, Is.EqualTo(50));
                Assert.That(player.Doctrine, Is.EqualTo(50)); Assert.That(player.FollowerCount, Is.EqualTo(5));
                Assert.That(player.Hand.Count, Is.EqualTo(5)); Assert.That(player.Deck.Count, Is.EqualTo(10));
                var cards = player.Hand.Concat(player.Deck).ToArray();
                Assert.That(cards.Count(c => c.Definition.Id == CardId.Evangelism), Is.EqualTo(4));
                Assert.That(cards.Count(c => c.Definition.Id == CardId.RequestOffering), Is.EqualTo(3));
                Assert.That(cards.Count(c => c.Definition.Id == CardId.RegularWorship), Is.EqualTo(3));
            }
            Assert.That(game.Players.SelectMany(p => p.Hand.Concat(p.Deck)).Select(c => c.InstanceId).Distinct().Count(), Is.EqualTo(60));
        }

        [Test] public void FirstPlayerRotatesAndWrongActorCannotPlay()
        {
            GameSession game = Create(); CardInstance wrong = Give(game, CardId.RegularWorship, 1);
            Assert.That(game.TryPlay(1, wrong.InstanceId, out _), Is.False);
            EndRound(game);
            Assert.That(game.Round, Is.EqualTo(2)); Assert.That(game.FirstPlayerIndex, Is.EqualTo(1));
            foreach (PlayerState p in game.Players) Assert.That(p.Offering, Is.EqualTo(10));
            int[] order = { 1, 2, 3, 0 };
            foreach (int actor in order) { Assert.That(game.ActivePlayerIndex, Is.EqualTo(actor)); game.EndActionTurn(actor); }
            Assert.That(game.FirstPlayerIndex, Is.EqualTo(2));
        }

        [Test] public void FortyRoundsHaveCorrectRewardScheduleAndStopAfterFinalReward()
        {
            GameSession game = Create(); int general = 0, faction = 0;
            for (int round = 1; round <= 40; round++)
            {
                Assert.That(game.Round, Is.EqualTo(round)); EndRound(game);
                if (round % 4 != 0) continue;
                Assert.That(game.Phase, Is.EqualTo(GamePhase.RewardChoice));
                if (round % 8 == 0) { faction++; Assert.That(game.RewardOptions().Count, Is.EqualTo(4)); }
                else { general++; Assert.That(game.RewardOptions().Count, Is.EqualTo(20)); }
                foreach (CardDefinition def in game.RewardOptions())
                    Assert.That(def.Category == CardCategory.Follower || def.Category == CardCategory.Heretic, Is.False);
                ResolveRewards(game);
            }
            Assert.That(general, Is.EqualTo(5)); Assert.That(faction, Is.EqualTo(5));
            Assert.That(game.Phase, Is.EqualTo(GamePhase.Finished)); Assert.That(game.Round, Is.EqualTo(40));
            Assert.That(game.EndActionTurn(game.ActivePlayerIndex), Is.False);
        }

        [Test] public void WorshipAndOfferingPreserveLegacyEffectsAndClampStats()
        {
            GameSession game = Create(); GameDebugTools.SetResources(game, 0, 5, 98, 99);
            var worship = Give(game, CardId.RegularWorship);
            Assert.That(game.TryPlay(0, worship.InstanceId, out _), Is.True);
            Assert.That(game.Players[0].Faith, Is.EqualTo(100)); Assert.That(game.Players[0].Doctrine, Is.EqualTo(100));
            GameDebugTools.SetResources(game, 0, 0, 1, 2);
            var offering = Give(game, CardId.RequestOffering);
            Assert.That(game.TryPlay(0, offering.InstanceId, out _), Is.True);
            Assert.That(game.Players[0].Offering, Is.EqualTo(2)); Assert.That(game.Players[0].Faith, Is.Zero); Assert.That(game.Players[0].Doctrine, Is.Zero);
        }

        [Test] public void EvangelismAddsTwoFollowersToDeckAndChangesOnlyActor()
        {
            GameSession game = Create(); var card = Give(game, CardId.Evangelism);
            Assert.That(game.TryPlay(0, card.InstanceId, out _), Is.True);
            Assert.That(game.Atheists, Is.EqualTo(98)); Assert.That(game.Players[0].FollowerCount, Is.EqualTo(7));
            Assert.That(game.Players[0].Faith, Is.EqualTo(48)); Assert.That(game.Players[1].Faith, Is.EqualTo(50));
            Assert.That(game.FindCard(card.InstanceId).Card, Is.SameAs(card));
        }

        [Test] public void FaithfulFollowerCountsButCannotBeTransformedRemovedOrStolen()
        {
            GameSession game = Create(); var faithful = GameDebugTools.GiveCard(game, 1, CardId.FaithfulFollower, CardPile.Deck);
            Assert.That(game.Players[1].FollowerCount, Is.EqualTo(6));
            foreach (CardId id in new[] { CardId.Reaper, CardId.PublicDenunciation, CardId.AggressiveMission, CardId.PoorRelief })
            {
                GameDebugTools.SetResources(game, 0, 50, 80, 80);
                CardInstance card = Give(game, id);
                Assert.That(game.TryPlay(0, card.InstanceId, out _, new EffectTarget { PlayerIndex = 1 }), Is.False);
                Assert.That(game.FindCard(faithful.InstanceId).Owner, Is.EqualTo(1));
                Assert.That(faithful.Definition.Id, Is.EqualTo(CardId.FaithfulFollower));
            }
        }

        [Test] public void HereticUseConvertsSameObjectAndEndTurnConvertsTopEligibleDeckFollower()
        {
            GameSession game = Create(); var heretic = Give(game, CardId.Heretic);
            Assert.That(game.TryPlay(0, heretic.InstanceId, out _), Is.True);
            Assert.That(heretic.Definition.Id, Is.EqualTo(CardId.Follower));
            Assert.That(game.FindCard(heretic.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
            var follower = GameDebugTools.GiveCard(game, 0, CardId.Follower, CardPile.Deck);
            Give(game, CardId.Heretic); game.EndActionTurn(0);
            Assert.That(follower.Definition.Id, Is.EqualTo(CardId.Heretic));
        }

        [Test] public void StealingAndConvertingPreserveInstanceIdentity()
        {
            GameSession game = Create(); var target = GameDebugTools.GiveCard(game, 1, CardId.Follower, CardPile.Deck);
            var mission = Give(game, CardId.AggressiveMission);
            Assert.That(game.TryPlay(0, mission.InstanceId, out _, new EffectTarget { PlayerIndex = 1 }), Is.True);
            Assert.That(game.Players[0].Offering, Is.EqualTo(3));
            Assert.That(game.FindCard(target.InstanceId).Owner, Is.Zero);
            Assert.That(game.FindCard(target.InstanceId).Card, Is.SameAs(target));
            var ritual = Give(game, CardId.MoltingRitual); GameDebugTools.GiveCard(game, 0, CardId.Follower, CardPile.Deck);
            GameDebugTools.SetResources(game, 0, 50, 80, 80);
            Assert.That(game.TryPlay(0, ritual.InstanceId, out _), Is.True);
            Assert.That(target.Definition.Id, Is.EqualTo(CardId.FaithfulFollower));
        }

        [Test] public void InterestHasNoOncePerTurnRestrictionAndRemodelingCannotPretendToExecute()
        {
            GameSession game = Create(); GameDebugTools.SetResources(game, 0, 5, 50, 50, 29);
            for (int i = 0; i < 2; i++) Assert.That(game.TryPlay(0, Give(game, CardId.Interest).InstanceId, out _), Is.True);
            Assert.That(game.Players[0].Offering, Is.EqualTo(9));
            Assert.That(game.TryPlay(0, Give(game, CardId.TempleRemodeling).InstanceId, out string reason), Is.False);
            Assert.That(reason, Does.Contain("사용")); Assert.That(game.Players[0].ChurchProperty, Is.EqualTo(29));
        }

        [Test] public void PublicDenunciationTracksRemovedObjectAndIncreasesAtheists()
        {
            GameSession game = Create(); var target = GameDebugTools.GiveCard(game, 1, CardId.Follower, CardPile.Deck);
            Assert.That(game.TryPlay(0, Give(game, CardId.PublicDenunciation).InstanceId, out _, new EffectTarget { PlayerIndex = 1 }), Is.True);
            Assert.That(game.FindCard(target.InstanceId).Pile, Is.EqualTo(CardPile.Removed));
            Assert.That(game.Players[1].FollowerCount, Is.EqualTo(5)); Assert.That(game.Atheists, Is.EqualTo(101));
        }

        [Test] public void RoarRequiresSeventyFaithAndAddsHereticsOnEachOpponentsDeckTop()
        {
            GameSession game = Create(); var roar = Give(game, CardId.WildRoar);
            Assert.That(game.TryPlay(0, roar.InstanceId, out _), Is.False);
            GameDebugTools.SetResources(game, 0, 5, 70, 50);
            Assert.That(game.TryPlay(0, roar.InstanceId, out _), Is.True);
            Assert.That(game.Atheists, Is.EqualTo(97));
            for (int i = 1; i < 4; i++) Assert.That(game.Players[i].Deck[0].Definition.Id, Is.EqualTo(CardId.Heretic));
        }

        [Test] public void ReshufflingPreservesAllOriginalIdentitiesAcrossManyRounds()
        {
            GameSession game = Create();
            var ids = game.Players.SelectMany(p => p.Hand.Concat(p.Deck)).Select(c => c.InstanceId).ToArray();
            for (int i = 0; i < 3; i++) EndRound(game);
            var actual = game.Players.SelectMany(p => p.Hand.Concat(p.Deck).Concat(p.Discard)).Select(c => c.InstanceId).ToArray();
            Assert.That(actual, Is.EquivalentTo(ids)); Assert.That(actual.Distinct().Count(), Is.EqualTo(60));
        }

        [Test] public void PartialDrawKeepsRemainingDeckThenReshufflesOnlyForMissingCards()
        {
            GameSession game = Create();
            CardInstance training = Give(game, CardId.ClergyTraining);
            Assert.That(game.TryPlay(0, training.InstanceId, out _), Is.True);
            EndRound(game); EndRound(game);
            Assert.That(game.Round, Is.EqualTo(3));
            var lastTwo = game.Players[0].Deck.ToArray();
            Assert.That(lastTwo.Length, Is.EqualTo(2));
            var originalIds = game.Players[0].Deck.Concat(game.Players[0].Hand).Concat(game.Players[0].Discard).Select(c => c.InstanceId).ToArray();
            EndRound(game);
            PlayerState player = game.Players[0];
            Assert.That(game.Round, Is.EqualTo(4));
            Assert.That(player.Hand.Count, Is.EqualTo(5));
            Assert.That(player.Hand.Take(2), Is.EqualTo(lastTwo));
            Assert.That(player.Discard, Is.Empty);
            Assert.That(player.Deck.Count, Is.EqualTo(13));
            var afterIds = player.Deck.Concat(player.Hand).Concat(player.Discard).Select(c => c.InstanceId).ToArray();
            Assert.That(afterIds, Is.EquivalentTo(originalIds));
            Assert.That(afterIds.Distinct().Count(), Is.EqualTo(originalIds.Length));
        }

        [Test] public void PlayedCardCannotBeReusedAndRoundCleanupDoesNotDiscardItTwice()
        {
            GameSession game = Create();
            CardInstance worship = Give(game, CardId.RegularWorship);
            var initial = game.Players.SelectMany(p => p.Deck.Concat(p.Hand).Concat(p.Discard).Concat(p.Removed)).Select(c => c.InstanceId).ToArray();
            Assert.That(game.TryPlay(0, worship.InstanceId, out _), Is.True);
            Assert.That(game.TryPlay(0, worship.InstanceId, out _), Is.False);
            Assert.That(game.Players[0].Offering, Is.EqualTo(4));
            Assert.That(game.Players[0].Faith, Is.EqualTo(55));
            Assert.That(game.FindCard(worship.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
            EndRound(game);
            var actual = game.Players.SelectMany(p => p.Deck.Concat(p.Hand).Concat(p.Discard).Concat(p.Removed)).ToArray();
            Assert.That(actual.Select(c => c.InstanceId), Is.EquivalentTo(initial));
            Assert.That(actual.Count(c => c.InstanceId == worship.InstanceId), Is.EqualTo(1));
            Assert.That(actual.Single(c => c.InstanceId == worship.InstanceId), Is.SameAs(worship));
        }

        [Test] public void FanLayoutHandlesOneFiveAndLargeHandsWithoutOverflow()
        {
            foreach (int count in new[] { 1, 5, 7, 31 })
            {
                var slots = Enumerable.Range(0, count).Select(i => HandFanLayout.Calculate(i, count, 1300, 200)).ToArray();
                Assert.That(slots.All(s => Math.Abs(s.X) <= 550), Is.True);
                Assert.That(slots.All(s => !float.IsNaN(s.X)), Is.True);
                Assert.That(slots[0].X, Is.EqualTo(-slots[count - 1].X).Within(0.01));
            }
        }
    }
}
