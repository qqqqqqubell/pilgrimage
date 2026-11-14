using System.Linq;
using NUnit.Framework;

namespace Pilgrimage.Tests
{
    public sealed class CardPrototypeTests
    {
        [Test]
        public void EvangelismChangesOnlyActingPlayerAndAddsTwoDistinctFollowersToDeck()
        {
            var game = new PrototypeGame(new PrototypeTestConfig());
            PlayerState player = game.ActivePlayer;
            CardInstance card = player.Hand[0];
            Assert.That(game.TryPlay(0, card, out _), Is.True);
            Assert.That(player.Offering, Is.EqualTo(4));
            Assert.That(player.Faith, Is.EqualTo(18));
            Assert.That(player.Doctrine, Is.EqualTo(20));
            Assert.That(game.Atheists, Is.EqualTo(18));
            Assert.That(player.Deck.Count, Is.EqualTo(10));
            Assert.That(player.Deck[8].Definition.Id, Is.EqualTo(CardId.Follower));
            Assert.That(player.Deck[9].Definition.Id, Is.EqualTo(CardId.Follower));
            Assert.That(player.Deck[8].InstanceId, Is.Not.EqualTo(player.Deck[9].InstanceId));
            Assert.That(player.Hand.Count, Is.EqualTo(3));
            Assert.That(player.Discard.Single(), Is.SameAs(card));
            Assert.That(game.Players[1].Offering, Is.EqualTo(5));
            Assert.That(game.Players[1].Faith, Is.EqualTo(20));
            Assert.That(game.Players[1].Deck.Count, Is.EqualTo(8));
        }

        [Test]
        public void RequestOfferingCostsZeroAndReceivesTwo()
        {
            var game = new PrototypeGame(new PrototypeTestConfig { startingOffering = 1 });
            Assert.That(game.TryPlay(0, game.ActivePlayer.Hand[1], out _), Is.True);
            Assert.That(game.ActivePlayer.Offering, Is.EqualTo(3));
            Assert.That(game.ActivePlayer.Faith, Is.EqualTo(15));
            Assert.That(game.ActivePlayer.Doctrine, Is.EqualTo(15));
            Assert.That(game.Atheists, Is.EqualTo(20));
        }

        [Test]
        public void WorshipPaysCostAndIncreasesBothStats()
        {
            var game = new PrototypeGame(new PrototypeTestConfig());
            Assert.That(game.TryPlay(0, game.ActivePlayer.Hand[2], out _), Is.True);
            Assert.That(game.ActivePlayer.Offering, Is.EqualTo(4));
            Assert.That(game.ActivePlayer.Faith, Is.EqualTo(25));
            Assert.That(game.ActivePlayer.Doctrine, Is.EqualTo(25));
            Assert.That(game.Atheists, Is.EqualTo(20));
        }

        [Test]
        public void ZeroOfferingRejectsPaidCardsButAllowsFreeOfferingRequest()
        {
            var game = new PrototypeGame(new PrototypeTestConfig { startingOffering = 0 });
            foreach (CardInstance card in game.ActivePlayer.Hand.Where(c => c.Definition.Id != CardId.RequestOffering).ToArray())
                Assert.That(game.TryPlay(0, card, out _), Is.False);
            Assert.That(game.ActivePlayer.Offering, Is.Zero);
            Assert.That(game.ActivePlayer.Faith, Is.EqualTo(20));
            Assert.That(game.ActivePlayer.Doctrine, Is.EqualTo(20));
            Assert.That(game.ActivePlayer.Hand.Count, Is.EqualTo(4));
            Assert.That(game.ActivePlayer.Deck.Count, Is.EqualTo(8));
            Assert.That(game.ActivePlayer.Discard, Is.Empty);
            Assert.That(game.Atheists, Is.EqualTo(20));
            Assert.That(game.CardsPlayedThisTurn, Is.Zero);
            Assert.That(game.TryPlay(0, game.ActivePlayer.Hand[1], out _), Is.True);
            Assert.That(game.ActivePlayer.Offering, Is.EqualTo(2));
        }

        [Test]
        public void FollowerCannotBePlayedEvenWithEnoughOffering()
        {
            var game = new PrototypeGame(new PrototypeTestConfig());
            Assert.That(game.TryPlay(0, game.ActivePlayer.Hand[3], out string reason), Is.False);
            Assert.That(reason, Does.Contain("신도"));
            Assert.That(game.ActivePlayer.Offering, Is.EqualTo(5));
            Assert.That(game.ActivePlayer.Hand.Count, Is.EqualTo(4));
            Assert.That(game.ActivePlayer.Discard, Is.Empty);
        }

        [Test]
        public void WrongPlayerForeignCardAndAlreadyUsedCardAreRejected()
        {
            var game = new PrototypeGame(new PrototypeTestConfig());
            CardInstance card = game.ActivePlayer.Hand[0];
            Assert.That(game.TryPlay(1, game.Players[1].Hand[0], out _), Is.False);
            Assert.That(game.TryPlay(0, new CardInstance(CardId.Evangelism), out _), Is.False);
            Assert.That(game.TryPlay(0, null, out _), Is.False);
            Assert.That(game.TryPlay(0, card, out _), Is.True);
            Assert.That(game.TryPlay(0, card, out _), Is.False);
            Assert.That(game.ActivePlayer.Offering, Is.EqualTo(4));
            Assert.That(game.ActivePlayer.Discard.Count, Is.EqualTo(1));
        }

        [Test]
        public void DefaultTurnKeepsHandAndGrantsOnlyNextPlayerOneCard()
        {
            var game = new PrototypeGame(new PrototypeTestConfig());
            Assert.That(game.ActivePlayer.Hand.Count, Is.EqualTo(4));
            game.EndTurn();
            Assert.That(game.ActivePlayerIndex, Is.EqualTo(1));
            Assert.That(game.TurnNumber, Is.EqualTo(2));
            Assert.That(game.Players[0].Hand.Count, Is.EqualTo(4));
            Assert.That(game.Players[1].Hand.Count, Is.EqualTo(5));
            Assert.That(game.Players[1].Deck.Count, Is.EqualTo(7));
            Assert.That(game.ActivePlayer.Offering, Is.EqualTo(5));
            game.EndTurn();
            Assert.That(game.ActivePlayerIndex, Is.Zero);
        }

        [Test]
        public void TurnLimitResetsAndOptionalOfferingGrantIsApplied()
        {
            var game = new PrototypeGame(new PrototypeTestConfig {
                playerCount = 1, maxCardsPerTurn = 1, offeringOnTurnStart = 2, drawOnTurnStart = 0
            });
            Assert.That(game.TryPlay(0, game.ActivePlayer.Hand[0], out _), Is.True);
            Assert.That(game.TryPlay(0, game.ActivePlayer.Hand[0], out _), Is.False);
            game.EndTurn();
            Assert.That(game.CardsPlayedThisTurn, Is.Zero);
            Assert.That(game.ActivePlayer.Offering, Is.EqualTo(6));
            Assert.That(game.TryPlay(0, game.ActivePlayer.Hand[0], out _), Is.True);
        }

        [Test]
        public void OptionalDiscardAndReshufflePreserveAllCardIdentities()
        {
            var game = new PrototypeGame(new PrototypeTestConfig {
                playerCount = 1, initialDeck = new CardId[0], discardHandOnTurnEnd = true,
                drawOnTurnStart = 10, reshuffleDiscardWhenDeckEmpty = true
            });
            var ids = game.ActivePlayer.Hand.Select(card => card.InstanceId).ToArray();
            game.EndTurn();
            Assert.That(game.ActivePlayer.Hand.Select(card => card.InstanceId), Is.EquivalentTo(ids));
            Assert.That(game.ActivePlayer.Deck, Is.Empty);
            Assert.That(game.ActivePlayer.Discard, Is.Empty);
        }

        [Test]
        public void EmptyDeckDoesNotReshuffleWhenDisabled()
        {
            var game = new PrototypeGame(new PrototypeTestConfig {
                playerCount = 1, initialDeck = new CardId[0], discardHandOnTurnEnd = true,
                reshuffleDiscardWhenDeckEmpty = false
            });
            game.EndTurn();
            Assert.That(game.ActivePlayer.Hand, Is.Empty);
            Assert.That(game.ActivePlayer.Discard.Count, Is.EqualTo(4));
        }

        [Test]
        public void UnconfirmedLowerBoundIsExplicitlyConfigurable()
        {
            foreach (bool clamp in new[] { false, true })
            {
                var game = new PrototypeGame(new PrototypeTestConfig {
                    startingFaith = 0, startingDoctrine = 0, startingAtheists = 1,
                    clampEffectValuesAtZero = clamp
                });
                game.TryPlay(0, game.ActivePlayer.Hand[0], out _);
                Assert.That(game.ActivePlayer.Faith, Is.EqualTo(clamp ? 0 : -2));
                Assert.That(game.Atheists, Is.EqualTo(clamp ? 0 : -1));
                game.TryPlay(0, game.ActivePlayer.Hand[0], out _);
                Assert.That(game.ActivePlayer.Doctrine, Is.EqualTo(clamp ? 0 : -5));
            }
        }

        [Test]
        public void SettingsEditsDoNotAlterRunningSessionAndResetRebuildsState()
        {
            var config = new PrototypeTestConfig { drawOnTurnStart = 0, playerCount = 1 };
            var game = new PrototypeGame(config);
            config.drawOnTurnStart = 99;
            config.initialHand[0] = CardId.Follower;
            game.EndTurn();
            Assert.That(game.ActivePlayer.Hand.Count, Is.EqualTo(4));
            Assert.That(game.ActivePlayer.Hand[0].Definition.Id, Is.EqualTo(CardId.Evangelism));
            var reset = new PrototypeGame(new PrototypeTestConfig());
            Assert.That(reset.TurnNumber, Is.EqualTo(1));
            Assert.That(reset.ActivePlayer.Offering, Is.EqualTo(5));
            Assert.That(reset.ActivePlayer.Discard, Is.Empty);
        }

        [Test]
        public void SeededShuffleIsReproducibleAndPlayersHaveSeparateCopies()
        {
            var config = new PrototypeTestConfig { shuffleInitialDeck = true };
            var first = new PrototypeGame(config);
            var second = new PrototypeGame(config);
            Assert.That(first.ActivePlayer.Deck.Select(c => c.Definition.Id),
                Is.EqualTo(second.ActivePlayer.Deck.Select(c => c.Definition.Id)));
            Assert.That(first.Players[0].Hand[0].InstanceId,
                Is.Not.EqualTo(first.Players[1].Hand[0].InstanceId));
        }
    }
}
