using System;
using System.Collections.Generic;
using System.Linq;

namespace Pilgrimage
{
    public enum VisualCause { Card, TurnEnd, NaturalDecay, RoundIncome, RoundDraw, Reward, Delayed, Threshold, Duration }
    public enum StatusKind { Offering, Faith, Doctrine, Followers, Property, Atheists, HereticConversion, FollowerConversion, FaithfulConversion, RefusingConversion, Draw, Acquisition, EffectStarted, EffectEnded, Defense }
    [Serializable] public sealed class StatusDelta { public int player, amount; public StatusKind kind; }
    [Serializable] public sealed class GameVisualEvent
    {
        public int serial, round, actor, presentation;
        public VisualCause cause;
        public CardId card;
        public string source;
        public StatusDelta[] changes;
    }
    public sealed partial class GameSession
    {
        private readonly List<GameVisualEvent> visualEvents=new List<GameVisualEvent>();
        private int visualSerial;
        private VisualCapture visualCapture;
        public IReadOnlyList<GameVisualEvent> VisualEvents=>visualEvents.AsReadOnly();
        public GameVisualEvent LastVisualEvent=>visualEvents.LastOrDefault();
        // Temporary host-only capture. No card maps, private GUIDs or order enter the public event.
        private sealed class VisualCapture
        {
            internal int[][] values;
            internal Dictionary<Guid,Tuple<int,CardId,CardPile>> cards;
            internal HashSet<TimedCardEffect>[] effects;
            internal readonly HashSet<int> defenses=new HashSet<int>();
            internal VisualCause cause;internal int actor;internal CardId card;internal string source;
        }
        private IEnumerable<Tuple<int,CardPile,CardInstance>> PhysicalCards()
        {
            for(int owner=0;owner<PlayerCount;owner++)foreach(CardPile pile in new[]{CardPile.Hand,CardPile.Deck,CardPile.Discard})
                foreach(var c in Pile(players[owner],pile))yield return Tuple.Create(owner,pile,c);
        }
        private int[] PublicValues(PlayerState p)=>new[]{p.Offering,p.Faith,p.Doctrine,p.FollowerCount,p.ChurchProperty};
        private void BeginVisual(VisualCause cause,int actor=-1,CardInstance source=null)
        {
            visualCapture=new VisualCapture {cause=cause,actor=actor,card=source?.Definition.Id??default,source=source?.InstanceId.ToString("D"),
                values=players.Select(PublicValues).Concat(new[]{new[]{Atheists}}).ToArray(),
                cards=PhysicalCards().ToDictionary(c=>c.Item3.InstanceId,c=>Tuple.Create(c.Item1,c.Item3.Definition.Id,c.Item2)),
                effects=players.Select(p=>new HashSet<TimedCardEffect>(p.TimedEffects)).ToArray()};
        }
        private void FinishVisual()
        {
            var capture=visualCapture;visualCapture=null;if(capture==null)return;
            var changes=new List<StatusDelta>();
            Action<int,StatusKind,int> add=(p,k,n)=>{if(n!=0)changes.Add(new StatusDelta{player=p,kind=k,amount=n});};
            StatusKind[] kinds={StatusKind.Offering,StatusKind.Faith,StatusKind.Doctrine,StatusKind.Followers,StatusKind.Property};
            for(int p=0;p<PlayerCount;p++){
                var after=PublicValues(players[p]);for(int k=0;k<kinds.Length;k++)add(p,kinds[k],after[k]-capture.values[p][k]);
                add(p,StatusKind.EffectStarted,players[p].TimedEffects.Count(e=>!capture.effects[p].Contains(e)));
                add(p,StatusKind.EffectEnded,capture.effects[p].Count(e=>!players[p].TimedEffects.Contains(e)));
                if(capture.defenses.Contains(p))add(p,StatusKind.Defense,1);
            }
            add(-1,StatusKind.Atheists,Atheists-capture.values[PlayerCount][0]);
            var tallies=new Dictionary<Tuple<int,StatusKind>,int>();
            Action<int,StatusKind> count=(p,k)=>{var key=Tuple.Create(p,k);tallies.TryGetValue(key,out int n);tallies[key]=n+1;};
            foreach(var located in PhysicalCards()){
                int p=located.Item1;var c=located.Item3;
                if(!capture.cards.TryGetValue(c.InstanceId,out var old)||old.Item1!=p)count(p,StatusKind.Acquisition);
                if(located.Item2==CardPile.Hand&&old!=null&&(old.Item1!=p||old.Item3!=CardPile.Hand))count(p,StatusKind.Draw);
                if(old==null||old.Item2==c.Definition.Id)continue;
                if(c.Definition.Id==CardId.Heretic&&CardCatalog.Get(old.Item2).CountsAsFollower)count(p,StatusKind.HereticConversion);
                else if(old.Item2==CardId.Heretic&&c.Definition.CountsAsFollower)count(p,StatusKind.FollowerConversion);
                else if(c.Definition.Id==CardId.FaithfulFollower)count(p,StatusKind.FaithfulConversion);
                else if(c.Definition.Id==CardId.RefusingFollower)count(p,StatusKind.RefusingConversion);
            }
            foreach(var t in tallies)add(t.Key.Item1,t.Key.Item2,t.Value);
            if(changes.Count==0&&capture.cause!=VisualCause.Card)return;
            visualEvents.Add(new GameVisualEvent{serial=++visualSerial,round=Round,actor=capture.actor,cause=capture.cause,card=capture.card,source=capture.source,changes=changes.ToArray()});
            if(visualEvents.Count>64)visualEvents.RemoveRange(0,visualEvents.Count-64);
        }
        private void RecordDefense(int player,EffectProtection _) { visualCapture?.defenses.Add(player); }
    }
}
