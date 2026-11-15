using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Pilgrimage.UI
{
    public sealed partial class DemoController
    {
        [Header("Authored card prefabs — UI panels live in the scene")]
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private RectTransform cardBackPrefab;
        [SerializeField] private Button cardChoicePrefab;
        private bool buildingSceneUI;
        public bool IsAuthoringUI => buildingSceneUI;
        public void ApplyFont(TMP_Text text) { if (font != null && text.font == TMP_Settings.defaultFontAsset) text.font = font; }

        private static Button FindButton(Transform parent, string name)
            => parent.GetComponentsInChildren<Button>(true).First(b => b.name == name);
        private static void Wire(Button button, UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
        private void BindSceneUI()
        {
            // Bind behavior only. RectTransforms, sprites, colors and typography belong to the scene.
            messageText.text = logText.text = "";
            modeScreen.Find("Menu Feedback").GetComponent<TMP_Text>().text = "";
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (font != null && text.font == TMP_Settings.defaultFontAsset) text.font = font;
            Wire(FindButton(root, "카드 도감"), ShowCatalog);
            Wire(FindButton(root, "메뉴"), ShowMenu);
            Wire(endButton, EndLocalTurn);
            Wire(FindButton(root.Find("Shared Table"), "대상"), CycleTarget);
            for (int i = 0; i < 4; i++)
            {
                int seat = i;
                Wire(FindButton(root.Find("Player " + i + " Table"), i == 0 ? "내 추가 상태" : "상태"), () => ShowPlayerStats(seat));
                FactionId faction = (FactionId)(i + 1);
                Wire(factionSelection.GetComponentsInChildren<Button>(true)[i], () => ChooseFaction(faction));
            }
            Wire(FindButton(modal, "닫기  ·  ESC / 우클릭"), CloseDetail);
            Wire(grantDebugButton, GrantAllForDebug);
            Wire(debugButton, () => { CloseDetail(); ResetGame(); });
            Wire(manualAiButton, () => { CloseDetail(); DebugOpponentStep(); });
            Wire(FindButton(modeScreen, "싱글플레이"), SelectSinglePlayer);
            Wire(FindButton(modeScreen, "LAN 멀티플레이"), SelectLanMode);
            Wire(FindButton(lanScreen, "방 만들기"), () => StartLan(true));
            Wire(FindButton(lanScreen, "참가하기"), () => StartLan(false));
            Wire(FindButton(lanScreen, "방 나가기 / 메뉴"), LeaveLan);
            Wire(leaveLanButton, LeaveLan); Wire(kickStalledButton, KickStalled);
            Wire(FindButton(cardPicker, "선택 확정"), ConfirmCardTargets);
            Wire(FindButton(cardPicker, "취소 · ESC"), CloseCardPicker);
            Wire(labEntryButton, () => { if (!CanInteract) return; EnterCardLab(); OpenLabPanel(); });
            Wire(FindButton(labPanel, "요청한 행동 카드 5장"), () => SetLabHand(GameDebugTools.FiveActionPreset));
            Wire(FindButton(labPanel, "신도 5장"), () => SetLabHand(Enumerable.Repeat(CardId.Follower, 5)));
            Wire(FindButton(labPanel, "선택한 5장으로 구성"), ApplyQueuedLabHand);
            Wire(FindButton(labPanel, "필드로 닫기"), CloseLabPanel);
            Wire(FindButton(labPanel, "정규 게임 복귀"), ExitCardLab);
            Wire(FindButton(labPanel, "편집할 교단 변경"), () => { labPlayer = (labPlayer + 1) % 4; FillLabResources(); });
            Wire(FindButton(labPanel, "자원 적용"), ApplyLabResources);
            Wire(FindButton(labPanel, "이 교단 덱에 신도 추가"), () => { if (!IsCardLab) return; GameDebugTools.GiveCard(Game, labPlayer, CardId.Follower, CardPile.Deck); Refresh(); });
            Wire(FindButton(labPanel, "더미 / GUID 확인"), InspectLabPiles);
            Wire(FindButton(labPanel, "다음 내 차례로"), AdvanceLabTurn);
            Wire(FindButton(labPanel, "내 손패 비우기"), () => SetLabHand(Array.Empty<CardId>()));
            labSearch.onValueChanged.AddListener(_ => RefreshLabLibrary());
            labEntryButton.gameObject.SetActive(DevelopmentToolsAvailable);
            labPanel.gameObject.SetActive(false); labBadge.gameObject.SetActive(false);
            modal.gameObject.SetActive(false); cardPicker.gameObject.SetActive(false);
            rewardRoot.gameObject.SetActive(false); revealedCard.gameObject.SetActive(false);
            factionSelection.gameObject.SetActive(false); lanScreen.gameObject.SetActive(false);
            leaveLanButton.gameObject.SetActive(false); kickStalledButton.gameObject.SetActive(false);
            modeScreen.gameObject.SetActive(true); modeScreen.SetAsLastSibling();
        }

        private Button CardChoice(Transform parent, string label, Vector2 position, Vector2 size, UnityAction action)
        {
            var button = Instantiate(cardChoicePrefab, parent, false);
            button.name = label;
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = position;
            var text = button.GetComponentInChildren<TMP_Text>(true);
            text.text = label; if (font != null && text.font == TMP_Settings.defaultFontAsset) text.font = font;
            Wire(button, action);
            return button;
        }

#if UNITY_EDITOR
        // Explicit one-time migration; never invoked automatically on import or Play.
        public void AuthorSceneUI(CardView front, RectTransform back, Button choice)
        {
            buildingSceneUI = true;
            cardPrefab = front; cardBackPrefab = back; cardChoicePrefab = choice;
            font = TMP_Settings.defaultFontAsset;
            aiSettings = new AiSettings();
            BuildUI(); BuildFactionSelection(); BuildModeSelection();
            Text(rewardRoot, "Reward Title", "카드 획득 결과", new Vector2(0, 210), new Vector2(1070, 55), 25);
            for (int i = 0; i < 4; i++) Text(rewardRoot, "Award " + i, "교단 · 획득 카드", new Vector2(0, 125 - i * 75), new Vector2(1050, 60), 24);
            Button(rewardRoot, "획득 확인 · 계속", new Vector2(0, -190), new Vector2(500, 55), () => {});
            Button(rewardRoot, "다른 참가자 확인 대기", new Vector2(0, -190), new Vector2(500, 55), () => {});
            Button(lobbyContent, "방화벽 다시 설정", new Vector2(640, -215), new Vector2(360, 50), () => {});
            for (int i = 0; i < 4; i++)
            {
                Text(lobbyContent, "Lobby Seat " + i, "플레이어 / AI", new Vector2(-600 + i * 400, 100), new Vector2(385, 120), 23);
                Button(lobbyContent, "내보내기 " + i, new Vector2(-600 + i * 400, 195), new Vector2(180, 40), () => {});
                Button(lobbyContent, FactionSetup.Name((FactionId)(i + 1)), new Vector2(-600 + i * 400, -45), new Vector2(380, 60), () => {});
            }
            Text(lobbyContent, "Assignment Seed", "최종 교단 배정", new Vector2(0, -50), new Vector2(1500, 85), 25);
            Button(lobbyContent, "배정 확인", new Vector2(0, -180), new Vector2(400, 60), () => {});
            Button(lobbyContent, "준비", new Vector2(-240, -160), new Vector2(360, 60), () => {});
            Button(lobbyContent, "준비 취소", new Vector2(-240, -160), new Vector2(360, 60), () => {});
            Button(lobbyContent, "게임 시작 · 배정", new Vector2(240, -160), new Vector2(400, 60), () => {});
            for (int i = 0; i < CardCatalog.All.Count(); i++)
            {
                float y = -45 - i * 90;
                var text = Text(libraryContent, "Card Lab Entry", CardCatalog.All.ElementAt(i).Name, new Vector2(-190, y), new Vector2(620, 78), 19);
                AnchorTop(text.rectTransform);
                AnchorTop((RectTransform)Button(libraryContent, "손패 +1", new Vector2(210, y), new Vector2(160, 60), () => {}).transform);
                AnchorTop((RectTransform)Button(libraryContent, "5장 선택", new Vector2(410, y), new Vector2(170, 60), () => {}).transform);
            }
            libraryContent.sizeDelta = new Vector2(1040, CardCatalog.All.Count() * 90);
            foreach (var list in new[] { rewardRoot, lobbyContent, libraryContent })
                foreach (Transform child in list) child.gameObject.AddComponent<SceneUIRefreshSlot>();
            BuildGameplayVisuals();
            buildingSceneUI = false;
            // Human-readable placeholders for initially data-driven labels.
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (string.IsNullOrWhiteSpace(text.text) && text.GetComponentInParent<TMP_InputField>() == null) text.text = text.name;
        }
#endif
    }
}
