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
    public sealed class DemoInteractionTests
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
            Assert.That(demo.HandViews.Count, Is.EqualTo(5));
            var backs = demo.GetComponentsInChildren<RectTransform>().Where(t => t.name == "Private Card Back").ToArray();
            Assert.That(backs.Length, Is.EqualTo(15));
            Assert.That(backs.All(t => t.GetComponent<CardView>() == null), Is.True);
            Assert.That(backs.SelectMany(t => t.GetComponentsInChildren<TMP_Text>()).All(t => t.text == "?"), Is.True);
            Assert.That(demo.HandViews.Select(v => v.Rect.anchoredPosition.x).Distinct().Count(), Is.EqualTo(5));
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
            Assert.That(demo.Game.Players[0].Offering, Is.EqualTo(4));
            Assert.That(demo.Game.Players[0].Faith, Is.EqualTo(55));
            Assert.That(demo.Game.Players[0].Hand.Count, Is.EqualTo(hand - 1));
            Assert.That(demo.Game.FindCard(worship.InstanceId).Pile, Is.EqualTo(CardPile.Discard));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(demo.IsAnimating, Is.False);
            Assert.That(demo.HandViews.Count, Is.EqualTo(hand - 1));
        }

        [UnityTest] public IEnumerator UnusableAndInsufficientCardsDoNotAnimateOrConsumeState()
        {
            CardInstance faithful = GameDebugTools.GiveCard(demo.Game, 0, CardId.FaithfulFollower);
            CardInstance worship = GameDebugTools.GiveCard(demo.Game, 0, CardId.RegularWorship);
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

#if UNITY_EDITOR
        [UnityTest] public IEnumerator ShippedDemoSceneLoadsWithoutManualReferences()
        {
            Object.Destroy(demo.gameObject);
            yield return null;
            loadedDemoScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PilgrimageDemo.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; // Scene integration completes on the next player-loop update.
            demo = loadedDemoScene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<DemoController>()).Single();
            yield return null;
            Assert.That(demo.Game.Players.Count, Is.EqualTo(4));
            Assert.That(demo.HandViews.Count, Is.EqualTo(5));
            // Optional renderer QA artifact. Null graphics devices still run the scene assertions.
            string[] arguments = System.Environment.GetCommandLineArgs();
            int flag = System.Array.IndexOf(arguments, "-pilgrimageScreenshot");
            if (flag >= 0 && flag + 1 < arguments.Length && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                for (int i = 0; i < 5; i++) yield return null;
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
