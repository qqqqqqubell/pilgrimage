using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pilgrimage.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Pilgrimage.Tests
{
    public sealed partial class DemoInteractionTests
    {
        private void SelectNewGame(FactionId faction)
        {
            demo.AiConfiguration.autoPlay = false;
            demo.SendMessage("ResetGame");
            Assert.That(demo.IsFactionSelectionOpen, Is.True); Assert.That(demo.Game, Is.Null);
            Assert.That(demo.ChooseFaction(faction), Is.True);
        }
        private static void ResolveModelRewards(GameSession game)
        { while (game.Phase == GamePhase.RewardChoice) Assert.That(game.ChooseReward(game.RewardPlayerIndex, game.RewardOptions()[0].Id), Is.True); }
        private static void AdvanceModelRound(GameSession game)
        {
            int round = game.Round;
            for (int i = 0; i < 9 && game.Round == round && game.Phase != GamePhase.Finished; i++)
                if (game.Phase == GamePhase.RewardChoice) ResolveModelRewards(game);
                else Assert.That(game.EndActionTurn(game.ActivePlayerIndex), Is.True);
        }

        [UnityTest] public IEnumerator FactionSelectionBlocksStartupAndAllFourButtonsAssignUniqueSeats()
        {
            foreach (var faction in new[] { FactionId.Poverty, FactionId.Gold, FactionId.Discipline, FactionId.Fur })
            {
                demo.SendMessage("ResetGame");
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(demo.IsFactionSelectionOpen, Is.True); Assert.That(demo.Game, Is.Null);
                Assert.That(demo.IsAiRunning || demo.CanInteract, Is.False);
                demo.DebugOpponentStep(); demo.PlaySelected(); demo.EnterCardLab();
                Assert.That(demo.Game, Is.Null); Assert.That(demo.IsCardLab, Is.False);
                Assert.That(demo.ChooseFaction(FactionId.None), Is.False);
                var choices = demo.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("선택 · ")).ToArray();
                Assert.That(choices.Length, Is.EqualTo(4));
#if UNITY_EDITOR
                if (faction == FactionId.Poverty) CaptureRequested("-pilgrimageFactionScreenshot");
#endif
                choices.Single(b => b.name == "선택 · " + FactionSetup.Name(faction)).onClick.Invoke();
                var session = demo.Game;
                Assert.That(session, Is.Not.Null); Assert.That(session.Players[0].Faction, Is.EqualTo(faction));
                Assert.That(session.Players.Select(p => p.Faction).Distinct().Count(), Is.EqualTo(4));
                Assert.That(session.Players.All(p => p.TotalCardCount == 15 && p.Hand.Count == 5 && p.Offering == 5 && p.Faith == 50 && p.Doctrine == 50), Is.True);
                Assert.That(session.Round, Is.EqualTo(1)); Assert.That(session.ActivePlayerIndex, Is.Zero);
                Assert.That(demo.IsFactionSelectionOpen, Is.False);
                Assert.That(demo.ChooseFaction(faction), Is.False); Assert.That(demo.Game, Is.SameAs(session));
            }
        }

        [UnityTest] public IEnumerator SevenNewCardsUseExistingHandInputAndTheirTimedEffectsActuallyResolve()
        {
            foreach (var id in new[] { CardId.Penance, CardId.SolitaryReflection, CardId.CaravanInvestment, CardId.WitchHunt,
                CardId.HumbleTemple, CardId.TempleRemodeling, CardId.Archbishop })
            {
                demo.EnterCardLab(); demo.SetLabHand(new[] { id });
                var game = demo.Game; var player = game.Players[0]; var card = player.Hand.Single();
                var targets = player.Deck.Where(c => c.Definition.Id == CardId.Follower).ToArray();
                int beforeFollowers = player.FollowerCount;
                demo.Select(card.InstanceId); Press(Key.Enter);
                Assert.That(demo.IsAnimating, Is.True, id.ToString()); demo.PlaySelected();
                Assert.That(game.FindCard(card.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
                Assert.That(player.Offering, Is.EqualTo(20 - card.Definition.Cost + (id == CardId.WitchHunt ? 3 : 0)));
                if (id == CardId.WitchHunt)
                {
                    Assert.That(player.FollowerCount, Is.EqualTo(beforeFollowers - 1));
                    Assert.That(targets[0].Definition.Id, Is.EqualTo(CardId.Heretic));
                    Assert.That(game.FindCard(targets[0].InstanceId).Card, Is.SameAs(targets[0]));
                }
                if (id == CardId.HumbleTemple || id == CardId.SolitaryReflection)
                { Assert.That(player.Faith, Is.EqualTo(id == CardId.HumbleTemple ? 65 : 60)); Assert.That(player.Doctrine, Is.EqualTo(player.Faith)); }
                if (id == CardId.TempleRemodeling)
                { Assert.That(player.ChurchProperty, Is.EqualTo(-4)); Assert.That(game.PreviewRoundIncome(0), Is.EqualTo(10)); }
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(demo.IsAnimating || demo.IsAiRunning, Is.False);
                int settlements = id == CardId.Penance ? 5 : id == CardId.Archbishop ? 3 : id == CardId.SolitaryReflection ? 1 : 2;
                if (id != CardId.WitchHunt)
                {
                    for (int turn = 0; turn < settlements; turn++)
                    { demo.SendMessage("AdvanceLabTurn"); yield return null; Assert.That(demo.IsAiRunning, Is.False); }
                    Assert.That(player.TimedEffects, Is.Empty, id.ToString());
                    if (id == CardId.Penance)
                    { Assert.That(player.Offering, Is.EqualTo(48)); Assert.That(player.Faith, Is.EqualTo(60)); Assert.That(player.Doctrine, Is.EqualTo(60)); }
                    if (id == CardId.CaravanInvestment) Assert.That(player.Offering, Is.InRange(29, 34));
                    if (id == CardId.SolitaryReflection) Assert.That(player.Offering, Is.EqualTo(21));
                    if (id == CardId.HumbleTemple) Assert.That(player.Offering, Is.EqualTo(22));
                    if (id == CardId.TempleRemodeling) Assert.That(player.Offering, Is.EqualTo(37));
                }
                Assert.That(game.FindCard(card.InstanceId).Card, Is.SameAs(card));
                demo.ExitCardLab(); Assert.That(demo.IsCardLab, Is.False);
            }
        }

        [UnityTest] public IEnumerator AllFactionSelectionsRestoreLabAndThreeAiOpponentsReturnControl()
        {
            foreach (var faction in new[] { FactionId.Poverty, FactionId.Gold, FactionId.Discipline, FactionId.Fur })
            {
                SelectNewGame(faction); var regular = demo.Game;
                demo.EnterCardLab(); Assert.That(demo.Game.HumanFaction, Is.EqualTo(faction));
                // A foreign faction card is legal in this explicit test environment.
                demo.SetLabHand(new[] { CardId.Archbishop }); demo.Select(demo.Game.Players[0].Hand[0].InstanceId); demo.PlaySelected();
                yield return new WaitForSecondsRealtime(0.4f); demo.ExitCardLab();
                Assert.That(demo.Game, Is.SameAs(regular)); Assert.That(regular.HumanFaction, Is.EqualTo(faction));
                Assert.That(regular.Players[0].TimedEffects, Is.Empty);
                demo.AiConfiguration.revealInterval = 0.1f; demo.AiConfiguration.travelDuration = 0.05f; demo.AiConfiguration.actionSafetyLimit = 2;
                int[] followers = regular.Players.Select(p => p.FollowerCount).ToArray();
                for (int owner = 1; owner < 4; owner++)
                {
                    GameDebugTools.ReplaceHand(regular, owner, Enumerable.Repeat(CardId.Follower, 5));
                    GameDebugTools.GiveCard(regular, owner, CardId.Evangelism);
                    GameDebugTools.SetResources(regular, owner, 20, 80, 80);
                    followers[owner] = regular.Players[owner].FollowerCount;
                }
                regular.EndActionTurn(0); demo.AiConfiguration.autoPlay = true;
                float deadline = Time.realtimeSinceStartup + 40;
                while ((regular.Round < 2 || regular.ActivePlayerIndex != 0 || demo.IsAiRunning) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(regular.Round, Is.EqualTo(2)); Assert.That(regular.ActivePlayerIndex, Is.Zero); Assert.That(demo.IsAiRunning, Is.False);
                for (int owner = 1; owner < 4; owner++) Assert.That(regular.Players[owner].FollowerCount, Is.GreaterThanOrEqualTo(followers[owner] + 2));
            }
        }

        [UnityTest] public IEnumerator EveryFactionUsesRoundEightRewardFromUiAfterNaturalDrawAndAiContinues()
        {
            foreach (var faction in new[] { FactionId.Poverty, FactionId.Gold, FactionId.Discipline, FactionId.Fur })
            {
                SelectNewGame(faction); var game = demo.Game;
                for (int round = 1; round < 8; round++) AdvanceModelRound(game);
                for (int turn = 0; turn < 4; turn++) game.EndActionTurn(game.ActivePlayerIndex);
                while (game.RewardPlayerIndex != 0) game.ChooseReward(game.RewardPlayerIndex, game.RewardOptions()[0].Id);
                var id = faction == FactionId.Gold ? CardId.Indulgence : faction == FactionId.Poverty ? CardId.HumbleTemple : faction == FactionId.Discipline ? CardId.Archbishop : CardId.MoltingRitual;
                Assert.That(game.RewardOptions().Count, Is.EqualTo(4)); Assert.That(game.RewardOptions().All(c => c.Faction == faction), Is.True);
                demo.SendMessage("Refresh"); yield return null;
                var known = new HashSet<System.Guid>(game.Players[0].Discard.Select(c => c.InstanceId));
                demo.GetComponentsInChildren<Button>().Single(b => b.name.StartsWith(CardCatalog.Get(id).Name + "\n")).onClick.Invoke();
                var reward = game.Players[0].Discard.Single(c => !known.Contains(c.InstanceId));
                Assert.That(reward.Definition.Id, Is.EqualTo(id)); ResolveModelRewards(game);
                for (int n = 0; n < 12 && !game.Players[0].Hand.Contains(reward); n++) AdvanceModelRound(game);
                Assert.That(game.Players[0].Hand.Contains(reward), Is.True);
                for (int turn = 0; turn < 4 && game.ActivePlayerIndex != 0; turn++) game.EndActionTurn(game.ActivePlayerIndex);
                GameDebugTools.SetResources(game, 0, 20, 50, 50, 20);
                GameDebugTools.GiveCard(game, 0, CardId.Follower, CardPile.Deck); GameDebugTools.GiveCard(game, 0, CardId.Follower, CardPile.Deck);
                int followers = game.Players[0].FollowerCount;
                demo.SendMessage("Refresh"); demo.Select(reward.InstanceId); Press(Key.Enter);
                Assert.That(game.FindCard(reward.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
                if (faction == FactionId.Gold)
                {
                    Assert.That(game.Players[0].FollowerCount, Is.EqualTo(followers - 2)); Assert.That(game.Players[0].Offering, Is.EqualTo(26));
                    Assert.That(game.Players[0].Faith, Is.EqualTo(40)); Assert.That(game.Players[0].Doctrine, Is.EqualTo(40)); Assert.That(game.Players[0].ChurchProperty, Is.EqualTo(26));
                }
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(demo.IsAnimating, Is.False);
                int roundBefore = game.Round;
                demo.AiConfiguration.revealInterval = 0.1f; demo.AiConfiguration.travelDuration = 0.05f; demo.AiConfiguration.actionSafetyLimit = 1;
                game.EndActionTurn(0); demo.AiConfiguration.autoPlay = true;
                float deadline = Time.realtimeSinceStartup + 40;
                while ((game.Round <= roundBefore || game.ActivePlayerIndex != 0 || demo.IsAiRunning) && Time.realtimeSinceStartup < deadline)
                {
                    if (!demo.IsAiRunning && game.Phase == GamePhase.RewardChoice && game.RewardPlayerIndex == 0)
                        game.ChooseReward(0, game.RewardOptions()[0].Id);
                    yield return null;
                }
                Assert.That(game.Round, Is.GreaterThan(roundBefore)); Assert.That(game.ActivePlayerIndex, Is.Zero); Assert.That(demo.IsAiRunning, Is.False);
            }
        }

        [UnityTest] public IEnumerator GoldHumanAndThreeRealUiAiPlayersCompleteFortyRoundsWithoutDeadlock()
        {
            SelectNewGame(FactionId.Gold); var game = demo.Game;
            demo.AiConfiguration.revealInterval = 0.1f; demo.AiConfiguration.travelDuration = 0.05f;
            demo.AiConfiguration.actionSafetyLimit = 1; demo.AiConfiguration.autoPlay = true;
            float deadline = Time.realtimeSinceStartup + 240;
            var known = new HashSet<System.Guid>(); int humanTurns = 0, humanRewards = 0;
            while (game.Phase != GamePhase.Finished && Time.realtimeSinceStartup < deadline)
            {
                if (!demo.IsAiRunning)
                {
                    if (game.Phase == GamePhase.Actions && game.ActivePlayerIndex == 0)
                    { Assert.That(game.EndActionTurn(0), Is.True); humanTurns++; }
                    else if (game.Phase == GamePhase.RewardChoice && game.RewardPlayerIndex == 0)
                    { Assert.That(game.ChooseReward(0, game.RewardOptions()[0].Id), Is.True); humanRewards++; }
                }
                var cards = game.Players.SelectMany(p => p.Hand.Concat(p.Deck).Concat(p.Discard).Concat(p.Removed)).Select(c => c.InstanceId).ToArray();
                Assert.That(cards.Distinct().Count(), Is.EqualTo(cards.Length)); Assert.That(known.IsSubsetOf(cards), Is.True); known.UnionWith(cards);
                yield return null;
            }
            Assert.That(game.Phase, Is.EqualTo(GamePhase.Finished), "Last state: round " + game.Round + " actor " + game.ActivePlayerIndex + " ai=" + demo.IsAiRunning);
            Assert.That(game.Round, Is.EqualTo(40)); Assert.That(humanTurns, Is.EqualTo(40)); Assert.That(humanRewards, Is.EqualTo(10));
            // The last AI reward commits Finished before its short reveal interval
            // completes. Verify the coroutine's finally block, rather than racing it.
            float finishDeadline = Time.realtimeSinceStartup + 5;
            while (demo.IsAiRunning && Time.realtimeSinceStartup < finishDeadline) yield return null;
            Assert.That(game.HumanFaction, Is.EqualTo(FactionId.Gold)); Assert.That(demo.IsAiRunning, Is.False);
        }
    }
}
