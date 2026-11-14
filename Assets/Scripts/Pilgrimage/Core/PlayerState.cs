using System.Collections.Generic;

namespace Pilgrimage
{
    public sealed class PlayerState
    {
        public string Name { get; }
        public int Offering { get; internal set; }
        public int Faith { get; internal set; }
        public int Doctrine { get; internal set; }
        public FactionId Faction { get; internal set; }
        public int ChurchProperty { get; internal set; }
        public int JudgmentUses { get; internal set; }
        internal readonly List<OngoingEffect> MutableStatuses = new List<OngoingEffect>();
        public IReadOnlyList<OngoingEffect> Statuses => MutableStatuses.AsReadOnly();
        internal readonly List<TimedCardEffect> MutableTimedEffects = new List<TimedCardEffect>();
        public IReadOnlyList<TimedCardEffect> TimedEffects => MutableTimedEffects.AsReadOnly();
        public int FollowerCount => CountFollowers();
        public int TotalCardCount => deck.Count + hand.Count + discard.Count;

        private int CountFollowers()
        {
            int count = 0;
            foreach (var pile in new[] { deck, hand, discard })
                foreach (CardInstance card in pile) if (card.Definition.CountsAsFollower) count++;
            return count;
        }
        private readonly List<CardInstance> deck = new List<CardInstance>();
        private readonly List<CardInstance> hand = new List<CardInstance>();
        private readonly List<CardInstance> discard = new List<CardInstance>();
        private readonly List<CardInstance> removed = new List<CardInstance>();
        public IReadOnlyList<CardInstance> Deck { get; }
        public IReadOnlyList<CardInstance> Hand { get; }
        public IReadOnlyList<CardInstance> Discard { get; }
        public IReadOnlyList<CardInstance> Removed { get; }
        internal List<CardInstance> MutableDeck => deck;
        internal List<CardInstance> MutableHand => hand;
        internal List<CardInstance> MutableDiscard => discard;
        internal List<CardInstance> MutableRemoved => removed;

        internal PlayerState(string name, int offering, int faith, int doctrine)
        {
            Name = name;
            Offering = offering;
            Faith = faith;
            Doctrine = doctrine;
            Deck = deck.AsReadOnly();
            Hand = hand.AsReadOnly();
            Discard = discard.AsReadOnly();
            Removed = removed.AsReadOnly();
        }
    }
}
