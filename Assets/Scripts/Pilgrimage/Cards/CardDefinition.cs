using System;
using System.Collections.Generic;

namespace Pilgrimage
{
    // Existing numeric values are stable for serialized v1 settings.
    public enum CardId { Evangelism, RequestOffering, RegularWorship, Follower,
        Heretic, TempleRemodeling, Interest, ClergyTraining, PublicDenunciation,
        Council, Reaper, AggressiveMission, PoorRelief, SalvationFundraising,
        CharityPot, HumbleTemple, Tithe, Indulgence, Pope, Inquisitor, Inerrancy,
        Confession, FaithfulFollower, MoltingRitual, DeepForestSanctuary, WildRoar }
    public enum CardCategory { General, Follower, Heretic, Faction }
    public enum FactionId { None, Poverty, Gold, Discipline, Fur }
    public enum EffectImplementation { Implemented, SpecificationPending, NotImplemented }
    public enum TargetKind { Self, Player, Card }

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
        public CardCategory Category { get; }
        public FactionId Faction { get; }
        public EffectImplementation Implementation { get; }
        public TargetKind Target { get; }
        public string Conditions { get; }
        public string FlavorText { get; }
        public string EffectKey { get; }
        public int ChurchPropertyChange { get; }
        public string CostText { get; }
        public bool CountsAsFollower { get; }
        public bool ProtectedFromCardEffects { get; }

        public CardDefinition(CardId id, string name, string description, int cost,
            bool playable, int offeringGain = 0, int faithChange = 0,
            int doctrineChange = 0, int atheistChange = 0, int followersToDeck = 0,
            CardCategory category = CardCategory.General, FactionId faction = FactionId.None,
            EffectImplementation implementation = EffectImplementation.Implemented,
            TargetKind target = TargetKind.Self, string conditions = "헌금 코스트 지불 가능",
            string flavorText = "미정", string effectKey = "basic", int churchPropertyChange = 0,
            string costText = null, bool countsAsFollower = false, bool protectedFromCardEffects = false)
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
            Category = category;
            Faction = faction;
            Implementation = implementation;
            Target = target;
            Conditions = conditions;
            FlavorText = flavorText;
            EffectKey = effectKey;
            ChurchPropertyChange = churchPropertyChange;
            CostText = costText ?? (cost < 0 ? "미정" : $"헌금 {cost}");
            CountsAsFollower = countsAsFollower || category == CardCategory.Follower;
            ProtectedFromCardEffects = protectedFromCardEffects;
        }
    }

    // Each physical copy has its own identity, even when the definition is shared.
    public sealed class CardInstance
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
        public CardDefinition Definition { get; private set; }

        public CardInstance(CardId id) { Definition = CardCatalog.Get(id); }
        public CardInstance(CardDefinition definition)
        { Definition = definition ?? throw new ArgumentNullException(nameof(definition)); }
        // Conversion preserves the identity of the physical card.
        internal void Transform(CardDefinition definition) { Definition = definition; }
    }

    public static class CardCatalog
    {
        private static readonly Dictionary<CardId, CardDefinition> Definitions = CardCatalogData.Create();

        public static CardDefinition Get(CardId id)
        {
            if (!Definitions.TryGetValue(id, out CardDefinition definition))
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown card.");
            return definition;
        }
        public static IReadOnlyCollection<CardDefinition> All => Definitions.Values;
    }
}
