using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Pilgrimage.Tests
{
    public sealed class AiStrategyTests
    {
        [Test] public void CardLabPresetsAndPassiveOnlyHandsDoNotChangeNormalGame()
        {
            var regular = new GameSession();
            var original = regular.Players.SelectMany(p => p.Hand.Concat(p.Deck)).Select(c => c.InstanceId).ToArray();
            var lab = GameDebugTools.CreateCardLab();
            Assert.That(lab.Players[0].Hand.Select(c => c.Definition.Id), Is.EqualTo(GameDebugTools.FiveActionPreset));
            var oldHand = lab.Players[0].Hand.ToArray();
            GameDebugTools.ReplaceHand(lab, 0, Enumerable.Repeat(CardId.Follower, 5));
            Assert.That(lab.Players[0].Hand.Count, Is.EqualTo(5));
            Assert.That(lab.Players[0].Hand.All(c => !lab.CanPlay(0, c.InstanceId, out _)), Is.True);
            Assert.That(oldHand.All(c => lab.FindCard(c.InstanceId).Pile == CardPile.Discard), Is.True);
            Assert.That(regular.Players.SelectMany(p => p.Hand.Concat(p.Deck)).Select(c => c.InstanceId), Is.EquivalentTo(original));
            Assert.That(regular.Players.All(p => p.TotalCardCount == 15 && p.Offering == 5), Is.True);
        }

        [Test] public void RequestedFiveCardsExecuteWithExactResourceAndIdentityChanges()
        {
            var game = GameDebugTools.CreateCardLab(); var player = game.Players[0];
            Func<CardId, CardInstance> card = id => player.Hand.Single(c => c.Definition.Id == id);
            var evangelism = card(CardId.Evangelism); int deck = player.Deck.Count, followers = player.FollowerCount;
            Assert.That(game.TryPlay(0, evangelism.InstanceId, out _), Is.True);
            Assert.That(game.Atheists, Is.EqualTo(98)); Assert.That(player.Faith, Is.EqualTo(48));
            Assert.That(player.Deck.Count, Is.EqualTo(deck + 2)); Assert.That(player.FollowerCount, Is.EqualTo(followers + 2));
            var edict = card(CardId.Edict); var removal = player.Deck[0];
            Assert.That(game.TryPlay(0, edict.InstanceId, out _, new EffectTarget { PlayerIndex = 0, CardInstanceId = removal.InstanceId }), Is.True);
            Assert.That(game.FindCard(removal.InstanceId).Pile, Is.EqualTo(CardPile.Removed));
            Assert.That(game.FindCard(removal.InstanceId).Card, Is.SameAs(removal));
            int missionaryCards = player.Deck.Count(c => c.Definition.Id == CardId.Evangelism);
            Assert.That(game.TryPlay(0, card(CardId.Preaching).InstanceId, out _), Is.True);
            Assert.That(player.Doctrine, Is.EqualTo(60));
            Assert.That(player.Deck.Count(c => c.Definition.Id == CardId.Evangelism), Is.EqualTo(missionaryCards + 1));
            var gift = player.Deck.First(c => c.Definition.Id == CardId.Follower);
            Assert.That(game.TryPlay(0, card(CardId.BondOfFaith).InstanceId, out _, new EffectTarget { PlayerIndex = 1 }), Is.True);
            Assert.That(game.Players[1].Deck.Last(), Is.SameAs(gift));
            Assert.That(player.Faith, Is.EqualTo(58)); Assert.That(player.Doctrine, Is.EqualTo(70));
            var stolen = game.Players[1].Deck.First(c => c.Definition.Id == CardId.Follower);
            GameDebugTools.SetResources(game, 1, 20, 30, 50);
            Assert.That(game.TryPlay(0, card(CardId.AggressiveMission).InstanceId, out _, new EffectTarget { PlayerIndex = 1 }), Is.True);
            Assert.That(player.Offering, Is.EqualTo(12));
            Assert.That(game.FindCard(stolen.InstanceId).Owner, Is.Zero);
            Assert.That(game.FindCard(stolen.InstanceId).Card, Is.SameAs(stolen));
        }

        [Test] public void AggressiveMissionUsesBothFaithThresholdCostsAndRejectsInsufficientOffering()
        {
            foreach (int faith in new[] { 30, 31 })
            {
                var game = GameDebugTools.CreateCardLab(); var card = game.Players[0].Hand.Single(c => c.Definition.Id == CardId.AggressiveMission);
                GameDebugTools.SetResources(game, 1, 20, faith, 50);
                GameDebugTools.SetResources(game, 0, 0, 50, 50);
                Assert.That(game.TryPlay(0, card.InstanceId, out _, new EffectTarget { PlayerIndex = 1 }), Is.False);
                Assert.That(game.FindCard(card.InstanceId).Pile, Is.EqualTo(CardPile.Hand));
                GameDebugTools.SetResources(game, 0, 20, 50, 50);
                Assert.That(game.TryPlay(0, card.InstanceId, out _, new EffectTarget { PlayerIndex = 1 }), Is.True);
                Assert.That(game.Players[0].Offering, Is.EqualTo(faith == 30 ? 19 : 18));
            }
        }

        [Test] public void EdictValidatesOwnHandOrDeckAndProtectsFaithfulFollowersBeforePayment()
        {
            var game = GameDebugTools.CreateCardLab(); var edict = game.Players[0].Hand.Single(c => c.Definition.Id == CardId.Edict);
            var faithful = Give(game, CardId.FaithfulFollower);
            var discarded = Give(game, CardId.Follower, 0, CardPile.Discard);
            var opponent = game.Players[1].Hand[0];
            foreach (var target in new[] { edict, faithful, discarded, opponent })
            {
                Assert.That(game.TryPlay(0, edict.InstanceId, out _, new EffectTarget { PlayerIndex = 0, CardInstanceId = target.InstanceId }), Is.False);
                Assert.That(game.Players[0].Offering, Is.EqualTo(20));
            }
            var handTarget = game.Players[0].Hand.Single(c => c.Definition.Id == CardId.Preaching);
            Assert.That(game.TryPlay(0, edict.InstanceId, out _, new EffectTarget { PlayerIndex = 0, CardInstanceId = handTarget.InstanceId }), Is.True);
            Assert.That(game.FindCard(handTarget.InstanceId).Pile, Is.EqualTo(CardPile.Removed));
            Assert.That(game.FindCard(edict.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
        }

        [Test] public void AiCanChooseEdictForOwnDeadCardWithoutOpponentPrivateInformation()
        {
            var game = Create(); var edict = Give(game, CardId.Edict); var heretic = Give(game, CardId.Heretic, 0, CardPile.Deck);
            var decision = new RuleBasedAi(Config()).Choose(new AiObservation(game, 0));
            Assert.That(decision.Definition.Id, Is.EqualTo(CardId.Edict));
            Assert.That(decision.Target.CardInstanceId, Is.EqualTo(heretic.InstanceId));
            Assert.That(game.TryPlay(0, decision.CardInstanceId, out _, decision.Target), Is.True);
            Assert.That(game.FindCard(heretic.InstanceId).Pile, Is.EqualTo(CardPile.Removed));
            Assert.That(game.FindCard(edict.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
        }

        [Test] public void PassiveNoCostIsDistinctFromFreeVariableAndMissingSpecification()
        {
            foreach (CardId id in new[] { CardId.Follower, CardId.FaithfulFollower })
            {
                var card = CardCatalog.Get(id);
                Assert.That(card.CostKind, Is.EqualTo(CardCostKind.None));
                Assert.That(card.Cost, Is.EqualTo(-1)); Assert.That(card.FixedOfferingCost, Is.Null);
                Assert.That(card.CostText, Is.Empty); Assert.That(card.IsPlayable, Is.False);
                Assert.That(CardPresentation.CostLabel(card), Is.Empty);
            }
            var free = CardCatalog.Get(CardId.RequestOffering);
            Assert.That(free.CostKind, Is.EqualTo(CardCostKind.Fixed)); Assert.That(free.FixedOfferingCost, Is.EqualTo(0));
            Assert.That(CardPresentation.CostLabel(free), Is.EqualTo("헌금 0"));
            Assert.That(CardCatalog.Get(CardId.AggressiveMission).CostKind, Is.EqualTo(CardCostKind.Variable));
            var missing = new CardDefinition((CardId)999, "검증 카드", "TODO: 대상 명세 대기", -1, true);
            Assert.That(missing.CostKind, Is.EqualTo(CardCostKind.MissingSpecification)); Assert.That(missing.IsPlayable, Is.False);
            Assert.That(CardPresentation.Diagnostics(new[] { missing }).Any(s => s.Contains("코스트 명세 누락")), Is.True);
            Assert.That(CardPresentation.Diagnostics(CardCatalog.All).Any(s => s.Contains("코스트 명세 누락")), Is.False);
        }

        [Test] public void PlayerCardTextHidesDiagnosticsButRetainsSourceAndUnavailability()
        {
            foreach (var card in CardCatalog.All)
            {
                string visible = CardPresentation.Detail(card) + CardPresentation.CostLabel(card);
                foreach (var marker in new[] { "원문 미명시", "명세 대기", "TODO", "NotImplemented", "구현 상태", "개별 ID" })
                    Assert.That(visible, Does.Not.Contain(marker));
            }
            var pending = new CardDefinition((CardId)999, "검증 카드", "실제 효과\nTODO: 대상 명세 대기", -1, true);
            Assert.That(pending.Description, Does.Contain("TODO"));
            Assert.That(CardPresentation.Detail(pending), Does.Contain("실제 효과"));
            Assert.That(CardPresentation.Detail(pending), Does.Contain("현재 사용할 수 없습니다"));
            Assert.That(CardPresentation.Detail(pending), Does.Not.Contain("TODO"));
            Assert.That(CardPresentation.Diagnostics(new[] { pending }).Any(s => s.Contains("개발용")), Is.True);
        }

        [Test] public void PassiveFollowerDetailsContainOnlyNameInherentEffectAndFlavor()
        {
            foreach (var id in new[] { CardId.Follower, CardId.FaithfulFollower })
            {
                var card = CardCatalog.Get(id);
                Assert.That(CardPresentation.Detail(card), Is.EqualTo(card.Name + "\n\n" + card.Description + "\n\n" + card.FlavorText));
            }
        }

        private static GameSession Create() => new GameSession(new DemoRuleAssumptions { shuffleInitialDeck = false });
        private static AiSettings Config() => new AiSettings { randomSeed = 42, similarScoreWindow = 0 };
        private static CardInstance Give(GameSession game, CardId id, int owner = 0, CardPile pile = CardPile.Hand)
            => GameDebugTools.GiveCard(game, owner, id, pile);

        [Test] public void AiReevaluatesResourcesAndUsesMultipleCardsThroughSharedRules()
        {
            var game = Create(); var config = Config();
            GameDebugTools.SetResources(game, 0, 0, 50, 50);
            Give(game, CardId.RequestOffering); Give(game, CardId.Evangelism);
            var turn = new AiTurn(game, 0, new RuleBasedAi(config), config);
            var first = turn.Next(game); Assert.That(first.Definition.Id, Is.EqualTo(CardId.RequestOffering));
            Assert.That(game.TryPlay(0, first.CardInstanceId, out _, first.Target), Is.True);
            var next = turn.Next(game); Assert.That(next.Definition.Id, Is.EqualTo(CardId.Evangelism));
            Assert.That(game.TryPlay(0, next.CardInstanceId, out _, next.Target), Is.True);
            Assert.That(game.Players[0].Offering, Is.EqualTo(1));
            Assert.That(game.Players[0].FollowerCount, Is.EqualTo(7));
            Assert.That(turn.Next(game), Is.Null);
        }

        [Test] public void AiSkipsPassiveUnaffordableAndConditionalCards()
        {
            var game = Create();
            foreach (var id in new[] { CardId.FaithfulFollower, CardId.WildRoar, CardId.GlimmerOfHope, CardId.Paradise }) Give(game, id);

            Assert.That(new RuleBasedAi(Config()).Choose(new AiObservation(game, 0)), Is.Null);
            GameDebugTools.SetResources(game, 0, 0, 50, 50);
            Give(game, CardId.Archbishop); Give(game, CardId.Penance);
            var worship = Give(game, CardId.RegularWorship);
            var decision = new RuleBasedAi(Config()).Choose(new AiObservation(game, 0));
            Assert.That(decision.Definition.Id, Is.EqualTo(CardId.GlimmerOfHope));
            Assert.That(decision.CardInstanceId, Is.Not.EqualTo(worship.InstanceId));
        }

        [Test] public void AiStopsWhenOnlyCappedStatRestorationWouldWasteResources()
        {
            var game = Create(); GameDebugTools.SetResources(game, 0, 10, 100, 100);
            Give(game, CardId.RegularWorship);
            Assert.That(new RuleBasedAi(Config()).Choose(new AiObservation(game, 0)), Is.Null);
        }

        [Test] public void SafetyLimitEndsAiTurnWithoutChangingGameActionLimits()
        {
            var game = Create(); Give(game, CardId.RequestOffering); Give(game, CardId.RequestOffering);
            var config = Config(); config.actionSafetyLimit = 1;
            var turn = new AiTurn(game, 0, new RuleBasedAi(config), config);
            var first = turn.Next(game);
            Assert.That(game.TryPlay(0, first.CardInstanceId, out _, first.Target), Is.True);
            Assert.That(turn.Next(game), Is.Null); Assert.That(turn.Attempts, Is.EqualTo(1));
            Assert.That(game.Players[0].Hand.Any(c => game.CanPlay(0, c.InstanceId, out _)), Is.True);
        }

        [Test] public void ConfigurableWeightsChangeCardPreference()
        {
            var game = Create(); Give(game, CardId.RegularWorship); Give(game, CardId.RequestOffering);
            var config = Config(); config.factions = new FactionAiProfile[0];
            config.defaultWeights = new AiWeights { faith = 3, doctrine = 3, offering = 0, cost = 0 };
            Assert.That(new RuleBasedAi(config).Choose(new AiObservation(game, 0)).Definition.Id, Is.EqualTo(CardId.RegularWorship));
            config.defaultWeights = new AiWeights { faith = 0, doctrine = 0, offering = 10, cost = 0 };
            Assert.That(new RuleBasedAi(config).Choose(new AiObservation(game, 0)).Definition.Id, Is.EqualTo(CardId.RequestOffering));
        }

        [Test] public void SimilarScoresHaveSeededButNonconstantChoices()
        {
            var game = Create(); Give(game, CardId.RegularWorship); Give(game, CardId.RegularWorship);
            var planner = new RuleBasedAi(Config()); var ids = new HashSet<Guid>();
            for (int i = 0; i < 30; i++) ids.Add(planner.Choose(new AiObservation(game, 0)).CardInstanceId);
            Assert.That(ids.Count, Is.EqualTo(2));
        }

        [Test] public void OpponentSnapshotContainsNoHandCardsOrDeckOrder()
        {
            Assert.That(typeof(AiPublicPlayer).GetProperties().All(p => p.PropertyType.IsEnum || p.PropertyType == typeof(int)), Is.True);
            var game = Create(); Give(game, CardId.Evangelism);
            var before = new RuleBasedAi(Config()).Choose(new AiObservation(game, 0));
            // Secret hand type changes while public counts/statistics stay identical.
            var secret = Give(game, CardId.RegularWorship, 1);
            var observation = new AiObservation(game, 0);
            Assert.That(observation.Hand.Any(c => c.InstanceId == secret.InstanceId), Is.False);
            var after = new RuleBasedAi(Config()).Choose(observation);
            Assert.That(after.Definition.Id, Is.EqualTo(before.Definition.Id));
            Assert.That(after.Target.PlayerIndex, Is.EqualTo(before.Target.PlayerIndex));
        }

        [Test] public void InvalidTargetsAreAllCheckedAndNoActionIsRepeatedOrCharged()
        {
            var game = Create(); var stolen = Give(game, CardId.AggressiveMission);
            var config = Config(); var turn = new AiTurn(game, 0, new RuleBasedAi(config), config);
            Assert.That(turn.Next(game), Is.Null); // opponents' decks contain no eligible followers
            Assert.That(turn.Attempts, Is.Zero);
            Assert.That(turn.LegalityChecks, Is.EqualTo(3));
            Assert.That(game.FindCard(stolen.InstanceId).Pile, Is.EqualTo(CardPile.Hand));
            Assert.That(game.Players[0].Offering, Is.EqualTo(5));
        }

        [Test] public void AllEightReportedCardsAreEvaluatedIncludingUnprofitableActions()
        {
            foreach (var id in new[] { CardId.ClergyTraining, CardId.Preaching, CardId.BondOfFaith, CardId.Council,
                CardId.Reaper, CardId.PublicDenunciation, CardId.Blessing, CardId.MoltingRitual })
            {
                var game = Create(); var source = Give(game, id);
                for (int owner = 0; owner < 4; owner++)
                    for (int n = 0; n < 2; n++) Give(game, CardId.Follower, owner, CardPile.Deck);
                GameDebugTools.SetResources(game, 0, 50, 20, 20);
                var config = Config(); var ai = new RuleBasedAi(config);
                var evaluated = ai.EvaluateActions(new AiObservation(game, 0));
                Assert.That(evaluated.Any(c => c.CardInstanceId == source.InstanceId), Is.True, id.ToString());
                Assert.That(evaluated.All(c => game.CanPlay(0, c.CardInstanceId, out _, c.Target)), Is.True);
                if (id == CardId.ClergyTraining || id == CardId.Reaper || id == CardId.PublicDenunciation || id == CardId.Blessing || id == CardId.MoltingRitual)
                {
                    Assert.That(evaluated.All(c => c.Score < 0), Is.True, "Default negative scores must remain negative: " + id);
                    Assert.That(ai.Choose(new AiObservation(game, 0)), Is.Null);
                }
                // A strategy profile, not a hardcoded positive-score floor, can value these effects.
                config.factions = new FactionAiProfile[0];
                config.defaultWeights = new AiWeights { faith = 5, doctrine = 5, futureCards = 10, disruption = 10,
                    factionAbility = 10, offering = 0, cost = 0 };
                var identities = new HashSet<Guid>(); AssertCardConservation(game, identities);
                var turn = new AiTurn(game, 0, ai, config);
                var decision = turn.Next(game);
                Assert.That(decision, Is.Not.Null, id.ToString());
                Assert.That(decision.CardInstanceId, Is.EqualTo(source.InstanceId));
                Assert.That(game.TryPlay(0, decision.CardInstanceId, out _, decision.Target), Is.True);
                AssertCardConservation(game, identities);
                Assert.That(turn.Next(game), Is.Null); // One source can never be submitted twice.
            }
        }

        [Test] public void InvalidPreferredTargetDoesNotHideOtherLegalTargetOrSpendActionLimit()
        {
            var game = Create(); var mission = Give(game, CardId.AggressiveMission);
            var target = Give(game, CardId.Follower, 2, CardPile.Deck);
            GameDebugTools.SetResources(game, 1, 5, 20, 50); // Preferred cost 1, but no eligible deck follower.
            var config = Config(); config.actionSafetyLimit = 1;
            var turn = new AiTurn(game, 0, new RuleBasedAi(config), config);
            var decision = turn.Next(game);
            Assert.That(decision, Is.Not.Null); Assert.That(decision.Target.PlayerIndex, Is.EqualTo(2));
            Assert.That(turn.LegalityChecks, Is.EqualTo(3)); Assert.That(turn.Attempts, Is.EqualTo(1));
            Assert.That(game.TryPlay(0, mission.InstanceId, out _, decision.Target), Is.True);
            Assert.That(game.FindCard(target.InstanceId).Owner, Is.Zero);
            Assert.That(turn.Next(game), Is.Null);
        }

        [Test] public void SubmittedActionThatFailsAfterStateChangeIsNotRetried()
        {
            var game = Create(); var evangelism = Give(game, CardId.Evangelism);
            var charity = Give(game, CardId.CharityPot);
            var config = Config(); var turn = new AiTurn(game, 0, new RuleBasedAi(config), config);
            var first = turn.Next(game); Assert.That(first.CardInstanceId, Is.EqualTo(evangelism.InstanceId));
            GameDebugTools.SetResources(game, 0, 0, 50, 50);
            Assert.That(game.TryPlay(0, first.CardInstanceId, out _, first.Target), Is.False);
            var next = turn.Next(game); Assert.That(next.CardInstanceId, Is.EqualTo(charity.InstanceId));
            Assert.That(game.TryPlay(0, next.CardInstanceId, out _, next.Target), Is.True);
            Assert.That(turn.Next(game), Is.Null);
            Assert.That(game.FindCard(evangelism.InstanceId).Pile, Is.EqualTo(CardPile.Hand));
        }

        [Test] public void AiUsesSessionPoliciesForShortageAndSelfTargetInsteadOfHardcodedRestrictions()
        {
            var game = new GameSession(new DemoRuleAssumptions { shuffleInitialDeck = false,
                requireFullFollowerTargets = false, rejectWhenAtheistsInsufficient = false, allowSelfAsOpponentTarget = true });
            foreach (var id in new[] { CardId.Indulgence, CardId.MoltingRitual, CardId.BondOfFaith, CardId.Blessing, CardId.Evangelism, CardId.WildRoar }) Give(game, id);
            GameDebugTools.SetResources(game, 0, 100, 70, 50);
            // Drain the pool through real, shared effects rather than a separate AI-only state.
            for (int i = 0; i < 50; i++) Assert.That(game.TryPlay(0, Give(game, CardId.Evangelism).InstanceId, out _), Is.True);
            // Own deck now has followers; an additional empty-deck session checks the shortage toggle below.
            var actions = new RuleBasedAi(Config()).EvaluateActions(new AiObservation(game, 0));
            Assert.That(actions.Any(c => c.Definition.Id == CardId.Evangelism), Is.True);
            Assert.That(actions.Any(c => c.Definition.Id == CardId.BondOfFaith && c.Target.PlayerIndex == 0), Is.True);
            GameDebugTools.SetResources(game, 0, 50, 70, 50);
            Assert.That(new RuleBasedAi(Config()).EvaluateActions(new AiObservation(game, 0)).Any(c => c.Definition.Id == CardId.WildRoar), Is.True);
            var empty = new GameSession(new DemoRuleAssumptions { shuffleInitialDeck = false, requireFullFollowerTargets = false });
            foreach (var id in new[] { CardId.Indulgence, CardId.MoltingRitual, CardId.BondOfFaith, CardId.Blessing }) Give(empty, id);
            var emptyActions = new RuleBasedAi(Config()).EvaluateActions(new AiObservation(empty, 0));
            Assert.That(emptyActions.Select(c => c.Definition.Id).Distinct().Count(), Is.EqualTo(4));
            Assert.That(emptyActions.All(c => empty.CanPlay(0, c.CardInstanceId, out _, c.Target)), Is.True);
        }

        [Test] public void SelfTargetPolicyDoesNotScoreHarmToOwnFollowersAsOpponentDisruption()
        {
            var game = new GameSession(new DemoRuleAssumptions { shuffleInitialDeck = false, allowSelfAsOpponentTarget = true });
            GameDebugTools.SetResources(game, 0, 50, 50, 50);
            Give(game, CardId.Follower, 0, CardPile.Deck); Give(game, CardId.Follower, 0, CardPile.Deck);
            foreach (var id in new[] { CardId.PublicDenunciation, CardId.Reaper, CardId.AggressiveMission, CardId.PoorRelief, CardId.FalseRevelation }) Give(game, id);
            var actions = new RuleBasedAi(Config()).EvaluateActions(new AiObservation(game, 0));
            var own = actions.Where(c => c.Target.PlayerIndex == 0).ToArray();
            Assert.That(own.Length, Is.EqualTo(5));
            Assert.That(own.All(c => c.Score < 0), Is.True);
            Assert.That(own.All(c => game.CanPlay(0, c.CardInstanceId, out _, c.Target)), Is.True);
        }

        [Test] public void FullFortyRoundAiSimulationFinishesWithLegalBoundedActions()
        {
            var game = new GameSession(); var config = Config();
            var agents = Enumerable.Range(0, 4).Select(i => new RuleBasedAi(config, i)).ToArray();
            var knownIds = new HashSet<Guid>();
            AssertCardConservation(game, knownIds);
            int steps = 0;
            while (game.Phase != GamePhase.Finished && steps++ < 300)
            {
                if (game.Phase == GamePhase.RewardChoice)
                {
                    int owner = game.RewardPlayerIndex;
                    var choice = agents[owner].ChooseReward(new AiObservation(game, owner), game.RewardOptions());
                    Assert.That(choice, Is.Not.Null); Assert.That(game.ChooseReward(owner, choice.Id), Is.True);
                    AssertCardConservation(game, knownIds);
                    continue;
                }
                int actor = game.ActivePlayerIndex;
                var turn = new AiTurn(game, actor, agents[actor], config);
                for (var decision = turn.Next(game); decision != null; decision = turn.Next(game))
                {
                    Assert.That(game.CanPlay(actor, decision.CardInstanceId, out _, decision.Target), Is.True);
                    Assert.That(game.TryPlay(actor, decision.CardInstanceId, out _, decision.Target), Is.True);
                    Assert.That(game.TryPlay(actor, decision.CardInstanceId, out _, decision.Target), Is.False);
                    AssertCardConservation(game, knownIds);
                }
                Assert.That(turn.Attempts, Is.LessThanOrEqualTo(config.actionSafetyLimit));
                Assert.That(game.EndActionTurn(actor), Is.True);
                AssertCardConservation(game, knownIds);
                foreach (var p in game.Players) { Assert.That(p.Faith, Is.InRange(0, 100)); Assert.That(p.Doctrine, Is.InRange(0, 100)); Assert.That(p.Offering, Is.GreaterThanOrEqualTo(0)); }
            }
            Assert.That(game.Phase, Is.EqualTo(GamePhase.Finished)); Assert.That(game.Round, Is.EqualTo(40));
        }

        private static void AssertCardConservation(GameSession game, HashSet<Guid> knownIds)
        {
            var ids = game.Players.SelectMany(p => p.Deck.Concat(p.Hand).Concat(p.Discard).Concat(p.Removed)).Select(c => c.InstanceId).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length), "One physical card must occupy exactly one pile.");
            Assert.That(knownIds.IsSubsetOf(ids), Is.True, "Known physical cards cannot disappear, even after removal or transfer.");
            knownIds.UnionWith(ids); // Card creation/rewards may legitimately add new identities.
        }

        [Test] public void RoundCleanupWaitsForAllFourPlayersThenDrawsFive()
        {
            var game = Create(); var first = game.Players[0].Hand.ToArray();
            game.EndActionTurn(0);
            Assert.That(game.Players[0].Hand, Is.EquivalentTo(first));
            game.EndActionTurn(1); game.EndActionTurn(2); game.EndActionTurn(3);
            Assert.That(game.Players[0].Hand.Count, Is.EqualTo(5));
            Assert.That(first.All(c => game.FindCard(c.InstanceId).Pile == CardPile.Discard), Is.True);
        }

        [Test] public void NewInstantCardsUseOriginalConditionsAndPreserveCardIdentity()
        {
            var game = Create(); GameDebugTools.SetResources(game, 0, 20, 50, 50);
            var preaching = Give(game, CardId.Preaching);
            Assert.That(game.TryPlay(0, preaching.InstanceId, out _), Is.True);
            Assert.That(game.Players[0].Doctrine, Is.EqualTo(60));
            var follower = Give(game, CardId.Follower, 0, CardPile.Deck);
            var bond = Give(game, CardId.BondOfFaith);
            Assert.That(game.TryPlay(0, bond.InstanceId, out _, new EffectTarget { PlayerIndex = 1 }), Is.True);
            Assert.That(game.Players[1].Deck.Last(), Is.SameAs(follower));
            Assert.That(game.FindCard(follower.InstanceId).Owner, Is.EqualTo(1));
            Assert.That(game.Players[0].Faith, Is.EqualTo(60));
        }
    }
}
