using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pilgrimage.UI
{
    [DisallowMultipleComponent]
    public sealed partial class DemoController : MonoBehaviour
    {
        [SerializeField] private DemoSettings settings = null;
        public GameSession Game { get; private set; }
        public bool IsAnimating { get; private set; }
        public bool IsDetailOpen => modal != null && modal.gameObject.activeSelf;
        public bool CanInteract => Game != null && !IsFactionSelectionOpen && !IsAnimating && !IsDetailOpen && !IsAiRunning && !IsLabPanelOpen && !IsCardPickerOpen;
        public bool IsAiRunning { get; private set; }
        public CardDefinition RevealedAiCard { get; private set; }
        // Presentation reference only. The physical card remains in exactly one model pile.
        public Guid? RevealedCardInstanceId { get; private set; }
        public AiSettings AiConfiguration => aiSettings;
        public Guid? SelectedCardId { get; private set; }
        public IReadOnlyList<CardView> HandViews => handViews.AsReadOnly();
        public Sprite Illustration => settings != null ? settings.cardIllustration : null;
        private TMP_FontAsset font;
        private bool ownsFont;
        private RectTransform root, handRoot, modal, rewardRoot;
        private TMP_Text roundText, messageText, detailText;
        private TMP_Text activeText, revealedName, revealedNumber;
        private RectTransform revealedCard;
        private readonly TMP_Text[,] opponentStats = new TMP_Text[3, 3];
        private readonly TMP_Text[] ownStats = new TMP_Text[4];
        private AiSettings aiSettings;
        private RuleBasedAi[] aiPlayers;
        private Button manualAiButton;
        private readonly TMP_Text[] playerLabels = new TMP_Text[4];
        private readonly RectTransform[] opponentHands = new RectTransform[3];
        private readonly List<CardView> handViews = new List<CardView>();
        private readonly List<string> logs = new List<string>();
        private TMP_Text logText, targetLabel;
        private Button endButton, debugButton;
        private Button grantDebugButton;
        private int targetPlayer = 1;
        private bool keyboardSelection;
        private GameObject ownedEventSystem;

        private void Start()
        {
            InitializeFont();
            if (Debug.isDebugBuild || Application.isEditor)
                Debug.Log("Pilgrimage 카드 검증 (개발 로그)\n" + string.Join("\n", CardPresentation.Diagnostics(CardCatalog.All)));
            aiSettings = settings != null && settings.ai != null ? settings.ai : new AiSettings();
            BuildUI();
            BuildFactionSelection();
        }

        private void InitializeAiPlayers()
            => aiPlayers = Enumerable.Range(0, 4).Select(i => new RuleBasedAi(aiSettings, i)).ToArray();

        private void InitializeFont()
        {
            if (settings != null && settings.koreanFont != null) { font = settings.koreanFont; return; }
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string family in new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic" })
            {
                if (!installed.Contains(family)) continue;
                font = TMP_FontAsset.CreateFontAsset(family, "Regular", 42);
                if (font != null) { font.name = "Pilgrimage Runtime Korean"; ownsFont = true; break; }
            }
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
                Debug.LogWarning("Pilgrimage: OS 한글 폰트를 찾지 못했습니다. DemoSettings의 Korean Font에 TMP 한글 폰트를 연결하세요.");
            }
            if (font != null && !font.HasCharacters("신앙심 교리 정합성 헌금 포교", out uint[] missing, true, true))
                Debug.LogWarning("Pilgrimage: 폰트에 한글 글리프가 없습니다: " + string.Join(",", missing));
        }

        private void BuildUI()
        {
            var cameraObject = new GameObject("Demo Background Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            Camera backgroundCamera = cameraObject.GetComponent<Camera>();
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color(0.035f, 0.065f, 0.09f);
            backgroundCamera.cullingMask = 0;
            backgroundCamera.orthographic = true;
            var canvasObject = new GameObject("Pilgrimage v2 Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            root = canvasObject.GetComponent<RectTransform>();
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            // No automatic navigation: arrows/Space/Enter belong to hand selection.
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Pilgrimage EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(transform, false);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            RectTransform background = Panel(root, "Background", Vector2.zero, Vector2.zero, new Color(0.035f, 0.065f, 0.09f));
            Stretch(background);
            background.GetComponent<Image>().raycastTarget = false;
            Text(root, "Title", "PILGRIMAGE  /  FOUR ORDERS", new Vector2(-370, 501), new Vector2(1050, 48), 35, TextAlignmentOptions.Left);
            Button(root, "카드 도감", new Vector2(650, 501), new Vector2(165, 40), ShowCatalog);
            Button(root, "메뉴", new Vector2(830, 501), new Vector2(130, 40), ShowMenu);

            BuildPlayerPanel(0, new Vector2(0, -487), new Vector2(1540, 86));
            BuildPlayerPanel(1, new Vector2(0, 356), new Vector2(680, 205));
            BuildPlayerPanel(2, new Vector2(-755, 105), new Vector2(340, 405));
            BuildPlayerPanel(3, new Vector2(755, 105), new Vector2(340, 405));

            RectTransform table = Panel(root, "Shared Table", new Vector2(0, 98), new Vector2(1050, 300), new Color(0.055f, 0.1f, 0.125f));
            roundText = Text(table, "Round State", "", new Vector2(0, 125), new Vector2(990, 35), 25);
            activeText = Text(table, "Active Order", "", new Vector2(-300, 66), new Vector2(370, 70), 26);
            messageText = Text(table, "Feedback", "", new Vector2(295, 8), new Vector2(350, 120), 22);
            endButton = Button(table, "행동 종료", new Vector2(365, -113), new Vector2(230, 45), EndLocalTurn);
            Button(table, "대상", new Vector2(-410, -113), new Vector2(110, 42), CycleTarget);
            targetLabel = Text(table, "Target", "", new Vector2(-175, -113), new Vector2(335, 42), 18);
            revealedCard = Panel(table, "Submitted AI Card", new Vector2(0, 6), new Vector2(148, 206), new Color(0.15f, 0.25f, 0.3f));
            revealedNumber = Text(revealedCard, "Public Number", "", new Vector2(0, 78), new Vector2(130, 26), 18);
            revealedName = Text(revealedCard, "Public Name", "", new Vector2(0, 37), new Vector2(130, 62), 23);
            RectTransform art = Panel(revealedCard, "Public Illustration", new Vector2(0, -41), new Vector2(110, 75), new Color(0, 0, 0, 0.22f));
            art.GetComponent<Image>().raycastTarget = false;
            if (Illustration != null) { art.GetComponent<Image>().sprite = Illustration; art.GetComponent<Image>().color = Color.white; }
            else Text(art, "Public Placeholder", "+", Vector2.zero, new Vector2(100, 60), 40);
            revealedCard.gameObject.SetActive(false);
            logText = Text(root, "Public Action Log", "", new Vector2(0, -91), new Vector2(1020, 40), 16);
            handRoot = Rect(root, "My Hand", new Vector2(0, -281), new Vector2(1300, 300));

            rewardRoot = Panel(root, "Reward Selection", new Vector2(0, 80), new Vector2(1130, 510), new Color(0.085f, 0.14f, 0.19f));
            rewardRoot.gameObject.SetActive(false);
            modal = Panel(root, "Detail Overlay", Vector2.zero, Vector2.zero, new Color(0.005f, 0.012f, 0.02f, 0.96f));
            Stretch(modal);
            RectTransform detailPanel = Panel(modal, "Detail Face (not opponent back)", Vector2.zero, new Vector2(1240, 870), new Color(0.09f, 0.15f, 0.2f));
            RectTransform viewport = Rect(detailPanel, "Detail Scroll", new Vector2(0, 12), new Vector2(1140, 720));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            RectTransform content = Rect(viewport, "Content", Vector2.zero, new Vector2(1140, 720));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, 720);
            detailText = Text(content, "Detail Text", "", Vector2.zero, new Vector2(1090, 700), 26, TextAlignmentOptions.TopLeft);
            detailText.rectTransform.anchorMin = new Vector2(0.5f, 1); detailText.rectTransform.anchorMax = new Vector2(0.5f, 1); detailText.rectTransform.pivot = new Vector2(0.5f, 1);
            detailText.rectTransform.anchoredPosition = new Vector2(0, -10);
            scroll.content = content; scroll.viewport = viewport;
            Button(detailPanel, "닫기  ·  ESC / 우클릭", new Vector2(-270, -392), new Vector2(450, 48), CloseDetail);
            grantDebugButton = Button(detailPanel, "카드 실험", new Vector2(270, -392), new Vector2(270, 48), GrantAllForDebug);
            debugButton = Button(detailPanel, "새 게임", new Vector2(215, -392), new Vector2(190, 48), () => { CloseDetail(); ResetGame(); });
            manualAiButton = Button(detailPanel, "AI 수동 진행", new Vector2(440, -392), new Vector2(210, 48), () => { CloseDetail(); DebugOpponentStep(); });
            debugButton.gameObject.SetActive(false); manualAiButton.gameObject.SetActive(false);
            grantDebugButton.gameObject.SetActive(false);
            modal.gameObject.SetActive(false);
            BuildCardLabUI();
        }

        private void BuildPlayerPanel(int index, Vector2 position, Vector2 size)
        {
            RectTransform panel = Panel(root, $"Player {index} Table", position, size, index == 0 ? new Color(0.1f, 0.22f, 0.23f) : new Color(0.095f, 0.13f, 0.17f));
            playerLabels[index] = Text(panel, "Public Stats", "", new Vector2(index == 0 ? -550 : 0, index == 0 ? 0 : size.y / 2 - 30), new Vector2(index == 0 ? 380 : size.x - 24, 42), 24);
            if (index == 0)
            {
                string[] glyphs = { "$", "믿", "율", "人" };
                for (int i = 0; i < 4; i++) ownStats[i] = StatChip(panel, glyphs[i], new Vector2(-155 + i * 180, 0), i);
            }
            else for (int i = 0; i < 3; i++) opponentStats[index - 1, i] = StatChip(panel, new[] { "人", "$", "패" }[i], new Vector2(-100 + i * 100, index == 1 ? 20 : 108), i);
            Button(panel, index == 0 ? "내 추가 상태" : "상태", new Vector2(size.x / 2 - (index == 0 ? 100 : 48), -size.y / 2 + 23), new Vector2(index == 0 ? 170 : 80, 33), () => ShowPlayerStats(index));
            if (index > 0)
                opponentHands[index - 1] = Rect(panel, "Private Hand Backs", new Vector2(0, index == 1 ? -47 : -26), new Vector2(size.x - 25, index == 1 ? 80 : 200));
        }

        private TMP_Text StatChip(Transform parent, string glyph, Vector2 position, int color)
        {
            RectTransform chip = Rect(parent, "Stat " + glyph, position, new Vector2(95, 38));
            Color[] colors = { new Color(0.24f, 0.49f, 0.42f), new Color(0.48f, 0.38f, 0.2f), new Color(0.27f, 0.36f, 0.5f), new Color(0.45f, 0.3f, 0.43f) };
            RectTransform icon = Panel(chip, "Icon", new Vector2(-29, 0), new Vector2(30, 30), colors[color % 4]);
            icon.GetComponent<Image>().raycastTarget = false;
            Text(icon, "Symbol", glyph, Vector2.zero, new Vector2(29, 29), 19);
            return Text(chip, "Value", "0", new Vector2(20, 0), new Vector2(58, 36), 24);
        }

        private void Update()
        {
            if (Game == null || IsAnimating || IsAiRunning) return;
            if (IsLabPanelOpen || IsCardPickerOpen)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { CloseCardPicker(); CloseLabPanel(); }
                return;
            }
            if (!IsCardLab && !IsDetailOpen && aiSettings.autoPlay && ((Game.Phase == GamePhase.Actions && Game.ActivePlayerIndex != 0) || (Game.Phase == GamePhase.RewardChoice && Game.RewardPlayerIndex != 0)))
            { StartCoroutine(RunAi()); return; }
            Keyboard keys = Keyboard.current;
            if (IsDetailOpen)
            {
                if ((keys != null && (keys.escapeKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame)) ||
                    (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)) CloseDetail();
                return;
            }
            if (keys == null) return;
            if (keys.leftArrowKey.wasPressedThisFrame) SelectRelative(-1);
            if (keys.rightArrowKey.wasPressedThisFrame) SelectRelative(1);
            if (keys.spaceKey.wasPressedThisFrame) ToggleDetail();
            if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) PlaySelected();
        }

        public void Select(Guid id)
        {
            if (!CanInteract || !handViews.Any(v => v.Card.InstanceId == id)) return;
            SelectedCardId = id;
            ApplySelection();
        }
        public void SelectFromMouse(Guid id) { keyboardSelection = false; Select(id); }

        public void ReleaseMouseSelection(Guid id)
        {
            if (!CanInteract || keyboardSelection || SelectedCardId != id) return;
            SelectedCardId = null;
            ApplySelection();
        }

        public void SelectRelative(int direction)
        {
            if (!CanInteract || handViews.Count == 0) return;
            keyboardSelection = true;
            int index = handViews.FindIndex(v => v.Card.InstanceId == SelectedCardId);
            index = index < 0 ? direction < 0 ? handViews.Count - 1 : 0 : (index + direction + handViews.Count) % handViews.Count;
            Select(handViews[index].Card.InstanceId);
        }

        private void ApplySelection()
        {
            foreach (CardView view in handViews) view.RestoreSiblingOrder();
            foreach (CardView view in handViews) view.SetHighlight(view.Card.InstanceId == SelectedCardId);
        }

        public void ToggleDetail()
        {
            if (IsAnimating || IsAiRunning) return;
            if (IsDetailOpen) { CloseDetail(); return; }
            CardView view = handViews.FirstOrDefault(v => v.Card.InstanceId == SelectedCardId);
            if (view == null) { Feedback("먼저 카드를 선택하세요."); return; }
            CardDefinition def = view.Card.Definition;
            OpenDetail(CardPresentation.Detail(def));
        }

        private void OpenDetail(string text)
        {
            grantDebugButton.gameObject.SetActive(false);
            debugButton.gameObject.SetActive(false); manualAiButton.gameObject.SetActive(false);
            text = CardPresentation.PlayerText(text);
            detailText.text = text;
            detailText.rectTransform.sizeDelta = new Vector2(1090, Mathf.Max(700, detailText.GetPreferredValues(text, 1090, 0).y + 25));
            var content = (RectTransform)detailText.transform.parent;
            content.sizeDelta = new Vector2(0, detailText.rectTransform.sizeDelta.y + 20);
            content.anchoredPosition = Vector2.zero;
            modal.gameObject.SetActive(true);
            modal.SetAsLastSibling();
        }

        public void CloseDetail() { if (modal != null) modal.gameObject.SetActive(false); }

        public void PlaySelected()
        {
            if (!CanInteract || !SelectedCardId.HasValue) return;
            Guid id = SelectedCardId.Value;
            if (Game.Players[0].Hand.FirstOrDefault(c => c.InstanceId == id)?.Definition.Target == TargetKind.Card)
            { OpenRemovalPicker(id); return; }
            ExecutePlayerCard(id, new EffectTarget { PlayerIndex = targetPlayer });
        }

        private void ExecutePlayerCard(Guid id, EffectTarget target)
        {
            CardDefinition definition = Game.Players[0].Hand.FirstOrDefault(c => c.InstanceId == id)?.Definition;
            int beforeOffering = Game.Players[0].Offering, beforeFollowers = Game.Players[0].FollowerCount;
            int beforeFaith = Game.Players[0].Faith, beforeDoctrine = Game.Players[0].Doctrine;
            if (!Game.TryPlay(0, id, out string message, target)) { Feedback(message); return; }
            Log(message);
            Reveal(definition, false, id);
            ShowResult(0, beforeOffering, beforeFollowers, beforeFaith, beforeDoctrine);
            CardView view = handViews.FirstOrDefault(v => v.Card.InstanceId == id);
            StartCoroutine(ReleaseCard(view));
        }

        private IEnumerator ReleaseCard(CardView view)
        {
            IsAnimating = true;
            endButton.interactable = false;
            if (view != null)
            {
                var group = view.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false;
                view.Rect.SetAsLastSibling();
                Vector2 start = view.Rect.anchoredPosition;
                float duration = settings != null ? Mathf.Max(0.1f, settings.releaseDuration) : 0.25f;
                for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    float t = elapsed / duration;
                    view.Rect.anchoredPosition = start + Vector2.up * (t * 180);
                    view.Rect.localScale = Vector3.one * (1 - t * 0.3f);
                    group.alpha = 1 - t;
                    yield return null;
                }
            }
            SelectedCardId = null;
            IsAnimating = false;
            Refresh();
        }

        private void Refresh()
        {
            if (Game == null) return;
            for (int i = 0; i < 4; i++)
            {
                PlayerState p = Game.Players[i];
                string marker = Game.Phase == GamePhase.Actions && i == Game.ActivePlayerIndex ? "▶ " : "";
                playerLabels[i].text = marker + p.Name;
                if (i > 0)
                {
                    opponentStats[i - 1, 0].text = p.FollowerCount.ToString();
                    opponentStats[i - 1, 1].text = p.Offering.ToString();
                    opponentStats[i - 1, 2].text = p.Hand.Count.ToString();
                }
                else
                {
                    int[] values = { p.Offering, p.Faith, p.Doctrine, p.FollowerCount };
                    for (int stat = 0; stat < 4; stat++) ownStats[stat].text = values[stat].ToString();
                }
                if (i > 0) RefreshBacks(i);
            }
            roundText.text = $"ROUND {Game.Round:00} / 40     무신론자 {Game.Atheists}";
            activeText.text = Game.Phase == GamePhase.Actions ? Game.Players[Game.ActivePlayerIndex].Name + (Game.ActivePlayerIndex == 0 ? "\n내 차례" : "\nAI 차례") : Game.Phase == GamePhase.RewardChoice ? "보상 선택" : "게임 종료";
            if (Game.Phase == GamePhase.Actions && Game.ActivePlayerIndex == 0) targetLabel.text = Game.Players[targetPlayer].Name;
            endButton.interactable = !IsAnimating && !IsAiRunning && Game.Phase == GamePhase.Actions && Game.ActivePlayerIndex == 0;
            foreach (CardView view in handViews) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            handViews.Clear();
            for (int i = 0; i < Game.Players[0].Hand.Count; i++)
            {
                RectTransform card = Panel(handRoot, "My Card " + i, Vector2.zero, new Vector2(200, 280), Color.black);
                CardView view = card.gameObject.AddComponent<CardView>();
                view.Bind(this, Game.Players[0].Hand[i], i, 1300);
                handViews.Add(view);
            }
            if (SelectedCardId.HasValue && !handViews.Any(v => v.Card.InstanceId == SelectedCardId)) SelectedCardId = null;
            ApplySelection();
            BuildRewards();
            if (IsLabPanelOpen) RefreshLabLibrary();
        }

        private void RefreshBacks(int playerIndex)
        {
            RectTransform area = opponentHands[playerIndex - 1];
            foreach (Transform child in area) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            int count = Game.Players[playerIndex].Hand.Count;
            for (int i = 0; i < count; i++)
            {
                // Only count/index enters this UI. No hidden CardInstance or definition is bound.
                float spacing = count <= 1 ? 0 : Mathf.Min(56, (area.sizeDelta.x - 65) / (count - 1));
                RectTransform back = Panel(area, "Private Card Back", new Vector2((i - (count - 1) / 2f) * spacing, 0), new Vector2(62, 82), new Color(0.3f, 0.32f, 0.35f));
                back.GetComponent<Image>().raycastTarget = false;
                var outline = back.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.7f, 0.72f, 0.74f); outline.effectDistance = new Vector2(1.5f, -1.5f);
                if (settings != null && settings.cardBack != null) back.GetComponent<Image>().sprite = settings.cardBack;
                else Text(back, "Back Symbol", "?", Vector2.zero, new Vector2(54, 70), 30);
            }
        }

        private void BuildRewards()
        {
            foreach (Transform child in rewardRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            rewardRoot.gameObject.SetActive(Game.Phase == GamePhase.RewardChoice && (Game.RewardPlayerIndex == 0 || !aiSettings.autoPlay));
            if (!rewardRoot.gameObject.activeSelf) return;
            rewardRoot.SetAsLastSibling();
            Text(rewardRoot, "Reward Title", $"{Game.Round}라운드 · 무작위 카드 획득 결과", new Vector2(0, 210), new Vector2(1070, 55), 25);
            for (int i = 0; i < Game.RoundRewards.Count; i++)
            {
                var receipt = Game.RoundRewards[i];
                string pile = receipt.Destination == CardPile.Discard ? "버린 더미" : receipt.Destination == CardPile.Deck ? "덱" : receipt.Destination == CardPile.Hand ? "손패" : "제거 더미";
                Text(rewardRoot, "Award " + receipt.PlayerIndex,
                    Game.Players[receipt.PlayerIndex].Name + "  ·  " + CardCatalog.Get(receipt.CardId).Name + "  →  " + pile,
                    new Vector2(0, 125 - i * 75), new Vector2(1050, 60), 24);
            }
            Button(rewardRoot, "획득 확인 · 계속", new Vector2(0, -190), new Vector2(500, 55), () => {
                if (IsAnimating || IsAiRunning) return;
                Game.AcknowledgeRewards(); Log(Game.LastMessage); Refresh();
            });
        }

        private void EndLocalTurn()
        {
            if (!CanInteract || Game.ActivePlayerIndex != 0) return;
            Game.EndActionTurn(0); Log(Game.LastMessage); SelectedCardId = null; Refresh();
        }

        public void DebugOpponentStep()
        {
            if (IsCardLab || !CanInteract || Game.Phase != GamePhase.Actions || Game.ActivePlayerIndex == 0) return;
            StartCoroutine(RunAi());
        }

        private IEnumerator RunAi()
        {
            if (IsAiRunning) yield break;
            IsAiRunning = true;
            try
            {
                if (Game.Phase != GamePhase.Actions) yield break;
                int actor = Game.ActivePlayerIndex;
                if (actor == 0 || Game.Phase != GamePhase.Actions) yield break;
                var turn = new AiTurn(Game, actor, aiPlayers[actor], aiSettings);
                for (AiDecision decision = turn.Next(Game); decision != null; decision = turn.Next(Game))
                {
                    Reveal(decision.Definition, true, decision.CardInstanceId);
                    targetLabel.text = decision.Definition.Target == TargetKind.Self ? "" : Game.Players[decision.Target.PlayerIndex].Name;
                    float travel = Mathf.Clamp(aiSettings.travelDuration, 0.05f, 2);
                    float interval = Mathf.Clamp(aiSettings.revealInterval, 0.1f, 10);
                    Vector2 start = actor == 1 ? new Vector2(0, 211) : actor == 2 ? new Vector2(-755, -19) : new Vector2(755, -19);
                    for (float t = 0; t < travel; t += Time.unscaledDeltaTime)
                    {
                        revealedCard.anchoredPosition = Vector2.Lerp(start, new Vector2(0, 6), t / travel);
                        revealedCard.localScale = Vector3.one * Mathf.Lerp(0.45f, 1, t / travel);
                        yield return null;
                    }
                    revealedCard.anchoredPosition = new Vector2(0, 6); revealedCard.localScale = Vector3.one;
                    int offering = Game.Players[actor].Offering, followers = Game.Players[actor].FollowerCount;
                    int faith = Game.Players[actor].Faith, doctrine = Game.Players[actor].Doctrine;
                    if (Game.TryPlay(actor, decision.CardInstanceId, out string message, decision.Target))
                    { Log(message); ShowResult(actor, offering, followers, faith, doctrine); }
                    else Feedback(message);
                    Refresh();
                    yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, interval - travel));
                }
                Game.EndActionTurn(actor); Log(Game.LastMessage);
            }
            finally { IsAiRunning = false; if (isActiveAndEnabled) Refresh(); }
        }

        private void Reveal(CardDefinition definition, bool ai, Guid instanceId)
        {
            if (definition == null) return;
            RevealedAiCard = ai ? definition : null;
            RevealedCardInstanceId = instanceId;
            revealedCard.gameObject.SetActive(true);
            revealedCard.anchoredPosition = new Vector2(0, 6); revealedCard.localScale = Vector3.one;
            revealedCard.GetComponent<Image>().color = CardView.CategoryColor(definition.Category);
            revealedNumber.text = $"#{(int)definition.Id + 1:00}"; revealedName.text = definition.Name;
        }
        private void ShowResult(int actor, int offering, int followers, int faith, int doctrine)
        {
            PlayerState player = Game.Players[actor];
            var changes = new List<string>();
            if (offering != player.Offering) changes.Add($"헌금 {player.Offering - offering:+#;-#;0}");
            if (followers != player.FollowerCount) changes.Add($"신도 {player.FollowerCount - followers:+#;-#;0}");
            if (faith != player.Faith) changes.Add($"신앙 {player.Faith - faith:+#;-#;0}");
            if (doctrine != player.Doctrine) changes.Add($"교리 {player.Doctrine - doctrine:+#;-#;0}");
            Feedback(changes.Count == 0 ? "효과 적용" : string.Join(" · ", changes));
        }

        private void ResetGame()
        {
            if (!CanInteract) return;
            SelectedCardId = null; RevealedAiCard = null; RevealedCardInstanceId = null; revealedCard.gameObject.SetActive(false); logs.Clear(); logText.text = ""; messageText.text = "";
            if (IsCardLab) { Game = GameDebugTools.CreateCardLab(Game.HumanFaction); Refresh(); }
            else { rewardRoot.gameObject.SetActive(false); ShowFactionSelection(); }
        }
        private void CycleTarget() { if (!CanInteract) return; targetPlayer = Game.AllowSelfAsOpponentTarget ? (targetPlayer + 1) % 4 : targetPlayer % 3 + 1; targetLabel.text = "효과 대상: " + Game.Players[targetPlayer].Name; }
        private void ShowPlayerStats(int index)
        {
            if (!CanInteract) return;
            PlayerState player = Game.Players[index];
            string effects = string.Join("\n", player.TimedEffects.Select(e => CardCatalog.Get(e.Source).Name + " · 남은 정산 " + e.RemainingRoundStarts + "회"));
            OpenDetail($"<size=38>{player.Name}</size>\n\n신앙심: {player.Faith} / 100\n교리 정합성: {player.Doctrine} / 100\n헌금: {player.Offering}\n신도 수: {player.FollowerCount}\n덱: {player.Deck.Count}장 / 손패: {player.Hand.Count}장 / 버린 더미: {player.Discard.Count}장\n교회 재산: {player.ChurchProperty}\n다음 라운드 수입: {Game.PreviewRoundIncome(index)}\n지속 효과: {player.Statuses.Count + player.TimedEffects.Count}개\n{effects}\n\n상대 손패·덱의 카드 이름은 공개하지 않습니다.");
        }
        private void ShowCatalog()
        {
            if (!CanInteract) return;
            OpenDetail($"<size=36>카드 도감 · {Game.Catalog.Count}종</size>\n\n" + string.Join("\n\n────────\n\n", Game.Catalog.Select(CardPresentation.Detail)));
            grantDebugButton.gameObject.SetActive(DevelopmentToolsAvailable);
        }
        private void GrantAllForDebug()
        {
            if (IsAnimating || IsAiRunning || !DevelopmentToolsAvailable) return;
            CloseDetail(); EnterCardLab(); OpenLabPanel();
        }
        private void ShowMenu()
        {
            if (!CanInteract) return;
            OpenDetail("<size=36>메뉴 · 조작 안내</size>\n\n← / → : 카드 선택\nSpace / 우클릭 : 상세보기\nEnter / 더블클릭 : 사용\nESC : 닫기\n\n금($): 헌금 · 믿: 신앙심 · 율: 교리 정합성 · 人: 신도 · 패: 손패\n\nAI 가중치와 공개 간격은 Demo Settings에서 변경합니다.\n\n최근 기록\n" + string.Join("\n", logs));
            debugButton.gameObject.SetActive(true); manualAiButton.gameObject.SetActive(true);
        }
        private void Feedback(string text) { string visible = CardPresentation.PlayerText(text); messageText.text = string.IsNullOrWhiteSpace(visible) ? "현재 진행할 수 없습니다." : visible; }
        private void Log(string text) { logs.Add(text); if (logs.Count > 12) logs.RemoveAt(0); logText.text = ""; }

        public Sprite FrameSprite(CardCategory category)
        {
            if (settings == null) return null;
            switch (category) { case CardCategory.Follower: return settings.followerCardFrame; case CardCategory.Heretic: return settings.hereticCardFrame; case CardCategory.Faction: return settings.factionCardFrame; default: return settings.generalCardFrame; }
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
        public RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        { RectTransform rect = Rect(parent, name, position, size); rect.gameObject.AddComponent<Image>().color = color; return rect; }
        public TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            RectTransform rect = Rect(parent, name, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = fontSize; text.alignment = alignment; text.color = new Color(0.91f, 0.94f, 0.92f);
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
        private Button Button(Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Panel(parent, label, position, size, new Color(0.13f, 0.3f, 0.31f));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(0.7f, 1, 0.85f); colors.pressedColor = new Color(0.45f, 0.8f, 0.65f); button.colors = colors;
            button.onClick.AddListener(action);
            Text(rect, "Label", label, Vector2.zero, size - new Vector2(12, 4), 20);
            return button;
        }
        private void OnDestroy()
        {
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
            if (ownsFont && font != null)
            {
                if (font.material != null) Destroy(font.material);
                foreach (Texture2D atlas in font.atlasTextures) if (atlas != null) Destroy(atlas);
                Destroy(font);
            }
        }
        private void OnDisable() { StopAllCoroutines(); IsAiRunning = false; IsAnimating = false; }
        private void OnEnable() { if (Game != null && root != null) Refresh(); }
    }
}
