using System.Linq;
using UnityEngine;

namespace Pilgrimage.UI
{
    // Match lifecycle: result screen, restart (single / LAN rematch) and returning to the main menu.
    public sealed partial class DemoController
    {
        private bool resultShownForMatch;
        private bool lanInGame;

        // Every regular match gets its own deck / reward / effect seed.
        private DemoRuleAssumptions NewGameRules()
            => (settings != null && settings.assumptions != null ? settings.assumptions : new DemoRuleAssumptions()).WithSeed(DemoRuleAssumptions.FreshSeed());

        private void UpdateMenuSkin()
        {
            UpdateLanSkin();
            if (resultScreen == null || IsResultOpen || resultShownForMatch) return;
            bool finished = IsLanMode ? LanView != null && LanView.phase == GamePhase.Finished
                : Game != null && !IsCardLab && Game.Phase == GamePhase.Finished;
            // Wait for the last card / settlement presentation before covering the table.
            if (!finished || IsAnimating || IsAiRunning || (Visuals != null && Visuals.IsBusy)) return;
            ShowResultScreen();
        }

        /// <summary>
        /// Drops everything the previous match left behind in this controller: coroutines (AI turn, card
        /// release, LAN reveal queue, firewall), presentation queues, selection, overlays and the hand.
        /// Model objects are replaced by the caller.
        /// </summary>
        private void ResetGameplayPresentation()
        {
            StopAllCoroutines();
            lanPresentationRoutine = null; lanPresentations.Clear(); lanResolved.Clear();
            IsAiRunning = false; IsAnimating = false; firewallBusy = false;
            SelectedCardId = null; keyboardSelection = false;
            RevealedAiCard = null; RevealedCardInstanceId = null;
            if (revealedCard != null) revealedCard.gameObject.SetActive(false);
            logs.Clear();
            if (logText != null) logText.text = "";
            if (messageText != null) messageText.text = "";
            CloseCardPicker(); CloseDetail(); CloseLabPanel();
            if (rewardRoot != null) { ClearChildren(rewardRoot); rewardRoot.gameObject.SetActive(false); }
            ClearChildren(handRoot); handViews.Clear();
            for (int i = 0; i < opponentHands.Length; i++) if (opponentHands[i] != null) ClearChildren(opponentHands[i]);
            if (endButton != null) { endButton.interactable = false; SetEndLabel("행동 종료"); }
            kickConfirmUntil = 0;
            if (kickStalledButton != null) kickStalledButton.gameObject.SetActive(false);
            Visuals?.ResetSession();
            HideResultScreen(); resultShownForMatch = false; IsResultVictory = false;
            targetPlayer = 1;
        }

        /// <summary>Single player: brand-new GameSession with the same faction and fresh AI planners.</summary>
        public bool RestartSingleGame()
        {
            if (IsLanMode || Network != null || Game == null || IsCardLab) return false;
            FactionId faction = Game.HumanFaction;
            ResetGameplayPresentation();
            Game = new GameSession(NewGameRules(), humanFaction: faction);
            Visuals.ResetSession();
            factionSelection.gameObject.SetActive(false);
            InitializeAiPlayers();
            Refresh();
            return true;
        }

        /// <summary>Leaves the current match (session and network) and shows the main menu.</summary>
        public void ReturnToMainMenu()
        {
            if (IsLanMode || Network != null) { LeaveLan(); return; }
            if (IsCardLab)
            {
                IsCardLab = false; savedRegularGame = null;
                if (labBadge != null) labBadge.gameObject.SetActive(false);
            }
            ResetGameplayPresentation();
            Game = null; aiPlayers = null;
            ShowMainMenu("");
        }

        public void ShowMainMenu(string reason)
        {
            if (Game != null || Network != null) return;
            CloseSettings(); HideResultScreen();
            factionSelection.gameObject.SetActive(false); lanScreen.gameObject.SetActive(false);
            modeScreen.gameObject.SetActive(true); modeScreen.SetAsLastSibling();
            SetMenuFeedback(reason);
        }

        private void OnResultRetry()
        {
            if (IsLanMode)
            {
                if (Network == null || !Network.IsHost) return;
                if (!Network.Send(new LanCommand { kind = LanCommandKind.Rematch })) resultHint.text = "지금은 재경기를 요청할 수 없습니다. 잠시 후 다시 시도하세요.";
                return;
            }
            RestartSingleGame();
        }

        private void OnResultMenu() => ReturnToMainMenu();

        // LAN: the host sent everyone back to the lobby. Clear the finished match before drawing the lobby.
        private void ResetLanMatchPresentation()
        {
            lanInGame = false;
            ResetGameplayPresentation();
            Game = null; LanView = null; displayedReveal = 0;
            if (leaveLanButton != null) leaveLanButton.gameObject.SetActive(false);
        }
    }
}
