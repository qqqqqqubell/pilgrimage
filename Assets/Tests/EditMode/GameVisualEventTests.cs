using System.Linq;
using NUnit.Framework;

namespace Pilgrimage.Tests
{
    public sealed class GameVisualEventTests
    {
        private static int Change(GameVisualEvent e,int player,StatusKind kind)=>e.changes.Where(d=>d.player==player&&d.kind==kind).Sum(d=>d.amount);
        [Test] public void EvangelismEventContainsExactActualDeltasAndNoFailedOrDuplicatePlayEvent()
        {
            var g=BalanceTestSupport.Game();var source=BalanceTestSupport.Play(g,CardId.Evangelism);var e=g.LastVisualEvent;
            Assert.That(e.cause,Is.EqualTo(VisualCause.Card));Assert.That(e.source,Is.EqualTo(source.InstanceId.ToString("D")));
            Assert.That(Change(e,0,StatusKind.Offering),Is.EqualTo(-8));Assert.That(Change(e,0,StatusKind.Faith),Is.EqualTo(-2));Assert.That(Change(e,0,StatusKind.Followers),Is.EqualTo(2));
            Assert.That(Change(e,-1,StatusKind.Atheists),Is.EqualTo(-2));Assert.That(Change(e,0,StatusKind.Acquisition),Is.EqualTo(2));
            int serial=e.serial;Assert.That(g.TryPlay(0,source.InstanceId,out _),Is.False);Assert.That(g.LastVisualEvent.serial,Is.EqualTo(serial));
            var follower=g.Players[0].Hand.First(c=>c.Definition.Id==CardId.Follower);Assert.That(g.TryPlay(0,follower.InstanceId,out _),Is.False);Assert.That(g.LastVisualEvent.serial,Is.EqualTo(serial));
        }
        [Test] public void IndulgenceReportsActualConversionNotNewPeopleAndBlessingReportsActualDraw()
        {
            var g=BalanceTestSupport.Game();BalanceTestSupport.Play(g,CardId.Indulgence);var e=g.LastVisualEvent;
            Assert.That(Change(e,0,StatusKind.HereticConversion),Is.EqualTo(1));Assert.That(Change(e,0,StatusKind.Followers),Is.EqualTo(-1));Assert.That(Change(e,0,StatusKind.Property),Is.EqualTo(10));Assert.That(Change(e,0,StatusKind.Offering),Is.EqualTo(10));
            Assert.That(Change(e,-1,StatusKind.Atheists),Is.Zero);
            BalanceTestSupport.Give(g,CardId.Follower,pile:CardPile.Deck);BalanceTestSupport.Give(g,CardId.Follower,pile:CardPile.Deck);
            BalanceTestSupport.Play(g,CardId.Blessing);Assert.That(Change(g.LastVisualEvent,0,StatusKind.Draw),Is.EqualTo(2));
            Assert.That(Change(g.LastVisualEvent,0,StatusKind.Acquisition),Is.Zero);BalanceTestSupport.Conserved(g);
        }
        [Test] public void BlockedAttackEmitsDefenseWithoutInventedDamageAndNaturalDecayIsSeparate()
        {
            var g=BalanceTestSupport.Game();g.EndActionTurn(0);BalanceTestSupport.Play(g,CardId.Archbishop,1);
            g.EndActionTurn(1);g.EndActionTurn(2);g.EndActionTurn(3);g.EndActionTurn(1);g.EndActionTurn(2);g.EndActionTurn(3);
            int faith=g.Players[1].Faith,doc=g.Players[1].Doctrine;
            BalanceTestSupport.Play(g,CardId.FalseRevelation,target:new EffectTarget{PlayerIndex=1});var e=g.LastVisualEvent;
            Assert.That(Change(e,1,StatusKind.Defense),Is.EqualTo(1));Assert.That(Change(e,1,StatusKind.Faith),Is.Zero);Assert.That(Change(e,1,StatusKind.Doctrine),Is.Zero);
            Assert.That(g.Players[1].Faith,Is.EqualTo(faith));Assert.That(g.Players[1].Doctrine,Is.EqualTo(doc));
            Assert.That(g.VisualEvents.Where(v=>v.cause==VisualCause.NaturalDecay).SelectMany(v=>v.changes).Any(d=>d.player==1&&d.kind==StatusKind.Faith&&d.amount==-2),Is.True);
        }
        [Test] public void IncomeDrawRewardDelayAndDecayHaveDistinctCausesAndMonotonicIds()
        {
            var g=BalanceTestSupport.Game();BalanceTestSupport.Play(g,CardId.Penance);
            for(int n=0;n<4;n++)BalanceTestSupport.NextRound(g);
            var events=g.VisualEvents;Assert.That(events.Select(e=>e.serial).Distinct().Count(),Is.EqualTo(events.Count));
            foreach(var cause in new[]{VisualCause.RoundIncome,VisualCause.RoundDraw,VisualCause.Delayed,VisualCause.Reward,VisualCause.NaturalDecay})Assert.That(events.Any(e=>e.cause==cause),Is.True,cause.ToString());
            Assert.That(events.Where(e=>e.cause==VisualCause.Delayed).SelectMany(e=>e.changes).Any(d=>d.kind==StatusKind.Offering&&d.amount==20),Is.True);
            Assert.That(typeof(StatusDelta).GetFields().All(f=>f.FieldType!=typeof(string)&&f.FieldType!=typeof(System.Guid)),Is.True,"Public deltas contain no private card identifiers or names.");
        }
    }
}
