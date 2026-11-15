using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Pilgrimage.Tests
{
    public sealed class LanRoomTests
    {
        private readonly Dictionary<ulong,long> sequences=new Dictionary<ulong,long>();
        private bool Send(LanRoom r,ulong peer,LanCommandKind kind,out string reason,string card=null,int target=0,string[] cards=null,int? version=null,int? round=null)
        {
            sequences.TryGetValue(peer,out long seq); sequences[peer]=seq+1;
            return r.Submit(peer,new LanCommand {session=r.Session,sequence=seq+1,version=version??r.Version,round=round??r.Game?.Round??0,kind=kind,ready=true,card=card,target=target,targets=cards},out reason,9137);
        }
        private LanRoom Start(int humans=2)
        {
            sequences.Clear();var r=new LanRoom(0,"시험 방","호스트",new AiSettings{randomSeed=87},new DemoRuleAssumptions{shuffleInitialDeck=false});
            for(int i=1;i<humans;i++)Assert.That(r.Join((ulong)i,"참가자 "+i,out _),Is.True);
            for(int i=0;i<humans;i++)Assert.That(Send(r,(ulong)i,LanCommandKind.Ready,out _),Is.True);
            Assert.That(Send(r,0,LanCommandKind.Prepare,out _),Is.True);Assert.That(r.Game,Is.Null);
            int version=r.Version;
            for(int i=0;i<humans;i++)Assert.That(Send(r,(ulong)i,LanCommandKind.ConfirmAssignment,out _,version:version),Is.True);
            Assert.That(r.Phase,Is.EqualTo(LanPhase.Playing));return r;
        }
        [Test] public void EveryWishCombinationPreservesUniqueChoicesAndDeterministicFourFactions()
        {
            for(int count=2;count<=4;count++)for(int bits=0;bits<(1<<(count*2));bits++) {
                var desired=Enumerable.Range(0,count).Select(i=>(FactionId)(1+((bits>>(i*2))&3))).ToArray();
                var seats=LanRoom.Assign(desired,991);
                Assert.That(seats.Distinct().Count(),Is.EqualTo(4));Assert.That(seats,Is.EqualTo(LanRoom.Assign(desired,991)));
                for(int i=0;i<count;i++)if(desired.Count(f=>f==desired[i])==1)Assert.That(seats[i],Is.EqualTo(desired[i]));
            }
        }
        [Test] public void LobbyNeedsTwoHumansReadyAndAllAssignmentConfirmationsBeforeGameExists()
        {
            sequences.Clear();var r=new LanRoom(0,"로비","주인");
            Assert.That(Send(r,0,LanCommandKind.Ready,out _),Is.True);Assert.That(Send(r,0,LanCommandKind.Prepare,out _),Is.False);
            Assert.That(r.Join(42,"다른 사람",out _),Is.True);int version=r.Version;
            Assert.That(Send(r,42,LanCommandKind.Ready,out _,version:version),Is.True);
            Assert.That(Send(r,42,LanCommandKind.Prepare,out _),Is.False);Assert.That(Send(r,0,LanCommandKind.Prepare,out _),Is.True);
            Assert.That(r.Game,Is.Null);Assert.That(r.AssignmentSeed,Is.EqualTo(9137));Assert.That(r.CanJoin,Is.False);
            Assert.That(Send(r,0,LanCommandKind.ConfirmAssignment,out _),Is.True);Assert.That(r.Game,Is.Null);
            Assert.That(Send(r,42,LanCommandKind.ConfirmAssignment,out _),Is.True);
            Assert.That(r.Game.Players.Select(p=>p.Faction),Is.EqualTo(r.Assignment));Assert.That(r.Game.IsNetworkGame,Is.True);
            Assert.That(r.IsAi(0)||r.IsAi(1),Is.False);Assert.That(r.IsAi(2)&&r.IsAi(3),Is.True);
        }
        [Test] public void NetworkViewsExposeOnlyOwnCardsAndPublicCountsWithoutDeckOrder()
        {
            var r=Start(4);
            for(ulong peer=0;peer<4;peer++) {
                var view=r.ViewFor(peer);Assert.That(view.localSeat,Is.EqualTo((int)peer));
                Assert.That(view.hand.Select(c=>c.guid),Is.EqualTo(r.Game.Players[(int)peer].Hand.Select(c=>c.InstanceId.ToString("D"))));
                Assert.That(view.players.All(p=>p.handCount==4&&p.deckCount==11),Is.True);
                Assert.That(typeof(LanPlayer).GetFields().Any(f=>f.FieldType==typeof(LanCard[])||f.FieldType==typeof(Guid[])||f.FieldType==typeof(CardInstance[])),Is.False);
                var exposed=view.hand.Select(c=>c.guid).Concat(view.selections.SelectMany(s=>s.cards).Select(c=>c.guid)).ToHashSet();
                Assert.That(r.Game.Players.Where((p,i)=>i!=(int)peer).SelectMany(p=>p.Hand.Concat(p.Deck)).Any(c=>exposed.Contains(c.InstanceId.ToString("D"))),Is.False);
            }
        }
        [Test] public void ServerRejectsWrongActorForeignCardPassiveDebugStaleAndReplayWithoutMutation()
        {
            var r=Start();var card=r.Game.Players[0].Hand[0];int version=r.Version;
            Assert.That(Send(r,1,LanCommandKind.Play,out _,card.InstanceId.ToString("D")),Is.False);
            Assert.That(Send(r,0,LanCommandKind.Play,out _,r.Game.Players[1].Hand[0].InstanceId.ToString("D")),Is.False);
            Assert.That(Send(r,0,LanCommandKind.Play,out _,card.InstanceId.ToString("D")),Is.False);
            Assert.That(Send(r,0,LanCommandKind.Debug,out _),Is.False);
            Assert.That(Send(r,0,LanCommandKind.EndTurn,out _,version:version-1),Is.False);
            var command=new LanCommand{session=r.Session,sequence=1000,version=r.Version,round=1,kind=LanCommandKind.EndTurn};
            Assert.That(r.Submit(0,command,out _),Is.True);Assert.That(r.Submit(0,command,out _),Is.False);
            Assert.That(r.Game.ActivePlayerIndex,Is.EqualTo(1));Assert.That(r.Game.Players[0].Offering,Is.EqualTo(5));
            Assert.Throws<InvalidOperationException>(()=>GameDebugTools.GiveCard(r.Game,0,CardId.Evangelism));
            Assert.Throws<InvalidOperationException>(()=>GameDebugTools.ReplaceHand(r.Game,0,new[]{CardId.Evangelism}));
            Assert.Throws<InvalidOperationException>(()=>GameDebugTools.SetResources(r.Game,0,900,100,100));
            BalanceTestSupport.Conserved(r.Game);
        }
        [Test] public void RevealAppliesOnceAndDuplicateGuidCannotBeReusedAfterAnimation()
        {
            var r=Start(4);
            for(ulong i=0;i<4;i++)Assert.That(Send(r,i,LanCommandKind.EndTurn,out _),Is.True);
            Assert.That(r.Game.Round,Is.EqualTo(2));int actor=r.Game.ActivePlayerIndex;Assert.That(actor,Is.EqualTo(1));
            var card=r.Game.Players[actor].Hand.First(c=>c.Definition.Id==CardId.Evangelism);string id=card.InstanceId.ToString("D");
            Assert.That(Send(r,1,LanCommandKind.Play,out _,id,target:0),Is.True);
            Assert.That(r.Busy,Is.True);Assert.That(r.Game.Players[1].Offering,Is.EqualTo(8));
            Assert.That(Send(r,1,LanCommandKind.Play,out _,id),Is.False);
            Assert.That(r.CompletePlay(out _),Is.True);Assert.That(r.Game.Players[1].Offering,Is.EqualTo(3));
            Assert.That(r.CompletePlay(out _),Is.False);Assert.That(Send(r,1,LanCommandKind.EndTurn,out _),Is.False);
            r.FinishReveal();Assert.That(r.Busy,Is.False);Assert.That(Send(r,1,LanCommandKind.Play,out _,id),Is.False);
            Assert.That(r.Game.FindCard(card.InstanceId).Pile,Is.EqualTo(CardPile.Discard));Assert.That(r.Game.Atheists,Is.EqualTo(98));
            Assert.That(r.ViewFor(0).reveal.guid,Is.EqualTo(id));BalanceTestSupport.Conserved(r.Game);
        }
        [Test] public void LobbyDepartureResetsAssignmentAndGameDepartureHandsSeatToAi()
        {
            var r=Start(3);r.Disconnect(1);Assert.That(r.IsAi(1),Is.True);Assert.That(r.Members.Count,Is.EqualTo(2));
            Assert.That(r.Join(9,"늦은 참가",out _),Is.False);Assert.That(Send(r,0,LanCommandKind.EndTurn,out _),Is.True);
            Assert.That(r.StepAi(),Is.True);Assert.That(r.Game.ActivePlayerIndex,Is.EqualTo(2));
            r.Disconnect(0);Assert.That(r.Phase,Is.EqualTo(LanPhase.Closed));Assert.That(r.StepAi(),Is.False);
            sequences.Clear();var lobby=new LanRoom(0,"로비","주인");lobby.Join(1,"손님",out _);
            Send(lobby,0,LanCommandKind.Ready,out _);Send(lobby,1,LanCommandKind.Ready,out _);Send(lobby,0,LanCommandKind.Prepare,out _);
            lobby.Disconnect(1);Assert.That(lobby.Phase,Is.EqualTo(LanPhase.Lobby));Assert.That(lobby.Game,Is.Null);Assert.That(lobby.Members[0].Ready,Is.False);
        }
        [Test] public void SeatsKeepLobbyNicknamesAndHostCanSeeWhoTheGameIsWaitingOn()
        {
            var r=Start(3);var names=r.ViewFor(2).players.Select(p=>p.name).ToArray();
            Assert.That(names[0],Is.EqualTo("호스트 · "+r.Game.Players[0].Name));Assert.That(names[1],Does.StartWith("참가자 1 · "));
            Assert.That(names[3],Is.EqualTo(r.Game.Players[3].Name),"AI seat shows only its faction");
            Assert.That(r.Game.ActivePlayerIndex,Is.EqualTo(0));Assert.That(r.StalledRemoteHumans(),Is.Empty,"host's own turn is never a remote stall");
            Assert.That(Send(r,0,LanCommandKind.EndTurn,out _),Is.True);Assert.That(r.StalledRemoteHumans(),Is.EqualTo(new ulong[]{1}));
            r.Disconnect(1);Assert.That(r.IsAi(1),Is.True);Assert.That(r.StalledRemoteHumans(),Is.Empty);
            Assert.That(r.ViewFor(0).players[1].name,Does.StartWith("참가자 1 · "),"departed seat keeps its nickname");
            for(int n=0;n<2000&&r.Game.Phase!=GamePhase.RewardChoice;n++) {
                if(r.Busy){r.CompletePlay(out _);r.FinishReveal();}
                else if(r.IsAi(r.Game.ActivePlayerIndex))Assert.That(r.StepAi(),Is.True);
                else Assert.That(Send(r,(ulong)r.Game.ActivePlayerIndex,LanCommandKind.EndTurn,out _),Is.True);
            }
            Assert.That(r.Game.Phase,Is.EqualTo(GamePhase.RewardChoice));Assert.That(r.StalledRemoteHumans(),Is.EqualTo(new ulong[]{2}));
            Assert.That(Send(r,0,LanCommandKind.Acknowledge,out _),Is.True);Assert.That(r.Game.Phase,Is.EqualTo(GamePhase.RewardChoice));
            r.Disconnect(2);Assert.That(r.Game.Phase,Is.EqualTo(GamePhase.Actions),"removing the last unacknowledged player resumes the game");
            Assert.That(r.Game.Round,Is.EqualTo(5));
        }
        [Test] public void ClientCostLookupUsesOnlyHostComputedCosts()
        {
            var r=Start(2);var view=r.ViewFor(1);var card=view.hand[0];
            for(int t=0;t<4;t++)Assert.That(view.EffectiveCost(card,t),Is.EqualTo(r.Game.EffectiveCost(1,r.Game.FindCard(Guid.Parse(card.guid)).Card,new EffectTarget{PlayerIndex=t})));
            card.costs=null;Assert.That(view.EffectiveCost(card,0),Is.EqualTo(-1));Assert.That(view.EffectiveCost(view.hand[0],9),Is.EqualTo(-1));
        }
        [Test] public void TwoThreeFourHumanAuthorityGamesFinishFortyRoundsWithRandomRewardsAndNoLostCards()
        {
            for(int humans=2;humans<=4;humans++) {
                var r=Start(humans);var known=BalanceTestSupport.Cards(r.Game).Select(c=>c.InstanceId).ToHashSet();
                var bots=Enumerable.Range(0,4).Select(i=>new RuleBasedAi(new AiSettings{randomSeed=741},i)).ToArray();AiTurn turn=null;int actor=-1,round=-1;
                for(int n=0;n<12000&&r.Game.Phase!=GamePhase.Finished;n++) {
                    if(r.Busy){r.CompletePlay(out _);r.FinishReveal();}
                    else if(r.Game.Phase==GamePhase.RewardChoice){int v=r.Version;for(int i=0;i<humans;i++)Assert.That(Send(r,(ulong)i,LanCommandKind.Acknowledge,out _,version:v),Is.True);}
                    else if(r.IsAi(r.Game.ActivePlayerIndex))Assert.That(r.StepAi(),Is.True);
                    else {
                        int a=r.Game.ActivePlayerIndex;if(turn==null||actor!=a||round!=r.Game.Round){actor=a;round=r.Game.Round;turn=new AiTurn(r.Game,a,bots[a],new AiSettings());}
                        var decision=turn.Next(r.Game);
                        if(decision==null){Assert.That(Send(r,(ulong)a,LanCommandKind.EndTurn,out _),Is.True);turn=null;}
                        else Assert.That(Send(r,(ulong)a,LanCommandKind.Play,out string message,decision.CardInstanceId.ToString("D"),decision.Target.PlayerIndex,decision.Target.Cards.Select(id=>id.ToString("D")).ToArray()),Is.True,message);
                    }
                    BalanceTestSupport.Conserved(r.Game,known);
                }
                Assert.That(r.Game.Phase,Is.EqualTo(GamePhase.Finished),"humans="+humans);Assert.That(r.Game.Round,Is.EqualTo(40));Assert.That(r.Game.RewardHistory.Count,Is.EqualTo(40));
            }
        }
    }
}
