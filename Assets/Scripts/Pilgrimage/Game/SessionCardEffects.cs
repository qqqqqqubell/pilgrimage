using System.Collections.Generic;
using System.Linq;

namespace Pilgrimage
{
    internal sealed class SessionCardEffects : ICardEffectHandler
    {
        internal static void Register(Dictionary<string, ICardEffectHandler> handlers)
        {
            var handler = new SessionCardEffects();
            foreach (CardDefinition def in CardCatalog.All)
                if (def.EffectKey != "basic" && def.EffectKey != "pending") handlers[def.EffectKey] = handler;
        }

        internal static IEnumerable<CardInstance> EligibleFollowers(IEnumerable<CardInstance> cards)
            => cards.Where(c => c.Definition.CountsAsFollower && !c.Definition.ProtectedFromCardEffects);

        public bool Validate(GameSession game, int actor, CardInstance card, EffectTarget target, out string reason)
        {
            int required = 0;
            int owner = actor;
            switch (card.Definition.Id)
            {
                case CardId.Edict:
                    var removal = game.FindCard(target.CardInstanceId);
                    if (removal == null || removal.Owner != actor || target.PlayerIndex != actor ||
                        (removal.Pile != CardPile.Hand && removal.Pile != CardPile.Deck) ||
                        removal.Card.InstanceId == card.InstanceId || removal.Card.Definition.ProtectedFromCardEffects)
                    { reason = "자신의 손패 또는 덱에서 제거할 수 있는 다른 카드 한 장을 선택하세요."; return false; }
                    break;
                case CardId.PublicDenunciation: case CardId.AggressiveMission: required = 1; owner = target.PlayerIndex; break;
                case CardId.Reaper: required = 2; owner = target.PlayerIndex; break;
                case CardId.PoorRelief: required = game.Players[target.PlayerIndex].Faith <= 20 ? 3 : 2; owner = target.PlayerIndex; break;
                case CardId.Indulgence: case CardId.MoltingRitual: required = 2; break;
                case CardId.BondOfFaith: required = 1; break;
                case CardId.Blessing:
                    if (game.RequireFullFollowerTargets && game.Players[actor].Deck.Count(c => c.Definition.CountsAsFollower) < 2)
                    { reason = "덱의 신도가 부족합니다."; return false; }
                    break;
                case CardId.GlimmerOfHope:
                    if (game.Players[actor].Offering != 0) { reason = "헌금이 0이어야 합니다."; return false; }
                    break;
                case CardId.Paradise:
                    if (game.Players[actor].FollowerCount < 20) { reason = "신도 20명 이상이어야 합니다."; return false; }
                    break;
                case CardId.WildRoar:
                    if (game.Players[actor].Faith < 70) { reason = "신앙심 70 이상이어야 합니다."; return false; }
                    if (game.RequiresAtheists && game.Atheists < 3) { reason = "무신론자가 부족합니다. (임시 사용 조건)"; return false; }
                    break;
            }
            if (game.RequireFullFollowerTargets && required > EligibleFollowers(game.Players[owner].Deck).Count())
            { reason = "덱에 효과 대상 신도가 부족합니다. 신실한 신도는 제외됩니다. (임시 처리)"; return false; }
            reason = "사용 가능";
            return true;
        }

        public void Execute(CardEffectContext context)
        {
            GameSession game = context.Game;
            int actor = context.Actor;
            PlayerState player = game.Players[actor];
            CardId id = context.Source.Definition.Id;
            context.ApplyBasic();
            switch (id)
            {
                case CardId.Edict:
                    game.RemoveCard(context.Target.CardInstanceId);
                    break;
                case CardId.Preaching:
                    player.MutableDeck.Add(new CardInstance(CardId.Evangelism));
                    break;
                case CardId.BondOfFaith:
                    var gift = EligibleFollowers(player.Deck).FirstOrDefault();
                    if (gift != null) game.Transfer(gift.InstanceId, context.Target.PlayerIndex, CardPile.Deck);
                    break;
                case CardId.FalseRevelation:
                    var victim = game.Players[context.Target.PlayerIndex];
                    int beforeFaith = victim.Faith, beforeDoctrine = victim.Doctrine;
                    CardEffectResolver.ApplyBasic(victim, new CardDefinition(id, "", "", 0, true, faithChange: -10, doctrineChange: -10), game.Atheists, true, true, true, false);
                    if ((victim.Faith < beforeFaith && victim.Faith <= 30) || (victim.Doctrine < beforeDoctrine && victim.Doctrine <= 30)) player.Offering++;
                    break;
                case CardId.Blessing:
                    foreach (var drawn in player.Deck.Where(c => c.Definition.CountsAsFollower).Take(2).ToArray())
                        game.Transfer(drawn.InstanceId, actor, CardPile.Hand);
                    break;
                case CardId.ReturnToFaith:
                    foreach (var returned in player.Deck.Where(c => c.Definition.Id == CardId.Heretic).Take(2).ToArray())
                        game.Transform(returned.InstanceId, CardCatalog.Get(CardId.Follower));
                    break;
                case CardId.ClergyTraining:
                    for (int i = 0; i < 2; i++) player.MutableDeck.Add(new CardInstance(CardId.Evangelism));
                    break;
                case CardId.Heretic:
                    game.Transform(context.Source.InstanceId, CardCatalog.Get(CardId.Follower));
                    break;
                case CardId.PublicDenunciation:
                    CardInstance removed = EligibleFollowers(game.Players[context.Target.PlayerIndex].Deck).FirstOrDefault();
                    if (removed != null && game.RemoveCard(removed.InstanceId)) game.AddAtheists(1);
                    break;
                case CardId.Reaper:
                    Convert(game, game.Players[context.Target.PlayerIndex].Deck, 2, CardId.Heretic);
                    break;
                case CardId.AggressiveMission:
                    CardInstance stolen = EligibleFollowers(game.Players[context.Target.PlayerIndex].Deck).FirstOrDefault();
                    if (stolen != null) game.Transfer(stolen.InstanceId, actor, CardPile.Deck);
                    break;
                case CardId.PoorRelief:
                    PlayerState target = game.Players[context.Target.PlayerIndex];
                    foreach (CardInstance follower in EligibleFollowers(target.Deck).Reverse().Take(target.Faith <= 20 ? 3 : 2).ToArray())
                        game.Transfer(follower.InstanceId, actor, CardPile.Hand);
                    break;
                case CardId.SalvationFundraising:
                    player.Offering++;
                    player.MutableDeck.Insert(0, new CardInstance(CardId.CharityPot));
                    break;
                case CardId.Interest:
                    player.Offering += System.Math.Max(0, player.ChurchProperty / 10);
                    break;
                case CardId.Indulgence:
                    Convert(game, player.Deck, 2, CardId.Heretic);
                    break;
                case CardId.Inquisitor:
                    foreach (CardInstance heretic in player.Hand.Where(c => c.Definition.Id == CardId.Heretic).ToArray())
                        game.Transform(heretic.InstanceId, CardCatalog.Get(CardId.Follower));
                    break;
                case CardId.MoltingRitual:
                    Convert(game, player.Deck, 2, CardId.FaithfulFollower);
                    break;
                case CardId.DeepForestSanctuary:
                    int faithful = player.Hand.Concat(player.Deck).Count(c => c.Definition.Id == CardId.FaithfulFollower);
                    player.Faith = System.Math.Min(100, player.Faith + faithful / 4 * 5);
                    break;
                case CardId.WildRoar:
                    game.AddAtheists(-3);
                    for (int i = 0; i < GameSession.PlayerCount; i++)
                        if (i != actor) game.Players[i].MutableDeck.Insert(0, new CardInstance(CardId.Heretic));
                    break;
            }
        }

        private static void Convert(GameSession game, IEnumerable<CardInstance> pile, int count, CardId to)
        {
            foreach (CardInstance card in EligibleFollowers(pile).Take(count).ToArray())
                game.Transform(card.InstanceId, CardCatalog.Get(to));
        }
    }
}
