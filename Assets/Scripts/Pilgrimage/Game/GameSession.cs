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
        public int startingOffering = 5; // Retained for old assets; final regular games use StartingOffering.
        public int startingChurchProperty = 0;
        public int randomSeed = 20261114;
        public bool randomFirstPlayer = false; // true: 1라운드 선플레이어를 무작위로 정한다(이후 라운드마다 한 칸씩 순환).
        public bool shuffleInitialDeck = true;
        public bool accumulateOfferingBetweenRounds = true;
        public int maxCardsPerActionTurn = 0; // 0: unlimited until explicit end.
        public CardPile rewardDestination = CardPile.Discard;
        public bool rejectWhenAtheistsInsufficient = true;
        public bool requireFullFollowerTargets = true;
        public bool allowSelfAsOpponentTarget = false;
        public TimedEffectPolicy timedEffects = new TimedEffectPolicy();

        // 설정 에셋을 건드리지 않고 시드만 바꾼 복사본을 만든다. (새 필드를 추가하면 여기에도 복사 코드를 추가할 것)
        public DemoRuleAssumptions WithRandomSeed(int seed) => new DemoRuleAssumptions
        {
            startingOffering = startingOffering, startingChurchProperty = startingChurchProperty, randomSeed = seed, randomFirstPlayer = randomFirstPlayer,
            shuffleInitialDeck = shuffleInitialDeck, accumulateOfferingBetweenRounds = accumulateOfferingBetweenRounds,
            maxCardsPerActionTurn = maxCardsPerActionTurn, rewardDestination = rewardDestination,
            rejectWhenAtheistsInsufficient = rejectWhenAtheistsInsufficient, requireFullFollowerTargets = requireFullFollowerTargets,
            allowSelfAsOpponentTarget = allowSelfAsOpponentTarget, timedEffects = timedEffects
        };
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
        public Guid[] CardInstanceIds;
        public Guid[] Cards => CardInstanceIds ?? (CardInstanceId == Guid.Empty ? Array.Empty<Guid>() : new[] { CardInstanceId });
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

    public sealed partial class GameSession
    {
        public const int PlayerCount = 4, TotalRounds = 40, StartingOffering = 5, RoundOffering = 3, RoundDraw = 4;
        public static readonly string[] FactionNames = {
            "청빈의 사도단", "순금의 형제단", "절대 교율 교단", "숲의 털가죽 교단"
        };
        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly List<CardDefinition> catalog;
        private readonly List<DeferredReward> deferred = new List<DeferredReward>();
        private readonly Dictionary<string, ICardEffectHandler> handlers;
        private readonly Random random;
        private readonly Random effectRandom;
        public TimedEffectPolicy EffectPolicy { get; }
        public bool IsNetworkGame { get; }
        public FactionId HumanFaction => players[0].Faction;
        private readonly bool accumulate;
        public bool RequireFullFollowerTargets { get; }
        public bool AllowSelfAsOpponentTarget { get; }
        public bool RequiresAtheists => true; // Final population rules require sufficient people for creation.
        private readonly int maxCards;
        private readonly CardPile rewardDestination;
        private int actionOffset;
        public IReadOnlyList<PlayerState> Players { get; }
        public IReadOnlyList<CardDefinition> Catalog { get; }
        public IReadOnlyList<DeferredReward> DeferredRewards { get; }
        public GamePhase Phase { get; private set; }
        public int Round { get; private set; } = 1;
        private readonly int startingSeat;
        public int FirstPlayerIndex => (Round - 1 + startingSeat) % PlayerCount;
        public int ActivePlayerIndex => (FirstPlayerIndex + actionOffset) % PlayerCount;
        public int RewardPlayerIndex => 0; // All four rewards are already awarded; human acknowledges the results.
        public RewardKind CurrentRewardKind => Round % 8 == 0 ? RewardKind.Faction : RewardKind.General;
        public int Atheists { get; private set; } = 100;
        public int CardsPlayedThisTurn { get; private set; }
        public string LastMessage { get; private set; } = "4인 데모 시작. 행동 제출 방식은 순차 즉시 실행으로 임시 구현되었습니다.";

        public GameSession(DemoRuleAssumptions assumptions = null,
            IEnumerable<CardDefinition> additionalCards = null,
            IDictionary<string, ICardEffectHandler> additionalHandlers = null,
            FactionId humanFaction = FactionId.Poverty,
            IReadOnlyList<FactionId> factionSeats = null, bool networkControlled = false)
        {
            var rules = assumptions ?? new DemoRuleAssumptions();
            random = new Random(rules.randomSeed);
            effectRandom = new Random(rules.randomSeed ^ 0x51A7);
            rewardRandom = new Random(rules.randomSeed ^ 0x7E19);
            startingSeat = rules.randomFirstPlayer ? new Random(rules.randomSeed ^ 0x3C6D).Next(PlayerCount) : 0;
            EffectPolicy = (rules.timedEffects ?? new TimedEffectPolicy()).Copy();
            IsNetworkGame = networkControlled;
            var seats = factionSeats == null ? FactionSetup.Seats(humanFaction) : factionSeats.ToArray();
            if (seats.Length != PlayerCount || seats.Distinct().Count() != PlayerCount || seats.Any(f => !FactionSetup.IsPlayable(f)))
                throw new ArgumentException("네 교단은 좌석에 중복 없이 배정되어야 합니다.");
            accumulate = rules.accumulateOfferingBetweenRounds;
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
                var player = new PlayerState(FactionSetup.Name(seats[i]), StartingOffering, 50, 50)
                { Faction = seats[i], ChurchProperty = rules.startingChurchProperty };
                Add(player.MutableDeck, CardId.Follower, 5);
                Add(player.MutableDeck, CardId.Evangelism, 3);
                Add(player.MutableDeck, CardId.RequestOffering, 3);
                Add(player.MutableDeck, CardId.RegularWorship, 4);
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
            if (def.AtheistChange < 0 && Atheists < -def.AtheistChange)
                return Fail("공용 무신론자가 부족합니다. (임시 사용 조건)", out reason);
            if (def.Target != TargetKind.Self && (target.PlayerIndex < 0 || target.PlayerIndex >= PlayerCount))
                return Fail("유효한 대상 교단을 지정하세요.", out reason);
            if (def.Target == TargetKind.Player && !AllowSelfAsOpponentTarget && target.PlayerIndex == actor)
                return Fail("현재 임시 설정에서는 다른 교단을 지정해야 합니다.", out reason);
            if ((def.Id == CardId.AggressiveMission || def.Id == CardId.PoorRelief) && players[actor].Faith <= 20)
                return Fail("신앙심 20 이하에서는 사용할 수 없습니다.", out reason);
            if (def.Id == CardId.Interest && interestUsed.Contains(actor)) return Fail("이자는 한 턴에 한 장만 사용할 수 있습니다.", out reason);
            int cost = EffectiveCost(actor, card, target);
            if (cost < 0) return Fail("현재 사용할 수 없는 카드입니다.", out reason);
            if (players[actor].Offering < cost) return Fail("헌금이 부족합니다.", out reason);
            if (def.Target == TargetKind.Card)
            {
                if (target.Cards.Distinct().Count() != target.Cards.Length) return Fail("같은 카드를 중복 지정할 수 없습니다.", out reason);
                foreach (Guid selected in target.Cards)
                {
                    LocatedCard located = FindCard(selected);
                    if (located == null || located.Owner != target.PlayerIndex) return Fail("지정한 대상 카드가 해당 교단에 없습니다.", out reason);
                }
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
            CardId playedId = card.Definition.Id;
            BeginVisual(VisualCause.Card, actor, card);
            player.Offering -= EffectiveCost(actor, card, target);
            handlers[card.Definition.EffectKey].Execute(new CardEffectContext(this, actor, card, target));
            card.SuccessfulUses++;
            if (playedId == CardId.HeresyJudgment) player.JudgmentUses++;
            if (playedId == CardId.Interest) interestUsed.Add(actor);
            // Temporary resolution policy: successful plays discard immediately.
            // A central-table animation is a presentation reference, never a second card pile.
            player.MutableHand.Remove(card);
            player.MutableDiscard.Add(card);
            CardsPlayedThisTurn++;
            FinishVisual();
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
            BeginVisual(VisualCause.TurnEnd, actor);
            foreach (CardInstance ignored in player.Hand.Where(c => c.Definition.Id == CardId.Heretic).ToArray())
            {
                foreach (var follower in SessionCardEffects.EligibleFollowers(player.Deck).Take(player.Doctrine <= 30 ? 2 : 1).ToArray())
                    Transform(follower.InstanceId, CardCatalog.Get(CardId.Heretic));
            }
            // Duration counts completed owner action turns, including the turn granted.
            foreach (OngoingEffect status in player.MutableStatuses) status.RemainingOwnerTurns--;
            player.MutableStatuses.RemoveAll(status => status.RemainingOwnerTurns <= 0);
            CardsPlayedThisTurn = 0;
            interestUsed.Remove(actor);
            actionOffset++;
            LastMessage = player.Name + " 행동 종료";
            FinishVisual();
            if (actionOffset == PlayerCount)
            {
                BeginVisual(VisualCause.NaturalDecay);
                foreach (PlayerState p in players)
                {
                    ApplyNaturalDecay(p);
                    // Played cards are already in Discard; move only residual hand cards.
                    p.MutableDiscard.AddRange(p.MutableHand);
                    p.MutableHand.Clear();
                }
                FinishVisual();
                if (Round % 4 == 0) AwardRoundRewards();
                else AdvanceRound();
            }
            return true;
        }

        public IReadOnlyList<CardDefinition> RewardOptions()
        {
            if (Phase != GamePhase.RewardChoice) return Array.Empty<CardDefinition>();
            return EligibleRewards(0, CurrentRewardKind);
        }



        public bool DeferMissingReward(int player)
        {
            if (Phase != GamePhase.RewardChoice || player != RewardPlayerIndex || RewardOptions().Count != 0) return false;
            return false;
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
            if (located.Card.Definition.Id == definition.Id) return true;
            located.Card.Transform(definition);
            refusing.Remove(id);
            if (definition.Id == CardId.RefusingFollower) refusing[id] = 3;
            return true;
        }

        internal void ApplyBasic(int actor, CardDefinition def)
        { Atheists = CardEffectResolver.ApplyBasic(players[actor], def, Atheists, true, true, true, false, protection => RecordDefense(actor, protection)); }

        public int EffectiveCost(CardDefinition definition, int targetPlayer)
            => CostFor(definition, ActivePlayerIndex, targetPlayer, players[ActivePlayerIndex].Hand.FirstOrDefault(c => c.Definition.Id == definition.Id));

        internal void AddAtheists(int delta) { Atheists = Math.Max(0, Atheists + delta); }
        internal int RollInvestment() => effectRandom.Next(0, 31);
        public int PreviewRoundIncome(int owner) => TimedCardEffects.Income(players[owner], RoundOffering + players[owner].FollowerCount / 10, EffectPolicy.incomeOrder);
        internal bool RemoveCard(Guid id)
        {
            LocatedCard located = FindCard(id);
            if (located == null || !Transfer(id, located.Owner, CardPile.Removed)) return false;
            if (IsPerson(located.Card.Definition)) AddAtheists(1);
            refusing.Remove(id);
            return true;
        }

        private void BeginRound()
        {
            Phase = GamePhase.Actions;
            actionOffset = 0;
            BeginVisual(VisualCause.Delayed); ResolveRefusingFollowers(); FinishVisual();
            for (int owner = 0; owner < PlayerCount; owner++)
            {
                PlayerState player = players[owner];
                BeginVisual(VisualCause.RoundIncome, owner);
                if (Round != 1 && !accumulate) player.Offering = 0;
                FinishVisual();
                BeginVisual(VisualCause.Delayed, owner);
                TimedCardEffects.ResolveDelayed(this, owner);
                FinishVisual();
                BeginVisual(VisualCause.Threshold, owner);
                if (player.Doctrine <= 20)
                {
                    var follower = SessionCardEffects.EligibleFollowers(player.Deck).FirstOrDefault();
                    if (follower != null) Transform(follower.InstanceId, CardCatalog.Get(CardId.Heretic));
                }
                FinishVisual();
                BeginVisual(VisualCause.RoundIncome, owner);
                if (Round != 1) player.Offering = TimedCardEffects.SaturatingAdd(player.Offering, PreviewRoundIncome(owner));
                FinishVisual();
            }
            for (int offset = 0; offset < PlayerCount; offset++)
            {
                int owner = EffectPolicy.loansResolveInLeaderOrder ? (FirstPlayerIndex + offset) % PlayerCount : offset;
                PlayerState player = players[owner];
                BeginVisual(VisualCause.Delayed, owner); TimedCardEffects.ResolveLoans(this, owner); FinishVisual();
                BeginVisual(VisualCause.Duration, owner); TimedCardEffects.CompleteIncome(player); FinishVisual();
                BeginVisual(VisualCause.RoundDraw, owner); Draw(player, DrawCount(owner)); FinishVisual();
            }
        }

        private void AdvanceRound()
        {
            if (Round == TotalRounds)
            { Phase = GamePhase.Finished; ComputeWinners(); return; }
            Round++;
            BeginRound();
            LastMessage = $"{Round}라운드 시작 · 선플레이어: {players[FirstPlayerIndex].Name}";
        }

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
