using System;

namespace Pilgrimage
{
    public enum EffectProtection { FaithChanges, DoctrineChanges }

    public sealed class OngoingEffect
    {
        public EffectProtection Protection { get; }
        public int RemainingOwnerTurns { get; internal set; }
        public OngoingEffect(EffectProtection protection, int ownerTurns)
        { Protection = protection; RemainingOwnerTurns = Math.Max(1, ownerTurns); }
    }

    public static class CardEffectResolver
    {
        // v1 keeps its historical test-only bounds; v2 always passes limitStats=true.
        public static int ApplyBasic(PlayerState player, CardDefinition effect, int atheists,
            bool clampAtZero, bool limitStats, bool honorProtection = false, bool payCost = true)
        {
            player.Offering = Math.Max(0, player.Offering - (payCost ? effect.Cost : 0) + effect.OfferingGain);
            bool faithProtected = false, doctrineProtected = false;
            if (honorProtection)
                foreach (OngoingEffect status in player.Statuses)
                {
                    faithProtected |= status.Protection == EffectProtection.FaithChanges;
                    doctrineProtected |= status.Protection == EffectProtection.DoctrineChanges;
                }
            int faith = player.Faith + (faithProtected ? 0 : effect.FaithChange);
            int doctrine = player.Doctrine + (doctrineProtected ? 0 : effect.DoctrineChange);
            player.Faith = Bound(faith, clampAtZero, limitStats);
            player.Doctrine = Bound(doctrine, clampAtZero, limitStats);
            player.ChurchProperty += effect.ChurchPropertyChange;
            for (int i = 0; i < effect.FollowersToDeck; i++)
                player.MutableDeck.Add(new CardInstance(CardId.Follower));
            int result = atheists + effect.AtheistChange;
            return clampAtZero ? Math.Max(0, result) : result;
        }

        private static int Bound(int value, bool minimum, bool maximum)
            => maximum ? Math.Max(0, Math.Min(100, value)) : minimum ? Math.Max(0, value) : value;
    }
}
