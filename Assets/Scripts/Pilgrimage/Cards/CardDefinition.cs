using System;
using System.Collections.Generic;

namespace Pilgrimage
{
    public enum CardId { Evangelism, RequestOffering, RegularWorship, Follower }

    // Immutable definitions describe the supplied card rules, not test setup.
    public sealed class CardDefinition
    {
        public CardId Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Cost { get; }
        public bool IsPlayable { get; }
        public int OfferingGain { get; }
        public int FaithChange { get; }
        public int DoctrineChange { get; }
        public int AtheistChange { get; }
        public int FollowersToDeck { get; }

        internal CardDefinition(CardId id, string name, string description, int cost,
            bool playable, int offeringGain = 0, int faithChange = 0,
            int doctrineChange = 0, int atheistChange = 0, int followersToDeck = 0)
        {
            Id = id;
            Name = name;
            Description = description;
            Cost = cost;
            IsPlayable = playable;
            OfferingGain = offeringGain;
            FaithChange = faithChange;
            DoctrineChange = doctrineChange;
            AtheistChange = atheistChange;
            FollowersToDeck = followersToDeck;
        }
    }

    // Each physical copy has its own identity, even when the definition is shared.
    public sealed class CardInstance
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
        public CardDefinition Definition { get; }

        public CardInstance(CardId id) { Definition = CardCatalog.Get(id); }
    }

    public static class CardCatalog
    {
        private static readonly Dictionary<CardId, CardDefinition> Definitions =
            new Dictionary<CardId, CardDefinition>
            {
                { CardId.Evangelism, new CardDefinition(CardId.Evangelism, "포교",
                    "헌금 -1 / 무신론자 -2 / 신앙심 -2 / 신도 2장을 덱에 추가",
                    1, true, faithChange: -2, atheistChange: -2, followersToDeck: 2) },
                { CardId.RequestOffering, new CardDefinition(CardId.RequestOffering, "헌금 요구",
                    "헌금 -1 후 +2 / 신앙심 -5 / 교리 정합성 -5",
                    1, true, offeringGain: 2, faithChange: -5, doctrineChange: -5) },
                { CardId.RegularWorship, new CardDefinition(CardId.RegularWorship, "정기 예배",
                    "헌금 -1 / 신앙심 +5 / 교리 정합성 +5",
                    1, true, faithChange: 5, doctrineChange: 5) },
                { CardId.Follower, new CardDefinition(CardId.Follower, "[신도]",
                    "사용할 수 없는 카드입니다.", 0, false) }
            };

        public static CardDefinition Get(CardId id)
        {
            if (!Definitions.TryGetValue(id, out CardDefinition definition))
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown card.");
            return definition;
        }
    }
}
