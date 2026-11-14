using System.Collections.Generic;

namespace Pilgrimage
{
    public sealed class PlayerState
    {
        public string Name { get; }
        public int Offering { get; internal set; }
        public int Faith { get; internal set; }
        public int Doctrine { get; internal set; }
        private readonly List<CardInstance> deck = new List<CardInstance>();
        private readonly List<CardInstance> hand = new List<CardInstance>();
        private readonly List<CardInstance> discard = new List<CardInstance>();
        public IReadOnlyList<CardInstance> Deck { get; }
        public IReadOnlyList<CardInstance> Hand { get; }
        public IReadOnlyList<CardInstance> Discard { get; }
        internal List<CardInstance> MutableDeck => deck;
        internal List<CardInstance> MutableHand => hand;
        internal List<CardInstance> MutableDiscard => discard;

        internal PlayerState(string name, int offering, int faith, int doctrine)
        {
            Name = name;
            Offering = offering;
            Faith = faith;
            Doctrine = doctrine;
            Deck = deck.AsReadOnly();
            Hand = hand.AsReadOnly();
            Discard = discard.AsReadOnly();
        }
    }
}
