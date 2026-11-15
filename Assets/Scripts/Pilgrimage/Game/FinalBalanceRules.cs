using System;
using System.Collections.Generic;
using System.Linq;

namespace Pilgrimage
{
    public enum JudgmentCountScope { Congregation, PhysicalCard }
    public enum LoanShortfallPolicy { CollectAvailable, CarryRemainder }

    public sealed class RoundRewardResult
    {
        public int Round { get; }
        public int PlayerIndex { get; }
        public CardId CardId { get; }
        public Guid InstanceId { get; }
        public CardPile Destination { get; }
        internal RoundRewardResult(int round, int owner, CardInstance card, CardPile destination)
        { Round = round; PlayerIndex = owner; CardId = card.Definition.Id; InstanceId = card.InstanceId; Destination = destination; }
    }

    public sealed partial class GameSession
    {
        private readonly Random rewardRandom;
        private readonly List<RoundRewardResult> rewardHistory = new List<RoundRewardResult>();
        private readonly List<RoundRewardResult> roundRewards = new List<RoundRewardResult>();
        private readonly HashSet<int> interestUsed = new HashSet<int>();
        private readonly Dictionary<Guid, int> refusing = new Dictionary<Guid, int>();
        private readonly List<int> winners = new List<int>();
        public IReadOnlyList<RoundRewardResult> RewardHistory => rewardHistory.AsReadOnly();
        public IReadOnlyList<RoundRewardResult> RoundRewards => roundRewards.AsReadOnly();
        public IReadOnlyList<int> Winners => winners.AsReadOnly();
        public int DebugPopulationDelta { get; internal set; }
        public int PopulationTotal => Atheists + players.Sum(p => p.Hand.Concat(p.Deck).Concat(p.Discard).Count(c => IsPerson(c.Definition)));
        public bool PopulationIsConserved => PopulationTotal == 120 + DebugPopulationDelta;
        internal static bool IsPerson(CardDefinition def) => def.CountsAsFollower || def.Id == CardId.Heretic;
        public int DrawCount(int owner) => Math.Max(0, RoundDraw - (players[owner].Faith <= 10 ? 1 : 0) - (players[owner].Doctrine <= 10 ? 1 : 0));
        public int RefusingTurns(Guid id) => refusing.TryGetValue(id, out int turns) ? turns : 0;
        public bool HasUsedInterest(int owner) => interestUsed.Contains(owner);
        internal void AddShuffled(int owner, CardId id) { players[owner].MutableDeck.Add(new CardInstance(id)); Shuffle(players[owner].MutableDeck); }
        internal void TrackDebugCard(CardInstance card, CardPile pile)
        {
            if (pile == CardPile.Removed) return;
            if (IsPerson(card.Definition)) DebugPopulationDelta++;
            if (card.Definition.Id == CardId.RefusingFollower) refusing[card.InstanceId] = 3;
        }
        public int JudgmentCount(int actor, CardInstance card)
            => (EffectPolicy.judgmentCountScope == JudgmentCountScope.PhysicalCard ? card?.SuccessfulUses ?? 0 : players[actor].JudgmentUses)
                + (EffectPolicy.judgmentIncludesCurrentUse ? 1 : 0);
        public int EffectiveCost(int actor, CardInstance card, EffectTarget target = default)
            => CostFor(card.Definition, actor, target.PlayerIndex, card);
        private int CostFor(CardDefinition def, int actor, int target, CardInstance source)
        {
            if (def.CostKind == CardCostKind.None || def.CostKind == CardCostKind.MissingSpecification) return -1;
            int cost = def.Id == CardId.HeresyJudgment ? 3 + JudgmentCount(actor, source) : def.Cost;
            if (def.Id == CardId.AggressiveMission)
            { if (target < 0 || target >= PlayerCount) return -1; cost = players[target].Faith <= 30 ? 3 : 5; }
            return cost;
        }
        public IReadOnlyList<CardDefinition> EligibleRewards(int owner, RewardKind kind)
            => catalog.Where(c => kind == RewardKind.General ? c.Category == CardCategory.General :
                c.Category == CardCategory.Faction && c.Faction == players[owner].Faction && c.Id != CardId.FaithfulFollower && c.Id != CardId.RefusingFollower).ToList().AsReadOnly();
        private void AwardRoundRewards()
        {
            BeginVisual(VisualCause.Reward);
            roundRewards.Clear();
            for (int offset = 0; offset < PlayerCount; offset++)
            {
                int owner = (FirstPlayerIndex + offset) % PlayerCount;
                var eligible = EligibleRewards(owner, CurrentRewardKind);
                var card = new CardInstance(eligible[rewardRandom.Next(eligible.Count)]);
                Pile(players[owner], rewardDestination).Add(card);
                var receipt = new RoundRewardResult(Round, owner, card, rewardDestination);
                roundRewards.Add(receipt); rewardHistory.Add(receipt);
            }
            Phase = GamePhase.RewardChoice; // Serialized enum compatibility; this is a results acknowledgement now.
            LastMessage = Round + "라운드 종료 · 4개 교단 무작위 카드 획득";
            FinishVisual();
        }
        public bool AcknowledgeRewards(int player = 0)
        {
            if (Phase != GamePhase.RewardChoice || player != 0) return false;
            AdvanceRound(); return true;
        }
        public IReadOnlyList<CardInstance> SelectableCards(int actor, CardId sourceId, Guid sourceGuid)
        {
            var own = players[actor];
            IEnumerable<CardInstance> candidates = own.Hand.Concat(own.Deck).Concat(own.Discard);
            if (sourceId == CardId.FurRally)
                candidates = SessionCardEffects.EligibleFollowers(own.Hand).Concat(SessionCardEffects.EligibleFollowers(own.Deck).Take(1));
            return candidates.Where(c => c.InstanceId != sourceGuid && !c.Definition.ProtectedFromCardEffects).ToArray();
        }
        internal void ApplyNaturalDecay(PlayerState player)
        {
            int decrease = EffectPolicy.remodelingAffectsNaturalDecay ? TimedCardEffects.StatChange(player, -2, false) : -2;
            player.Faith = (int)Math.Max(0, (long)player.Faith + decrease);
            player.Doctrine = (int)Math.Max(0, (long)player.Doctrine + decrease);
        }
        private void ResolveRefusingFollowers()
        {
            foreach (Guid id in refusing.Keys.ToArray())
            {
                var located = FindCard(id);
                if (located == null || located.Pile == CardPile.Removed || located.Card.Definition.Id != CardId.RefusingFollower)
                { refusing.Remove(id); continue; }
                if (--refusing[id] > 0) continue;
                int furOwner = players.FindIndex(p => p.Faction == FactionId.Fur);
                // Transfer before protection is acquired; keep the same physical object and GUID.
                Transfer(id, furOwner, CardPile.Deck);
                Transform(id, CardCatalog.Get(CardId.FaithfulFollower));
            }
        }
        private void ComputeWinners()
        {
            winners.Clear(); int best = players.Max(p => p.FollowerCount);
            for (int i = 0; i < PlayerCount; i++) if (players[i].FollowerCount == best) winners.Add(i);
            LastMessage = "40라운드 종료 · " + string.Join(" / ", winners.Select(i => players[i].Name)) + " · 신도 " + best;
        }
    }
}
