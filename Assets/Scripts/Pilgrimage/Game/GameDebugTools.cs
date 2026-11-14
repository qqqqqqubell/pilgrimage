using System;

namespace Pilgrimage
{
    // Explicitly bypasses acquisition rules for local experiments, never AI logic.
    public static class GameDebugTools
    {
        public static readonly CardId[] FiveActionPreset = { CardId.Evangelism, CardId.Edict, CardId.Preaching, CardId.BondOfFaith, CardId.AggressiveMission };

        public static void ReplaceHand(GameSession game, int owner, System.Collections.Generic.IEnumerable<CardId> ids)
        {
            // Explicit development operation, preserving existing physical identities.
            var definitions = System.Linq.Enumerable.ToArray(ids);
            foreach (var id in definitions) CardCatalog.Get(id); // Validate before mutation.
            PlayerState player = game.Players[owner];
            player.MutableDiscard.AddRange(player.MutableHand); player.MutableHand.Clear();
            foreach (var id in definitions) GiveCard(game, owner, id);
        }

        public static GameSession CreateCardLab(FactionId humanFaction = FactionId.Poverty)
        {
            var game = new GameSession(new DemoRuleAssumptions { shuffleInitialDeck = false }, humanFaction: humanFaction);
            ReplaceHand(game, 0, FiveActionPreset);
            for (int player = 0; player < GameSession.PlayerCount; player++)
            {
                SetResources(game, player, 100, 50, 50);
                // Initial followers were drawn into hand by the unshuffled normal setup.
                // Add explicit lab targets without changing the normal starting deck.
                GiveCard(game, player, CardId.Follower, CardPile.Deck);
                GiveCard(game, player, CardId.Follower, CardPile.Deck);
            }
            return game;
        }
        public static CardInstance GiveCard(GameSession game, int owner, CardId id, CardPile pile = CardPile.Hand)
        {
            if (owner < 0 || owner >= GameSession.PlayerCount) throw new ArgumentOutOfRangeException(nameof(owner));
            var card = new CardInstance(id);
            GameSession.Pile(game.Players[owner], pile).Add(card);
            game.TrackDebugCard(card, pile);
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
