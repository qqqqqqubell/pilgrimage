using System.Collections.Generic;
using UnityEngine;

namespace Pilgrimage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Pilgrimage/Card Prototype Controller")]
    public sealed class CardPrototypeController : MonoBehaviour
    {
        [Tooltip("선택 사항. 비워 두면 코드에 정의된 임시 테스트 기본값으로 실행합니다.")]
        [SerializeField] private PrototypeTestSettings testSettings = null;
        [Tooltip("한글이 보이지 않으면 한글을 지원하는 Font 에셋을 지정하세요.")]
        [SerializeField] private Font koreanFont = null;
        private PrototypeTestSettings fallbackSettings;
        private Font generatedFont;
        private PrototypeGame game;
        private readonly List<string> history = new List<string>();
        private Vector2 scroll;
        private GUIStyle titleStyle, headingStyle, bodyStyle, buttonStyle, toggleStyle;
        private bool showPiles;

        private void Start()
        {
            if (koreanFont == null)
            {
                string[] installed = Font.GetOSInstalledFontNames();
                string[] candidates = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo",
                    "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic" };
                foreach (string candidate in candidates)
                {
                    if (System.Array.IndexOf(installed, candidate) < 0) continue;
                    generatedFont = Font.CreateDynamicFontFromOSFont(candidate, 18);
                    break;
                }
            }
            ResetSession();
        }

        private void ResetSession()
        {
            if (testSettings == null && fallbackSettings == null)
            {
                fallbackSettings = ScriptableObject.CreateInstance<PrototypeTestSettings>();
                fallbackSettings.hideFlags = HideFlags.HideAndDontSave;
            }
            PrototypeTestSettings source = testSettings != null ? testSettings : fallbackSettings;
            game = new PrototypeGame(source.values ?? new PrototypeTestConfig());
            history.Clear();
            Log("테스트 시작 — 초기 손패를 그대로 사용합니다. 첫 턴에는 추가 드로우·헌금 지급이 없습니다.");
            scroll = Vector2.zero;
        }

        private void OnGUI()
        {
            if (game == null) return;
            EnsureStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousBackground = GUI.backgroundColor;
            bool previousEnabled = GUI.enabled;
            float scale = Mathf.Clamp(Screen.width / 1280f, 0.75f, 1.5f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            CardInstance pendingCard = null;
            bool endTurn = false, reset = false;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                GUI.Box(new Rect(0, 0, width, height), GUIContent.none);
                GUILayout.BeginArea(new Rect(16, 12, width - 32, height - 24));
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.Label("PILGRIMAGE  /  카드 효과 프로토타입", titleStyle);
                GUILayout.Label("초기 수치·무신론자 공용 처리·턴 진행은 임시 테스트 가정입니다. 승패 규칙은 구현하지 않았습니다.", bodyStyle);
                GUILayout.Label(game.TemporaryRulesSummary, bodyStyle);
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"턴 {game.TurnNumber}  |  현재: {game.ActivePlayer.Name}  |  공용 무신론자: {game.Atheists}", headingStyle);
                endTurn = GUILayout.Button("턴 종료 (임시)", buttonStyle, GUILayout.Width(150), GUILayout.Height(38));
                reset = GUILayout.Button("테스트 초기화", buttonStyle, GUILayout.Width(145), GUILayout.Height(38));
                GUILayout.EndHorizontal();

                foreach (PlayerState player in game.Players)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    string marker = player == game.ActivePlayer ? "▶ " : "";
                    GUILayout.Label($"{marker}{player.Name}   헌금 {player.Offering}   신앙심 {player.Faith}   교리 정합성 {player.Doctrine}", headingStyle);
                    GUILayout.Label($"덱 {player.Deck.Count}장  /  손패 {player.Hand.Count}장  /  버린 더미 {player.Discard.Count}장", bodyStyle);
                    GUILayout.EndVertical();
                }

                GUILayout.Space(8);
                GUILayout.Label($"현재 손패 — 이번 턴 사용 {game.CardsPlayedThisTurn}장", headingStyle);
                if (game.ActivePlayer.Hand.Count == 0) GUILayout.Label("손패가 비었습니다. 턴 종료로 다음 플레이어에게 넘길 수 있습니다.", bodyStyle);
                foreach (CardInstance card in game.ActivePlayer.Hand)
                {
                    bool playable = game.CanPlay(game.ActivePlayerIndex, card, out string reason);
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label(CardPresentation.PlayerText(card.Definition.Name) + (CardPresentation.CostLabel(card.Definition) == "" ? "" : "  |  " + CardPresentation.CostLabel(card.Definition)), headingStyle);
                    GUILayout.Label(CardPresentation.PlayerText(card.Definition.Description), bodyStyle);
                    GUILayout.BeginHorizontal();
                    GUI.enabled = playable;
                    if (GUILayout.Button("카드 사용", buttonStyle, GUILayout.Width(140), GUILayout.Height(34))) pendingCard = card;
                    GUI.enabled = previousEnabled;
                    GUILayout.Label(reason, bodyStyle);
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }

                showPiles = GUILayout.Toggle(showPiles, "더미 내용 공개 (테스트용: 모든 플레이어의 덱 순서와 손패 표시)", toggleStyle);
                if (showPiles)
                {
                    foreach (PlayerState player in game.Players)
                    {
                        GUILayout.Label(player.Name, headingStyle);
                        GUILayout.Label("덱 (왼쪽이 다음 드로우): " + PileNames(player.Deck), bodyStyle);
                        GUILayout.Label("손패: " + PileNames(player.Hand), bodyStyle);
                        GUILayout.Label("버린 더미: " + PileNames(player.Discard), bodyStyle);
                    }
                }

                GUILayout.Space(8);
                GUILayout.Label("최근 기록 (최신순)", headingStyle);
                for (int i = history.Count - 1; i >= 0; i--) GUILayout.Label(history[i], bodyStyle);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.backgroundColor = previousBackground;
                GUI.enabled = previousEnabled;
            }
            // Process commands after drawing so pile changes cannot invalidate layout iteration.
            if (reset) ResetSession();
            else if (endTurn) Log(game.EndTurn());
            else if (pendingCard != null)
            {
                game.TryPlay(game.ActivePlayerIndex, pendingCard, out string message);
                Log(message);
            }
        }

        private void EnsureStyles()
        {
            if (bodyStyle != null) return;
            Font font = koreanFont != null ? koreanFont : generatedFont;
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            if (font != null) bodyStyle.font = font;
            headingStyle = new GUIStyle(bodyStyle) { fontSize = 20, fontStyle = FontStyle.Bold };
            titleStyle = new GUIStyle(headingStyle) { fontSize = 27 };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 17, wordWrap = true };
            toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 17, wordWrap = true };
            if (font != null)
            {
                buttonStyle.font = font;
                toggleStyle.font = font;
            }
        }

        private static string PileNames(IReadOnlyList<CardInstance> pile)
        {
            if (pile.Count == 0) return "(없음)";
            var names = new string[pile.Count];
            for (int i = 0; i < pile.Count; i++) names[i] = pile[i].Definition.Name;
            return string.Join(" → ", names);
        }

        private void Log(string message)
        {
            history.Add(message);
            if (history.Count > 12) history.RemoveAt(0);
        }

        private void OnDestroy()
        {
            if (fallbackSettings != null) Destroy(fallbackSettings);
            if (generatedFont != null) Destroy(generatedFont);
        }
    }
}
