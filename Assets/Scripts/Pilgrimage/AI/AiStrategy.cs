using System;
using System.Collections.Generic;
using System.Linq;

namespace Pilgrimage
{
    [Serializable]
    public sealed class AiWeights
    {
        public float followers = 8;
        public float faith = 0.25f;
        public float doctrine = 0.25f;
        public float offering = 1.5f;
        public float disruption = 2;
        public float factionAbility = 0.5f;
        public float futureCards = 1.5f;
        public float cost = 0.8f;
        public float removeDeadCard = 6;
    }

    [Serializable]
    public sealed class FactionAiProfile
    {
        public FactionId faction;
        public AiWeights weights = new AiWeights();
    }

    [Serializable]
    public sealed class AiSettings
    {
        public bool autoPlay = true;
        public int actionSafetyLimit = 40;
        public float minimumScore = 0.05f;
        public float similarScoreWindow = 1;
        public int lowStatThreshold = 35;
        public float lowStatMultiplier = 2;
        public int randomSeed = -1; // Negative: varied games. Nonnegative: reproducible tests.
        public float revealInterval = 1;
        public float travelDuration = 0.25f;
        public AiWeights defaultWeights = new AiWeights();
        public FactionAiProfile[] factions = {
            new FactionAiProfile { faction = FactionId.Poverty, weights = new AiWeights { followers = 10, futureCards = 2 } },
            new FactionAiProfile { faction = FactionId.Gold, weights = new AiWeights { offering = 2.5f, factionAbility = 1.5f } },
            new FactionAiProfile { faction = FactionId.Discipline, weights = new AiWeights { faith = 0.5f, doctrine = 0.6f } },
            new FactionAiProfile { faction = FactionId.Fur, weights = new AiWeights { followers = 10, faith = 0.45f } }
        };
        public AiWeights For(FactionId faction)
            => factions?.FirstOrDefault(p => p != null && p.faction == faction)?.weights ?? defaultWeights ?? new AiWeights();
    }

    // Deliberately contains neither opponents' CardInstances nor any deck order.
    public sealed class AiPublicPlayer
    {
        public int Index { get; }
        public FactionId Faction { get; }
        public int Faith { get; }
        public int Doctrine { get; }
        public int Followers { get; }
        public int Offering { get; }
        public int HandCount { get; }
        public AiPublicPlayer(int index, PlayerState player)
        { Index = index; Faction = player.Faction; Faith = player.Faith; Doctrine = player.Doctrine; Followers = player.FollowerCount; Offering = player.Offering; HandCount = player.Hand.Count; }
    }

    public sealed class AiHandCard
    {
        public Guid InstanceId { get; }
        public CardDefinition Definition { get; }
        public AiHandCard(CardInstance card) { InstanceId = card.InstanceId; Definition = card.Definition; }
    }

    public sealed class AiObservation
    {
        public int Actor { get; }
        public int Round { get; }
        public int Atheists { get; }
        public int OwnChurchProperty { get; }
        public int OwnDeckFollowers { get; }
        public int OwnDeckFaithful { get; }
        public int OwnDeckHeretics { get; }
        public IReadOnlyList<AiHandCard> Hand { get; }
        public IReadOnlyList<AiHandCard> OwnRemovalCandidates { get; }
        public IReadOnlyList<AiPublicPlayer> Players { get; }
        public AiPublicPlayer Self => Players[Actor];
        public AiObservation(GameSession game, int actor)
        {
            Actor = actor; Round = game.Round; Atheists = game.Atheists;
            var own = game.Players[actor];
            OwnChurchProperty = own.ChurchProperty;
            OwnDeckFollowers = own.Deck.Count(c => c.Definition.CountsAsFollower && !c.Definition.ProtectedFromCardEffects);
            OwnDeckFaithful = own.Deck.Count(c => c.Definition.Id == CardId.FaithfulFollower);
            OwnDeckHeretics = own.Deck.Count(c => c.Definition.Id == CardId.Heretic);
            Hand = own.Hand.Select(c => new AiHandCard(c)).ToList().AsReadOnly();
            // Known own-card composition only, sorted by identity rather than deck position.
            OwnRemovalCandidates = own.Hand.Concat(own.Deck).Where(c => !c.Definition.ProtectedFromCardEffects)
                .OrderBy(c => c.InstanceId).Select(c => new AiHandCard(c)).ToList().AsReadOnly();
            Players = game.Players.Select((p, i) => new AiPublicPlayer(i, p)).ToList().AsReadOnly();
        }
    }

    public sealed class AiDecision
    {
        public Guid CardInstanceId { get; }
        public CardDefinition Definition { get; }
        public EffectTarget Target { get; }
        public float Score { get; }
        internal AiDecision(AiHandCard card, int target, float score, Guid targetCard = default)
        { CardInstanceId = card.InstanceId; Definition = card.Definition; Target = new EffectTarget { PlayerIndex = target, CardInstanceId = targetCard }; Score = score; }
    }

    // The evaluator cannot receive a GameSession. All choices use a sanitized snapshot.
    public sealed class RuleBasedAi
    {
        private readonly AiSettings settings;
        private readonly Random random;
        public RuleBasedAi(AiSettings settings, int playerIndex = 0)
        {
            this.settings = settings ?? new AiSettings();
            random = this.settings.randomSeed < 0 ? new Random(Guid.NewGuid().GetHashCode()) : new Random(this.settings.randomSeed + playerIndex);
        }

        public AiDecision Choose(AiObservation view, ISet<Guid> excluded = null)
        {
            var candidates = new List<AiDecision>();
            foreach (AiHandCard card in view.Hand)
            {
                CardDefinition def = card.Definition;
                if (excluded != null && excluded.Contains(card.InstanceId)) continue;
                if (!def.IsPlayable || def.Implementation != EffectImplementation.Implemented) continue;
                if (def.Id == CardId.Edict)
                {
                    if (view.Self.Offering < def.Cost) continue;
                    var weights = settings.For(view.Self.Faction);
                    foreach (var removal in view.OwnRemovalCandidates.Where(c => c.InstanceId != card.InstanceId))
                    {
                        float value = removal.Definition.Id == CardId.Heretic || removal.Definition.Implementation != EffectImplementation.Implemented ? weights.removeDeadCard : 0;
                        float score = value - def.Cost * (weights.offering + weights.cost);
                        if (score > settings.minimumScore) candidates.Add(new AiDecision(card, view.Actor, score, removal.InstanceId));
                    }
                    continue;
                }
                if (def.Target == TargetKind.Card) continue;
                foreach (AiPublicPlayer target in view.Players)
                {
                    if (def.Target == TargetKind.Self && target.Index != view.Actor) continue;
                    if (def.Target != TargetKind.Self && target.Index == view.Actor) continue;
                    int cost = def.Id == CardId.AggressiveMission ? target.Faith <= 30 ? 1 : 2 : def.Cost;
                    if (cost < 0 || cost > view.Self.Offering || !PublicConditions(view, def)) continue;
                    float score = Evaluate(view, def, target, cost, settings.For(view.Self.Faction));
                    if (score > settings.minimumScore) candidates.Add(new AiDecision(card, target.Index, score));
                }
            }
            if (candidates.Count == 0) return null;
            float best = candidates.Max(c => c.Score);
            var similar = candidates.Where(c => c.Score >= best - Math.Max(0, settings.similarScoreWindow)).ToArray();
            return similar[random.Next(similar.Length)];
        }

        private static bool PublicConditions(AiObservation v, CardDefinition def)
        {
            if (def.AtheistChange < 0 && v.Atheists < -def.AtheistChange) return false;
            switch (def.Id)
            {
                case CardId.GlimmerOfHope: return v.Self.Offering == 0;
                case CardId.Paradise: return v.Self.Followers >= 20;
                case CardId.WildRoar: return v.Self.Faith >= 70 && v.Atheists >= 3;
                case CardId.Indulgence: case CardId.MoltingRitual: return v.OwnDeckFollowers >= 2;
                case CardId.BondOfFaith: return v.OwnDeckFollowers >= 1;
                case CardId.Blessing: return v.OwnDeckFollowers + v.OwnDeckFaithful >= 2;
                default: return true;
            }
        }

        private float Evaluate(AiObservation v, CardDefinition c, AiPublicPlayer target, int cost, AiWeights w)
        {
            float followers = c.FollowersToDeck, faith = c.FaithChange, doctrine = c.DoctrineChange;
            float money = c.OfferingGain - cost, disruption = 0, faction = c.ChurchPropertyChange, future = 0;
            switch (c.Id)
            {
                case CardId.Heretic: followers = 1; break;
                case CardId.ClergyTraining: future = 2; break;
                case CardId.Preaching: future = 1; break;
                case CardId.BondOfFaith: followers = -1; break;
                case CardId.AggressiveMission: followers = 1; disruption = 1; break;
                case CardId.PoorRelief: followers = target.Faith <= 20 ? 3 : 2; disruption = followers; break;
                case CardId.PublicDenunciation: disruption = 1; break;
                case CardId.Reaper: disruption = 2; break;
                case CardId.SalvationFundraising: money++; future++; break;
                case CardId.Interest: money += Math.Max(0, v.OwnChurchProperty / 10); break;
                case CardId.Indulgence: followers = -2; break;
                case CardId.Inquisitor: followers = v.Hand.Count(h => h.Definition.Id == CardId.Heretic); break;
                case CardId.MoltingRitual: faction += 2; break;
                case CardId.DeepForestSanctuary: faith = (v.OwnDeckFaithful + v.Hand.Count(h => h.Definition.Id == CardId.FaithfulFollower)) / 4 * 5; break;
                case CardId.WildRoar: disruption = 3; break;
                case CardId.FalseRevelation: disruption = Math.Min(10, target.Faith) + Math.Min(10, target.Doctrine); disruption /= 10; if (target.Faith <= 40 || target.Doctrine <= 40) money++; break;
                case CardId.Blessing: future = 2; break;
                case CardId.ReturnToFaith: followers = Math.Min(2, v.OwnDeckHeretics); break;
            }
            // Do not spend resources on capped statistics or effects with no useful gain.
            faith = Math.Max(-v.Self.Faith, Math.Min(100 - v.Self.Faith, faith));
            doctrine = Math.Max(-v.Self.Doctrine, Math.Min(100 - v.Self.Doctrine, doctrine));
            float faithPriority = v.Self.Faith < settings.lowStatThreshold ? Math.Max(1, settings.lowStatMultiplier) : 1;
            float doctrinePriority = v.Self.Doctrine < settings.lowStatThreshold ? Math.Max(1, settings.lowStatMultiplier) : 1;
            return followers * w.followers + faith * w.faith * faithPriority + doctrine * w.doctrine * doctrinePriority + money * w.offering + disruption * w.disruption + faction * w.factionAbility + future * w.futureCards - cost * w.cost;
        }

        public CardDefinition ChooseReward(AiObservation view, IReadOnlyList<CardDefinition> options)
        {
            var usable = options.Where(c => c.Implementation == EffectImplementation.Implemented).ToArray();
            if (usable.Length == 0) return null;
            var weights = settings.For(view.Self.Faction);
            var scored = usable.Select(c => new { Card = c, Score = c.CountsAsFollower ? weights.followers : Evaluate(view, c, view.Players[(view.Actor + 1) % 4], Math.Max(0, c.Cost), weights) }).ToArray();
            float best = scored.Max(c => c.Score);
            var close = scored.Where(c => c.Score >= best - Math.Max(0, settings.similarScoreWindow)).ToArray();
            return close[random.Next(close.Length)].Card;
        }
    }

    // Broker uses the exact same legality check as humans, without exposing failed
    // hidden-target checks back to the evaluator. A failed card is skipped for this turn.
    public sealed class AiTurn
    {
        private readonly int actor, round, limit;
        private readonly RuleBasedAi planner;
        private readonly HashSet<Guid> excluded = new HashSet<Guid>();
        public int Attempts { get; private set; }
        public AiTurn(GameSession game, int actor, RuleBasedAi planner, AiSettings settings)
        { this.actor = actor; round = game.Round; this.planner = planner; limit = Math.Max(1, Math.Min(200, settings.actionSafetyLimit)); }
        public AiDecision Next(GameSession game)
        {
            while (Attempts < limit && game.Phase == GamePhase.Actions && game.Round == round && game.ActivePlayerIndex == actor)
            {
                var decision = planner.Choose(new AiObservation(game, actor), excluded);
                if (decision == null) return null;
                Attempts++;
                excluded.Add(decision.CardInstanceId);
                if (game.CanPlay(actor, decision.CardInstanceId, out _, decision.Target)) return decision;
            }
            return null;
        }
    }
}
