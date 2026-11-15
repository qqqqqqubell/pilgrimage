using UnityEngine;

namespace Pilgrimage.UI
{
    public sealed partial class DemoController
    {
        private RectTransform factionSelection;
        public bool IsFactionSelectionOpen => factionSelection != null && factionSelection.gameObject.activeSelf;

        private void BuildFactionSelection()
        {
            factionSelection = Panel(root, "Faction Selection", Vector2.zero, Vector2.zero, new Color(0.025f, 0.055f, 0.08f, 1));
            Stretch(factionSelection);
            Text(factionSelection, "Faction Title", "PILGRIMAGE", new Vector2(0, 400), new Vector2(1500, 80), 52);
            Text(factionSelection, "Faction Prompt", "이끌어 갈 교단을 선택하세요", new Vector2(0, 310), new Vector2(1500, 65), 32);
            for (int i = 0; i < GameSession.PlayerCount; i++)
            {
                FactionId faction = (FactionId)(i + 1);
                Vector2 position = new Vector2(i % 2 == 0 ? -400 : 400, i < 2 ? 100 : -205);
                RectTransform panel = Panel(factionSelection, "Faction " + faction, position, new Vector2(740, 260), new Color(0.09f + i * 0.015f, 0.18f, 0.22f));
                Text(panel, "Faction Name", FactionSetup.Name(faction), new Vector2(0, 75), new Vector2(690, 60), 31);
                Text(panel, "Faction Description", FactionSetup.Description(faction), new Vector2(0, 0), new Vector2(680, 85), 24);
                Button(panel, "선택 · " + FactionSetup.Name(faction), new Vector2(0, -85), new Vector2(620, 48), () => ChooseFaction(faction));
            }
            Text(factionSelection, "Selection Note", "선택한 교단은 내가, 나머지 세 교단은 AI가 담당합니다.", new Vector2(0, -430), new Vector2(1600, 55), 23);
        }

        public bool ChooseFaction(FactionId faction)
        {
            if (IsLanMode || IsModeSelectionOpen || !IsFactionSelectionOpen || Game != null || !FactionSetup.IsPlayable(faction)) return false;
            Game = new GameSession(settings != null ? settings.assumptions : null, humanFaction: faction);
            Visuals.ResetSession();
            factionSelection.gameObject.SetActive(false);
            targetPlayer = 1;
            InitializeAiPlayers();
            Refresh();
            return true;
        }

        private void ShowFactionSelection()
        {
            Game = null;
            Visuals.ResetSession();
            factionSelection.gameObject.SetActive(true);
            factionSelection.SetAsLastSibling();
        }
    }
}
