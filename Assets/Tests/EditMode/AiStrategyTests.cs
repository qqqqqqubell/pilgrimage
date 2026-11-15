using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Pilgrimage.Tests.BalanceTestSupport;
namespace Pilgrimage.Tests
{
 public sealed class AiStrategyTests
 {
  private static GameSession Create()=>Game();
  private static AiSettings Config()=>new AiSettings{randomSeed=42,similarScoreWindow=0,savingPenalty=0};
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
        [Test] public void AiStopsWhenOnlyCappedStatRestorationWouldWasteResources()
        {
            var game = Create(); GameDebugTools.SetResources(game, 0, 10, 100, 100);
            Give(game, CardId.RegularWorship);
            Assert.That(new RuleBasedAi(Config()).Choose(new AiObservation(game, 0)), Is.Null);
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

  [Test] public void FiveLabCardsUseFinalCostsEffectsAndSameObjects()
  {
   var g=GameDebugTools.CreateCardLab(); var p=g.Players[0];
   Func<CardId,CardInstance> card=id=>p.Hand.Single(c=>c.Definition.Id==id);
   var evangelism=card(CardId.Evangelism); Assert.That(g.TryPlay(0,evangelism.InstanceId,out _),Is.True);
   Assert.That(g.Atheists,Is.EqualTo(98)); var removed=p.Deck.First(c=>c.Definition.Id==CardId.RegularWorship);
   Assert.That(g.TryPlay(0,card(CardId.Edict).InstanceId,out _,new EffectTarget{PlayerIndex=0,CardInstanceId=removed.InstanceId}),Is.True);
   Assert.That(g.FindCard(removed.InstanceId).Pile,Is.EqualTo(CardPile.Removed));
   Assert.That(g.TryPlay(0,card(CardId.Preaching).InstanceId,out _),Is.True);
   var gift=Follower(g); Assert.That(g.TryPlay(0,card(CardId.BondOfFaith).InstanceId,out _,new EffectTarget{PlayerIndex=1}),Is.True);
   Assert.That(g.Players[1].Deck.Last(),Is.SameAs(gift)); Fund(g,1,100,30);
   Assert.That(g.TryPlay(0,card(CardId.AggressiveMission).InstanceId,out _,new EffectTarget{PlayerIndex=1}),Is.True);
   Assert.That(p.Offering,Is.EqualTo(77)); Assert.That(p.Faith,Is.EqualTo(63)); Assert.That(p.Doctrine,Is.EqualTo(75)); Conserved(g);
  }
  [Test] public void AiReevaluatesResourceFundingAndUsesMultipleCards()
  {
   var g=Game(); Fund(g,0,0); var request=Give(g,CardId.RequestOffering); var evangelism=Give(g,CardId.Evangelism);
   var config=Config(); var turn=new AiTurn(g,0,new RuleBasedAi(config),config);
   var a=turn.Next(g); Assert.That(a,Is.Not.Null); Assert.That(a.CardInstanceId,Is.EqualTo(request.InstanceId)); Assert.That(g.TryPlay(0,a.CardInstanceId,out _,a.Target),Is.True);
   var b=turn.Next(g); Assert.That(b,Is.Not.Null); Assert.That(b.CardInstanceId,Is.EqualTo(evangelism.InstanceId)); Assert.That(g.TryPlay(0,b.CardInstanceId,out _,b.Target),Is.True);
   Assert.That(g.Players[0].Offering,Is.Zero); Assert.That(turn.Next(g),Is.Null); Conserved(g);
  }
  [Test] public void AiSkipsPassiveUnaffordableThresholdAndEmptyTargetCases()
  {
   var g=Game(); Fund(g,0,0,20,50);
   foreach(var id in new[]{CardId.FaithfulFollower,CardId.AggressiveMission,CardId.PoorRelief,CardId.WildRoar,CardId.Paradise,CardId.MassAgitation}) Give(g,id);
   Assert.That(new RuleBasedAi(Config()).Choose(new AiObservation(g,0)),Is.Null);
   var hope=Give(g,CardId.GlimmerOfHope); Assert.That(new RuleBasedAi(Config()).Choose(new AiObservation(g,0)).CardInstanceId,Is.EqualTo(hope.InstanceId));
  }
  [Test] public void SafetyLimitBoundsSubmittedActionsWithoutAddingGameCardLimit()
  {
   var g=Game(); Give(g,CardId.CharityPot); Give(g,CardId.CharityPot); var config=Config(); config.actionSafetyLimit=1;
   var turn=new AiTurn(g,0,new RuleBasedAi(config),config); var a=turn.Next(g);
   Assert.That(g.TryPlay(0,a.CardInstanceId,out _,a.Target),Is.True); Assert.That(turn.Next(g),Is.Null); Assert.That(turn.Attempts,Is.EqualTo(1));
   Assert.That(g.Players[0].Hand.Any(c=>g.CanPlay(0,c.InstanceId,out _)),Is.True);
  }
  [Test] public void FailedRevealActionIsNotSubmittedAgainAfterFundsChange()
  {
   var g=Game(); var action=Give(g,CardId.Evangelism); var free=Give(g,CardId.CharityPot); var config=Config();
   var turn=new AiTurn(g,0,new RuleBasedAi(config),config); var a=turn.Next(g); Assert.That(a.CardInstanceId,Is.EqualTo(action.InstanceId));
   Fund(g,0,0); Assert.That(g.TryPlay(0,a.CardInstanceId,out _,a.Target),Is.False);
   var b=turn.Next(g); Assert.That(b.CardInstanceId,Is.EqualTo(free.InstanceId)); Assert.That(g.TryPlay(0,b.CardInstanceId,out _,b.Target),Is.True);
   Assert.That(turn.Next(g),Is.Null); Assert.That(g.FindCard(action.InstanceId).Pile,Is.EqualTo(CardPile.Hand));
  }
  private static void ClearOpponentDeckFollowers(GameSession g)
  {
   for(int owner=1;owner<4;owner++) while(g.Players[owner].Deck.Any(c=>c.Definition.Id==CardId.Follower))
     Play(g,CardId.PublicDenunciation,0,new EffectTarget{PlayerIndex=owner});
  }
  [Test] public void InvalidPreferredTargetDoesNotHideAnotherValidOpponent()
  {
   var g=Game(); ClearOpponentDeckFollowers(g); var victim=Give(g,CardId.Follower,2,CardPile.Deck); Fund(g,1,200,20);
   var card=Give(g,CardId.AggressiveMission); var config=Config(); config.actionSafetyLimit=1; var turn=new AiTurn(g,0,new RuleBasedAi(config),config);
   var choice=turn.Next(g); Assert.That(choice,Is.Not.Null); Assert.That(choice.Target.PlayerIndex,Is.EqualTo(2)); Assert.That(turn.LegalityChecks,Is.EqualTo(3));
   Assert.That(g.TryPlay(0,card.InstanceId,out _,choice.Target),Is.True); Assert.That(g.FindCard(victim.InstanceId).Owner,Is.Zero); Assert.That(turn.Next(g),Is.Null); Conserved(g);
  }
  [Test] public void NoValidTargetsEndsWithoutRepeatingOrCharging()
  {
   var g=Game(); ClearOpponentDeckFollowers(g); var card=Give(g,CardId.AggressiveMission); int funds=g.Players[0].Offering;
   var config=Config(); var turn=new AiTurn(g,0,new RuleBasedAi(config),config);
   Assert.That(turn.Next(g),Is.Null); Assert.That(turn.LegalityChecks,Is.EqualTo(3)); Assert.That(turn.Attempts,Is.Zero); Assert.That(g.Players[0].Offering,Is.EqualTo(funds));
   Assert.That(g.FindCard(card.InstanceId).Pile,Is.EqualTo(CardPile.Hand));
  }
  [Test] public void AiCanChooseOneOrTwoOwnHereticsForEdictWithoutSeeingEnemyCards()
  {
   var g=Game(); var card=Give(g,CardId.Edict); var one=Give(g,CardId.Heretic,0,CardPile.Deck); var two=Give(g,CardId.Heretic,0,CardPile.Discard);
   var config=Config(); var turn=new AiTurn(g,0,new RuleBasedAi(config),config); var choice=turn.Next(g);
   Assert.That(choice.Definition.Id,Is.EqualTo(CardId.Edict)); Assert.That(choice.Target.Cards,Is.EquivalentTo(new[]{one.InstanceId,two.InstanceId}));
   Assert.That(g.TryPlay(0,card.InstanceId,out _,choice.Target),Is.True); Assert.That(g.Atheists,Is.EqualTo(102)); Conserved(g);
  }
  [Test] public void NewAreaCardsAndJudgmentAreEvaluatedAndRallyFindsLegalOwnCard()
  {
   foreach(var id in new[]{CardId.AreaCleansing,CardId.FurRally,CardId.HeresyJudgment,CardId.InterfaithLoan,CardId.CallOfFur,CardId.MassAgitation}) {
    var g=Game(); var source=Give(g,id); var config=Config();
    config.factions=new FactionAiProfile[0]; config.defaultWeights=new AiWeights{offering=0,cost=0,faith=0,doctrine=0,followers=0,disruption=10,factionAbility=10};
    var ai=new RuleBasedAi(config); Assert.That(ai.EvaluateActions(new AiObservation(g,0)).Any(c=>c.CardInstanceId==source.InstanceId),Is.True);
    var turn=new AiTurn(g,0,ai,config); var choice=turn.Next(g); Assert.That(choice,Is.Not.Null,id.ToString());
    Assert.That(g.TryPlay(0,choice.CardInstanceId,out _,choice.Target),Is.True); Assert.That(turn.Next(g),Is.Null); Conserved(g);
   }
  }
  [Test] public void ExpensiveCardReservationCanDelayLowValuePaidActionsWithoutLooping()
  {
   var g=Game(); Fund(g,0,35); Give(g,CardId.MassAgitation); Give(g,CardId.PublicDenunciation); var config=Config(); config.savingPenalty=2;
   var ai=new RuleBasedAi(config); Assert.That(ai.EvaluateActions(new AiObservation(g,0)).Any(),Is.True);
   var turn=new AiTurn(g,0,ai,config); var result=turn.Next(g);
   Assert.That(result==null||result.Score>config.minimumScore,Is.True); Assert.That(turn.Attempts,Is.LessThanOrEqualTo(1));
  }
  [Test] public void LabPassiveHandsAndEdictCancellationDoNotAlterRegularSession()
  {
   var regular=Game(false,FactionId.Gold); var ids=Cards(regular).Select(c=>c.InstanceId).ToArray(); var lab=GameDebugTools.CreateCardLab(FactionId.Gold);
   GameDebugTools.ReplaceHand(lab,0,Enumerable.Repeat(CardId.Follower,5)); Assert.That(lab.Players[0].Hand.All(c=>!lab.CanPlay(0,c.InstanceId,out _)),Is.True);
   Assert.That(Cards(regular).Select(c=>c.InstanceId),Is.EquivalentTo(ids)); Assert.That(regular.HumanFaction,Is.EqualTo(FactionId.Gold));
  }
  [Test] public void AiUsesNewMissionAndJudgmentCostsEvenAtLowOwnFaith()
  {
   var g=Game(); Fund(g,0,3,10); Fund(g,1,200,30); var mission=Give(g,CardId.AggressiveMission); var judgment=Give(g,CardId.HeresyJudgment);
   var actions=new RuleBasedAi(Config()).EvaluateActions(new AiObservation(g,0));
   Assert.That(actions.Any(a=>a.CardInstanceId==mission.InstanceId&&a.Target.PlayerIndex==1),Is.True);
   Assert.That(actions.Any(a=>a.CardInstanceId==judgment.InstanceId),Is.True);
   Assert.That(g.EffectiveCost(0,judgment,new EffectTarget{PlayerIndex=1}),Is.EqualTo(3));
  }
  [Test] public void AiValuesDeckHereticRecoveryAndMassAgitationFollowerAcquisition()
  {
   foreach(var id in new[]{CardId.Inquisitor,CardId.MassAgitation}) {
    var g=Game(); var source=Give(g,id);
    for(int n=0;n<3;n++) Give(g,CardId.Heretic,0,CardPile.Deck);
    var config=Config(); config.factions=new FactionAiProfile[0]; config.thresholdCrossingValue=0; config.resourceUnlockWeight=0;
    config.defaultWeights=new AiWeights{followers=1,faith=0,doctrine=0,offering=0,disruption=0,factionAbility=0,futureCards=0,cost=0};
    var decision=new RuleBasedAi(config).EvaluateActions(new AiObservation(g,0)).Single(a=>a.CardInstanceId==source.InstanceId);
    Assert.That(decision.Score,Is.EqualTo(3),id.ToString());
   }
  }
 }
}
