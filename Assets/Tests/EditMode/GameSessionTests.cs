using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Pilgrimage.Tests.BalanceTestSupport;

namespace Pilgrimage.Tests
{
    public sealed class GameSessionTests
    {
        [Test] public void FinalCatalogHasFortyFiveStableIdsAndExactCosts()
        {
            int[] costs = {5,0,1,-1,5,2,0,10,5,10,20,-1,30,5,0,5,0,0,4,8,2,5,-1,6,5,3,10,5,0,5,0,10,0,0,10,5,10,15,0,20,-1,5,10,10,20};
            Assert.That(CardCatalog.All.Count, Is.EqualTo(45));
            for (int i = 0; i < costs.Length; i++) {
                var def = CardCatalog.Get((CardId)i); Assert.That(def.Cost, Is.EqualTo(costs[i]), def.Name);
                Assert.That(def.Implementation, Is.EqualTo(EffectImplementation.Implemented));
                Assert.That(def.FlavorText, Is.Not.Empty);
            }
            Assert.That((int)CardId.Paradise, Is.EqualTo(37));
            Assert.That(CardCatalog.Get(CardId.Inquisitor).Name, Is.EqualTo("이단 심판관"));
            Assert.That(CardCatalog.Get(CardId.MassAgitation).CostText, Is.EqualTo("헌금 20"));
            Assert.That(CardCatalog.All.Count(c => c.Faction == FactionId.None), Is.EqualTo(22));
        }
        [Test] public void InitialFourPlayersHaveFinalResourcesDeckAndFourCardHands()
        {
            var game = Game(false); Assert.That(game.Atheists, Is.EqualTo(100));
            foreach (var p in game.Players) {
                Assert.That(p.Offering, Is.EqualTo(5)); Assert.That(p.Faith, Is.EqualTo(50)); Assert.That(p.Doctrine, Is.EqualTo(50));
                Assert.That(p.Hand.Count, Is.EqualTo(4)); Assert.That(p.Deck.Count, Is.EqualTo(11)); Assert.That(p.FollowerCount, Is.EqualTo(5));
                var cards = p.Hand.Concat(p.Deck).ToArray();
                foreach (var entry in new[] { Tuple.Create(CardId.Follower,5),Tuple.Create(CardId.Evangelism,3),Tuple.Create(CardId.RequestOffering,3),Tuple.Create(CardId.RegularWorship,4) })
                    Assert.That(cards.Count(c=>c.Definition.Id==entry.Item1), Is.EqualTo(entry.Item2));
            }
            Assert.That(game.PopulationTotal, Is.EqualTo(120)); Conserved(game);
        }
        [Test] public void FirstPlayerRotatesAndWrongActorCannotPlay()
        {
            var game = Game(false); var wrong = Give(game,CardId.RegularWorship,1);
            Assert.That(game.TryPlay(1,wrong.InstanceId,out _), Is.False);
            NextRound(game); Assert.That(game.FirstPlayerIndex, Is.EqualTo(1));
            Assert.That(game.Players.All(p=>p.Offering==8 && p.Faith==48 && p.Doctrine==48), Is.True);
            NextRound(game); Assert.That(game.FirstPlayerIndex, Is.EqualTo(2));
        }
        [Test] public void FollowerBonusIncomeUsesTenPersonBoundaryAndAllFollowerKinds()
        {
            foreach (int count in new[]{9,10,19,20}) {
                var game=Game(false);
                for(int n=5;n<count;n++) Give(game,n%2==0?CardId.FaithfulFollower:CardId.Follower,0,CardPile.Deck);
                Assert.That(game.PreviewRoundIncome(0), Is.EqualTo(3+count/10)); NextRound(game);
                Assert.That(game.Players[0].Offering, Is.EqualTo(8+count/10)); Conserved(game);
            }
        }
        [Test] public void NaturalDecayIsNotStoppedByArchbishopOrAffectedByRemodeling()
        {
            var game=Game(); Play(game,CardId.Archbishop); NextRound(game);
            Assert.That(game.Players[0].Faith, Is.EqualTo(48));
            var doubled=Game(); Play(doubled,CardId.TempleRemodeling); NextRound(doubled);
            Assert.That(doubled.Players[0].Faith, Is.EqualTo(48));
            var plain=Game(policy:new TimedEffectPolicy{remodelingAffectsNaturalDecay=false});
            Play(plain,CardId.TempleRemodeling); NextRound(plain); Assert.That(plain.Players[0].Faith,Is.EqualTo(48));
        }
        [Test] public void MissionAndReliefIgnoreOwnFaithAndMissionCostUsesTargetFaith()
        {
            foreach(int own in new[]{20,21,30,31}) foreach(int targetFaith in new[]{30,31}) {
                var game=Game(); Fund(game,0,200,own); Fund(game,1,200,targetFaith);
                var card=Give(game,CardId.AggressiveMission);
                Assert.That(game.EffectiveCost(0,card,new EffectTarget{PlayerIndex=1}),Is.EqualTo(targetFaith<=30?3:5));
                Assert.That(game.CanPlay(0,card.InstanceId,out _,new EffectTarget{PlayerIndex=1}),Is.True);
                var relief=Give(game,CardId.PoorRelief);
                Assert.That(game.EffectiveCost(0,relief,new EffectTarget{PlayerIndex=1}),Is.EqualTo(30));
                Assert.That(game.CanPlay(0,relief.InstanceId,out _,new EffectTarget{PlayerIndex=1}),Is.True);
            }
        }
        [Test] public void FaithAndDoctrineTenBoundariesReduceDrawIndependently()
        {
            foreach(int faith in new[]{10,11}) foreach(int doctrine in new[]{10,11}) {
                var game=Game(false); Fund(game,0,5,faith+2,doctrine+2); NextRound(game);
                Assert.That(game.Players[0].Hand.Count,Is.EqualTo(4-(faith<=10?1:0)-(doctrine<=10?1:0)));
            }
        }
        [Test] public void HeldHereticConvertsOneAtAllDoctrineLevelsAndSharedStartConversionRemains()
        {
            foreach(int doctrine in new[]{20,21,30,31}) {
                var game=Game(); Fund(game,0,200,50,doctrine);
                for(int n=0;n<3;n++) Give(game,CardId.Follower,0,CardPile.Deck);
                Give(game,CardId.Heretic);
                var targets=game.Players[0].Deck.Where(c=>c.Definition.Id==CardId.Follower).ToArray();
                game.EndActionTurn(0);
                Assert.That(targets.Count(c=>c.Definition.Id==CardId.Heretic),Is.EqualTo(1));
            }
            foreach(int doctrine in new[]{20,21}) {
                var game=Game(false); Fund(game,0,5,50,doctrine+2); var top=Follower(game);
                NextRound(game); Assert.That(top.Definition.Id,Is.EqualTo(doctrine<=20?CardId.Heretic:CardId.Follower));
            }
        }
        [Test] public void WorshipOfferingClampAndPaidCardFailureUseFinalPayloads()
        {
            var game=Game(false); Fund(game,0,1,99,99);
            Play(game,CardId.RegularWorship); Assert.That(game.Players[0].Faith,Is.EqualTo(100)); Assert.That(game.Players[0].Offering,Is.Zero);
            Play(game,CardId.RequestOffering); Assert.That(game.Players[0].Offering,Is.EqualTo(5)); Assert.That(game.Players[0].Doctrine,Is.EqualTo(95));
            Fund(game,0,4);
            var paid=Give(game,CardId.Evangelism); Assert.That(game.TryPlay(0,paid.InstanceId,out _),Is.False);
        }
        [Test] public void EvangelismPaysFiveAndConservesPeopleAndIdentities()
        {
            var game=Game(); var known=new HashSet<Guid>(Cards(game).Select(c=>c.InstanceId)); int followers=game.Players[0].FollowerCount;
            var source=Play(game,CardId.Evangelism);
            Assert.That(game.Players[0].Offering,Is.EqualTo(195)); Assert.That(game.Atheists,Is.EqualTo(98));
            Assert.That(game.Players[0].FollowerCount,Is.EqualTo(followers+2)); Assert.That(game.Players[0].Faith,Is.EqualTo(48));
            Assert.That(game.TryPlay(0,source.InstanceId,out _),Is.False); Conserved(game,known);
        }
        [Test] public void FaithfulFollowersAreDrawnCountedAndProtectedFromAllMutation()
        {
            var game=Game(); var normal=Follower(game,1);
            Play(game,CardId.PublicDenunciation,0,new EffectTarget{PlayerIndex=1});
            Assert.That(game.FindCard(normal.InstanceId).Pile,Is.EqualTo(CardPile.Removed));
            var faithful=Give(game,CardId.FaithfulFollower,1,CardPile.Deck);
            foreach(var id in new[]{CardId.PublicDenunciation,CardId.AggressiveMission,CardId.Reaper}) {
                var source=Give(game,id); Assert.That(game.TryPlay(0,source.InstanceId,out _,new EffectTarget{PlayerIndex=1}),Is.False);
            }
            Play(game,CardId.PoorRelief,0,new EffectTarget{PlayerIndex=1}); // "up to" permits an empty result.
            Assert.That(game.FindCard(faithful.InstanceId).Owner,Is.EqualTo(1)); Assert.That(faithful.Definition.Id,Is.EqualTo(CardId.FaithfulFollower)); Conserved(game);
        }
        [Test] public void IndulgenceUsesSpecialHandlerConvertsOneAndPreservesGuidAndPosition()
        {
            var game=Game(faction:FactionId.Gold); var p=game.Players[0]; var first=Follower(game);
            var second=Give(game,CardId.Follower,0,CardPile.Deck); var deck=p.Deck.ToArray(); int count=p.FollowerCount;
            var source=Play(game,CardId.Indulgence);
            Assert.That(source.Definition.EffectKey,Is.EqualTo("Indulgence"));
            Assert.That(p.Offering,Is.EqualTo(240)); Assert.That(p.ChurchProperty,Is.EqualTo(50)); Assert.That(p.Faith,Is.EqualTo(45)); Assert.That(p.Doctrine,Is.EqualTo(45));
            Assert.That(first.Definition.Id,Is.EqualTo(CardId.Heretic)); Assert.That(second.Definition.Id,Is.EqualTo(CardId.Follower));
            Assert.That(p.Deck,Is.EqualTo(deck)); Assert.That(p.FollowerCount,Is.EqualTo(count-1)); Conserved(game);
        }
        [Test] public void InterestAllowsOnlyOneSuccessfulCopyPerActionTurn()
        {
            var game=Game(faction:FactionId.Gold); Play(game,CardId.Interest);
            var next=Give(game,CardId.Interest); Assert.That(game.TryPlay(0,next.InstanceId,out _),Is.False);
            Assert.That(game.Players[0].Offering,Is.EqualTo(205)); Assert.That(game.Players[0].Hand.Contains(next),Is.True);
            NextRound(game); HumanTurn(game); Fund(game); var fresh=Give(game,CardId.Interest);
            Assert.That(game.TryPlay(0,fresh.InstanceId,out _),Is.True);
        }
        [Test] public void EdictRemovesUpToTwoFromAllThreePilesAndReturnsPeopleToAtheists()
        {
            var game=Game(); var one=Follower(game); var two=Give(game,CardId.Heretic,0,CardPile.Discard);
            var source=Give(game,CardId.Edict); var known=new HashSet<Guid>(Cards(game).Select(c=>c.InstanceId));
            var target=new EffectTarget{PlayerIndex=0,CardInstanceIds=new[]{one.InstanceId,two.InstanceId}};
            Assert.That(game.TryPlay(0,source.InstanceId,out _,target),Is.True);
            Assert.That(game.Atheists,Is.EqualTo(102)); Assert.That(game.Players[0].Offering,Is.EqualTo(190));
            Assert.That(game.FindCard(one.InstanceId).Card,Is.SameAs(one)); Assert.That(game.FindCard(two.InstanceId).Pile,Is.EqualTo(CardPile.Removed)); Conserved(game,known);
            var duplicate=Give(game,CardId.Edict);
            Assert.That(game.TryPlay(0,duplicate.InstanceId,out _,new EffectTarget{PlayerIndex=0,CardInstanceIds=new[]{duplicate.InstanceId,duplicate.InstanceId}}),Is.False);
        }
        [Test] public void RandomRewardsAreAlreadyAwardedEquallyAndCannotBeChosen()
        {
            var game=Game(false); for(int n=1;n<4;n++) NextRound(game);
            for(int n=0;n<4;n++) game.EndActionTurn(game.ActivePlayerIndex);
            Assert.That(game.RoundRewards.Count,Is.EqualTo(4)); Assert.That(game.Players.All(p=>p.TotalCardCount==16),Is.True);
            var ids=game.RoundRewards.Select(r=>r.InstanceId).ToArray();
            Assert.That(game.RoundRewards.All(r=>game.FindCard(r.InstanceId).Pile==CardPile.Discard),Is.True);
            Assert.That(game.RoundRewards.All(r=>game.EligibleRewards(r.PlayerIndex,RewardKind.General).Any(c=>c.Id==r.CardId)),Is.True);
            Assert.That(game.AcknowledgeRewards(),Is.True); Assert.That(game.Round,Is.EqualTo(5)); Assert.That(game.AcknowledgeRewards(),Is.False);
            Assert.That(game.RewardHistory.Select(r=>r.InstanceId),Is.EqualTo(ids)); Conserved(game);
        }
        [Test] public void EveryFactionRewardPoolExcludesFollowerPassivesAndRollsAreReproducible()
        {
            var a=Game(false); var b=Game(false);
            for(int round=1;round<=40;round++) {
                if(a.Phase==GamePhase.Finished) break;
                NextRound(a); NextRound(b);
                Assert.That(a.RewardHistory.Select(r=>r.CardId),Is.EqualTo(b.RewardHistory.Select(r=>r.CardId)));
                Conserved(a); Conserved(b);
            }
            Assert.That(a.RewardHistory.Count,Is.EqualTo(40));
            Assert.That(a.RewardHistory.All(r=>r.CardId!=CardId.Follower && r.CardId!=CardId.Heretic && r.CardId!=CardId.FaithfulFollower && r.CardId!=CardId.RefusingFollower),Is.True);
        }
        [Test] public void RoundCleanupReshuffleAndUsedCardsKeepEveryPhysicalIdentity()
        {
            var game=Game(false); var first=game.Players[0].Hand.ToArray(); var source=Play(game,CardId.RegularWorship);
            var known=new HashSet<Guid>(Cards(game).Select(c=>c.InstanceId)); game.EndActionTurn(0);
            Assert.That(game.Players[0].Hand,Is.EquivalentTo(first));
            for(int i=1;i<4;i++) game.EndActionTurn(i);
            Assert.That(game.Players[0].Hand.Count,Is.EqualTo(4)); Assert.That(game.Players[0].Discard.Count(c=>c.InstanceId==source.InstanceId),Is.EqualTo(1));
            for(int n=0;n<12;n++) { NextRound(game); Conserved(game,known); }
        }
        [Test] public void FanLayoutHandlesFourFiveAndLargeHandsWithoutOverflow()
        {
            foreach(int count in new[]{1,4,5,7,31}) {
                var slots=Enumerable.Range(0,count).Select(i=>HandFanLayout.Calculate(i,count,1300,200)).ToArray();
                Assert.That(slots.All(s=>Math.Abs(s.X)<=550 && !float.IsNaN(s.X)),Is.True);
                Assert.That(slots[0].X,Is.EqualTo(-slots[count-1].X).Within(.01));
            }
        }
    }
}
