using System;
using System.Linq;

namespace Pilgrimage
{
    public enum EffectStacking { Independent, RefreshSameCard }
    public enum IncomeOrder { AddThenMultiply, MultiplyThenAdd }
    public enum WitchHuntSource { OwnDeckTop, OwnHandFirst }

    // Provisional interpretation, separate from the card text and confirmed numbers.
    [Serializable]
    public sealed class TimedEffectPolicy
    {
        public EffectStacking stacking = EffectStacking.Independent;
        public IncomeOrder incomeOrder = IncomeOrder.AddThenMultiply;
        public bool durationIncludesActivationRound = false;
        public WitchHuntSource witchHuntSource = WitchHuntSource.OwnDeckTop;
        public bool remodelingRequiresProperty = false;
        public bool remodelingAffectsNaturalDecay = true;
        public JudgmentCountScope judgmentCountScope = JudgmentCountScope.Congregation;
        public bool judgmentIncludesCurrentUse = false;
        public LoanShortfallPolicy loanShortfall = LoanShortfallPolicy.CollectAvailable;
        public bool furCallConvertsAllHandFollowers = true;
        internal TimedEffectPolicy Copy() => (TimedEffectPolicy)MemberwiseClone();
        internal int Duration(int turns) => Math.Max(1, turns - (durationIncludesActivationRound ? 1 : 0));
    }

    public enum TimedEffectKind { Income, DecreaseMultiplier, PreventDecrease, PenanceReward, CaravanReward, LoanRecovery }

    public sealed class TimedCardEffect
    {
        public CardId Source { get; }
        public Guid SourceInstanceId { get; }
        public TimedEffectKind Kind { get; }
        public int RemainingRoundStarts { get; internal set; }
        public int IncomeAddition { get; }
        public double Multiplier { get; }
        public int TargetOwner { get; }
        public int Outstanding { get; internal set; } = 30;
        public bool IsDelayed => Kind == TimedEffectKind.PenanceReward || Kind == TimedEffectKind.CaravanReward || Kind == TimedEffectKind.LoanRecovery;
        internal TimedCardEffect(CardInstance source, TimedEffectKind kind, int turns, int addition, double multiplier, int targetOwner)
        { Source = source.Definition.Id; SourceInstanceId = source.InstanceId; Kind = kind;
          RemainingRoundStarts = turns; IncomeAddition = addition; Multiplier = multiplier; TargetOwner = targetOwner; }
    }

    public static class TimedCardEffects
    {
        internal static void Register(GameSession game, int owner, CardInstance source, TimedEffectKind kind,
            int turns, int addition = 0, double multiplier = 1, int targetOwner = -1)
        {
            var effects = game.Players[owner].MutableTimedEffects;
            bool delayed = kind == TimedEffectKind.PenanceReward || kind == TimedEffectKind.CaravanReward || kind == TimedEffectKind.LoanRecovery;
            if (!delayed && game.EffectPolicy.stacking == EffectStacking.RefreshSameCard)
                effects.RemoveAll(e => e.Source == source.Definition.Id && e.Kind == kind);
            effects.Add(new TimedCardEffect(source, kind, delayed ? turns : game.EffectPolicy.Duration(turns), addition, multiplier, targetOwner));
        }

        public static int Income(PlayerState player, int baseIncome, IncomeOrder order)
        {
            double addition = 0, multiplier = 1;
            foreach (var effect in player.TimedEffects.Where(e => e.Kind == TimedEffectKind.Income))
            { addition += effect.IncomeAddition; multiplier = Math.Min(1e100, multiplier * effect.Multiplier); }
            double income = order == IncomeOrder.AddThenMultiply ? (baseIncome + addition) * multiplier : baseIncome * multiplier + addition;
            return (int)Math.Max(0, Math.Min(int.MaxValue, Math.Floor(income)));
        }

        public static int StatChange(PlayerState player, int delta, bool cardEffect = true)
        {
            if (delta >= 0) return delta; // Archbishop never blocks recovery.
            if (cardEffect && player.TimedEffects.Any(e => e.Kind == TimedEffectKind.PreventDecrease)) return 0;
            double amount = delta;
            foreach (var effect in player.TimedEffects.Where(e => e.Kind == TimedEffectKind.DecreaseMultiplier)) amount *= effect.Multiplier;
            return (int)Math.Max(int.MinValue, amount);
        }

        internal static void ResolveDelayed(GameSession game, int owner)
        {
            var player = game.Players[owner];
            foreach (var effect in player.MutableTimedEffects.Where(e => e.IsDelayed && e.Kind != TimedEffectKind.LoanRecovery).ToArray())
            {
                if (--effect.RemainingRoundStarts > 0) continue;
                player.MutableTimedEffects.Remove(effect); // Resolve exactly once, even if the source has been reshuffled/removed.
                if (effect.Kind == TimedEffectKind.PenanceReward)
                    game.ApplyBasic(owner, new CardDefinition(CardId.Penance, "고행", "", 0, true,
                        offeringGain: 20, faithChange: 10, doctrineChange: 10));
                else player.Offering = SaturatingAdd(player.Offering, game.RollInvestment());
            }
        }

        internal static void ResolveLoans(GameSession game, int owner)
        {
            var creditor = game.Players[owner];
            foreach (var loan in creditor.MutableTimedEffects.Where(e => e.Kind == TimedEffectKind.LoanRecovery).ToArray())
            {
                if (--loan.RemainingRoundStarts > 0) continue;
                var debtor = game.Players[loan.TargetOwner];
                int recovered = Math.Min(debtor.Offering, loan.Outstanding);
                debtor.Offering -= recovered; creditor.Offering = SaturatingAdd(creditor.Offering, recovered); loan.Outstanding -= recovered;
                if (loan.Outstanding > 0 && game.EffectPolicy.loanShortfall == LoanShortfallPolicy.CarryRemainder) loan.RemainingRoundStarts = 1;
                else creditor.MutableTimedEffects.Remove(loan);
            }
        }

        internal static void CompleteIncome(PlayerState player)
        {
            foreach (var effect in player.MutableTimedEffects.Where(e => !e.IsDelayed)) effect.RemainingRoundStarts--;
            player.MutableTimedEffects.RemoveAll(e => !e.IsDelayed && e.RemainingRoundStarts <= 0);
        }

        internal static int SaturatingAdd(int value, int addition)
            => (int)Math.Max(0, Math.Min(int.MaxValue, (long)value + addition));
    }
}
