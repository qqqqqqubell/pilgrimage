using System;
using System.Collections.Generic;

namespace Pilgrimage
{
    // No scene/UI dependencies. Settings are copied at construction so an ongoing
    // session never changes rules when its source asset is edited in the Inspector.
    public sealed class PrototypeGame
    {
        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly Random random;
        private readonly bool clampAtZero, discardAtEnd, reshuffle;
        private readonly int maxCards, drawAtStart, offeringAtStart;
        public IReadOnlyList<PlayerState> Players { get; }
        public int ActivePlayerIndex { get; private set; }
        public PlayerState ActivePlayer => players[ActivePlayerIndex];
        public int Atheists { get; private set; }
        public int TurnNumber { get; private set; } = 1;
        public int CardsPlayedThisTurn { get; private set; }
        public string TemporaryRulesSummary { get; }

        public PrototypeGame(PrototypeTestConfig settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            clampAtZero = settings.clampEffectValuesAtZero;
            discardAtEnd = settings.discardHandOnTurnEnd;
            reshuffle = settings.reshuffleDiscardWhenDeckEmpty;
            maxCards = Math.Max(0, settings.maxCardsPerTurn);
            drawAtStart = Math.Max(0, settings.drawOnTurnStart);
            offeringAtStart = Math.Max(0, settings.offeringOnTurnStart);
            random = new Random(settings.randomSeed);
            Atheists = settings.startingAtheists;
            for (int i = 0; i < Math.Max(1, settings.playerCount); i++)
            {
                var player = new PlayerState($"플레이어 {i + 1}", Math.Max(0, settings.startingOffering),
                    settings.startingFaith, settings.startingDoctrine);
                AddCopies(player.MutableHand, settings.initialHand);
                AddCopies(player.MutableDeck, settings.initialDeck);
                if (settings.shuffleInitialDeck) Shuffle(player.MutableDeck);
                players.Add(player);
            }
            Players = players.AsReadOnly();
            TemporaryRulesSummary = $"임시 규칙 | 무신론자 공용 | 턴 시작 드로우 {drawAtStart}장 · 헌금 +{offeringAtStart}" +
                $" | 턴당 사용 {(maxCards == 0 ? "무제한" : maxCards.ToString())}" +
                $" | 종료 시 손패 {(discardAtEnd ? "버림" : "유지")}" +
                $" | 버린 더미 재섞기 {(reshuffle ? "켜짐" : "꺼짐")}" +
                $" | 수치 하한 {(clampAtZero ? "0" : "없음")}";
            // Turn 1 uses exactly the configured initial hand/resources. Automatic
            // start-of-turn grants occur only after pressing End Turn.
        }

        public bool CanPlay(int playerIndex, CardInstance card, out string reason)
        {
            if (playerIndex != ActivePlayerIndex) { reason = "현재 플레이어의 턴이 아닙니다."; return false; }
            if (card == null || !ActivePlayer.MutableHand.Contains(card))
            { reason = "손패에 없는 카드입니다."; return false; }
            if (!card.Definition.IsPlayable) { reason = "[신도]는 사용할 수 없습니다."; return false; }
            if (maxCards > 0 && CardsPlayedThisTurn >= maxCards)
            { reason = "임시 턴당 사용 제한에 도달했습니다."; return false; }
            if (ActivePlayer.Offering < card.Definition.Cost)
            { reason = $"헌금 부족: 필요 {card.Definition.Cost}, 보유 {ActivePlayer.Offering}"; return false; }
            reason = "사용 가능";
            return true;
        }

        public bool TryPlay(int playerIndex, CardInstance card, out string message)
        {
            // Validate everything before mutating resources or piles.
            if (!CanPlay(playerIndex, card, out message)) return false;
            PlayerState player = ActivePlayer;
            CardDefinition effect = card.Definition;
            int previousOffering = player.Offering;
            int previousFaith = player.Faith;
            int previousDoctrine = player.Doctrine;
            int previousAtheists = Atheists;
            player.Offering = Apply(player.Offering - effect.Cost + effect.OfferingGain);
            player.Faith = Apply(player.Faith + effect.FaithChange);
            player.Doctrine = Apply(player.Doctrine + effect.DoctrineChange);
            Atheists = Apply(Atheists + effect.AtheistChange);
            for (int i = 0; i < effect.FollowersToDeck; i++)
                player.MutableDeck.Add(new CardInstance(CardId.Follower));
            player.MutableHand.Remove(card);
            player.MutableDiscard.Add(card);
            CardsPlayedThisTurn++;
            message = $"{player.Name} · {effect.Name}: 헌금 {previousOffering}→{player.Offering}, " +
                $"신앙심 {previousFaith}→{player.Faith}, 교리 {previousDoctrine}→{player.Doctrine}, " +
                $"무신론자 {previousAtheists}→{Atheists}" +
                (effect.FollowersToDeck > 0 ? $", 덱에 신도 +{effect.FollowersToDeck}" : "");
            return true;
        }

        public string EndTurn()
        {
            if (discardAtEnd)
            {
                ActivePlayer.MutableDiscard.AddRange(ActivePlayer.MutableHand);
                ActivePlayer.MutableHand.Clear();
            }
            ActivePlayerIndex = (ActivePlayerIndex + 1) % players.Count;
            TurnNumber++;
            CardsPlayedThisTurn = 0;
            ActivePlayer.Offering += offeringAtStart;
            int drawn = Draw(ActivePlayer, drawAtStart);
            return $"턴 {TurnNumber} · {ActivePlayer.Name}: 임시 턴 시작 처리 — {drawn}장 드로우, 헌금 +{offeringAtStart}";
        }

        private int Draw(PlayerState player, int count)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (player.Deck.Count == 0 && reshuffle && player.Discard.Count > 0)
                {
                    player.MutableDeck.AddRange(player.MutableDiscard);
                    player.MutableDiscard.Clear();
                    Shuffle(player.MutableDeck);
                }
                if (player.Deck.Count == 0) break;
                CardInstance card = player.MutableDeck[0];
                player.MutableDeck.RemoveAt(0);
                player.MutableHand.Add(card);
                drawn++;
            }
            return drawn;
        }

        private int Apply(int value) => clampAtZero ? Math.Max(0, value) : value;

        private static void AddCopies(List<CardInstance> destination, CardId[] ids)
        {
            if (ids == null) return;
            foreach (CardId id in ids) destination.Add(new CardInstance(id));
        }

        private void Shuffle(List<CardInstance> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                CardInstance temporary = cards[i];
                cards[i] = cards[j];
                cards[j] = temporary;
            }
        }
    }
}
