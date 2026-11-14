using System;
using System.Collections.Generic;
using System.Linq;

namespace Pilgrimage
{
    public enum GamePhase { Actions, RewardChoice, Finished }
    public enum CardPile { Deck, Hand, Discard, Removed }
    public enum RewardKind { General, Faction }

    [Serializable]
    public sealed class DemoRuleAssumptions
    {
        // Only unresolved rules live here. Confirmed numbers live in GameSession.
        public int startingOffering = 0;
        public int startingChurchProperty = 0;
        public int randomSeed = 20261114;
        public bool shuffleInitialDeck = true;
        public bool accumulateOfferingBetweenRounds = true;
        public int maxCardsPerActionTurn = 0; // 0: unlimited until explicit end.
        public CardPile rewardDestination = CardPile.Discard;
        public bool rejectWhenAtheistsInsufficient = true;
        public bool requireFullFollowerTargets = true;
        public bool allowSelfAsOpponentTarget = false;
    }

    public sealed class DeferredReward
    {
        public int Round { get; }
        public int PlayerIndex { get; }
        public RewardKind Kind { get; }
        internal DeferredReward(int round, int playerIndex, RewardKind kind)
        { Round = round; PlayerIndex = playerIndex; Kind = kind; }
    }

    public sealed class LocatedCard
    {
        public int Owner { get; }
        public CardPile Pile { get; }
        public CardInstance Card { get; }
        internal LocatedCard(int owner, CardPile pile, CardInstance card)
        { Owner = owner; Pile = pile; Card = card; }
    }

    public struct EffectTarget
    {
        public int PlayerIndex;
        public Guid CardInstanceId;
    }

    // Complex effects can validate targets before mutation and execute through this context.
    // No unimplemented effect is silently routed to the basic effect handler.
    public interface ICardEffectHandler
    {
        bool Validate(GameSession game, int actor, CardInstance card, EffectTarget target, out string reason);
        void Execute(CardEffectContext context);
    }

    public sealed class CardEffectContext
    {
        public GameSession Game { get; }
        public int Actor { get; }
        public CardInstance Source { get; }
        public EffectTarget Target { get; }
        internal CardEffectContext(GameSession game, int actor, CardInstance card, EffectTarget target)
        { Game = game; Actor = actor; Source = card; Target = target; }
        public void ApplyBasic() { Game.ApplyBasic(Actor, Source.Definition); }
        public bool TransferTarget(int newOwner, CardPile pile)
            => Target.CardInstanceId != Source.InstanceId && Game.Transfer(Target.CardInstanceId, newOwner, pile);
        public bool TransformTarget(CardDefinition definition)
            => Target.CardInstanceId != Source.InstanceId && Game.Transform(Target.CardInstanceId, definition);
        public void Protect(int player, EffectProtection protection, int ownerTurns)
        { Game.Players[player].MutableStatuses.Add(new OngoingEffect(protection, ownerTurns)); }
    }

    public sealed class GameSession
    {
        public const int PlayerCount = 4, TotalRounds = 40, RoundOffering = 5, RoundDraw = 5;
        public static readonly string[] FactionNames = {
            "청빈의 사도단", "순금의 형제단", "절대 교율 교단", "숲의 털가죽 교단"
        };
        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly List<CardDefinition> catalog;
        private readonly List<DeferredReward> deferred = new List<DeferredReward>();
        private readonly Dictionary<string, ICardEffectHandler> handlers;
        private readonly Random random;
        private readonly bool accumulate, requireAtheists;
        public bool RequireFullFollowerTargets { get; }
        public bool AllowSelfAsOpponentTarget { get; }
        public bool RequiresAtheists => requireAtheists;
        private readonly int maxCards;
        private readonly CardPile rewardDestination;
        private int actionOffset, rewardOffset;
        public IReadOnlyList<PlayerState> Players { get; }
        public IReadOnlyList<CardDefinition> Catalog { get; }
        public IReadOnlyList<DeferredReward> DeferredRewards { get; }
        public GamePhase Phase { get; private set; }
        public int Round { get; private set; } = 1;
        public int FirstPlayerIndex => (Round - 1) % PlayerCount;
        public int ActivePlayerIndex => (FirstPlayerIndex + actionOffset) % PlayerCount;
        public int RewardPlayerIndex => (FirstPlayerIndex + rewardOffset) % PlayerCount;
        public RewardKind CurrentRewardKind => Round % 8 == 0 ? RewardKind.Faction : RewardKind.General;
        public int Atheists { get; private set; } = 100;
        public int CardsPlayedThisTurn { get; private set; }
        public string LastMessage { get; private set; } = "4인 데모 시작. 행동 제출 방식은 순차 즉시 실행으로 임시 구현되었습니다.";

        public GameSession(DemoRuleAssumptions assumptions = null,
            IEnumerable<CardDefinition> additionalCards = null,
            IDictionary<string, ICardEffectHandler> additionalHandlers = null)
        {
            var rules = assumptions ?? new DemoRuleAssumptions();
            random = new Random(rules.randomSeed);
            accumulate = rules.accumulateOfferingBetweenRounds;
            requireAtheists = rules.rejectWhenAtheistsInsufficient;
            RequireFullFollowerTargets = rules.requireFullFollowerTargets;
            AllowSelfAsOpponentTarget = rules.allowSelfAsOpponentTarget;
            maxCards = Math.Max(0, rules.maxCardsPerActionTurn);
            rewardDestination = rules.rewardDestination;
            catalog = new List<CardDefinition>(CardCatalog.All);
            if (additionalCards != null)
                foreach (CardDefinition card in additionalCards)
                {
                    if (card == null || catalog.Any(c => c.Id == card.Id))
                        throw new ArgumentException("추가 카드 ID는 null 또는 기존 ID와 중복될 수 없습니다.");
                    catalog.Add(card);
                }
            handlers = new Dictionary<string, ICardEffectHandler> { { "basic", new BasicHandler() } };
            SessionCardEffects.Register(handlers);
            if (additionalHandlers != null)
                foreach (var pair in additionalHandlers)
                {
                    if (pair.Key == "basic" || string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                        throw new ArgumentException("유효한 별도 효과 키와 핸들러를 사용하세요.");
                    handlers.Add(pair.Key, pair.Value);
                }
            for (int i = 0; i < PlayerCount; i++)
            {
                var player = new PlayerState(FactionNames[i], Math.Max(0, rules.startingOffering), 50, 50)
                { Faction = (FactionId)(i + 1), ChurchProperty = rules.startingChurchProperty };
                Add(player.MutableDeck, CardId.Follower, 5);
                Add(player.MutableDeck, CardId.Evangelism, 4);
                Add(player.MutableDeck, CardId.RequestOffering, 3);
                Add(player.MutableDeck, CardId.RegularWorship, 3);
                if (rules.shuffleInitialDeck) Shuffle(player.MutableDeck);
                players.Add(player);
            }
            Players = players.AsReadOnly();
            Catalog = catalog.AsReadOnly();
            DeferredRewards = deferred.AsReadOnly();
            BeginRound();
        }

        public bool CanPlay(int actor, Guid cardId, out string reason, EffectTarget target = default)
        {
            if (Phase != GamePhase.Actions) return Fail("현재는 카드 사용 단계가 아닙니다.", out reason);
            if (actor < 0 || actor >= PlayerCount || actor != ActivePlayerIndex)
                return Fail("선플레이어 순서에 따른 현재 교단의 행동을 기다리세요.", out reason);
            CardInstance card = players[actor].Hand.FirstOrDefault(c => c.InstanceId == cardId);
            if (card == null) return Fail("현재 교단의 손패에 없는 카드입니다.", out reason);
            CardDefinition def = card.Definition;
            if (!def.IsPlayable || def.Category == CardCategory.Follower)
                return Fail("현재 사용할 수 없는 카드입니다.", out reason);
            if (def.Implementation != EffectImplementation.Implemented || !handlers.TryGetValue(def.EffectKey, out ICardEffectHandler handler))
                return Fail("현재 사용할 수 없는 카드입니다.", out reason);
            if (maxCards > 0 && CardsPlayedThisTurn >= maxCards) return Fail("임시 행동당 카드 수 제한에 도달했습니다.", out reason);
            if (requireAtheists && def.AtheistChange < 0 && Atheists < -def.AtheistChange)
                return Fail("공용 무신론자가 부족합니다. (임시 사용 조건)", out reason);
            if (def.Target != TargetKind.Self && (target.PlayerIndex < 0 || target.PlayerIndex >= PlayerCount))
                return Fail("유효한 대상 교단을 지정하세요.", out reason);
            if (def.Target != TargetKind.Self && def.Id != CardId.Edict && !AllowSelfAsOpponentTarget && target.PlayerIndex == actor)
                return Fail("현재 임시 설정에서는 다른 교단을 지정해야 합니다.", out reason);
            int cost = EffectiveCost(def, target.PlayerIndex);
            if (cost < 0) return Fail("현재 사용할 수 없는 카드입니다.", out reason);
            if (players[actor].Offering < cost) return Fail("헌금이 부족합니다.", out reason);
            if (def.Target == TargetKind.Card)
            {
                LocatedCard located = FindCard(target.CardInstanceId);
                if (located == null || located.Owner != target.PlayerIndex)
                    return Fail("지정한 대상 카드가 해당 교단에 없습니다.", out reason);
            }
            return handler.Validate(this, actor, card, target, out reason);
        }

        public bool TryPlay(int actor, Guid cardId, out string message, EffectTarget target = default)
        {
            if (!CanPlay(actor, cardId, out message, target)) { LastMessage = message; return false; }
            PlayerState player = players[actor];
            CardInstance card = player.Hand.First(c => c.InstanceId == cardId);
            string cardName = card.Definition.Name;
            int offering = player.Offering, faith = player.Faith, doctrine = player.Doctrine;
            player.Offering -= EffectiveCost(card.Definition, target.PlayerIndex);
            handlers[card.Definition.EffectKey].Execute(new CardEffectContext(this, actor, card, target));
            // Temporary resolution policy: successful plays discard immediately.
            // A central-table animation is a presentation reference, never a second card pile.
            player.MutableHand.Remove(card);
            player.MutableDiscard.Add(card);
            CardsPlayedThisTurn++;
            message = $"{player.Name} · {cardName} | 헌금 {offering}→{player.Offering} · 신앙 {faith}→{player.Faith} · 교리 {doctrine}→{player.Doctrine}";
            LastMessage = message;
            return true;
        }

        public bool EndActionTurn(int actor)
        {
            if (Phase != GamePhase.Actions || actor != ActivePlayerIndex) return false;
            PlayerState player = players[actor];
            // Heretics trigger at their owner's action end. Remaining hands are
            // discarded only after all four players have ended the round.
            foreach (CardInstance ignored in player.Hand.Where(c => c.Definition.Id == CardId.Heretic).ToArray())
            {
                CardInstance follower = SessionCardEffects.EligibleFollowers(player.Deck).FirstOrDefault();
                if (follower != null) Transform(follower.InstanceId, CardCatalog.Get(CardId.Heretic));
            }
            // Duration counts completed owner action turns, including the turn granted.
            foreach (OngoingEffect status in player.MutableStatuses) status.RemainingOwnerTurns--;
            player.MutableStatuses.RemoveAll(status => status.RemainingOwnerTurns <= 0);
            CardsPlayedThisTurn = 0;
            actionOffset++;
            LastMessage = player.Name + " 행동 종료";
            if (actionOffset == PlayerCount)
            {
                foreach (PlayerState p in players)
                {
                    // Played cards are already in Discard; move only residual hand cards.
                    p.MutableDiscard.AddRange(p.MutableHand);
                    p.MutableHand.Clear();
                }
                if (Round % 4 == 0) { Phase = GamePhase.RewardChoice; rewardOffset = 0; LastMessage = $"{Round}라운드 종료: 보상 선택"; }
                else AdvanceRound();
            }
            return true;
        }

        public IReadOnlyList<CardDefinition> RewardOptions()
        {
            if (Phase != GamePhase.RewardChoice) return Array.Empty<CardDefinition>();
            FactionId faction = players[RewardPlayerIndex].Faction;
            // Acquisition is independent of playability (e.g. faithful followers).
            return catalog.Where(c => CurrentRewardKind == RewardKind.General ? c.Category == CardCategory.General : c.Category == CardCategory.Faction && c.Faction == faction).ToList().AsReadOnly();
        }

        public bool ChooseReward(int player, CardId id)
        {
            if (Phase != GamePhase.RewardChoice || player != RewardPlayerIndex) return false;
            CardDefinition card = RewardOptions().FirstOrDefault(c => c.Id == id);
            if (card == null) return false;
            Pile(players[player], rewardDestination).Add(new CardInstance(card));
            LastMessage = $"{players[player].Name} 보상: {card.Name} → {rewardDestination} (획득 위치는 임시 설정)";
            NextReward();
            return true;
        }

        public bool DeferMissingReward(int player)
        {
            if (Phase != GamePhase.RewardChoice || player != RewardPlayerIndex || RewardOptions().Count != 0) return false;
            deferred.Add(new DeferredReward(Round, player, CurrentRewardKind));
            LastMessage = "교단 카드 명세 미등록: 보상권을 보류 내역에 기록했습니다. 보상 지급 완료가 아닙니다.";
            NextReward();
            return true;
        }

        public LocatedCard FindCard(Guid id)
        {
            for (int i = 0; i < PlayerCount; i++)
                foreach (CardPile pile in new[] { CardPile.Deck, CardPile.Hand, CardPile.Discard, CardPile.Removed })
                    foreach (CardInstance card in Pile(players[i], pile))
                        if (card.InstanceId == id) return new LocatedCard(i, pile, card);
            return null;
        }

        internal bool Transfer(Guid id, int newOwner, CardPile destination)
        {
            LocatedCard source = FindCard(id);
            if (source == null || source.Pile == CardPile.Removed || newOwner < 0 || newOwner >= PlayerCount) return false;
            if (source.Card.Definition.ProtectedFromCardEffects && (newOwner != source.Owner || destination == CardPile.Removed)) return false;
            Pile(players[source.Owner], source.Pile).Remove(source.Card);
            Pile(players[newOwner], destination).Add(source.Card);
            return true;
        }

        internal bool Transform(Guid id, CardDefinition definition)
        {
            LocatedCard located = FindCard(id);
            if (located == null || located.Pile == CardPile.Removed || located.Card.Definition.ProtectedFromCardEffects || definition == null) return false;
            located.Card.Transform(definition);
            return true;
        }

        internal void ApplyBasic(int actor, CardDefinition def)
        { Atheists = CardEffectResolver.ApplyBasic(players[actor], def, Atheists, true, true, true, false); }

        public int EffectiveCost(CardDefinition definition, int targetPlayer)
            => definition.CostKind == CardCostKind.None || definition.CostKind == CardCostKind.MissingSpecification ? -1 : definition.Id == CardId.AggressiveMission
                ? (targetPlayer >= 0 && targetPlayer < PlayerCount ? players[targetPlayer].Faith <= 30 ? 1 : 2 : -1)
                : definition.Cost;

        internal void AddAtheists(int delta) { Atheists = Math.Max(0, Atheists + delta); }
        internal bool RemoveCard(Guid id)
        {
            LocatedCard located = FindCard(id);
            return located != null && Transfer(id, located.Owner, CardPile.Removed);
        }

        private void BeginRound()
        {
            Phase = GamePhase.Actions;
            actionOffset = 0;
            foreach (PlayerState player in players)
            {
                player.Offering = (accumulate ? player.Offering : 0) + RoundOffering;
                Draw(player, RoundDraw);
            }
        }

        private void AdvanceRound()
        {
            if (Round == TotalRounds)
            { Phase = GamePhase.Finished; LastMessage = "40라운드 종료. 점수·승리 판정은 기획 확정 대기입니다."; return; }
            Round++;
            BeginRound();
            LastMessage = $"{Round}라운드 시작 · 선플레이어: {players[FirstPlayerIndex].Name}";
        }

        private void NextReward() { if (++rewardOffset == PlayerCount) AdvanceRound(); }
        private void Draw(PlayerState player, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (player.Deck.Count == 0 && player.Discard.Count > 0)
                {
                    player.MutableDeck.AddRange(player.MutableDiscard);
                    player.MutableDiscard.Clear();
                    Shuffle(player.MutableDeck);
                }
                if (player.Deck.Count == 0) break;
                CardInstance card = player.MutableDeck[0];
                player.MutableDeck.RemoveAt(0);
                player.MutableHand.Add(card);
            }
        }
        private static void Add(List<CardInstance> pile, CardId id, int count)
        { for (int i = 0; i < count; i++) pile.Add(new CardInstance(id)); }
        internal static List<CardInstance> Pile(PlayerState player, CardPile pile)
        {
            switch (pile) { case CardPile.Hand: return player.MutableHand; case CardPile.Discard: return player.MutableDiscard; case CardPile.Removed: return player.MutableRemoved; default: return player.MutableDeck; }
        }
        private void Shuffle(List<CardInstance> pile)
        {
            for (int i = pile.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); CardInstance card = pile[i]; pile[i] = pile[j]; pile[j] = card; }
        }
        private static bool Fail(string text, out string reason) { reason = text; return false; }
        private sealed class BasicHandler : ICardEffectHandler
        {
            public bool Validate(GameSession game, int actor, CardInstance card, EffectTarget target, out string reason)
            { reason = "사용 가능"; return true; }
            public void Execute(CardEffectContext context) { context.ApplyBasic(); }
        }
    }
}
