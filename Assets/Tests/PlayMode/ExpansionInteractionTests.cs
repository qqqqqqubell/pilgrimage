using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pilgrimage.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Pilgrimage.Tests
{
 public sealed partial class DemoInteractionTests
 {
  private void SelectNewGame(FactionId faction) {
   demo.AiConfiguration.autoPlay=false; demo.SendMessage("ResetGame"); Assert.That(demo.Game,Is.Null); Assert.That(demo.ChooseFaction(faction),Is.True);
  }
  private static void AdvanceModelRound(GameSession g) {
   int round=g.Round;
   for(int n=0;n<8&&g.Round==round&&g.Phase!=GamePhase.Finished;n++)
    if(g.Phase==GamePhase.RewardChoice) g.AcknowledgeRewards(); else g.EndActionTurn(g.ActivePlayerIndex);
  }
  [UnityTest] public IEnumerator FactionSelectionBlocksStartupAndAllFourButtonsAssignUniqueSeats()
  {
   foreach(var faction in new[]{FactionId.Poverty,FactionId.Gold,FactionId.Discipline,FactionId.Fur}) {
    demo.SendMessage("ResetGame"); for(int n=0;n<3;n++) yield return null;
    Assert.That(demo.Game,Is.Null); Assert.That(demo.IsAiRunning||demo.CanInteract,Is.False); demo.DebugOpponentStep(); demo.EnterCardLab(); Assert.That(demo.Game,Is.Null);
    Assert.That(demo.ChooseFaction(FactionId.None),Is.False);
    var buttons=demo.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("선택 · ")).ToArray(); Assert.That(buttons.Length,Is.EqualTo(4));
#if UNITY_EDITOR
    if(faction==FactionId.Poverty) CaptureRequested("-pilgrimageFactionScreenshot");
#endif
    buttons.Single(b=>b.name=="선택 · "+FactionSetup.Name(faction)).onClick.Invoke();
    Assert.That(demo.Game.HumanFaction,Is.EqualTo(faction)); Assert.That(demo.Game.Players.Select(p=>p.Faction).Distinct().Count(),Is.EqualTo(4));
    Assert.That(demo.Game.Players.All(p=>p.Hand.Count==4&&p.TotalCardCount==15&&p.Offering==5&&p.Faith==50&&p.Doctrine==50),Is.True);
    Assert.That(demo.Game.PopulationTotal,Is.EqualTo(120)); Assert.That(demo.ChooseFaction(faction),Is.False);
   }
  }
  [UnityTest] public IEnumerator FourteenExtendedCardsUseKeyboardAndTimedEffectsResolve()
  {
   foreach(var id in new[]{CardId.Penance,CardId.SolitaryReflection,CardId.CaravanInvestment,CardId.WitchHunt,CardId.HumbleTemple,CardId.TempleRemodeling,CardId.Archbishop,
      CardId.InterfaithLoan,CardId.MassAgitation,CardId.HeresyJudgment,CardId.RefusingFollower,CardId.CallOfFur,CardId.AreaCleansing,CardId.FurRally}) {
    demo.EnterCardLab(); demo.SetLabHand(new[]{id}); var g=demo.Game; var p=g.Players[0]; var source=p.Hand.Single(); var def=source.Definition;
    int cost=g.EffectiveCost(0,source,new EffectTarget{PlayerIndex=1}); demo.Select(source.InstanceId); Press(Key.Enter);
    if(demo.IsCardPickerOpen) { demo.ToggleCardTarget(g.SelectableCards(0,id,source.InstanceId).First().InstanceId); demo.ConfirmCardTargets(); }
    Assert.That(demo.IsAnimating,Is.True,id.ToString()); demo.PlaySelected();
    yield return new WaitForSecondsRealtime(.2f);
    Assert.That(p.Offering,Is.EqualTo(100-cost+def.OfferingGain+(id==CardId.InterfaithLoan?-10:0)),id.ToString());
    Assert.That(g.FindCard(source.InstanceId).Pile,Is.EqualTo(CardPile.Discard)); yield return new WaitForSecondsRealtime(.4f);
    int turns=id==CardId.Penance?4:id==CardId.Archbishop?3:id==CardId.SolitaryReflection||id==CardId.InterfaithLoan?1:
      id==CardId.HumbleTemple||id==CardId.TempleRemodeling||id==CardId.CaravanInvestment?2:0;
    for(int n=0;n<turns;n++) { demo.SendMessage("AdvanceLabTurn"); yield return null; }
    if(turns>0) Assert.That(p.TimedEffects,Is.Empty,id.ToString());
    if(id==CardId.Penance) { Assert.That(p.Offering,Is.EqualTo(127)); Assert.That(p.Faith,Is.EqualTo(52)); }
    if(id==CardId.HumbleTemple) Assert.That(p.Offering,Is.EqualTo(95));
    if(id==CardId.TempleRemodeling) { Assert.That(p.Offering,Is.EqualTo(110)); Assert.That(p.ChurchProperty,Is.EqualTo(-5)); }
    if(id==CardId.CaravanInvestment) Assert.That(p.Offering,Is.InRange(96,126));
    if(id==CardId.SolitaryReflection) Assert.That(p.Offering,Is.EqualTo(101));
    if(id==CardId.InterfaithLoan) Assert.That(p.Offering,Is.EqualTo(133));
    Assert.That(g.PopulationIsConserved,Is.True); Assert.That(demo.IsAiRunning,Is.False); demo.ExitCardLab();
   }
  }
  [UnityTest] public IEnumerator EdictTwoCardConfirmationRemovesHandAndDiscardPeopleExactlyOnce()
  {
   demo.EnterCardLab(); demo.SetLabHand(new[]{CardId.Edict}); var g=demo.Game; var source=g.Players[0].Hand.Single();
   var one=GameDebugTools.GiveCard(g,0,CardId.Follower); var two=GameDebugTools.GiveCard(g,0,CardId.Heretic,CardPile.Discard); demo.SendMessage("Refresh");
   demo.Select(source.InstanceId); demo.PlaySelected(); Assert.That(demo.IsCardPickerOpen,Is.True); demo.CloseCardPicker(); Assert.That(g.Players[0].Offering,Is.EqualTo(100));
   demo.PlaySelected(); demo.ToggleCardTarget(one.InstanceId); demo.ToggleCardTarget(two.InstanceId); demo.ConfirmCardTargets(); demo.ConfirmCardTargets();
   yield return new WaitForSecondsRealtime(.2f);
   Assert.That(g.Players[0].Offering,Is.EqualTo(90)); Assert.That(g.Atheists,Is.EqualTo(102));
   Assert.That(g.FindCard(one.InstanceId).Pile,Is.EqualTo(CardPile.Removed)); Assert.That(g.FindCard(two.InstanceId).Pile,Is.EqualTo(CardPile.Removed));
   yield return new WaitForSecondsRealtime(.4f); Assert.That(demo.IsAnimating,Is.False); Assert.That(g.PopulationIsConserved,Is.True); demo.ExitCardLab();
  }
  [UnityTest] public IEnumerator AllFactionSelectionsRestoreLabAndThreeAiOpponentsReturnControl()
  {
   foreach(var faction in new[]{FactionId.Poverty,FactionId.Gold,FactionId.Discipline,FactionId.Fur}) {
    SelectNewGame(faction); var g=demo.Game; demo.EnterCardLab(); Assert.That(demo.Game.HumanFaction,Is.EqualTo(faction));
    demo.SetLabHand(new[]{CardId.Archbishop}); demo.Select(demo.Game.Players[0].Hand[0].InstanceId); demo.PlaySelected();
    yield return new WaitForSecondsRealtime(.4f); demo.ExitCardLab(); Assert.That(demo.Game,Is.SameAs(g)); Assert.That(g.Players[0].TimedEffects,Is.Empty);
    demo.AiConfiguration.revealInterval=.1f; demo.AiConfiguration.travelDuration=.05f; demo.AiConfiguration.actionSafetyLimit=2;
    var followers=new int[4];
    for(int owner=1;owner<4;owner++) {
     GameDebugTools.ReplaceHand(g,owner,Enumerable.Repeat(CardId.Follower,5)); GameDebugTools.GiveCard(g,owner,CardId.Evangelism);
     GameDebugTools.SetResources(g,owner,20,80,80); followers[owner]=g.Players[owner].FollowerCount;
    }
    g.EndActionTurn(0); demo.AiConfiguration.autoPlay=true; float deadline=Time.realtimeSinceStartup+40;
    while((g.Round<2||g.ActivePlayerIndex!=0||demo.IsAiRunning)&&Time.realtimeSinceStartup<deadline) yield return null;
    Assert.That(g.Round,Is.EqualTo(2)); Assert.That(g.ActivePlayerIndex,Is.Zero); Assert.That(demo.IsAiRunning,Is.False);
    for(int owner=1;owner<4;owner++) Assert.That(g.Players[owner].FollowerCount,Is.GreaterThanOrEqualTo(followers[owner]+2));
   }
  }
  [UnityTest] public IEnumerator EveryFactionRandomRoundEightReceiptCanBeDrawnUsedAndAiContinues()
  {
   foreach(var faction in new[]{FactionId.Poverty,FactionId.Gold,FactionId.Discipline,FactionId.Fur}) {
    SelectNewGame(faction); var g=demo.Game; for(int n=1;n<8;n++) AdvanceModelRound(g); for(int n=0;n<4;n++) g.EndActionTurn(g.ActivePlayerIndex);
    var receipts=g.RoundRewards.Where(r=>r.PlayerIndex==0).ToArray(); Assert.That(receipts.Length,Is.EqualTo(GameSession.FactionRewardCount)); var receipt=receipts[0]; var reward=g.FindCard(receipt.InstanceId).Card;
    Assert.That(reward.Definition.Faction,Is.EqualTo(faction)); Assert.That(receipt.CardId!=CardId.RefusingFollower&&receipt.CardId!=CardId.FaithfulFollower,Is.True);
    demo.SendMessage("Refresh"); yield return null;
    Assert.That(demo.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="Award 0"&&t.text.Contains(reward.Definition.Name)&&t.text.Contains("버린 더미")),Is.True);
    demo.GetComponentsInChildren<Button>().Single(b=>b.name=="획득 확인 · 계속").onClick.Invoke();
    for(int n=0;n<15&&!g.Players[0].Hand.Contains(reward);n++) AdvanceModelRound(g);
    Assert.That(g.Players[0].Hand.Contains(reward),Is.True);
    for(int n=0;n<4&&g.ActivePlayerIndex!=0;n++) g.EndActionTurn(g.ActivePlayerIndex);
    GameDebugTools.SetResources(g,0,200,50,50,20);
    for(int owner=0;owner<4;owner++) { GameDebugTools.GiveCard(g,owner,CardId.Follower,CardPile.Deck); GameDebugTools.GiveCard(g,owner,CardId.Follower,CardPile.Deck); }
    demo.SendMessage("Refresh"); demo.Select(reward.InstanceId); Press(Key.Enter);
    if(demo.IsCardPickerOpen) { demo.ToggleCardTarget(g.SelectableCards(0,reward.Definition.Id,reward.InstanceId).First().InstanceId); demo.ConfirmCardTargets(); }
    yield return new WaitForSecondsRealtime(.2f);
    Assert.That(g.FindCard(reward.InstanceId).Pile,Is.EqualTo(CardPile.Discard)); yield return new WaitForSecondsRealtime(.4f);
    int round=g.Round; g.EndActionTurn(0); demo.AiConfiguration.autoPlay=true; demo.AiConfiguration.revealInterval=.1f; demo.AiConfiguration.travelDuration=.05f; demo.AiConfiguration.actionSafetyLimit=1;
    float deadline=Time.realtimeSinceStartup+40;
    while((g.Round<=round||g.ActivePlayerIndex!=0||demo.IsAiRunning)&&Time.realtimeSinceStartup<deadline) {
     if(!demo.IsAiRunning&&g.Phase==GamePhase.RewardChoice) g.AcknowledgeRewards(); yield return null;
    }
    Assert.That(g.Round,Is.GreaterThan(round)); Assert.That(g.ActivePlayerIndex,Is.Zero); Assert.That(demo.IsAiRunning,Is.False);
   }
  }
  [UnityTest] public IEnumerator JudgmentAccumulationAndRefusingExpiryUseActualUiState()
  {
   SelectNewGame(FactionId.Fur); demo.EnterCardLab(); demo.SetLabHand(new[]{CardId.CallOfFur}); var g=demo.Game;
   demo.Select(g.Players[0].Hand.Single().InstanceId); Press(Key.Enter); yield return new WaitForSecondsRealtime(.4f);
   var converts=g.Players[1].Hand.Concat(g.Players[1].Deck).Where(c=>c.Definition.Id==CardId.RefusingFollower).ToArray(); Assert.That(converts.Length,Is.EqualTo(2));
   for(int n=0;n<3;n++) { demo.SendMessage("AdvanceLabTurn"); yield return null; }
   Assert.That(converts.All(c=>c.Definition.Id==CardId.FaithfulFollower&&g.FindCard(c.InstanceId).Owner==0),Is.True);
   demo.SetLabHand(new[]{CardId.HeresyJudgment,CardId.HeresyJudgment});
   GameDebugTools.SetResources(g,0,200,50,50); GameDebugTools.SetResources(g,1,100,50,50);
   for(int n=0;n<2;n++) {
    var card=g.Players[0].Hand.First(c=>c.Definition.Id==CardId.HeresyJudgment);
    Assert.That(g.EffectiveCost(0,card,new EffectTarget{PlayerIndex=1}),Is.EqualTo(3+n));
    demo.Select(card.InstanceId); Press(Key.Enter); yield return new WaitForSecondsRealtime(.4f);
   }
   Assert.That(g.Players[0].JudgmentUses,Is.EqualTo(2)); Assert.That(g.Players[1].Doctrine,Is.EqualTo(25)); Assert.That(g.PopulationIsConserved,Is.True); demo.ExitCardLab();
  }
  [UnityTest] public IEnumerator GoldHumanAndThreeActualAiPlayersFinishFortyRoundsWithPopulationConserved()
  {
   SelectNewGame(FactionId.Gold); var g=demo.Game; demo.AiConfiguration.revealInterval=.1f; demo.AiConfiguration.travelDuration=.05f; demo.AiConfiguration.actionSafetyLimit=2; demo.AiConfiguration.autoPlay=true;
   float deadline=Time.realtimeSinceStartup+240; var known=new HashSet<System.Guid>(); int turns=0,rewards=0;
   while(g.Phase!=GamePhase.Finished&&Time.realtimeSinceStartup<deadline) {
    if(!demo.IsAiRunning) {
     if(g.Phase==GamePhase.Actions&&g.ActivePlayerIndex==0) { g.EndActionTurn(0); turns++; }
     else if(g.Phase==GamePhase.RewardChoice) { g.AcknowledgeRewards(); rewards++; }
    }
    var cards=g.Players.SelectMany(p=>p.Hand.Concat(p.Deck).Concat(p.Discard).Concat(p.Removed)).Select(c=>c.InstanceId).ToArray();
    Assert.That(cards.Distinct().Count(),Is.EqualTo(cards.Length)); Assert.That(known.IsSubsetOf(cards),Is.True); known.UnionWith(cards);
    Assert.That(g.PopulationTotal,Is.EqualTo(120)); Assert.That(g.PopulationIsConserved,Is.True); yield return null;
   }
   Assert.That(g.Phase,Is.EqualTo(GamePhase.Finished),"round "+g.Round+" actor "+g.ActivePlayerIndex);
   Assert.That(turns,Is.EqualTo(40)); Assert.That(rewards,Is.EqualTo(10)); Assert.That(g.RewardHistory.Count,Is.EqualTo(5*4+5*4*GameSession.FactionRewardCount)); Assert.That(g.Winners,Is.Not.Empty);
   float end=Time.realtimeSinceStartup+5; while(demo.IsAiRunning&&Time.realtimeSinceStartup<end) yield return null;
   Assert.That(demo.IsAiRunning,Is.False);
  }
 }
}
