using System;
using System.Collections.Generic;
using System.Linq;

namespace Pilgrimage
{
    [Serializable]
    public sealed class AiWeights
    {
        public float followers = 8;
        public float faith = 0.4f;
        public float doctrine = 0.4f;
        public float offering = 0.15f;
        public float disruption = 3;
        public float factionAbility = 0.5f;
        public float futureCards = 1.5f;
        public float cost = 0.05f;
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
        public float presentationTimeScale = 1; // Visual timing only; tests may accelerate, never changes rules.
        public float delayedRewardDiscount = 0.9f;
        public int savingHorizon = 3;
        public float savingPenalty = 0.35f;
        public float thresholdRiskMultiplier = 3;
        public float resourceUnlockWeight = 0.25f;
        public float thresholdCrossingValue = 6;
        public AiWeights defaultWeights = new AiWeights();
        public FactionAiProfile[] factions = {
            new FactionAiProfile { faction = FactionId.Poverty, weights = new AiWeights { followers = 10, futureCards = 2 } },
            new FactionAiProfile { faction = FactionId.Gold, weights = new AiWeights { offering = 0.35f, factionAbility = 1.5f } },
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
        public int SuccessfulUses { get; }
        public AiHandCard(CardInstance card) { InstanceId = card.InstanceId; Definition = card.Definition; SuccessfulUses = card.SuccessfulUses; }
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
        public int OwnHeretics { get; }
        public int JudgmentUses { get; }
        public bool JudgmentByInstance { get; }
        public bool JudgmentIncludesCurrent { get; }
        public bool InterestUsed { get; }
        public int OwnWitchHuntTargets { get; }
        public int OwnRoundIncome { get; }
        public int NaturalDecay { get; }
        public bool HasDecreaseProtection { get; }
        public double FaithDecreaseMultiplier { get; }
        public double DoctrineDecreaseMultiplier { get; }
        public bool RequireFullFollowerTargets { get; }
        public bool RequiresAtheists { get; }
        public bool AllowSelfAsOpponentTarget { get; }
        public IReadOnlyList<AiHandCard> Hand { get; }
        public IReadOnlyList<AiHandCard> OwnRemovalCandidates { get; }
        public IReadOnlyList<AiPublicPlayer> Players { get; }
        public AiPublicPlayer Self => Players[Actor];
        public AiObservation(GameSession game, int actor)
        {
            Actor = actor; Round = game.Round; Atheists = game.Atheists;
            RequireFullFollowerTargets = game.RequireFullFollowerTargets;
            RequiresAtheists = game.RequiresAtheists;
            AllowSelfAsOpponentTarget = game.AllowSelfAsOpponentTarget;
            var own = game.Players[actor];
            OwnChurchProperty = own.ChurchProperty;
            OwnDeckFollowers = own.Deck.Count(c => c.Definition.CountsAsFollower && !c.Definition.ProtectedFromCardEffects);
            OwnDeckFaithful = own.Deck.Count(c => c.Definition.Id == CardId.FaithfulFollower);
            OwnDeckHeretics = own.Deck.Count(c => c.Definition.Id == CardId.Heretic);
            OwnHeretics = OwnDeckHeretics + own.Hand.Count(c => c.Definition.Id == CardId.Heretic);
            JudgmentUses = own.JudgmentUses; JudgmentByInstance = game.EffectPolicy.judgmentCountScope == JudgmentCountScope.PhysicalCard;
            JudgmentIncludesCurrent = game.EffectPolicy.judgmentIncludesCurrentUse;
            InterestUsed = game.HasUsedInterest(actor);
            OwnWitchHuntTargets = SessionCardEffects.WitchHuntTargets(game, actor).Count();
            OwnRoundIncome = game.PreviewRoundIncome(actor);
            NaturalDecay = game.EffectPolicy.remodelingAffectsNaturalDecay ? -TimedCardEffects.StatChange(own, -2, false) : 2;
            HasDecreaseProtection = own.TimedEffects.Any(e => e.Kind == TimedEffectKind.PreventDecrease);
            FaithDecreaseMultiplier = own.Statuses.Any(e => e.Protection == EffectProtection.FaithChanges) ? 0 : -(double)TimedCardEffects.StatChange(own, -1);
            DoctrineDecreaseMultiplier = own.Statuses.Any(e => e.Protection == EffectProtection.DoctrineChanges) ? 0 : -(double)TimedCardEffects.StatChange(own, -1);
            RemodelingRequiresProperty = game.EffectPolicy.remodelingRequiresProperty;
            Hand = own.Hand.Select(c => new AiHandCard(c)).ToList().AsReadOnly();
            // Known own-card composition only, sorted by identity rather than deck position.
            OwnRemovalCandidates = own.Hand.Concat(own.Deck).Concat(own.Discard).Where(c => !c.Definition.ProtectedFromCardEffects)
                .OrderBy(c => c.InstanceId).Select(c => new AiHandCard(c)).ToList().AsReadOnly();
            Players = game.Players.Select((p, i) => new AiPublicPlayer(i, p)).ToList().AsReadOnly();
        }
        public bool RemodelingRequiresProperty { get; }
    }

    public sealed class AiDecision
    {
        public Guid CardInstanceId { get; }
        public CardDefinition Definition { get; }
        public EffectTarget Target { get; }
        public float Score { get; }
        internal AiDecision(AiHandCard card, int target, float score, Guid targetCard = default, Guid[] targetCards = null)
        { CardInstanceId = card.InstanceId; Definition = card.Definition; Target = new EffectTarget { PlayerIndex = target, CardInstanceId = targetCard, CardInstanceIds = targetCards }; Score = score; }
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
            => ChooseFrom(EvaluateActions(view, excluded));

        // Includes zero/negative scores for diagnostics. Legality and strategic value
        // are separate: an affordable, implemented action is not necessarily useful.
        public IReadOnlyList<AiDecision> EvaluateActions(AiObservation view, ISet<Guid> excluded = null)
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
                    var removals = view.OwnRemovalCandidates.Where(c => c.InstanceId != card.InstanceId)
                        .OrderByDescending(c => c.Definition.Id == CardId.Heretic || c.Definition.Implementation != EffectImplementation.Implemented).Take(2).ToArray();
                    for (int n = 1; n <= removals.Length; n++)
                    {
                        float value = removals.Take(n).Sum(c => c.Definition.Id == CardId.Heretic || c.Definition.Implementation != EffectImplementation.Implemented ? weights.removeDeadCard : c.Definition.CountsAsFollower ? -weights.followers : 0);
                        float score = value - def.Cost * (weights.offering + weights.cost);
                        candidates.Add(new AiDecision(card, view.Actor, score, removals[0].InstanceId, removals.Take(n).Select(c => c.InstanceId).ToArray()));
                    }
                    continue;
                }
                if (def.Id == CardId.FurRally)
                {
                    if (view.Self.Offering < def.Cost) continue;
                    float score = Evaluate(view, def, view.Self, def.Cost, settings.For(view.Self.Faction));
                    foreach (var choice in view.OwnRemovalCandidates.Where(c => c.Definition.CountsAsFollower && c.InstanceId != card.InstanceId))
                        candidates.Add(new AiDecision(card, view.Actor, score, choice.InstanceId));
                    continue;
                }
                if (def.Target == TargetKind.Card) continue;
                foreach (AiPublicPlayer target in view.Players)
                {
                    if (def.Target == TargetKind.Self && target.Index != view.Actor) continue;
                    if (def.Target != TargetKind.Self && target.Index == view.Actor && !view.AllowSelfAsOpponentTarget) continue;
                    int uses = (view.JudgmentByInstance ? card.SuccessfulUses : view.JudgmentUses) + (view.JudgmentIncludesCurrent ? 1 : 0);
                    int cost = def.Id == CardId.HeresyJudgment ? 3 + uses : def.Id == CardId.AggressiveMission ? target.Faith <= 30 ? 3 : 5 : def.Cost;
                    if (cost < 0 || cost > view.Self.Offering || !PublicConditions(view, def)) continue;
                    float score = Evaluate(view, def, target, cost, settings.For(view.Self.Faction));
                    if (cost > 0 && settings.savingPenalty > 0)
                    {
                        float reserve = 0;
                        foreach (var expensive in view.Hand.Where(h => h.Definition.IsPlayable && h.Definition.Implementation == EffectImplementation.Implemented &&
                            h.Definition.Cost > view.Self.Offering && h.Definition.Cost <= view.Self.Offering + view.OwnRoundIncome * Math.Max(0, settings.savingHorizon) && PublicConditions(view,h.Definition)))
                            reserve = Math.Max(reserve, Evaluate(view, expensive.Definition, target, expensive.Definition.Cost, settings.For(view.Self.Faction)));
                        score -= reserve * Math.Max(0, settings.savingPenalty);
                    }
                    candidates.Add(new AiDecision(card, target.Index, score));
                }
            }
            return candidates.AsReadOnly();
        }

        internal AiDecision ChooseFrom(IEnumerable<AiDecision> actions)
        {
            var worthwhile = actions.Where(c => c.Score > settings.minimumScore).ToArray();
            if (worthwhile.Length == 0) return null;
            float best = worthwhile.Max(c => c.Score);
            var similar = worthwhile.Where(c => c.Score >= best - Math.Max(0, settings.similarScoreWindow)).ToArray();
            return similar[random.Next(similar.Length)];
        }

        private static bool PublicConditions(AiObservation v, CardDefinition def)
        {
            if (v.RequiresAtheists && def.AtheistChange < 0 && v.Atheists < -def.AtheistChange) return false;
            switch (def.Id)
            {
                case CardId.GlimmerOfHope: return v.Self.Offering <= 10;
                case CardId.InterfaithLoan: return v.Self.Offering >= 10;
                case CardId.Interest: return !v.InterestUsed;
                case CardId.Paradise: return v.Self.Followers >= 20;
                case CardId.WitchHunt: return v.OwnWitchHuntTargets > 0;
                case CardId.TempleRemodeling: return !v.RemodelingRequiresProperty || v.OwnChurchProperty >= 5;
                case CardId.WildRoar: return v.Self.Faith >= 40 && v.Atheists >= 3;
                case CardId.Indulgence: return !v.RequireFullFollowerTargets || v.OwnDeckFollowers >= 1;
                case CardId.MoltingRitual: return v.OwnDeckFollowers >= 2;
                case CardId.BondOfFaith: return !v.RequireFullFollowerTargets || v.OwnDeckFollowers >= 1;
                default: return true;
            }
        }

        private float Evaluate(AiObservation v, CardDefinition c, AiPublicPlayer target, int cost, AiWeights w)
        {
            float followers = c.FollowersToDeck, faith = c.FaithChange, doctrine = c.DoctrineChange;
            float money = c.OfferingGain - cost, disruption = 0, faction = c.ChurchPropertyChange, future = 0;
            int remainingRounds = GameSession.TotalRounds - v.Round;
            float discount = Math.Max(0, Math.Min(1, settings.delayedRewardDiscount));
            switch (c.Id)
            {
                case CardId.Penance:
                    if (remainingRounds >= 4) { float value = (float)Math.Pow(discount, 4); money += 20 * value; faith += 10 * value; doctrine += 10 * value; }
                    break;
                case CardId.CaravanInvestment:
                    if (remainingRounds >= 2) money += 15 * discount * discount; break;
                case CardId.SolitaryReflection:
                    if (remainingRounds > 0) money += (v.OwnRoundIncome / 2 - v.OwnRoundIncome) * discount; break;
                case CardId.HumbleTemple:
                    money -= Math.Min(3, v.OwnRoundIncome) * Math.Min(2, remainingRounds) * discount; break;
                case CardId.TempleRemodeling:
                    money += v.OwnRoundIncome * Math.Min(2, remainingRounds) * discount;
                    break;
                case CardId.Archbishop:
                    if (!v.HasDecreaseProtection) faction += Math.Min(3, remainingRounds + 1); break;
                case CardId.WitchHunt: followers = -1; break;
                case CardId.RefusingFollower: followers = v.Self.Faction == FactionId.Fur ? 0 : 1; faction -= v.Self.Faction == FactionId.Fur ? 1 : 0; break;
                case CardId.InterfaithLoan:
                    money -= 10;
                    if (remainingRounds >= 1) money += Math.Min(40, target.Offering + 10 + GameSession.RoundOffering + target.Followers / 10) * discount;
                    future++; break;
                case CardId.HeresyJudgment:
                    int judgmentDecrease = 10 + (cost - 3) * 5;
                    disruption = Math.Min(target.Doctrine, judgmentDecrease) / 10f;
                    if (target.Doctrine - judgmentDecrease < 30) disruption += Math.Min(target.Followers, 2 + cost - 3);
                    future++; break;
                case CardId.CallOfFur: disruption = Math.Min(2, target.Followers); faction++; break;
                case CardId.MassAgitation:
                    followers = v.Players.Count(p => p.Index != v.Actor && p.Followers > 0); disruption = followers; break;
                case CardId.AreaCleansing:
                    disruption = v.Players.Where(p => p.Index != v.Actor).Sum(p => Math.Min(30, p.Faith) + Math.Min(30, p.Doctrine)) / 10f;
                    faith = -30; doctrine = -30; break;
                case CardId.FurRally:
                    disruption = v.Players.Where(p => p.Index != v.Actor).Sum(p => Math.Min(20, p.Faith) + Math.Min(20, p.Doctrine)) / 10f;
                    faction++; break;
                case CardId.Heretic: followers = 1; break;
                case CardId.ClergyTraining: future = v.Atheists >= 2 ? 2 : 0; break;
                case CardId.Preaching: future = v.Atheists >= 2 ? 1 : 0; break;
                case CardId.BondOfFaith: followers = target.Index == v.Actor ? 0 : -Math.Min(1, v.OwnDeckFollowers); break;
                case CardId.AggressiveMission: followers = 1; disruption = 1; break;
                case CardId.PoorRelief: followers = target.Faith <= 20 ? 3 : 2; disruption = followers; break;
                case CardId.PublicDenunciation: disruption = 1; break;
                case CardId.Reaper: disruption = 2; break;
                case CardId.SalvationFundraising: money++; future++; break;
                case CardId.Interest: money += Math.Max(0, v.OwnChurchProperty / 4); break;
                case CardId.Indulgence: followers = -Math.Min(1, v.OwnDeckFollowers); break;
                case CardId.Inquisitor: followers = v.OwnHeretics; break;
                case CardId.MoltingRitual: faction += Math.Min(2, v.OwnDeckFollowers); break;
                case CardId.DeepForestSanctuary: faith = (v.OwnDeckFaithful + v.Hand.Count(h => h.Definition.Id == CardId.FaithfulFollower)) / 4 * 8; break;
                case CardId.WildRoar: disruption = 3; break;
                case CardId.FalseRevelation: disruption = Math.Min(10, target.Faith) + Math.Min(10, target.Doctrine); disruption /= 10; if (target.Faith <= 40 || target.Doctrine <= 40) money++; break;
                case CardId.Blessing: future = Math.Min(2, v.OwnDeckFollowers + v.OwnDeckFaithful); break;
                case CardId.ReturnToFaith: followers = Math.Min(2, v.OwnHeretics); break;
            }
            if (target.Index == v.Actor && c.Target == TargetKind.Player)
            {
                // A development rule may allow self-targeting. Do not count harming
                // our own congregation as disruption of an opponent.
                switch (c.Id)
                {
                    case CardId.InterfaithLoan: money = 0; break;
                    case CardId.HeresyJudgment:
                        disruption = 0;
                        doctrine = -(10 + (cost - 3) * 5);
                        if (v.Self.Doctrine + doctrine * v.DoctrineDecreaseMultiplier < 30) followers = -Math.Min(v.OwnDeckFollowers, 2 + cost - 3);
                        break;
                    case CardId.CallOfFur:
                        disruption = 0;
                        if (v.Self.Faction != FactionId.Fur) { followers = -Math.Min(2, v.Self.Followers); faction = 0; }
                        break;
                    case CardId.PublicDenunciation: followers = -Math.Min(1, v.OwnDeckFollowers); disruption = 0; break;
                    case CardId.Reaper: followers = -Math.Min(2, v.OwnDeckFollowers); disruption = 0; break;
                    case CardId.AggressiveMission: followers = 0; disruption = 0; break;
                    case CardId.PoorRelief:
                        future = Math.Min(target.Faith <= 20 ? 3 : 2, v.OwnDeckFollowers);
                        followers = 0; disruption = 0; break;
                    case CardId.FalseRevelation: faith = -10; doctrine = -10; disruption = 0; break;
                }
            }
            if (remainingRounds == 0) future = 0;
            // Do not spend resources on capped statistics or effects with no useful gain.
            if (faith < 0) faith *= (float)v.FaithDecreaseMultiplier;
            if (doctrine < 0) doctrine *= (float)v.DoctrineDecreaseMultiplier;
            faith = Math.Max(-v.Self.Faith, Math.Min(100 - v.Self.Faith, faith));
            doctrine = Math.Max(-v.Self.Doctrine, Math.Min(100 - v.Self.Doctrine, doctrine));
            float faithPriority = v.Self.Faith < settings.lowStatThreshold ? Math.Max(1, settings.lowStatMultiplier) : 1;
            float doctrinePriority = v.Self.Doctrine < settings.lowStatThreshold ? Math.Max(1, settings.lowStatMultiplier) : 1;
            if (v.Self.Faith <= 32) faithPriority = Math.Max(faithPriority, settings.thresholdRiskMultiplier);
            if (v.Self.Doctrine <= 32) doctrinePriority = Math.Max(doctrinePriority, settings.thresholdRiskMultiplier);
            float score = followers * w.followers + faith * w.faith * faithPriority + doctrine * w.doctrine * doctrinePriority + money * w.offering + disruption * w.disruption + faction * w.factionAbility + future * w.futureCards - cost * w.cost;
            foreach (int threshold in new[] { 10, 20, 30 })
            {
                bool beforeFaith = v.Self.Faith - v.NaturalDecay <= threshold, afterFaith = v.Self.Faith + faith - v.NaturalDecay <= threshold;
                bool beforeDoctrine = v.Self.Doctrine - v.NaturalDecay <= threshold, afterDoctrine = v.Self.Doctrine + doctrine - v.NaturalDecay <= threshold;
                if (beforeFaith != afterFaith) score += (beforeFaith ? 1 : -1) * settings.thresholdCrossingValue;
                if (beforeDoctrine != afterDoctrine) score += (beforeDoctrine ? 1 : -1) * settings.thresholdCrossingValue;
            }
            if (c.OfferingGain > 0 && settings.resourceUnlockWeight > 0)
            {
                float unlocked = 0;
                foreach (var funded in v.Hand.Where(h => h.Definition.IsPlayable && h.Definition.OfferingGain == 0 &&
                    h.Definition.Cost > v.Self.Offering && h.Definition.Cost <= v.Self.Offering + c.OfferingGain && PublicConditions(v,h.Definition)))
                    unlocked = Math.Max(unlocked, Evaluate(v, funded.Definition, target, funded.Definition.Cost, w));
                score += unlocked * settings.resourceUnlockWeight;
            }
            return score;
        }


    }

    // Broker checks every proposed action/target through the human legality path.
    // The evaluator still never receives opponents' private cards or validation reasons.
    public sealed class AiTurn
    {
        private readonly int actor, round, limit;
        private readonly RuleBasedAi planner;
        private readonly HashSet<Guid> excluded = new HashSet<Guid>();
        public int Attempts { get; private set; }
        public int LegalityChecks { get; private set; }
        public AiTurn(GameSession game, int actor, RuleBasedAi planner, AiSettings settings)
        { this.actor = actor; round = game.Round; this.planner = planner; limit = Math.Max(1, Math.Min(200, settings.actionSafetyLimit)); }
        public AiDecision Next(GameSession game)
        {
            if (Attempts < limit && game.Phase == GamePhase.Actions && game.Round == round && game.ActivePlayerIndex == actor)
            {
                var legal = new List<AiDecision>();
                // Finite snapshot: an invalid target cannot discard the other targets,
                // consume the action cap, or cause an unbounded retry loop.
                foreach (var candidate in planner.EvaluateActions(new AiObservation(game, actor), excluded))
                {
                    LegalityChecks++;
                    if (game.CanPlay(actor, candidate.CardInstanceId, out _, candidate.Target)) legal.Add(candidate);
                }
                var decision = planner.ChooseFrom(legal);
                if (decision == null) return null;
                Attempts++;
                // Also prevents repeating a submitted action that fails during reveal.
                excluded.Add(decision.CardInstanceId);
                return decision;
            }
            return null;
        }
    }
}
