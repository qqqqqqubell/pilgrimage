using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Pilgrimage.Tests.BalanceTestSupport;
namespace Pilgrimage.Tests
{
 public sealed class ExpansionTests
 {
  [Test] public void JudgmentFailureDoesNotCountAndPhysicalScopeUsesEachCopySeparately()
  {
   var g=Game(policy:new TimedEffectPolicy{judgmentCountScope=JudgmentCountScope.PhysicalCard}); Fund(g,0,4);
   var first=Give(g,CardId.HeresyJudgment); var second=Give(g,CardId.HeresyJudgment); var target=new EffectTarget{PlayerIndex=1};
   Assert.That(g.TryPlay(0,first.InstanceId,out _,target),Is.False); Assert.That(g.Players[0].JudgmentUses,Is.Zero); Assert.That(first.SuccessfulUses,Is.Zero);
   Fund(g); Assert.That(g.TryPlay(0,first.InstanceId,out _,target),Is.True);
   Assert.That(g.EffectiveCost(0,second,target),Is.EqualTo(5)); Assert.That(g.TryPlay(0,second.InstanceId,out _,target),Is.True);
   Assert.That(g.Players[0].JudgmentUses,Is.EqualTo(2));
   var inclusive=Game(policy:new TimedEffectPolicy{judgmentIncludesCurrentUse=true}); var card=Give(inclusive,CardId.HeresyJudgment);
   Assert.That(inclusive.EffectiveCost(0,card,target),Is.EqualTo(10));
  }
  [Test] public void ArchbishopProtectionWinsOverRemodelingButNaturalDecayStillApplies()
  {
   var g=Game(); Play(g,CardId.TempleRemodeling); Play(g,CardId.Archbishop); Play(g,CardId.AreaCleansing);
   Assert.That(g.Players[0].Faith,Is.EqualTo(50)); Assert.That(g.Players[0].Doctrine,Is.EqualTo(50));
   NextRound(g); Assert.That(g.Players[0].Faith,Is.EqualTo(46)); Assert.That(g.Players[0].Doctrine,Is.EqualTo(46));
  }
  [Test] public void BlessingCanDrawProtectedFollowerAndSmallUpToEffectsNeedNoExtraTargets()
  {
   var g=Game(); var faithful=Give(g,CardId.FaithfulFollower,0,CardPile.Deck); Play(g,CardId.Blessing);
   Assert.That(g.FindCard(faithful.InstanceId).Pile,Is.EqualTo(CardPile.Hand)); Assert.That(faithful.Definition.ProtectedFromCardEffects,Is.True);
   Play(g,CardId.Blessing); Conserved(g);
  }
  [Test] public void EdictRejectsProtectedForeignSelfAndTooManyTargetsBeforePayment()
  {
   var g=Game(); var source=Give(g,CardId.Edict); var faithful=Give(g,CardId.FaithfulFollower);
   var cases=new[]{new[]{source.InstanceId},new[]{faithful.InstanceId},new[]{g.Players[1].Hand[0].InstanceId},g.Players[0].Hand.Take(3).Select(c=>c.InstanceId).ToArray()};
   foreach(var ids in cases) Assert.That(g.TryPlay(0,source.InstanceId,out _,new EffectTarget{PlayerIndex=0,CardInstanceIds=ids}),Is.False);
   Assert.That(g.Players[0].Offering,Is.EqualTo(200));
   Assert.That(g.TryPlay(0,source.InstanceId,out _,new EffectTarget{PlayerIndex=0,CardInstanceIds=new Guid[0]}),Is.True);
   Assert.That(g.Players[0].Offering,Is.EqualTo(180)); Conserved(g);
  }
  [Test] public void EveryFactionStartsAsHumanWithThreeUniqueOpponents()
  {
   foreach(var faction in new[]{FactionId.Poverty,FactionId.Gold,FactionId.Discipline,FactionId.Fur}) {
    var g=Game(false,faction); Assert.That(g.HumanFaction,Is.EqualTo(faction));
    Assert.That(g.Players.Select(p=>p.Faction).Distinct().Count(),Is.EqualTo(4));
    Assert.That(g.Players.All(p=>p.Name==FactionSetup.Name(p.Faction)&&p.TotalCardCount==15&&p.Hand.Count==4&&p.Offering==5),Is.True); Conserved(g);
   }
   Assert.Throws<ArgumentOutOfRangeException>(()=>new GameSession(humanFaction:FactionId.None));
  }
  [Test] public void AllFortyFiveCardsUseRealCostsEffectsAndPreserveExistingIdentities()
  {
   foreach(var d in CardCatalog.All) {
    var g=Game(); for(int o=0;o<4;o++) for(int n=0;n<2;n++) Give(g,CardId.Follower,o,CardPile.Deck);
    if(d.Id==CardId.Paradise) for(int n=0;n<13;n++) Give(g,CardId.Follower,0,CardPile.Deck);
    if(d.Id==CardId.DeepForestSanctuary) for(int n=0;n<4;n++) Give(g,CardId.FaithfulFollower,0,CardPile.Deck);
    if(d.Id==CardId.GlimmerOfHope) Fund(g,0,10);
    if(d.Id==CardId.Inquisitor||d.Id==CardId.ReturnToFaith) { Give(g,CardId.Heretic); Give(g,CardId.Heretic,0,CardPile.Deck); }
    var source=Give(g,d.Id); var p=g.Players[0]; var target=new EffectTarget{PlayerIndex=1};
    CardInstance chosen=null;
    if(d.Id==CardId.Edict||d.Id==CardId.FurRally) { chosen=p.Hand[0]; target=new EffectTarget{PlayerIndex=0,CardInstanceId=chosen.InstanceId}; }
    int money=p.Offering,faith=p.Faith,doctrine=p.Doctrine,property=p.ChurchProperty,cost=g.EffectiveCost(0,source,target),deck=p.Deck.Count,followers=p.FollowerCount;
    var own=Follower(g); var victim=Follower(g,1); var known=new HashSet<Guid>(Cards(g).Select(c=>c.InstanceId));
    if(!d.IsPlayable) { Assert.That(g.TryPlay(0,source.InstanceId,out _,target),Is.False); Assert.That(p.Hand.Contains(source),Is.True); Conserved(g,known); continue; }
    Assert.That(g.TryPlay(0,source.InstanceId,out string reason,target),Is.True,d.Name+": "+reason);
    int extra=d.Id==CardId.Interest?2:d.Id==CardId.SalvationFundraising?1:d.Id==CardId.InterfaithLoan?-10:0;
    Assert.That(p.Offering,Is.EqualTo(money-cost+d.OfferingGain+extra),d.Name+" money");
    Assert.That(p.ChurchProperty,Is.EqualTo(property+d.ChurchPropertyChange),d.Name+" property");
    Assert.That(p.Faith,Is.EqualTo(Math.Min(100,faith+d.FaithChange+(d.Id==CardId.AreaCleansing?-30:d.Id==CardId.DeepForestSanctuary?8:0))),d.Name+" faith");
    Assert.That(p.Doctrine,Is.EqualTo(doctrine+d.DoctrineChange+(d.Id==CardId.AreaCleansing?-30:0)),d.Name+" doctrine");
    switch(d.Id) {
     case CardId.Evangelism: Assert.That(p.FollowerCount,Is.EqualTo(followers+2)); Assert.That(g.Atheists,Is.EqualTo(98)); break;
     case CardId.ClergyTraining: Assert.That(p.Deck.Count,Is.EqualTo(deck+2)); break;
     case CardId.Preaching: Assert.That(p.Deck.Count,Is.EqualTo(deck+1)); break;
     case CardId.SalvationFundraising: Assert.That(p.Deck[0].Definition.Id,Is.EqualTo(CardId.CharityPot)); Assert.That(p.Deck.Count,Is.EqualTo(deck+1)); break;
     case CardId.BondOfFaith: Assert.That(g.Players[1].Deck.Last(),Is.SameAs(own)); break;
     case CardId.AggressiveMission: Assert.That(g.FindCard(victim.InstanceId).Owner,Is.Zero); break;
     case CardId.PoorRelief: case CardId.Blessing: Assert.That(p.Hand.Count,Is.EqualTo(6)); break;
     case CardId.Reaper: Assert.That(g.Players[1].Deck.Count(c=>c.Definition.Id==CardId.Heretic),Is.EqualTo(2)); break;
     case CardId.PublicDenunciation: Assert.That(g.Atheists,Is.EqualTo(101)); Assert.That(g.FindCard(victim.InstanceId).Pile,Is.EqualTo(CardPile.Removed)); break;
     case CardId.Edict: Assert.That(g.Atheists,Is.EqualTo(101)); Assert.That(g.FindCard(chosen.InstanceId).Pile,Is.EqualTo(CardPile.Removed)); break;
     case CardId.Heretic: case CardId.RefusingFollower: Assert.That(source.Definition.Id,Is.EqualTo(CardId.Follower)); break;
     case CardId.Indulgence: case CardId.WitchHunt: Assert.That(own.Definition.Id,Is.EqualTo(CardId.Heretic)); break;
     case CardId.MoltingRitual: Assert.That(p.Deck.Count(c=>c.Definition.Id==CardId.FaithfulFollower),Is.EqualTo(2)); break;
     case CardId.InterfaithLoan: Assert.That(g.Players[1].Offering,Is.EqualTo(210)); Assert.That(p.Deck.Count(c=>c.Definition.Id==CardId.Interest),Is.EqualTo(1)); break;
     case CardId.HeresyJudgment: Assert.That(g.Players[1].Doctrine,Is.EqualTo(45)); Assert.That(p.JudgmentUses,Is.EqualTo(1)); Assert.That(p.Deck.Count(c=>c.Definition.Id==CardId.HeresyJudgment),Is.EqualTo(1)); break;
     case CardId.CallOfFur: Assert.That(g.Players[1].Hand.All(c=>c.Definition.Id==CardId.RefusingFollower),Is.True); Assert.That(victim.Definition.Id,Is.EqualTo(CardId.RefusingFollower)); break;
     case CardId.AreaCleansing: Assert.That(g.Players.All(q=>q.Faith==20&&q.Doctrine==20),Is.True); break;
     case CardId.MassAgitation: Assert.That(g.Players.Skip(1).All(q=>q.Faith==10&&q.Doctrine==10),Is.True); break;
     case CardId.FurRally: Assert.That(chosen.Definition.Id,Is.EqualTo(CardId.FaithfulFollower)); Assert.That(g.Players.Skip(1).All(q=>q.Faith==30&&q.Doctrine==30),Is.True); break;
     case CardId.WildRoar: Assert.That(g.Atheists,Is.EqualTo(97)); Assert.That(g.Players.Skip(1).All(q=>q.Deck[0].Definition.Id==CardId.Heretic),Is.True); break;
     case CardId.ReturnToFaith: Assert.That(p.Hand.Concat(p.Deck).All(c=>c.Definition.Id!=CardId.Heretic),Is.True); break;
     case CardId.Inquisitor: Assert.That(p.Hand.All(c=>c.Definition.Id!=CardId.Heretic),Is.True); break;
    }
    Assert.That(g.FindCard(source.InstanceId).Pile,Is.EqualTo(CardPile.Discard)); Assert.That(g.TryPlay(0,source.InstanceId,out _,target),Is.False); Conserved(g,known);
   }
  }
  [Test] public void PenanceResolvesAfterFourSettlementsExactlyOnce()
  {
   var g=Game(); Play(g,CardId.Penance); var p=g.Players[0];
   for(int n=1;n<=3;n++) { NextRound(g); Assert.That(p.Offering,Is.EqualTo(190+3*n)); Assert.That(p.Faith,Is.EqualTo(50-2*n)); }
   NextRound(g); Assert.That(p.Offering,Is.EqualTo(222)); Assert.That(p.Faith,Is.EqualTo(52)); Assert.That(p.Doctrine,Is.EqualTo(52)); Assert.That(p.TimedEffects,Is.Empty);
   NextRound(g); Assert.That(p.Offering,Is.EqualTo(225)); Conserved(g);
  }
  [Test] public void InvestmentUsesSeededInclusiveZeroToThirtyRangeAfterTwoSettlements()
  {
   var seen=new HashSet<int>();
   for(int seed=0;seed<120;seed++) {
    var g=new GameSession(new DemoRuleAssumptions{shuffleInitialDeck=false,randomSeed=seed}); Fund(g); Play(g,CardId.CaravanInvestment); NextRound(g);
    Assert.That(g.Players[0].Offering,Is.EqualTo(193)); int reward=new Random(seed^0x51A7).Next(0,31); seen.Add(reward); NextRound(g);
    Assert.That(g.Players[0].Offering,Is.EqualTo(196+reward)); Assert.That(g.Players[0].TimedEffects,Is.Empty);
   }
   Assert.That(seen.Contains(0)&&seen.Contains(30),Is.True);
  }
  [Test] public void ReflectionAndHumbleTempleIncomeAndRecoveryExpireAtCorrectSettlement()
  {
   var g=Game(); Play(g,CardId.SolitaryReflection); NextRound(g); Assert.That(g.Players[0].Offering,Is.EqualTo(201)); Assert.That(g.Players[0].Faith,Is.EqualTo(58));
   NextRound(g); Assert.That(g.Players[0].Offering,Is.EqualTo(204));
   var t=Game(); Play(t,CardId.HumbleTemple); NextRound(t); Assert.That(t.Players[0].Offering,Is.EqualTo(195));
   NextRound(t); Assert.That(t.Players[0].Offering,Is.EqualTo(195)); Assert.That(t.Players[0].TimedEffects,Is.Empty);
   NextRound(t); Assert.That(t.Players[0].Offering,Is.EqualTo(198)); Assert.That(t.Players[0].Faith,Is.EqualTo(59));
  }
  [Test] public void RemodelingPaysTwoPropertyDoublesIncomeAndDecreasesThenExpires()
  {
   var g=Game(); Play(g,CardId.TempleRemodeling); Play(g,CardId.RequestOffering); Play(g,CardId.RegularWorship);
   Assert.That(g.Players[0].ChurchProperty,Is.EqualTo(18)); Assert.That(g.Players[0].Faith,Is.EqualTo(51));
   NextRound(g); Assert.That(g.Players[0].Offering,Is.EqualTo(203)); Assert.That(g.Players[0].Faith,Is.EqualTo(47));
   NextRound(g); Assert.That(g.Players[0].Offering,Is.EqualTo(209)); Assert.That(g.Players[0].TimedEffects,Is.Empty);
   NextRound(g); Assert.That(g.Players[0].Offering,Is.EqualTo(212)); Assert.That(g.Players[0].Faith,Is.EqualTo(41));
  }
  [Test] public void ArchbishopBlocksCardDecreasesOnlyAndExpiresAfterThreeSettlements()
  {
   var g=Game(); Play(g,CardId.Archbishop); Play(g,CardId.AreaCleansing); Assert.That(g.Players[0].Faith,Is.EqualTo(50)); Assert.That(g.Players[1].Faith,Is.EqualTo(20));
   Play(g,CardId.RegularWorship); g.EndActionTurn(0); Play(g,CardId.FalseRevelation,1,new EffectTarget{PlayerIndex=0});
   Assert.That(g.Players[0].Faith,Is.EqualTo(55)); Assert.That(g.Players[1].Offering,Is.EqualTo(185));
   for(int n=0;n<3;n++) NextRound(g); Assert.That(g.Players[0].TimedEffects,Is.Empty); Assert.That(g.Players[0].Faith,Is.EqualTo(49));
  }
  [Test] public void WitchHuntConfiguredPileAndProtectionPreserveIdentity()
  {
   foreach(var pile in new[]{WitchHuntSource.OwnDeckTop,WitchHuntSource.OwnHandFirst}) {
    var g=Game(policy:new TimedEffectPolicy{witchHuntSource=pile}); var chosen=pile==WitchHuntSource.OwnDeckTop?Follower(g):g.Players[0].Hand[0];
    var faithful=Give(g,CardId.FaithfulFollower,0,CardPile.Deck); Play(g,CardId.WitchHunt);
    Assert.That(chosen.Definition.Id,Is.EqualTo(CardId.Heretic)); Assert.That(g.FindCard(chosen.InstanceId).Card,Is.SameAs(chosen));
    Assert.That(faithful.Definition.Id,Is.EqualTo(CardId.FaithfulFollower)); Assert.That(g.Players[0].Offering,Is.EqualTo(220)); Conserved(g);
   }
  }
  [Test] public void StackingIncomeOrderAndPropertyPoliciesRemainExplicit()
  {
   foreach(var mode in new[]{EffectStacking.Independent,EffectStacking.RefreshSameCard}) {
    var policy=new TimedEffectPolicy{stacking=mode}; var g=Game(policy:policy); policy.stacking=EffectStacking.Independent;
    Play(g,CardId.SolitaryReflection); Play(g,CardId.SolitaryReflection); Assert.That(g.PreviewRoundIncome(0),Is.EqualTo(mode==EffectStacking.Independent?0:1));
   }
   foreach(var order in new[]{IncomeOrder.AddThenMultiply,IncomeOrder.MultiplyThenAdd}) {
    var g=Game(policy:new TimedEffectPolicy{incomeOrder=order}); Play(g,CardId.HumbleTemple); Play(g,CardId.TempleRemodeling); Assert.That(g.PreviewRoundIncome(0),Is.EqualTo(order==IncomeOrder.AddThenMultiply?0:3));
   }
   var strict=Game(policy:new TimedEffectPolicy{remodelingRequiresProperty=true}); Fund(strict,0,200,50,50,1);
   var card=Give(strict,CardId.TempleRemodeling); Assert.That(strict.TryPlay(0,card.InstanceId,out _),Is.False);
  }
  [Test] public void LoanTransfersTenThenRecoversThirtyAndAddsOneInterest()
  {
   var g=Game(faction:FactionId.Gold); Play(g,CardId.InterfaithLoan,0,new EffectTarget{PlayerIndex=1});
   Assert.That(g.Players[0].Offering,Is.EqualTo(190)); Assert.That(g.Players[1].Offering,Is.EqualTo(210));
   NextRound(g); Assert.That(g.Players[0].Offering,Is.EqualTo(223)); Assert.That(g.Players[1].Offering,Is.EqualTo(183)); Assert.That(g.Players[0].TimedEffects,Is.Empty);
  }
  [Test] public void LoanShortfallNeverGeneratesMoneyAndOptionalDebtIsRetained()
  {
   foreach(var mode in new[]{LoanShortfallPolicy.CollectAvailable,LoanShortfallPolicy.CarryRemainder}) {
    var g=Game(policy:new TimedEffectPolicy{loanShortfall=mode}); Play(g,CardId.InterfaithLoan,0,new EffectTarget{PlayerIndex=1}); Fund(g,1,0); NextRound(g);
    Assert.That(g.Players[0].Offering,Is.EqualTo(196)); Assert.That(g.Players[1].Offering,Is.Zero);
    Assert.That(g.Players[0].TimedEffects.Count,Is.EqualTo(mode==LoanShortfallPolicy.CollectAvailable?0:1));
    if(mode==LoanShortfallPolicy.CarryRemainder) Assert.That(g.Players[0].TimedEffects.Single().Outstanding,Is.EqualTo(27));
   }
   var low=Game(); Fund(low,0,9); var card=Give(low,CardId.InterfaithLoan);
   Assert.That(low.TryPlay(0,card.InstanceId,out _,new EffectTarget{PlayerIndex=1}),Is.False);
  }
  [Test] public void JudgmentBoundaryThirtyUsesSuccessfulCountAndAddsDistinctCopies()
  {
   foreach(int doctrine in new[]{29,30}) {
    var g=Game(faction:FactionId.Discipline); Fund(g,1,200,50,doctrine); for(int n=0;n<3;n++) Give(g,CardId.Follower,1,CardPile.Deck);
    int people=g.Players[1].FollowerCount; var first=Play(g,CardId.HeresyJudgment,0,new EffectTarget{PlayerIndex=1});
    Assert.That(g.Players[0].Offering,Is.EqualTo(195));
    if(doctrine==30) Assert.That(g.Players[1].Doctrine,Is.EqualTo(25)); else Assert.That(g.Players[1].FollowerCount,Is.EqualTo(people-1));
    var second=Give(g,CardId.HeresyJudgment); Assert.That(g.EffectiveCost(0,second,new EffectTarget{PlayerIndex=1}),Is.EqualTo(10));
    Assert.That(g.TryPlay(0,second.InstanceId,out _,new EffectTarget{PlayerIndex=1}),Is.True);
    Assert.That(g.Players[0].JudgmentUses,Is.EqualTo(2)); Assert.That(g.Players[0].Deck.Count(c=>c.Definition.Id==CardId.HeresyJudgment),Is.EqualTo(2)); Conserved(g);
   }
  }
  [Test] public void RefusingFollowersRescueOrMigrateToActualFurSeatAfterThreeTurns()
  {
   foreach(bool rescue in new[]{false,true}) {
    var g=Game(faction:FactionId.Fur); Play(g,CardId.CallOfFur,0,new EffectTarget{PlayerIndex=1});
    var cards=g.Players[1].Hand.Concat(g.Players[1].Deck).Where(c=>c.Definition.Id==CardId.RefusingFollower).ToArray(); Assert.That(cards.Length,Is.EqualTo(5));
    if(rescue) { g.EndActionTurn(0); Assert.That(g.TryPlay(1,cards[0].InstanceId,out _),Is.True); Assert.That(cards[0].Definition.Id,Is.EqualTo(CardId.Follower)); }
    NextRound(g); NextRound(g); Assert.That(cards.Count(c=>c.Definition.Id==CardId.FaithfulFollower),Is.Zero); NextRound(g);
    Assert.That(cards.Count(c=>c.Definition.Id==CardId.FaithfulFollower),Is.EqualTo(rescue?4:5));
    foreach(var c in cards.Where(c=>c.Definition.Id==CardId.FaithfulFollower)) { Assert.That(g.FindCard(c.InstanceId).Owner,Is.Zero); Assert.That(g.FindCard(c.InstanceId).Card,Is.SameAs(c)); }
    Assert.That(g.PopulationTotal,Is.EqualTo(120)); Conserved(g);
   }
  }
  [Test] public void EveryFactionRoundEightRandomRewardCanBeNaturallyDrawn()
  {
   foreach(var faction in new[]{FactionId.Poverty,FactionId.Gold,FactionId.Discipline,FactionId.Fur}) {
    var g=Game(false,faction); for(int n=1;n<8;n++) NextRound(g); for(int n=0;n<4;n++) g.EndActionTurn(g.ActivePlayerIndex);
    var receipt=g.RoundRewards.Single(r=>r.PlayerIndex==0); var reward=g.FindCard(receipt.InstanceId).Card;
    Assert.That(reward.Definition.Faction,Is.EqualTo(faction)); Assert.That(receipt.Destination,Is.EqualTo(CardPile.Discard)); g.AcknowledgeRewards();
    for(int n=0;n<15&&!g.Players[0].Hand.Contains(reward);n++) NextRound(g);
    Assert.That(g.Players[0].Hand.Contains(reward),Is.True); Assert.That(g.FindCard(reward.InstanceId).Card,Is.SameAs(reward)); Conserved(g);
   }
  }
  [Test] public void FourFactionAiGamesFinishFortyRoundsWithExactlyOneHundredTwentyPeople()
  {
   foreach(var faction in new[]{FactionId.Poverty,FactionId.Gold,FactionId.Discipline,FactionId.Fur}) {
    var g=new GameSession(humanFaction:faction); var config=new AiSettings{randomSeed=11,actionSafetyLimit=40};
    var agents=Enumerable.Range(0,4).Select(i=>new RuleBasedAi(config,i)).ToArray(); var known=new HashSet<Guid>(); int steps=0;
    while(g.Phase!=GamePhase.Finished&&steps++<220) {
     if(g.Phase==GamePhase.RewardChoice) g.AcknowledgeRewards();
     else {
      int actor=g.ActivePlayerIndex; var turn=new AiTurn(g,actor,agents[actor],config);
      for(var c=turn.Next(g);c!=null;c=turn.Next(g)) { Assert.That(g.TryPlay(actor,c.CardInstanceId,out _,c.Target),Is.True); Conserved(g,known); Assert.That(g.TryPlay(actor,c.CardInstanceId,out _,c.Target),Is.False); }
      Assert.That(turn.Attempts,Is.LessThanOrEqualTo(40)); g.EndActionTurn(actor);
     }
     Assert.That(g.PopulationTotal,Is.EqualTo(120)); Conserved(g,known);
     foreach(var p in g.Players) { Assert.That(p.Faith,Is.InRange(0,100)); Assert.That(p.Doctrine,Is.InRange(0,100)); Assert.That(p.Offering,Is.GreaterThanOrEqualTo(0)); }
    }
    Assert.That(g.Phase,Is.EqualTo(GamePhase.Finished)); Assert.That(g.RewardHistory.Count,Is.EqualTo(40)); Assert.That(g.Winners,Is.Not.Empty);
    Assert.That(g.Winners.All(i=>g.Players[i].FollowerCount==g.Players.Max(p=>p.FollowerCount)),Is.True);
   }
  }
 }
}
