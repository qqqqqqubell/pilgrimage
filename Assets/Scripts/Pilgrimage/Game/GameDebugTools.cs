using System;

namespace Pilgrimage
{
    // Explicitly bypasses acquisition rules for local experiments, never AI logic.
    public static class GameDebugTools
    {
        public static CardInstance GiveCard(GameSession game, int owner, CardId id, CardPile pile = CardPile.Hand)
        {
            if (owner < 0 || owner >= GameSession.PlayerCount) throw new ArgumentOutOfRangeException(nameof(owner));
            var card = new CardInstance(id);
            GameSession.Pile(game.Players[owner], pile).Add(card);
            return card;
        }
        public static void SetResources(GameSession game, int owner, int offering, int faith, int doctrine, int churchProperty = 0)
        {
            PlayerState player = game.Players[owner];
            player.Offering = Math.Max(0, offering);
            player.Faith = Math.Max(0, Math.Min(100, faith));
            player.Doctrine = Math.Max(0, Math.Min(100, doctrine));
            player.ChurchProperty = churchProperty;
        }
    }
}
