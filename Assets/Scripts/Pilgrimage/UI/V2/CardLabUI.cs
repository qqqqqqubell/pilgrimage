using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pilgrimage.UI
{
    public sealed partial class DemoController
    {
        public bool IsCardLab { get; private set; }
        public bool DevelopmentToolsAvailable => !IsLanMode && (Application.isEditor || Debug.isDebugBuild);
        public bool IsLabPanelOpen => labPanel != null && labPanel.gameObject.activeSelf;
        public bool IsCardPickerOpen => cardPicker != null && cardPicker.gameObject.activeSelf;
        private GameSession savedRegularGame;
        private string[] savedRegularLogs;
        [SerializeField] private RectTransform labPanel, libraryContent, cardPicker, pickerContent;
        [SerializeField] private TMP_InputField labSearch, offeringInput, faithInput, doctrineInput;
        [SerializeField] private TMP_Text labStatus, labPlayerLabel, labBadge;
        private readonly List<CardId> labFive = new List<CardId>();
        private int labPlayer = 1;
        private Guid? pendingRemovalSource;
        private readonly HashSet<Guid> pendingCardTargets = new HashSet<Guid>();
        [SerializeField] private TMP_Text pickerTitle;

#if UNITY_EDITOR
        private void BuildCardLabUI()
        {
            if (DevelopmentToolsAvailable)
            {
                labEntryButton = Button(root, "카드 실험", new Vector2(440, 501), new Vector2(170, 40), () => { if (!CanInteract) return; EnterCardLab(); OpenLabPanel(); });
                labBadge = Text(root, "Development Mode Badge", "카드 실험 모드 · 정규 게임 일시 보관", new Vector2(-680, 448), new Vector2(480, 35), 19);
                labBadge.gameObject.SetActive(false);
                labPanel = Panel(root, "Development Card Laboratory", Vector2.zero, Vector2.zero, new Color(0.02f, 0.04f, 0.07f, 0.98f)); Stretch(labPanel);
                RectTransform body = Panel(labPanel, "Laboratory", Vector2.zero, new Vector2(1720, 940), new Color(0.06f, 0.12f, 0.15f));
                Text(body, "Lab Title", "개발용 카드 실험 · 정규 게임과 별도 세션", new Vector2(0, 425), new Vector2(1600, 50), 30);
                Button(body, "요청한 행동 카드 5장", new Vector2(-600, 350), new Vector2(340, 50), () => SetLabHand(GameDebugTools.FiveActionPreset));
                Button(body, "신도 5장", new Vector2(-280, 350), new Vector2(230, 50), () => SetLabHand(Enumerable.Repeat(CardId.Follower, 5)));
                Button(body, "선택한 5장으로 구성", new Vector2(20, 350), new Vector2(330, 50), ApplyQueuedLabHand);
                Button(body, "필드로 닫기", new Vector2(380, 350), new Vector2(260, 50), CloseLabPanel);
                Button(body, "정규 게임 복귀", new Vector2(690, 350), new Vector2(260, 50), ExitCardLab);
                labSearch = Input(body, "카드 이름 검색", new Vector2(-250, 280), new Vector2(1040, 44));
                CreateScroll(body, "Search Results", new Vector2(-250, -20), new Vector2(1040, 520), out libraryContent);
                labStatus = Text(body, "Lab State", "", new Vector2(-250, -365), new Vector2(1080, 130), 18);
                labPlayerLabel = Text(body, "Resource Owner", "", new Vector2(590, 260), new Vector2(430, 50), 24);
                Button(body, "편집할 교단 변경", new Vector2(590, 200), new Vector2(380, 44), () => { labPlayer = (labPlayer + 1) % 4; FillLabResources(); });
                Text(body, "Offering Label", "헌금", new Vector2(455, 130), new Vector2(100, 44), 22);
                offeringInput = Input(body, "헌금", new Vector2(650, 130), new Vector2(230, 44));
                Text(body, "Faith Label", "신앙심", new Vector2(455, 70), new Vector2(100, 44), 22);
                faithInput = Input(body, "신앙심", new Vector2(650, 70), new Vector2(230, 44));
                Text(body, "Doctrine Label", "교리", new Vector2(455, 10), new Vector2(100, 44), 22);
                doctrineInput = Input(body, "교리", new Vector2(650, 10), new Vector2(230, 44));
                Button(body, "자원 적용", new Vector2(590, -65), new Vector2(380, 45), ApplyLabResources);
                Button(body, "이 교단 덱에 신도 추가", new Vector2(590, -130), new Vector2(380, 45), () => { if (!IsCardLab) return; GameDebugTools.GiveCard(Game, labPlayer, CardId.Follower, CardPile.Deck); Refresh(); });
                Button(body, "더미 / GUID 확인", new Vector2(590, -195), new Vector2(380, 45), InspectLabPiles);
                Button(body, "다음 내 차례로", new Vector2(590, -260), new Vector2(380, 45), AdvanceLabTurn);
                Button(body, "내 손패 비우기", new Vector2(590, -325), new Vector2(380, 45), () => SetLabHand(new CardId[0]));
                Text(body, "Lab Note", "자원·카드 지급은 실험에만 적용됩니다.\n대상은 필드의 ‘대상’ 버튼으로 선택합니다.", new Vector2(590, -410), new Vector2(440, 70), 18);
                labSearch.onValueChanged.AddListener(_ => RefreshLabLibrary());
                labPanel.gameObject.SetActive(false);
            }
            // This choice UI is also available in normal/release games.
            cardPicker = Panel(root, "Edict Removal Selection", Vector2.zero, Vector2.zero, new Color(0.015f, 0.035f, 0.055f, 0.98f)); Stretch(cardPicker);
            pickerTitle = Text(cardPicker, "Picker Title", "", new Vector2(0, 405), new Vector2(1500, 65), 30);
            CreateScroll(cardPicker, "Removal Candidates", Vector2.zero, new Vector2(1400, 660), out pickerContent);
            Button(cardPicker, "선택 확정", new Vector2(-220, -405), new Vector2(340, 50), ConfirmCardTargets);
            Button(cardPicker, "취소 · ESC", new Vector2(220, -405), new Vector2(340, 50), CloseCardPicker);
            cardPicker.gameObject.SetActive(false);
        }
#endif

        public void EnterCardLab()
        {
            if (Game == null || IsFactionSelectionOpen || !DevelopmentToolsAvailable || IsAnimating || IsAiRunning || IsCardPickerOpen) return;
            CloseDetail();
            if (IsCardLab) return;
            savedRegularGame = Game; savedRegularLogs = logs.ToArray();
            Game = GameDebugTools.CreateCardLab(savedRegularGame.HumanFaction); IsCardLab = true;
            Visuals.ResetSession(Game.LastVisualEvent?.serial??0);
            SelectedCardId = null; logs.Clear(); messageText.text = "개발용 실험 세션";
            revealedCard.gameObject.SetActive(false); RevealedCardInstanceId = null; RevealedAiCard = null;
            labBadge.gameObject.SetActive(true); Refresh();
        }

        public void ExitCardLab()
        {
            if (!IsCardLab || IsAnimating || IsAiRunning) return;
            CloseCardPicker(); CloseLabPanel(); CloseDetail();
            Game = savedRegularGame; savedRegularGame = null; IsCardLab = false;
            Visuals.ResetSession(Game.LastVisualEvent?.serial??0);
            logs.Clear(); logs.AddRange(savedRegularLogs ?? new string[0]);
            SelectedCardId = null; revealedCard.gameObject.SetActive(false); RevealedCardInstanceId = null; RevealedAiCard = null;
            messageText.text = "정규 게임 복귀"; labBadge.gameObject.SetActive(false); Refresh();
        }
        private void OpenLabPanel()
        {
            if (!IsCardLab || !DevelopmentToolsAvailable || IsAnimating || IsAiRunning) return;
            CloseDetail(); labPanel.gameObject.SetActive(true); labPanel.SetAsLastSibling();
            FillLabResources(); RefreshLabLibrary();
        }
        public void CloseLabPanel() { if (labPanel != null) labPanel.gameObject.SetActive(false); }

        public void SetLabHand(IEnumerable<CardId> cards)
        {
            if (!IsCardLab || IsAnimating || IsAiRunning) return;
            GameDebugTools.ReplaceHand(Game, 0, cards); SelectedCardId = null; Refresh();
        }
        private void ApplyQueuedLabHand()
        {
            if (labFive.Count != 5) { labStatus.text = "정확히 5장을 선택하세요. 현재 " + labFive.Count + "장"; return; }
            SetLabHand(labFive.ToArray()); labFive.Clear(); RefreshLabLibrary();
        }
        private void RefreshLabLibrary()
        {
            if (!IsCardLab || libraryContent == null) return;
            ClearChildren(libraryContent);
            string query = labSearch.text.Trim();
            var cards = Game.Catalog.Where(c => c.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(c => (int)c.Id).ToArray();
            libraryContent.sizeDelta = new Vector2(libraryContent.sizeDelta.x, Mathf.Max(520, cards.Length * 90));
            for (int i = 0; i < cards.Length; i++)
            {
                CardDefinition card = cards[i]; float y = -45 - i * 90;
                string status = card.CostKind == CardCostKind.None ? "사용 불가 카드" : card.Implementation != EffectImplementation.Implemented ? "효과 미구현" : "효과 구현됨";
                var held = Game.Players[0].Hand.FirstOrDefault(c => c.Definition.Id == card.Id);
                if (held != null)
                {
                    var selection = new EffectTarget { PlayerIndex = targetPlayer };
                    if (card.Id == CardId.Edict)
                    {
                        var candidate = Game.Players[0].Hand.Concat(Game.Players[0].Deck).FirstOrDefault(c => c.InstanceId != held.InstanceId && !c.Definition.ProtectedFromCardEffects);
                        selection = new EffectTarget { PlayerIndex = 0, CardInstanceId = candidate?.InstanceId ?? Guid.Empty };
                    }
                    bool valid = Game.CanPlay(0, held.InstanceId, out string reason, selection);
                    status += valid ? card.Id == CardId.Edict ? " · 사용 가능 (제거 대상 선택)" : " · 현재 사용 가능" : " · " + reason;
                }
                else status += " · 손패에 없음";
                string cost = card.CostKind == CardCostKind.None ? "코스트 없음" : CardPresentation.CostLabel(card);
                if (card.CostKind == CardCostKind.Variable) cost += " (현재 대상 " + Game.EffectiveCost(card, targetPlayer) + ")";
                var text = Text(libraryContent, "Card Lab Entry", card.Name + " · " + cost + "\n" + status, new Vector2(-190, y), new Vector2(620, 78), 19);
                Button add = Button(libraryContent, "손패 +1", new Vector2(210, y), new Vector2(160, 60), () => { GameDebugTools.GiveCard(Game, 0, card.Id); Refresh(); });
                Button queue = Button(libraryContent, "5장 선택", new Vector2(410, y), new Vector2(170, 60), () => { if (labFive.Count == 5) labFive.Clear(); labFive.Add(card.Id); RefreshLabLibrary(); });
            }
            labStatus.text = "편집할 5장: " + (labFive.Count == 0 ? "(없음)" : string.Join(" / ", labFive.Select(id => CardCatalog.Get(id).Name))) +
                $"\n내 손패 {Game.Players[0].Hand.Count} · 덱 {Game.Players[0].Deck.Count} · 버린 더미 {Game.Players[0].Discard.Count} · 제거 {Game.Players[0].Removed.Count}\n필드로 닫은 뒤 기존 마우스/키보드 조작으로 사용하세요. AI 자동 진행은 실험 세션에서 정지합니다.";
        }
        private void FillLabResources()
        {
            var player = Game.Players[labPlayer]; labPlayerLabel.text = player.Name;
            offeringInput.text = player.Offering.ToString(); faithInput.text = player.Faith.ToString(); doctrineInput.text = player.Doctrine.ToString();
        }
        private void ApplyLabResources()
        {
            if (!IsCardLab) return;
            if (!int.TryParse(offeringInput.text, out int offering) || !int.TryParse(faithInput.text, out int faith) || !int.TryParse(doctrineInput.text, out int doctrine))
            { labStatus.text = "정수로 입력하세요. 자원은 변경하지 않았습니다."; return; }
            GameDebugTools.SetResources(Game, labPlayer, offering, faith, doctrine, Game.Players[labPlayer].ChurchProperty);
            FillLabResources(); Refresh();
        }
        private void InspectLabPiles()
        {
            if (!IsCardLab) return;
            CloseLabPanel();
            OpenDetail("개발용 더미 확인\n\n" + string.Join("\n\n", Game.Players.Select(p => p.Name + $" · 헌금 {p.Offering} · 신앙 {p.Faith} · 교리 {p.Doctrine}\n" +
                "덱 (위→아래): " + PileSummary(p.Deck) + "\n손패: " + PileSummary(p.Hand) + "\n버린 더미: " + PileSummary(p.Discard) + "\n제거: " + PileSummary(p.Removed))));
        }
        private static string PileSummary(IReadOnlyList<CardInstance> pile) => string.Join(" / ", pile.Select(c => c.Definition.Name + " [" + c.InstanceId.ToString("N").Substring(0, 8) + "]"));
        private void AdvanceLabTurn()
        {
            if (!IsCardLab) return;
            if (Game.Phase == GamePhase.Actions && Game.ActivePlayerIndex == 0) Game.EndActionTurn(0);
            for (int i = 0; i < 16 && Game.Phase != GamePhase.Finished; i++)
            {
                if (Game.Phase == GamePhase.RewardChoice) Game.AcknowledgeRewards();
                else if (Game.ActivePlayerIndex == 0) break;
                else Game.EndActionTurn(Game.ActivePlayerIndex);
            }
            Refresh();
        }

        private void OpenRemovalPicker(Guid source)
        {
            var sourceCard = Game.FindCard(source).Card;
            if (Game.Players[0].Offering < Game.EffectiveCost(0, sourceCard)) { Feedback("헌금이 부족합니다."); return; }
            pendingRemovalSource = source; pendingCardTargets.Clear(); ClearChildren(pickerContent);
            pickerTitle.text = sourceCard.Definition.Name + (sourceCard.Definition.Id == CardId.Edict ? " · 손패·덱·버린 더미에서 최대 2장 선택" : " · 신도 1장 선택");
            var candidates = Game.SelectableCards(0, sourceCard.Definition.Id, source).ToArray();
            pickerContent.sizeDelta = new Vector2(1400, Mathf.Max(660, candidates.Length * 70));
            for (int i = 0; i < candidates.Length; i++)
            {
                CardInstance candidate = candidates[i]; var located = Game.FindCard(candidate.InstanceId);
                Button choice = null;
                choice = CardChoice(pickerContent, candidate.Definition.Name + " · " + located.Pile + " · " + candidate.InstanceId.ToString("N").Substring(0, 8),
                    new Vector2(0, -35 - i * 70), new Vector2(1300, 60), () => {
                        ToggleCardTarget(candidate.InstanceId);
                        choice.GetComponent<Image>().color = pendingCardTargets.Contains(candidate.InstanceId) ? new Color(0.75f, 0.85f, 0.75f) : Color.white;
                    });
                AnchorTop((RectTransform)choice.transform);
            }
            cardPicker.gameObject.SetActive(true); cardPicker.SetAsLastSibling();
        }
        public void ChooseRemovalTarget(Guid target)
        {
            pendingCardTargets.Clear(); pendingCardTargets.Add(target); ConfirmCardTargets();
        }
        public void ToggleCardTarget(Guid target)
        {
            if (!IsCardPickerOpen || !pendingRemovalSource.HasValue || IsAnimating || IsAiRunning) return;
            if (pendingCardTargets.Remove(target)) return;
            CardId sourceId = IsLanMode ? LanView.hand.First(c => c.guid == pendingRemovalSource.Value.ToString("D")).id : Game.FindCard(pendingRemovalSource.Value).Card.Definition.Id;
            int limit = sourceId == CardId.Edict ? 2 : 1;
            if (pendingCardTargets.Count < limit) pendingCardTargets.Add(target);
        }
        public void ConfirmCardTargets()
        {
            if (!IsCardPickerOpen || !pendingRemovalSource.HasValue || IsAnimating || IsAiRunning) return;
            if (IsLanMode) { ConfirmLanTargets(); return; }
            Guid source = pendingRemovalSource.Value;
            var selection = new EffectTarget { PlayerIndex = 0, CardInstanceIds = pendingCardTargets.ToArray() };
            if (!Game.CanPlay(0, source, out string reason, selection)) { Feedback(reason); return; }
            CloseCardPicker(); ExecutePlayerCard(source, selection);
        }
        public void CloseCardPicker() { if (cardPicker != null) cardPicker.gameObject.SetActive(false); pendingRemovalSource = null; }

        private TMP_InputField Input(Transform parent, string hint, Vector2 position, Vector2 size)
        {
            var box = Panel(parent, hint, position, size, new Color(0.12f, 0.2f, 0.24f));
            var label = Text(box, "Input Text", "", Vector2.zero, size - new Vector2(20, 6), 21, TextAlignmentOptions.Left);
            var placeholder = Text(box, "Placeholder", hint, Vector2.zero, size - new Vector2(20, 6), 19, TextAlignmentOptions.Left);
            placeholder.color = new Color(0.6f, 0.65f, 0.7f);
            var input = box.gameObject.AddComponent<TMP_InputField>(); input.textViewport = box; input.textComponent = label; input.placeholder = placeholder;
            input.navigation = new Navigation { mode = Navigation.Mode.None }; return input;
        }
#if UNITY_EDITOR
        private void CreateScroll(Transform parent, string name, Vector2 position, Vector2 size, out RectTransform content)
        {
            var viewport = Panel(parent, name, position, size, Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            content = Rect(viewport, "Content", Vector2.zero, size); AnchorTop(content); content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport; scroll.content = content;
        }
#endif
        private static void AnchorTop(RectTransform rect) { rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1); }
        private static void ClearChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                bool card = child.GetComponent<CardView>() != null || child.GetComponent<SceneCardChoice>() != null || child.name == "Private Card Back";
                if (card || child.GetComponent<SceneUIRefreshSlot>() != null) child.gameObject.SetActive(false);
                if (card) UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }
}
