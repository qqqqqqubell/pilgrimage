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

        internal static IEnumerable<CardInstance> WitchHuntTargets(GameSession game, int actor)
            => EligibleFollowers(game.EffectPolicy.witchHuntSource == WitchHuntSource.OwnHandFirst ? game.Players[actor].Hand : game.Players[actor].Deck);

        public bool Validate(GameSession game, int actor, CardInstance card, EffectTarget target, out string reason)
        {
            int required = 0;
            int owner = actor;
            switch (card.Definition.Id)
            {
                case CardId.Evangelism:
                    if (game.Atheists < 2) { reason = "무신론자가 2명 이상이어야 합니다."; return false; }
                    break;
                case CardId.InterfaithLoan:
                    if (game.Players[actor].Offering < 10) { reason = "헌금이 10 이상이어야 합니다."; return false; }
                    break;
                case CardId.CallOfFur:
                    if (!FurCallTargets(game, target.PlayerIndex).Any()) { reason = "변환할 대상 신도가 없습니다."; return false; }
                    break;
                case CardId.FurRally:
                    if (target.PlayerIndex != actor || target.Cards.Length != 1 || !game.SelectableCards(actor, CardId.FurRally, card.InstanceId).Any(c => c.InstanceId == target.Cards[0]))
                    { reason = "자신의 손패 또는 덱 위에서 신도 1장을 선택하세요."; return false; }
                    break;
                case CardId.WitchHunt:
                    if (!WitchHuntTargets(game, actor).Any())
                    { reason = "변환할 수 있는 자신의 신도가 없습니다."; return false; }
                    break;
                case CardId.TempleRemodeling:
                    if (game.EffectPolicy.remodelingRequiresProperty && game.Players[actor].ChurchProperty < 2)
                    { reason = "교회 재산이 부족합니다."; return false; }
                    break;
                case CardId.Edict:
                    var selectable = game.SelectableCards(actor, CardId.Edict, card.InstanceId);
                    if (target.PlayerIndex != actor || target.Cards.Length > 2 || target.Cards.Any(id => !selectable.Any(c => c.InstanceId == id)))
                    { reason = "자신의 손패·덱·버린 더미에서 최대 2장을 선택하세요."; return false; }
                    break;
                case CardId.PublicDenunciation: case CardId.AggressiveMission: required = 1; owner = target.PlayerIndex; break;
                case CardId.Reaper: required = 2; owner = target.PlayerIndex; break;
                case CardId.Indulgence: required = 1; break;
                case CardId.MoltingRitual:
                    if (EligibleFollowers(game.Players[actor].Deck).Count() < 2) { reason = "자신의 덱에 신도 2장 이상이 있어야 합니다."; return false; }
                    break;
                case CardId.BondOfFaith: required = 1; break;
                case CardId.Blessing:
                    break;
                case CardId.GlimmerOfHope:
                    if (game.Players[actor].Offering > 10) { reason = "헌금이 10 이하이어야 합니다."; return false; }
                    break;
                case CardId.Paradise:
                    if (game.Players[actor].FollowerCount < 20) { reason = "신도 20명 이상이어야 합니다."; return false; }
                    break;
                case CardId.WildRoar:
                    if (game.Players[actor].Faith < 40) { reason = "신앙심 40 이상이어야 합니다."; return false; }
                    if (game.Atheists < GameSession.PlayerCount - 1) { reason = "무신론자가 부족합니다."; return false; }
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
                case CardId.Penance:
                    TimedCardEffects.Register(game, actor, context.Source, TimedEffectKind.PenanceReward, 4); break;
                case CardId.CaravanInvestment:
                    TimedCardEffects.Register(game, actor, context.Source, TimedEffectKind.CaravanReward, 2); break;
                case CardId.SolitaryReflection:
                    TimedCardEffects.Register(game, actor, context.Source, TimedEffectKind.Income, 1, multiplier: 0.5); break;
                case CardId.HumbleTemple:
                    TimedCardEffects.Register(game, actor, context.Source, TimedEffectKind.Income, 2, addition: -3); break;
                case CardId.TempleRemodeling:
                    TimedCardEffects.Register(game, actor, context.Source, TimedEffectKind.Income, 2, multiplier: 2);
                    TimedCardEffects.Register(game, actor, context.Source, TimedEffectKind.DecreaseMultiplier, 2, multiplier: 2); break;
                case CardId.Archbishop:
                    TimedCardEffects.Register(game, actor, context.Source, TimedEffectKind.PreventDecrease, 3); break;
                case CardId.WitchHunt:
                    game.Transform(WitchHuntTargets(game, actor).First().InstanceId, CardCatalog.Get(CardId.Heretic)); break;
                case CardId.Edict:
                    foreach (var selected in context.Target.Cards) game.RemoveCard(selected);
                    break;
                case CardId.InterfaithLoan:
                    player.Offering -= 10;
                    game.Players[context.Target.PlayerIndex].Offering = TimedCardEffects.SaturatingAdd(game.Players[context.Target.PlayerIndex].Offering, 10);
                    TimedCardEffects.Register(game, actor, context.Source, TimedEffectKind.LoanRecovery, 1, targetOwner: context.Target.PlayerIndex);
                    game.AddShuffled(actor, CardId.Interest);
                    break;
                case CardId.MassAgitation: ApplyToAll(game, actor, -40, false); break;
                case CardId.AreaCleansing: ApplyToAll(game, actor, -30, true); break;
                case CardId.FurRally:
                    game.Transform(context.Target.Cards[0], CardCatalog.Get(CardId.FaithfulFollower));
                    ApplyToAll(game, actor, -20, false); break;
                case CardId.HeresyJudgment:
                    int uses = game.JudgmentCount(actor, context.Source);
                    var judged = game.Players[context.Target.PlayerIndex];
                    if (judged.Doctrine >= 30)
                        CardEffectResolver.ApplyBasic(judged, new CardDefinition(id, "", "", 0, true, doctrineChange: -(5 + uses * 5)), game.Atheists, true, true, true, false);
                    else foreach (var follower in EligibleFollowers(judged.Deck).Take(1 + uses).ToArray()) game.RemoveCard(follower.InstanceId);
                    game.AddShuffled(actor, CardId.HeresyJudgment); break;
                case CardId.CallOfFur:
                    foreach (var follower in FurCallTargets(game, context.Target.PlayerIndex).ToArray()) game.Transform(follower.InstanceId, CardCatalog.Get(CardId.RefusingFollower));
                    break;
                case CardId.RefusingFollower:
                    game.Transform(context.Source.InstanceId, CardCatalog.Get(CardId.Follower)); break;
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
                    foreach (var returned in player.Hand.Concat(player.Deck).Where(c => c.Definition.Id == CardId.Heretic).Take(2).ToArray())
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
                    if (removed != null) game.RemoveCard(removed.InstanceId);
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
                    Convert(game, player.Deck, 1, CardId.Heretic);
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
                    player.Faith = System.Math.Min(100, player.Faith + faithful / 4 * 8);
                    break;
                case CardId.WildRoar:
                    game.AddAtheists(-3);
                    for (int i = 0; i < GameSession.PlayerCount; i++)
                        if (i != actor) game.Players[i].MutableDeck.Insert(0, new CardInstance(CardId.Heretic));
                    break;
            }
        }

        private static IEnumerable<CardInstance> FurCallTargets(GameSession game, int owner)
        {
            var player = game.Players[owner];
            var hand = EligibleFollowers(player.Hand).Where(c => c.Definition.Id != CardId.RefusingFollower);
            if (!game.EffectPolicy.furCallConvertsAllHandFollowers) hand = hand.Take(1);
            return hand.Concat(EligibleFollowers(player.Deck).Where(c => c.Definition.Id != CardId.RefusingFollower).Take(1));
        }

        private static void ApplyToAll(GameSession game, int actor, int decrease, bool includeSelf)
        {
            for (int i = 0; i < GameSession.PlayerCount; i++)
                if (includeSelf || i != actor)
                    CardEffectResolver.ApplyBasic(game.Players[i], new CardDefinition(CardId.AreaCleansing, "", "", 0, true,
                        faithChange: decrease, doctrineChange: decrease), game.Atheists, true, true, true, false);
        }

        private static void Convert(GameSession game, IEnumerable<CardInstance> pile, int count, CardId to)
        {
            foreach (CardInstance card in EligibleFollowers(pile).Take(count).ToArray())
                game.Transform(card.InstanceId, CardCatalog.Get(to));
        }
    }
}
