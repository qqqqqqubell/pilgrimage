using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pilgrimage.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace Pilgrimage.Tests
{
    public sealed partial class DemoInteractionTests
    {
        private DemoController demo;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior previousBackground;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private Scene loadedDemoScene;
#endif

        [UnitySetUp] public IEnumerator CreateUI()
        {
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keyboard = InputSystem.AddDevice<Keyboard>();
            demo = new GameObject("Demo Under Test").AddComponent<DemoController>();
            yield return null;
            Assert.That(demo.IsModeSelectionOpen, Is.True);
            demo.SelectSinglePlayer();
            Assert.That(demo.ChooseFaction(FactionId.Poverty), Is.True);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (demo != null) Object.Destroy(demo.gameObject);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
#endif
            yield return null;
#if UNITY_EDITOR
            if (loadedDemoScene.IsValid() && loadedDemoScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedDemoScene);
#endif
        }

        private void Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            Assert.That(keyboard[key].wasPressedThisFrame, Is.True, "Synthetic key press must reach the Input System.");
            demo.SendMessage("Update");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        [UnityTest] public IEnumerator FourPlayersFiveFanCardsAndOnlyAnonymousOpponentBacks()
        {
            Assert.That(demo.Game.Players.Count, Is.EqualTo(4));
            Assert.That(demo.HandViews.Count, Is.EqualTo(4));
            var backs = demo.GetComponentsInChildren<RectTransform>().Where(t => t.name == "Private Card Back").ToArray();
            Assert.That(backs.Length, Is.EqualTo(12));
            Assert.That(backs.All(t => t.GetComponent<CardView>() == null), Is.True);
            Assert.That(backs.SelectMany(t => t.GetComponentsInChildren<TMP_Text>()).All(t => t.text == "?"), Is.True);
            Assert.That(demo.HandViews.Select(v => v.Rect.anchoredPosition.x).Distinct().Count(), Is.EqualTo(4));
            Assert.That(demo.GetComponentInChildren<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(demo.GetComponentInChildren<TMP_Text>().font.HasCharacters("신앙심 헌금", out uint[] missing, true, true), Is.True, "Korean font glyphs must be available.");
            yield return null;
        }

        [UnityTest] public IEnumerator KeyboardAndMouseShareSelectionAndSpaceEscapeCloseDetails()
        {
            Press(Key.RightArrow);
            Assert.That(demo.SelectedCardId, Is.EqualTo(demo.HandViews[0].Card.InstanceId));
            Assert.That(demo.HandViews[0].Highlighted, Is.True);
            Press(Key.Space); Assert.That(demo.IsDetailOpen, Is.True);
            Press(Key.Enter); Assert.That(demo.IsAnimating, Is.False);
            Press(Key.Escape); Assert.That(demo.IsDetailOpen, Is.False);
            CardView next = demo.HandViews[1];
            ExecuteEvents.Execute(next.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Assert.That(demo.SelectedCardId, Is.EqualTo(next.Card.InstanceId));
            Assert.That(next.Highlighted, Is.True);
            var right = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            ExecuteEvents.Execute(next.gameObject, right, ExecuteEvents.pointerClickHandler);
            Assert.That(demo.IsDetailOpen, Is.True);
            Press(Key.Space); Assert.That(demo.IsDetailOpen, Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator DoubleClickAppliesRealEffectBlocksRepeatedInputAndRebuildsHand()
        {
            CardInstance worship = GameDebugTools.GiveCard(demo.Game, 0, CardId.RegularWorship);
            demo.SendMessage("Refresh");
            yield return null;
            CardView view = demo.HandViews.First(v => v.Card == worship);
            int hand = demo.Game.Players[0].Hand.Count;
            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, clickCount = 2 };
            ExecuteEvents.Execute(view.gameObject, click, ExecuteEvents.pointerClickHandler);
            Assert.That(demo.IsAnimating, Is.True);
            demo.PlaySelected(); // repeated input while animation is active
            Assert.That(demo.Game.Players[0].Offering, Is.EqualTo(5));
            Assert.That(demo.Game.Players[0].Faith, Is.EqualTo(55));
            Assert.That(demo.Game.Players[0].Hand.Count, Is.EqualTo(hand - 1));
            Assert.That(demo.Game.FindCard(worship.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
            Assert.That(demo.RevealedCardInstanceId, Is.EqualTo(worship.InstanceId));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(demo.IsAnimating, Is.False);
            Assert.That(demo.HandViews.Count, Is.EqualTo(hand - 1));
            Assert.That(demo.Game.Players[0].Hand.Concat(demo.Game.Players[0].Deck).Concat(demo.Game.Players[0].Discard).Count(c => c.InstanceId == demo.RevealedCardInstanceId), Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator IndulgenceFromLabHandConvertsFollowersAndRefreshesUiOnce()
        {
            demo.EnterCardLab(); demo.SetLabHand(new[] { CardId.Indulgence });
            var game = demo.Game; var player = game.Players[0]; var source = player.Hand.Single();
            var followers = player.Deck.Where(c => c.Definition.Id == CardId.Follower).Take(1).ToArray();
            int before = player.FollowerCount, total = player.TotalCardCount;
            var view = demo.HandViews.Single();
            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, clickCount = 2 };
            ExecuteEvents.Execute(view.gameObject, click, ExecuteEvents.pointerClickHandler);
            Assert.That(demo.IsAnimating, Is.True); demo.PlaySelected();
            Assert.That(player.Offering, Is.EqualTo(110)); Assert.That(player.ChurchProperty, Is.EqualTo(10));
            Assert.That(player.Faith, Is.EqualTo(40)); Assert.That(player.Doctrine, Is.EqualTo(40));
            Assert.That(player.FollowerCount, Is.EqualTo(before - 1)); Assert.That(player.TotalCardCount, Is.EqualTo(total));
            Assert.That(followers.All(c => c.Definition.Id == CardId.Heretic && game.FindCard(c.InstanceId).Card == c), Is.True);
            Assert.That(game.FindCard(source.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(demo.IsAnimating, Is.False); Assert.That(demo.HandViews, Is.Empty);
            Assert.That(demo.CanInteract, Is.True); demo.ExitCardLab();
        }

        [UnityTest] public IEnumerator UnusableAndInsufficientCardsDoNotAnimateOrConsumeState()
        {
            CardInstance faithful = GameDebugTools.GiveCard(demo.Game, 0, CardId.FaithfulFollower);
            CardInstance worship = GameDebugTools.GiveCard(demo.Game, 0, CardId.Evangelism);
            GameDebugTools.SetResources(demo.Game, 0, 0, 50, 50);
            demo.SendMessage("Refresh");
            yield return null;
            foreach (CardInstance card in new[] { faithful, worship })
            {
                demo.Select(card.InstanceId); demo.PlaySelected();
                Assert.That(demo.IsAnimating, Is.False);
                Assert.That(demo.Game.FindCard(card.InstanceId).Pile, Is.EqualTo(CardPile.Hand));
            }
            Assert.That(demo.Game.Players[0].Offering, Is.Zero);
        }

        [UnityTest] public IEnumerator PassiveFrontsAndDetailsHideCostsAndAllCatalogDevelopmentCopy()
        {
            var faithful = GameDebugTools.GiveCard(demo.Game, 0, CardId.FaithfulFollower);
            demo.SendMessage("Refresh"); yield return null;
            foreach (var view in demo.HandViews.Where(v => v.Card.Definition.CostKind == CardCostKind.None).ToArray())
            {
                var labels = view.GetComponentsInChildren<TMP_Text>();
                Assert.That(labels.Any(t => t.name == "Cost" || t.name == "Category" || t.name == "Play State"), Is.False);
                Assert.That(labels.Any(t => t.name == "Card Name"), Is.True);
                Assert.That(labels.Any(t => t.name == "Card Number"), Is.True);
                demo.Select(view.Card.InstanceId); demo.ToggleDetail();
                var detail = demo.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Detail Text");
                Assert.That(detail.text, Is.EqualTo(CardPresentation.Detail(view.Card.Definition)));
                Assert.That(detail.text, Does.Not.Contain("코스트")); demo.CloseDetail();
            }
            demo.SendMessage("ShowCatalog");
            string catalog = demo.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Detail Text").text;
            foreach (string marker in new[] { "원문 미명시", "명세 대기", "TODO", "NotImplemented", "구현 상태" }) Assert.That(catalog, Does.Not.Contain(marker));
            demo.CloseDetail();
            Assert.That(demo.Game.FindCard(faithful.InstanceId).Card.Definition.ProtectedFromCardEffects, Is.True);
        }

        [UnityTest] public IEnumerator DevelopmentLabPreservesRegularSessionAndSuppliesFiveRequestedCards()
        {
            var regular = demo.Game;
            var original = regular.Players.SelectMany(p => p.Hand.Concat(p.Deck)).Select(c => c.InstanceId).ToArray();
            demo.EnterCardLab();
            Assert.That(demo.IsCardLab, Is.True);
            Assert.That(demo.HandViews.Select(v => v.Card.Definition.Id), Is.EqualTo(GameDebugTools.FiveActionPreset));
            demo.SendMessage("OpenLabPanel");
            Assert.That(demo.IsLabPanelOpen, Is.True);
#if UNITY_EDITOR
            yield return null;
            CaptureRequested("-pilgrimageLabScreenshot");
#endif
            var search = demo.GetComponentsInChildren<TMP_InputField>().Single(f => f.name == "카드 이름 검색");
            search.text = "포고령"; yield return null;
            var rows = demo.GetComponentsInChildren<TMP_Text>().Where(t => t.name == "Card Lab Entry").ToArray();
            Assert.That(rows.Length, Is.EqualTo(1)); Assert.That(rows[0].text, Does.Contain("효과 구현됨"));
            var inputs = demo.GetComponentsInChildren<TMP_InputField>();
            inputs.Single(f => f.name == "헌금").text = "7";
            inputs.Single(f => f.name == "신앙심").text = "30";
            inputs.Single(f => f.name == "교리").text = "40";
            demo.SendMessage("ApplyLabResources");
            Assert.That(demo.Game.Players[1].Offering, Is.EqualTo(7));
            Assert.That(demo.Game.Players[1].Faith, Is.EqualTo(30));
            Assert.That(demo.Game.EffectiveCost(CardCatalog.Get(CardId.AggressiveMission), 1), Is.EqualTo(3));
            demo.SetLabHand(Enumerable.Repeat(CardId.Follower, 5));
            Assert.That(demo.Game.Players[0].Hand.All(c => !c.Definition.IsPlayable), Is.True);
            demo.ExitCardLab();
            Assert.That(demo.Game, Is.SameAs(regular)); Assert.That(demo.IsCardLab, Is.False);
            Assert.That(regular.Players.SelectMany(p => p.Hand.Concat(p.Deck)).Select(c => c.InstanceId), Is.EquivalentTo(original));
        }

        [UnityTest] public IEnumerator EdictPickerCancellationAndSelectionApplyExactlyOnce()
        {
            demo.EnterCardLab();
            var edict = demo.Game.Players[0].Hand.Single(c => c.Definition.Id == CardId.Edict);
            demo.Select(edict.InstanceId); demo.PlaySelected();
            Assert.That(demo.IsCardPickerOpen, Is.True);
            demo.CloseCardPicker(); Assert.That(demo.Game.Players[0].Offering, Is.EqualTo(100));
            demo.PlaySelected();
            var target = demo.Game.Players[0].Deck[0];
            demo.ChooseRemovalTarget(target.InstanceId); demo.ChooseRemovalTarget(target.InstanceId);
            Assert.That(demo.IsAnimating, Is.True);
            Assert.That(demo.Game.Players[0].Offering, Is.EqualTo(80));
            Assert.That(demo.Game.FindCard(target.InstanceId).Pile, Is.EqualTo(CardPile.Removed));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(demo.HandViews.Count, Is.EqualTo(4));
            demo.ExitCardLab();
        }

        [UnityTest] public IEnumerator CardLabUsesAllFiveCardsViaExistingFieldControls()
        {
            demo.EnterCardLab();
            foreach (var id in GameDebugTools.FiveActionPreset)
            {
                var card = demo.Game.Players[0].Hand.Single(c => c.Definition.Id == id);
                demo.Select(card.InstanceId); demo.PlaySelected();
                if (id == CardId.Edict)
                {
                    Assert.That(demo.IsCardPickerOpen, Is.True);
                    demo.ChooseRemovalTarget(demo.Game.Players[0].Deck[0].InstanceId);
                }
                Assert.That(demo.IsAnimating, Is.True, id.ToString());
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(demo.Game.FindCard(card.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
            }
            Assert.That(demo.Game.Players[0].Hand.Count, Is.Zero);
            Assert.That(demo.Game.Atheists, Is.EqualTo(99));
            Assert.That(demo.Game.Players[0].Faith, Is.EqualTo(63));
            Assert.That(demo.Game.Players[0].Doctrine, Is.EqualTo(75));
            Assert.That(demo.Game.Players[0].Offering, Is.EqualTo(60)); // target faith 50 => mission cost 2
            demo.ExitCardLab();
        }

        [UnityTest] public IEnumerator AiRevealsMultipleCardsAndBlocksDuplicateInput()
        {
            demo.AiConfiguration.revealInterval = 0.1f; demo.AiConfiguration.travelDuration = 0.05f;
            demo.AiConfiguration.actionSafetyLimit = 3; demo.AiConfiguration.randomSeed = 7;
            GameDebugTools.GiveCard(demo.Game, 1, CardId.Evangelism);
            GameDebugTools.GiveCard(demo.Game, 1, CardId.Evangelism);
            GameDebugTools.SetResources(demo.Game, 1, 20, 80, 80);
            demo.Game.EndActionTurn(0);
            float deadline = Time.realtimeSinceStartup + 30;
            bool sawReveal = false, checkedLock = false;
            GameSession original = demo.Game;
            while (demo.Game.Round == 1 && demo.Game.ActivePlayerIndex == 1 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (demo.RevealedAiCard != null)
                {
                    sawReveal = true;
                    Assert.That(demo.RevealedCardInstanceId.HasValue, Is.True);
                    var located = demo.Game.FindCard(demo.RevealedCardInstanceId.Value);
                    Assert.That(located, Is.Not.Null);
                    Assert.That(located.Pile == CardPile.Hand || located.Pile == CardPile.Discard, Is.True);
                    Assert.That(demo.Game.Players.SelectMany(p => p.Hand.Concat(p.Deck).Concat(p.Discard).Concat(p.Removed)).Count(c => c.InstanceId == demo.RevealedCardInstanceId), Is.EqualTo(1));
                    Assert.That(demo.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "Public Name" && t.text == demo.RevealedAiCard.Name), Is.True);
                }
                if (demo.IsAiRunning && !checkedLock)
                {
                    checkedLock = true;
                    demo.PlaySelected(); demo.DebugOpponentStep(); demo.ToggleDetail(); demo.SendMessage("ResetGame");
                    Assert.That(demo.Game, Is.SameAs(original)); Assert.That(demo.IsDetailOpen, Is.False);
                }
            }
            Assert.That(sawReveal && checkedLock, Is.True);
            Assert.That(demo.Game.ActivePlayerIndex, Is.Not.EqualTo(1));
            Assert.That(demo.Game.Players[1].Discard.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(demo.Game.Players[1].FollowerCount, Is.GreaterThanOrEqualTo(9));
        }

        [UnityTest] public IEnumerator LabExitResumesPendingAiAndLabResetPreservesRegularGame()
        {
            demo.AiConfiguration.autoPlay = false;
            demo.AiConfiguration.revealInterval = 0.1f; demo.AiConfiguration.travelDuration = 0.05f;
            demo.AiConfiguration.actionSafetyLimit = 2;
            var regular = demo.Game;
            regular.EndActionTurn(0);
            var ids = regular.Players.SelectMany(p => p.Hand.Concat(p.Deck).Concat(p.Discard)).Select(c => c.InstanceId).ToArray();
            demo.EnterCardLab(); demo.SendMessage("ResetGame"); demo.CloseLabPanel();
            demo.AiConfiguration.autoPlay = true;
            yield return null;
            Assert.That(demo.IsCardLab, Is.True); Assert.That(demo.IsAiRunning, Is.False);
            Assert.That(regular.ActivePlayerIndex, Is.EqualTo(1));
            Assert.That(regular.Players.SelectMany(p => p.Hand.Concat(p.Deck).Concat(p.Discard)).Select(c => c.InstanceId), Is.EquivalentTo(ids));
            demo.ExitCardLab();
            Assert.That(demo.Game, Is.SameAs(regular));
            float deadline = Time.realtimeSinceStartup + 30;
            while ((regular.Round < 2 || regular.ActivePlayerIndex != 0 || demo.IsAiRunning) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(regular.Round, Is.EqualTo(2)); Assert.That(regular.ActivePlayerIndex, Is.Zero);
            Assert.That(demo.IsAiRunning || demo.IsAnimating || demo.IsCardLab, Is.False);
        }

        [UnityTest] public IEnumerator AiPlayFailureDuringRevealFinishesWithoutRepeatingOrCharging()
        {
            demo.AiConfiguration.autoPlay = false;
            demo.AiConfiguration.revealInterval = 0.1f; demo.AiConfiguration.travelDuration = 0.2f;
            GameDebugTools.ReplaceHand(demo.Game, 1, Enumerable.Repeat(CardId.Follower, 5));
            var source = GameDebugTools.GiveCard(demo.Game, 1, CardId.Evangelism);
            GameDebugTools.SetResources(demo.Game, 1, 10, 50, 50);
            demo.Game.EndActionTurn(0); demo.DebugOpponentStep();
            Assert.That(demo.IsAiRunning, Is.True);
            Assert.That(demo.RevealedCardInstanceId, Is.EqualTo(source.InstanceId));
            demo.EnterCardLab(); Assert.That(demo.IsCardLab, Is.False); // Cannot swap sessions mid-animation.
            GameDebugTools.SetResources(demo.Game, 1, 0, 50, 50); // Invalidate after selection, before execution.
            float deadline = Time.realtimeSinceStartup + 30;
            while (demo.IsAiRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(demo.IsAiRunning, Is.False); Assert.That(demo.Game.ActivePlayerIndex, Is.EqualTo(2));
            Assert.That(demo.Game.FindCard(source.InstanceId).Pile, Is.EqualTo(CardPile.Hand));
            Assert.That(demo.Game.Players[1].Offering, Is.Zero); Assert.That(demo.Game.Atheists, Is.EqualTo(100));
            Assert.That(demo.Game.Players[1].Faith, Is.EqualTo(50)); Assert.That(demo.CanInteract, Is.True);
        }

        [UnityTest] public IEnumerator AiInterruptedRevealCanResumeAndApplyCardExactlyOnce()
        {
            demo.AiConfiguration.autoPlay = false;
            demo.AiConfiguration.revealInterval = 0.1f; demo.AiConfiguration.travelDuration = 0.2f;
            GameDebugTools.ReplaceHand(demo.Game, 1, Enumerable.Repeat(CardId.Follower, 5));
            var source = GameDebugTools.GiveCard(demo.Game, 1, CardId.Evangelism);
            GameDebugTools.SetResources(demo.Game, 1, 10, 50, 50);
            int followers = demo.Game.Players[1].FollowerCount;
            demo.Game.EndActionTurn(0); demo.DebugOpponentStep();
            Assert.That(demo.IsAiRunning, Is.True);
            demo.enabled = false; // OnDisable stops the coroutine before effects are committed.
            Assert.That(demo.IsAiRunning || demo.IsAnimating, Is.False);
            Assert.That(demo.Game.FindCard(source.InstanceId).Pile, Is.EqualTo(CardPile.Hand));
            demo.enabled = true; demo.DebugOpponentStep();
            float deadline = Time.realtimeSinceStartup + 30;
            while (demo.IsAiRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(demo.IsAiRunning, Is.False); Assert.That(demo.Game.ActivePlayerIndex, Is.EqualTo(2));
            Assert.That(demo.Game.Players[1].Offering, Is.EqualTo(2));
            Assert.That(demo.Game.Players[1].FollowerCount, Is.EqualTo(followers + 2));
            Assert.That(demo.Game.Players[1].Discard.Count(c => c.InstanceId == source.InstanceId), Is.EqualTo(1));
            Assert.That(demo.Game.Atheists, Is.EqualTo(98));
        }

        [UnityTest] public IEnumerator AiChecksOtherTargetsAndEndsNormallyWithOnlyPassiveCardsLeft()
        {
            demo.AiConfiguration.autoPlay = false;
            demo.AiConfiguration.revealInterval = 0.1f; demo.AiConfiguration.travelDuration = 0.05f;
            GameDebugTools.ReplaceHand(demo.Game, 1, Enumerable.Repeat(CardId.Follower, 5));
            var mission = GameDebugTools.GiveCard(demo.Game, 1, CardId.AggressiveMission);
            // Make only player 3 a valid target, independent of initial shuffle.
            GameDebugTools.SetResources(demo.Game, 0, 1000, 50, 50);
            foreach (int owner in new[] { 0, 2, 3 })
                foreach (var card in demo.Game.Players[owner].Deck.Where(c => c.Definition.Id == CardId.Follower).ToArray())
                {
                    var removal = GameDebugTools.GiveCard(demo.Game, 0, owner == 0 ? CardId.Edict : CardId.PublicDenunciation);
                    Assert.That(demo.Game.TryPlay(0, removal.InstanceId, out _, new EffectTarget { PlayerIndex = owner, CardInstanceId = card.InstanceId }), Is.True);
                }
            var victim = GameDebugTools.GiveCard(demo.Game, 3, CardId.Follower, CardPile.Deck);
            GameDebugTools.SetResources(demo.Game, 1, 10, 50, 50);
            demo.Game.EndActionTurn(0); demo.DebugOpponentStep();
            float deadline = Time.realtimeSinceStartup + 30;
            while (demo.IsAiRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(demo.IsAiRunning, Is.False); Assert.That(demo.Game.ActivePlayerIndex, Is.EqualTo(2));
            Assert.That(demo.Game.FindCard(mission.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
            Assert.That(demo.Game.FindCard(victim.InstanceId).Owner, Is.EqualTo(1));
            Assert.That(demo.Game.Players[1].Hand.All(c => c.Definition.Id == CardId.Follower), Is.True);
        }

        [UnityTest] public IEnumerator AutomaticOpponentsReturnControlToHumanWithoutButtons()
        {
            demo.AiConfiguration.revealInterval = 0.1f; demo.AiConfiguration.travelDuration = 0.05f;
            demo.AiConfiguration.actionSafetyLimit = 2;
            demo.Game.EndActionTurn(0);
            // Rendering/font warm-up can stall frames in batch mode; retain a
            // finite bound without confusing a slow renderer with AI nontermination.
            float deadline = Time.realtimeSinceStartup + 30;
            while ((demo.Game.Round < 2 || demo.Game.ActivePlayerIndex != 0 || demo.IsAiRunning) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(demo.Game.Round, Is.EqualTo(2)); Assert.That(demo.Game.ActivePlayerIndex, Is.Zero);
            Assert.That(demo.IsAiRunning, Is.False); Assert.That(demo.Game.Players[0].Hand.Count, Is.EqualTo(4));
        }

        [UnityTest] public IEnumerator RandomRewardsAreAwardedBeforeResultAcknowledgement()
        {
            demo.AiConfiguration.autoPlay = false;
            for (int round = 1; round <= 4; round++)
            {
                for (int turn = 0; turn < 4; turn++) demo.Game.EndActionTurn(demo.Game.ActivePlayerIndex);
                if (round != 4 && demo.Game.Phase == GamePhase.RewardChoice) demo.Game.AcknowledgeRewards();
            }
            demo.SendMessage("Refresh"); yield return null;
            Assert.That(demo.Game.RoundRewards.Count, Is.EqualTo(4));
            Assert.That(demo.GetComponentsInChildren<TMP_Text>().Count(t => t.name.StartsWith("Award ")), Is.EqualTo(4));
            var ids = demo.Game.RoundRewards.Select(r => r.InstanceId).ToArray();
            foreach (var receipt in demo.Game.RoundRewards) Assert.That(demo.Game.FindCard(receipt.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
            demo.GetComponentsInChildren<Button>().Single(b => b.name == "획득 확인 · 계속").onClick.Invoke();
            Assert.That(demo.Game.Round, Is.EqualTo(5));
            Assert.That(demo.Game.RewardHistory.Select(r => r.InstanceId), Is.EqualTo(ids));
            Assert.That(demo.Game.Players.All(p => p.TotalCardCount == 16), Is.True);
        }

#if UNITY_EDITOR
        [UnityTest] public IEnumerator ShippedDemoSceneLoadsWithoutManualReferences()
        {
            Object.Destroy(demo.gameObject);
            yield return null;
            loadedDemoScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PilgrimageDemo.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; // Scene integration completes on the next player-loop update.
            demo = loadedDemoScene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<DemoController>()).Single();
            yield return null;
            Assert.That(demo.IsModeSelectionOpen, Is.True); Assert.That(demo.Game, Is.Null);
            demo.SelectSinglePlayer();
            Assert.That(demo.IsFactionSelectionOpen, Is.True);
            Assert.That(demo.ChooseFaction(FactionId.Poverty), Is.True);
            Assert.That(demo.Game.Players.Count, Is.EqualTo(4));
            Assert.That(demo.HandViews.Count, Is.EqualTo(4));
            for (int i = 0; i < 5; i++) yield return null;
            CaptureRequested("-pilgrimageScreenshot");
        }

        private void CaptureRequested(string flagName)
        {
            // Optional renderer QA artifact. Null graphics devices still run the scene assertions.
            string[] arguments = System.Environment.GetCommandLineArgs();
            int flag = System.Array.IndexOf(arguments, flagName);
            if (flag >= 0 && flag + 1 < arguments.Length && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                // Batch mode has no Game-view end-of-frame capture. Render the
                // same canvas explicitly through its background camera instead.
                Canvas canvas = demo.GetComponentInChildren<Canvas>();
                Camera camera = demo.GetComponentInChildren<Camera>();
                var target = new RenderTexture(1920, 1080, 24);
                var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                int previousMask = camera.cullingMask;
                try
                {
                    camera.targetTexture = target; camera.cullingMask = -1;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
                    System.IO.File.WriteAllBytes(arguments[flag + 1], texture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous; camera.targetTexture = null; camera.cullingMask = previousMask;
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                    target.Release(); Object.Destroy(target); Object.Destroy(texture);
                }
                Assert.That(System.IO.File.Exists(arguments[flag + 1]), Is.True);
            }
        }
#endif
    }
}
