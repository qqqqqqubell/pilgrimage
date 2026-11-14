using System;

namespace Pilgrimage
{
    // Plain data, deliberately independent of confirmed card definitions and Unity.
    [Serializable]
    public sealed class PrototypeTestConfig
    {
        public int playerCount = 2;
        public int startingOffering = 5;
        public int startingFaith = 20;
        public int startingDoctrine = 20;
        public int startingAtheists = 20;
        public bool clampEffectValuesAtZero = false;
        public CardId[] initialHand = {
            CardId.Evangelism, CardId.RequestOffering, CardId.RegularWorship, CardId.Follower
        };
        public CardId[] initialDeck = {
            CardId.Evangelism, CardId.RequestOffering, CardId.RegularWorship,
            CardId.Evangelism, CardId.RequestOffering, CardId.RegularWorship,
            CardId.Follower, CardId.Follower
        };
        // 0 means unlimited. Turn 1 receives no extra start-of-turn grants.
        public int maxCardsPerTurn = 0;
        public int drawOnTurnStart = 1;
        public int offeringOnTurnStart = 0;
        public bool discardHandOnTurnEnd = false;
        public bool reshuffleDiscardWhenDeckEmpty = true;
        public bool shuffleInitialDeck = false;
        public int randomSeed = 12345;
    }
}
