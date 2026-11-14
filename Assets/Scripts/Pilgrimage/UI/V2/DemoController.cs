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
    public sealed class DemoController : MonoBehaviour
    {
        [SerializeField] private DemoSettings settings = null;
        public GameSession Game { get; private set; }
        public bool IsAnimating { get; private set; }
        public bool IsDetailOpen => modal != null && modal.gameObject.activeSelf;
        public bool CanInteract => !IsAnimating && !IsDetailOpen;
        public Guid? SelectedCardId { get; private set; }
        public IReadOnlyList<CardView> HandViews => handViews.AsReadOnly();
        public Sprite Illustration => settings != null ? settings.cardIllustration : null;
        private TMP_FontAsset font;
        private bool ownsFont;
        private RectTransform root, handRoot, modal, rewardRoot;
        private TMP_Text roundText, messageText, detailText;
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
            Game = new GameSession(settings != null ? settings.assumptions : null);
            BuildUI();
            Refresh();
        }

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
            Text(root, "Build Status", "LOCAL DEMO v2 · AI / 온라인 없음", new Vector2(650, 501), new Vector2(450, 40), 21);

            BuildPlayerPanel(0, new Vector2(0, -487), new Vector2(1540, 86));
            BuildPlayerPanel(1, new Vector2(0, 356), new Vector2(680, 205));
            BuildPlayerPanel(2, new Vector2(-755, 105), new Vector2(340, 405));
            BuildPlayerPanel(3, new Vector2(755, 105), new Vector2(340, 405));

            RectTransform table = Panel(root, "Shared Table", new Vector2(0, 98), new Vector2(1050, 300), new Color(0.075f, 0.125f, 0.155f));
            roundText = Text(table, "Round State", "", new Vector2(0, 118), new Vector2(1010, 45), 25);
            messageText = Text(table, "Feedback", "", new Vector2(0, 61), new Vector2(980, 65), 22);
            endButton = Button(table, "내 행동 종료 (임시)", new Vector2(-325, -2), new Vector2(290, 46), EndLocalTurn);
            debugButton = Button(table, "테스트 교단 행동 1회 + 종료", new Vector2(70, -2), new Vector2(440, 46), DebugOpponentStep);
            Button(table, "초기화", new Vector2(405, -2), new Vector2(160, 46), ResetGame);
            Button(table, "대상 변경", new Vector2(-360, -63), new Vector2(180, 42), CycleTarget);
            targetLabel = Text(table, "Target", "", new Vector2(-30, -63), new Vector2(460, 42), 21);
            Button(table, "카드 목록 26종", new Vector2(350, -63), new Vector2(270, 42), ShowCatalog);
            Text(table, "Controls", "← → 선택  ·  Space / 우클릭 상세  ·  Enter / 더블클릭 사용  ·  ESC 닫기", new Vector2(0, -118), new Vector2(1010, 35), 20);
            logText = Text(root, "Public Action Log", "", new Vector2(0, -91), new Vector2(1020, 50), 16);
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
            grantDebugButton = Button(detailPanel, "실험: 26종 손패 추가 + 자원 설정", new Vector2(270, -392), new Vector2(540, 48), GrantAllForDebug);
            grantDebugButton.gameObject.SetActive(false);
            modal.gameObject.SetActive(false);
        }

        private void BuildPlayerPanel(int index, Vector2 position, Vector2 size)
        {
            RectTransform panel = Panel(root, $"Player {index} Table", position, size, index == 0 ? new Color(0.1f, 0.22f, 0.23f) : new Color(0.095f, 0.13f, 0.17f));
            float top = size.y / 2 - 45;
            playerLabels[index] = Text(panel, "Public Stats", "", new Vector2(0, top), new Vector2(size.x - 24, index == 0 ? 70 : 90), index == 0 ? 22 : 21);
            if (index >= 2)
            {
                playerLabels[index].rectTransform.anchoredPosition = new Vector2(0, size.y / 2 - 75);
                playerLabels[index].rectTransform.sizeDelta = new Vector2(size.x - 24, 130);
            }
            Button(panel, index == 0 ? "내 추가 상태" : "상태", new Vector2(size.x / 2 - (index == 0 ? 100 : 48), -size.y / 2 + 23), new Vector2(index == 0 ? 170 : 80, 33), () => ShowPlayerStats(index));
            if (index > 0)
                opponentHands[index - 1] = Rect(panel, "Private Hand Backs", new Vector2(0, index == 1 ? -31 : -26), new Vector2(size.x - 25, index == 1 ? 80 : 200));
        }

        private void Update()
        {
            if (Game == null || IsAnimating) return;
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
            if (IsAnimating) return;
            if (IsDetailOpen) { CloseDetail(); return; }
            CardView view = handViews.FirstOrDefault(v => v.Card.InstanceId == SelectedCardId);
            if (view == null) { Feedback("먼저 카드를 선택하세요."); return; }
            CardDefinition def = view.Card.Definition;
            string cost = def.CostText + (def.Id == CardId.AggressiveMission ? $" (현재 대상: {Game.EffectiveCost(def, targetPlayer)})" : "");
            OpenDetail($"<size=38><b>{def.Name}</b></size>\n#{(int)def.Id + 1:00} · {def.Category} · {def.Faction}\n개별 ID: {view.Card.InstanceId}\n\n헌금 코스트: {cost}\n\n효과\n{def.Description}\n\n사용 조건: {def.Conditions}\n대상: {def.Target} {(def.Target == TargetKind.Self ? "" : "— 중앙 대상 변경 버튼으로 선택")}\n\n구현 상태: {def.Implementation}\n{(!def.IsPlayable ? "현재 사용 불가" : "현재 세션의 비용·대상 조건을 만족해야 사용 가능")}\n\n<i>{def.FlavorText}</i>");
        }

        private void OpenDetail(string text)
        {
            grantDebugButton.gameObject.SetActive(false);
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
            if (!Game.TryPlay(0, id, out string message, new EffectTarget { PlayerIndex = targetPlayer })) { Feedback(message); return; }
            Log(message);
            CardView view = handViews.FirstOrDefault(v => v.Card.InstanceId == id);
            StartCoroutine(ReleaseCard(view));
        }

        private IEnumerator ReleaseCard(CardView view)
        {
            IsAnimating = true;
            endButton.interactable = false; debugButton.interactable = false;
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
            for (int i = 0; i < 4; i++)
            {
                PlayerState p = Game.Players[i];
                string marker = Game.Phase == GamePhase.Actions && i == Game.ActivePlayerIndex ? "▶ " : "";
                playerLabels[i].text = $"{marker}{p.Name}{(i == 0 ? " · 나" : " · 테스트 플레이어")}\n헌금 {p.Offering}  ·  신도 {p.FollowerCount}  ·  보유 {p.TotalCardCount}장  ·  손패 {p.Hand.Count}장";
                if (i >= 2) playerLabels[i].text = $"{marker}{p.Name}\n테스트 플레이어\n헌금 {p.Offering} · 신도 {p.FollowerCount}\n보유 {p.TotalCardCount}장 · 손패 {p.Hand.Count}장";
                if (i > 0) RefreshBacks(i);
            }
            roundText.text = $"라운드 {Game.Round} / 40  ·  무신론자 {Game.Atheists}  ·  선플레이어 {Game.Players[Game.FirstPlayerIndex].Name}";
            messageText.text = Game.Phase == GamePhase.Actions ? $"현재 행동: {Game.Players[Game.ActivePlayerIndex].Name} · 순차 즉시 실행 (임시)" : Game.LastMessage;
            targetLabel.text = "효과 대상: " + Game.Players[targetPlayer].Name;
            endButton.interactable = !IsAnimating && Game.Phase == GamePhase.Actions && Game.ActivePlayerIndex == 0;
            debugButton.interactable = !IsAnimating && Game.Phase == GamePhase.Actions && Game.ActivePlayerIndex != 0;
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
            rewardRoot.gameObject.SetActive(Game.Phase == GamePhase.RewardChoice);
            if (!rewardRoot.gameObject.activeSelf) return;
            rewardRoot.SetAsLastSibling();
            int player = Game.RewardPlayerIndex;
            Text(rewardRoot, "Reward Title", $"{Game.Round}라운드 보상 · {Game.Players[player].Name}\n{Game.CurrentRewardKind} 카드 1장 선택 (테스트 플레이어도 수동 선택)", new Vector2(0, 190), new Vector2(1070, 90), 25);
            var options = Game.RewardOptions();
            for (int i = 0; i < options.Count; i++)
            {
                CardDefinition def = options[i];
                int row = i / 4, col = i % 4;
                Button(rewardRoot, def.Name + (!def.IsPlayable ? "\n사용 불가 / 미구현" : "") + "\n" + def.CostText,
                    new Vector2(-405 + col * 270, 55 - row * 125), new Vector2(250, 110), () => { if (IsAnimating) return; Game.ChooseReward(player, def.Id); Log(Game.LastMessage); Refresh(); });
            }
            Text(rewardRoot, "Reward Note", "획득 위치는 Demo Settings의 임시 설정입니다. 미구현 카드도 획득할 수 있으나 사용할 수 없습니다.", new Vector2(0, -215), new Vector2(1070, 50), 19);
            if (options.Count == 0)
                Button(rewardRoot, "명세 미등록 보상권 보류 (테스트)", Vector2.zero, new Vector2(750, 70), () => { Game.DeferMissingReward(player); Refresh(); });
        }

        private void EndLocalTurn()
        {
            if (!CanInteract || Game.ActivePlayerIndex != 0) return;
            Game.EndActionTurn(0); Log(Game.LastMessage); SelectedCardId = null; Refresh();
        }

        public void DebugOpponentStep()
        {
            if (!CanInteract || Game.Phase != GamePhase.Actions || Game.ActivePlayerIndex == 0) return;
            int actor = Game.ActivePlayerIndex;
            foreach (CardInstance card in Game.Players[actor].Hand.ToArray())
            {
                if (!Game.TryPlay(actor, card.InstanceId, out string message, new EffectTarget { PlayerIndex = (actor + 1) % 4 })) continue;
                Log(message); break;
            }
            Game.EndActionTurn(actor); Log(Game.LastMessage); Refresh();
        }

        private void ResetGame()
        {
            if (!CanInteract) return;
            Game = new GameSession(settings != null ? settings.assumptions : null);
            SelectedCardId = null; logs.Clear(); logText.text = ""; Refresh();
        }
        private void CycleTarget() { if (!CanInteract) return; targetPlayer = targetPlayer % 3 + 1; targetLabel.text = "효과 대상: " + Game.Players[targetPlayer].Name; }
        private void ShowPlayerStats(int index)
        {
            if (!CanInteract) return;
            PlayerState player = Game.Players[index];
            OpenDetail($"<size=38>{player.Name}</size>\n\n신앙심: {player.Faith} / 100\n교리 정합성: {player.Doctrine} / 100\n헌금: {player.Offering}\n신도 수: {player.FollowerCount}\n덱: {player.Deck.Count}장 / 손패: {player.Hand.Count}장 / 버린 더미: {player.Discard.Count}장\n교회 재산: {player.ChurchProperty}\n지속 효과: {player.Statuses.Count}개\n\n상대 손패·덱의 카드 이름은 공개하지 않습니다.");
        }
        private void ShowCatalog()
        {
            if (!CanInteract) return;
            OpenDetail("<size=36>카드 데이터 26종</size>\n\n" + string.Join("\n\n", Game.Catalog.Select(c => $"<b>{c.Name}</b> · {c.CostText} · {c.Faction} · {c.Implementation}\n{c.Description}\n<i>{c.FlavorText}</i>")));
            grantDebugButton.gameObject.SetActive(true);
        }
        private void GrantAllForDebug()
        {
            if (IsAnimating) return;
            foreach (CardDefinition card in Game.Catalog) GameDebugTools.GiveCard(Game, 0, card.Id);
            GameDebugTools.SetResources(Game, 0, 50, 80, 80, 20);
            CloseDetail(); Log("디버그: 내 손패에 26종 각 1장 추가, 헌금 50·신앙/교리 80·교회 재산 20 설정. 정규 획득 규칙이 아닙니다."); Refresh();
        }
        private void Feedback(string text) { messageText.text = text; }
        private void Log(string text) { logs.Add(text); if (logs.Count > 2) logs.RemoveAt(0); logText.text = string.Join("\n", logs); }

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
            RectTransform rect = Panel(parent, label, position, size, new Color(0.15f, 0.31f, 0.37f));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
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
    }
}
