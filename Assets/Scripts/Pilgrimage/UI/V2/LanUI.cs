using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pilgrimage.UI
{
    public sealed partial class DemoController
    {
        public bool IsLanMode { get; private set; }
        public bool IsModeSelectionOpen => modeScreen != null && modeScreen.gameObject.activeSelf;
        public int LocalSeat => IsLanMode ? LanView?.localSeat ?? -1 : 0;
        public int LocalHandCount => IsLanMode ? LanView?.hand?.Length ?? 0 : Game?.Players[0].Hand.Count ?? 0;
        public LanNetworkSession Network { get; private set; }
        public LanGameView LanView { get; private set; }
        public string LanError { get; private set; }
        [SerializeField] private RectTransform modeScreen, lanScreen, lobbyContent;
        [SerializeField] private TMP_InputField lanName, lanRoomName, lanAddress, lanPort;
        [SerializeField] private TMP_Text lanStatus;
        [SerializeField] private Button labEntryButton, leaveLanButton, kickStalledButton;
        private int displayedReveal;
        private readonly Queue<LanReveal> lanPresentations=new Queue<LanReveal>();
        private readonly Dictionary<int,bool> lanResolved=new Dictionary<int,bool>();
        private Coroutine lanPresentationRoutine;
        private float kickConfirmUntil;
        private const string KickStalledLabel = "미응답 플레이어 → AI";
        private bool firewallBusy;
        private ushort firewallPort;
        private LanFirewallState firewallState = LanFirewallState.Unsupported;

#if UNITY_EDITOR
        private void BuildModeSelection()
        {
            factionSelection.gameObject.SetActive(false);
            modeScreen = Panel(root, "Game Mode Selection", Vector2.zero, Vector2.zero, new Color(.025f,.055f,.08f)); Stretch(modeScreen);
            Text(modeScreen, "Mode Title", "PILGRIMAGE", new Vector2(0,300), new Vector2(1400,100), 60);
            Text(modeScreen, "Mode Prompt", "플레이 방식을 선택하세요", new Vector2(0,170), new Vector2(1400,65), 30);
            Button(modeScreen, "싱글플레이", new Vector2(-330,-20), new Vector2(550,150), SelectSinglePlayer);
            Button(modeScreen, "LAN 멀티플레이", new Vector2(330,-20), new Vector2(550,150), SelectLanMode);
            Text(modeScreen, "Single Note", "인간 1명 + AI 3명 · 오프라인\n교단 선택 · 카드 실험실", new Vector2(-330,-170), new Vector2(560,100), 24);
            Text(modeScreen, "LAN Note", "같은 LAN · 인간 2~4명 + 빈자리 AI\nIP 직접 접속 · 호스트가 게임 진행", new Vector2(330,-170), new Vector2(560,100), 24);
            Text(modeScreen, "Menu Feedback", "", new Vector2(0,-340), new Vector2(1600,100), 24);
            lanScreen = Panel(root, "LAN Lobby", Vector2.zero, Vector2.zero, new Color(.025f,.055f,.08f)); Stretch(lanScreen);
            Text(lanScreen, "LAN Title", "PILGRIMAGE  /  LAN", new Vector2(0,465), new Vector2(1500,70), 40);
            lanName = Input(lanScreen, "플레이어 이름", new Vector2(-560,370), new Vector2(480,50)); lanName.text = "플레이어";
            lanRoomName = Input(lanScreen, "방 이름", new Vector2(0,370), new Vector2(480,50)); lanRoomName.text = "Pilgrimage LAN";
            lanAddress = Input(lanScreen, "호스트 IPv4", new Vector2(-400,290), new Vector2(580,50)); lanAddress.text = "127.0.0.1";
            lanPort = Input(lanScreen, "포트", new Vector2(100,290), new Vector2(220,50)); lanPort.text = LanProtocol.DefaultPort.ToString();
            Button(lanScreen, "방 만들기", new Vector2(480,370), new Vector2(240,50), () => StartLan(true));
            Button(lanScreen, "참가하기", new Vector2(480,290), new Vector2(240,50), () => StartLan(false));
            Button(lanScreen, "방 나가기 / 메뉴", new Vector2(700,-460), new Vector2(330,50), LeaveLan);
            lanStatus = Text(lanScreen, "LAN Status", "", new Vector2(0,210), new Vector2(1750,80), 22);
            lobbyContent = Rect(lanScreen, "Lobby Contents", new Vector2(0,-90), new Vector2(1730,480));
            lanScreen.gameObject.SetActive(false);
            leaveLanButton = Button(root, "LAN 나가기", new Vector2(-800,501), new Vector2(250,40), LeaveLan);
            leaveLanButton.gameObject.SetActive(false);
            // Host-only escape hatch for an idle or frozen remote player (no turn timer in the design).
            kickStalledButton = Button(root, KickStalledLabel, new Vector2(-520,501), new Vector2(290,40), KickStalled);
            kickStalledButton.gameObject.SetActive(false);
            modeScreen.SetAsLastSibling();
        }
#endif
        public void SelectSinglePlayer()
        {
            if (Network != null || Game != null) return;
            IsLanMode = false; modeScreen.gameObject.SetActive(false); lanScreen.gameObject.SetActive(false);
            if (labEntryButton != null) labEntryButton.gameObject.SetActive(true);
            ShowFactionSelection();
        }
        public void SelectLanMode()
        {
            if (Game != null) return;
            IsLanMode = true; modeScreen.gameObject.SetActive(false); factionSelection.gameObject.SetActive(false);
            if (labEntryButton != null) labEntryButton.gameObject.SetActive(false);
            lanScreen.gameObject.SetActive(true); lanScreen.SetAsLastSibling();
            lanStatus.text = "같은 PC 테스트: 127.0.0.1 · 다른 PC: 호스트의 LAN IPv4";
            RenderLanIdle();
        }
        private void StartLan(bool host)
        {
            if (!ushort.TryParse(lanPort.text, out ushort port) || port == 0) { lanStatus.text = "포트는 1~65535 정수입니다."; return; }
            if (firewallBusy) return;
            // Windows exe host: make sure other PCs can reach the port before listening.
            if (host && LanFirewall.Supported) { StartCoroutine(HostAfterFirewall(port)); return; }
            OpenLan(host, lanAddress.text.Trim(), port, lanRoomName.text, lanName.text);
        }
        private IEnumerator HostAfterFirewall(ushort port)
        {
            firewallBusy = true; firewallPort = port;
            lanStatus.text = "방화벽 확인 중…";
            yield return LanFirewall.Ensure(port, false, s => firewallState = s);
            firewallBusy = false;
            if (Network != null || Game != null || !IsLanMode) yield break;
            OpenLan(true, lanAddress.text.Trim(), port, lanRoomName.text, lanName.text);
        }
        private IEnumerator RetryFirewall()
        {
            if (firewallBusy || Network == null || !Network.IsHost) yield break;
            firewallBusy = true; lanStatus.text += "\nWindows 승인 창(UAC)에서 '예'를 눌러 주세요.";
            yield return LanFirewall.Ensure(firewallPort, true, s => firewallState = s);
            firewallBusy = false;
            if (Network?.View?.lobby != null && Network.View.lobby.phase != LanPhase.Playing) RenderLobby(Network.View.lobby, null);
        }
        public bool OpenLan(bool host, string address, ushort port, string room, string name)
        {
            if (!IsLanMode || Network != null || Game != null) return false;
            Network = gameObject.AddComponent<LanNetworkSession>();
            Network.Changed += OnLanChanged; Network.Closed += ReturnFromLan;
            if (Network.Open(host,address,port,room,name,aiSettings,settings != null ? settings.assumptions : null,out string reason)) {
                if (Network.View == null) lanStatus.text = "호스트에 접속 중…";
                return true;
            }
            Network.Changed -= OnLanChanged; Network.Closed -= ReturnFromLan; Destroy(Network); Network = null;
            lanStatus.text = LanError = reason; return false;
        }
        public void LeaveLan()
        {
            if (Network != null) Network.Leave(); else ReturnFromLan("메뉴로 돌아왔습니다.");
        }
        private void ReturnFromLan(string reason)
        {
            if (Network != null) { Network.Changed -= OnLanChanged; Network.Closed -= ReturnFromLan; Destroy(Network); Network = null; }
            // Session and network are gone; clear every match-scoped coroutine, queue and overlay.
            ResetGameplayPresentation(); lanInGame = false;
            Game = null; LanView = null; IsLanMode = false; LanError = reason; displayedReveal = 0;
            leaveLanButton.gameObject.SetActive(false);
            foreach(var input in new[]{lanName,lanRoomName,lanAddress,lanPort}) input.interactable=true;
            ShowMainMenu(reason);
        }
        private void OnLanChanged(LanEnvelope envelope)
        {
            LanView = envelope.game;
            if (Network != null && Network.IsHost) Game = Network.Authority?.Game;
            if(!isActiveAndEnabled)return;
            if (!string.IsNullOrEmpty(envelope.error)) { LanError = envelope.error; Feedback(envelope.error); }
            if (envelope.lobby.phase == LanPhase.Playing && LanView != null) {
                lanInGame = true;
                lanScreen.gameObject.SetActive(false); leaveLanButton.gameObject.SetActive(true);
                if (targetPlayer == LocalSeat) targetPlayer = (LocalSeat + 1) % 4;
                RefreshLanGame();
                if (LanView.reveal != null && LanView.reveal.serial > displayedReveal) {
                    displayedReveal = LanView.reveal.serial;
                    Visuals.ReserveReveal(LanView.reveal.guid,LanView.reveal.serial);
                    lanPresentations.Enqueue(LanView.reveal);
                    if(lanPresentationRoutine==null)lanPresentationRoutine=StartCoroutine(PresentLanQueue());
                }
                if(LanView.reveal!=null&&LanView.reveal.completed)lanResolved[LanView.reveal.serial]=LanView.reveal.succeeded;
                Visuals.TimeScale=LanView.reveal!=null&&LanView.reveal.resultSeconds>0?LanView.reveal.resultSeconds/.8f:VisualScale;
                Visuals.Submit(LanView.events);
                if (!string.IsNullOrEmpty(envelope.error)) Feedback(envelope.error);
            } else {
                // Host-led rematch (or a game that never started): drop the finished match, then show the lobby.
                if (lanInGame) ResetLanMatchPresentation();
                RenderLobby(envelope.lobby, envelope.error);
            }
        }
        private void RenderLobby(LanLobbyView lobby, string error)
        {
            lanScreen.gameObject.SetActive(true); lanScreen.SetAsLastSibling();
            if (Network == null) { RenderLanIdle(); return; }
            string endpoint = Network.Address + ":" + Network.Port;
            lanStatus.text = lobby.room + "  ·  " + endpoint + "\n인간 " + lobby.members.Length + "/4 · AI " + (4-lobby.members.Length) + (string.IsNullOrEmpty(error) ? "" : "\n"+error);
            if (lobby.host && firewallState != LanFirewallState.Unsupported) lanStatus.text += "\n" + LanFirewall.Describe(firewallState, firewallPort);
            RenderLobbySkin(lobby, error);
            foreach(var input in new[]{lanName,lanRoomName,lanAddress,lanPort}) input.interactable=false;
        }
        private int SeatAt(int relative) => (LocalSeat+relative)%4;
        private void RefreshLanGame()
        {
            if(LanView==null) return;
            PilgrimageAudio.Instance?.Observe(LanView.round,LanView.activePlayer,LanView.phase,LanView.winners!=null&&LanView.winners.Contains(LocalSeat));
            for(int relative=0;relative<4;relative++) {
                var p=LanView.players[SeatAt(relative)];
                UpdatePlayerPortrait(relative,p.faction);
                playerLabels[relative].text=(p.seat==LanView.activePlayer&&LanView.phase==GamePhase.Actions?"▶ ":"") + p.name + (relative==0?" (나)":p.ai?" · AI":"");
                playerLabels[relative].enableAutoSizing=true; playerLabels[relative].fontSizeMin=14; playerLabels[relative].fontSizeMax=24;
                if(relative==0) { int[]v={p.handCount,p.faith,p.offering,p.doctrine}; for(int j=0;j<4;j++)ownStats[j].text=v[j].ToString(); }
                else {
                    opponentStats[(relative-1)*4].text=p.handCount.ToString(); opponentStats[(relative-1)*4+1].text=p.faith.ToString(); opponentStats[(relative-1)*4+2].text=p.offering.ToString(); opponentStats[(relative-1)*4+3].text=p.doctrine.ToString();
    
                }
            }
            roundText.text=LanView.round.ToString();
            if(atheistsText!=null)atheistsText.text="무신론자 "+LanView.atheists;
            activeText.text=LanView.phase==GamePhase.Finished?"승리\n"+string.Join(" · ",LanView.winners.Select(i=>LanView.players[i].name)):LanView.phase==GamePhase.RewardChoice?"카드 획득 결과":LanView.players[LanView.activePlayer].name+(LanView.activePlayer==LocalSeat?"\n내 차례":LanView.players[LanView.activePlayer].ai?"\nAI 차례":"\n상대 차례");
            activeText.enableAutoSizing=true; activeText.fontSizeMin=14;activeText.fontSizeMax=26;
            targetLabel.text=LanView.players[targetPlayer].name;
            UpdateLanEndButton();
            foreach(var view in handViews){view.gameObject.SetActive(false);Destroy(view.gameObject);} handViews.Clear();
            for(int i=0;i<LanView.hand.Length;i++) {
                var view=Instantiate(cardPrefab,handRoot,false); view.name="My Card "+i; view.Bind(this,LanView.hand[i].DisplayCard(),i,1300);handViews.Add(view);
            }
            if(SelectedCardId.HasValue&&!handViews.Any(v=>v.Card.InstanceId==SelectedCardId))SelectedCardId=null;
            ApplySelection(); BuildLanRewards(); UpdateKickStalled();
            if(LanView.reveal!=null&&LanView.reveal.completed) { if(LanView.reveal.succeeded)messageText.text=""; else if(!string.IsNullOrEmpty(LanView.reveal.result))Feedback(LanView.reveal.result); }
            if(LanView.phase==GamePhase.Finished)Feedback("최다 신도 "+LanView.players.Max(p=>p.followers)+"명 · 전체 인원 "+LanView.population+"명");
        }
        private bool LanCanAct => LanView!=null&&LanView.phase==GamePhase.Actions&&LanView.activePlayer==LocalSeat&&!LanView.busy&&!IsAnimating&&!(Visuals!=null&&Visuals.IsBusy)&&Network!=null&&!Network.Pending;
        private void RenderLanBacks(int relative,int count)
        {
            var area=opponentHands[relative-1];ClearChildren(area);
            for(int i=0;i<count;i++) {
                float spacing=count<=1?0:Mathf.Min(56,(area.sizeDelta.x-65)/(count-1));
                var back=Instantiate(cardBackPrefab,area,false);back.name="Private Card Back";
                back.anchoredPosition=new Vector2((i-(count-1)/2f)*spacing,0);
                if(settings!=null&&settings.cardBack!=null)back.GetComponent<Image>().sprite=settings.cardBack;
            }
        }
        private void BuildLanRewards()
        {
            ClearChildren(rewardRoot);rewardRoot.gameObject.SetActive(LanView.phase==GamePhase.RewardChoice);
            if(!rewardRoot.gameObject.activeSelf)return;rewardRoot.SetAsLastSibling();
            Text(rewardRoot,"Reward Title",LanView.round+"라운드 · 무작위 카드 획득 결과",new Vector2(0,210),new Vector2(1070,55),25);
            var awards=LanView.rewards.GroupBy(r=>r.owner).ToArray();
            for(int i=0;i<awards.Length;i++) {var a=awards[i];Text(rewardRoot,"Award "+i,LanView.players[a.Key].name+" · "+string.Join(", ",a.Select(r=>CardCatalog.Get(r.id).Name))+" → "+PileName(a.First().destination),new Vector2(0,125-i*75),new Vector2(1050,60),24);}
            var button=Button(rewardRoot,LanView.acknowledged?"다른 참가자 확인 대기":"획득 확인 · 계속",new Vector2(0,-190),new Vector2(500,55),()=>Network.Send(new LanCommand{kind=LanCommandKind.Acknowledge})); button.interactable=!LanView.acknowledged&&!Network.Pending;
        }
        // A disabled button gives no feedback, so the label says who the game is waiting on.
        private void UpdateLanEndButton()
        {
            endButton.interactable = LanCanAct;
            string label = "행동 종료";
            if (LanView.phase == GamePhase.RewardChoice) label = LanView.acknowledged ? "다른 참가자 확인 대기" : "획득 확인 필요";
            else if (LanView.phase == GamePhase.Finished) label = "게임 종료";
            else if (LanView.activePlayer != LocalSeat) label = (LanView.players[LanView.activePlayer].ai ? "AI" : "상대") + " 차례 대기";
            else if (LanView.busy || IsAnimating) label = "카드 처리 중";
            else if (Network != null && Network.Pending) label = "응답 대기";
            SetEndLabel(label);
        }
        private void SetEndLabel(string label) { var text = endButton != null ? endButton.GetComponentInChildren<TMP_Text>() : null; if (text != null) text.text = label; }
        private void UpdateKickStalled()
        {
            var room = Network != null && Network.IsHost ? Network.Authority : null;
            bool show = room != null && LanView.phase != GamePhase.Finished && room.StalledRemoteHumans().Count > 0;
            kickStalledButton.gameObject.SetActive(show);
            if (!show) kickConfirmUntil = 0;
            else { kickStalledButton.transform.SetAsLastSibling(); SetKickLabel(Time.realtimeSinceStartup < kickConfirmUntil ? "한 번 더 누르면 AI로 전환" : KickStalledLabel); }
        }
        private void SetKickLabel(string label) { var text = kickStalledButton.GetComponentInChildren<TMP_Text>(); if (text != null) text.text = label; }
        private void KickStalled()
        {
            var room = Network != null && Network.IsHost ? Network.Authority : null;
            if (room == null) return;
            // Two presses within 3 seconds: a departure cannot be undone (no rejoin).
            if (Time.realtimeSinceStartup >= kickConfirmUntil) { kickConfirmUntil = Time.realtimeSinceStartup + 3; SetKickLabel("한 번 더 누르면 AI로 전환"); return; }
            kickConfirmUntil = 0;
            foreach (ulong id in room.StalledRemoteHumans().ToArray()) Network.Kick(id);
            Feedback("응답이 없던 플레이어의 좌석을 AI가 이어받습니다.");
        }
        private static string PileName(CardPile pile)=>pile==CardPile.Deck?"덱":pile==CardPile.Hand?"손패":pile==CardPile.Discard?"버린 더미":"제거 더미";
        private void PlayLanSelected()
        {
            if(!LanCanAct||!SelectedCardId.HasValue)return;
            var card=LanView.hand.FirstOrDefault(c=>c.guid==SelectedCardId.Value.ToString("D"));if(card==null)return;
            if(CardCatalog.Get(card.id).Target==TargetKind.Card){OpenLanPicker(SelectedCardId.Value);return;}
            SendLanPlay(SelectedCardId.Value,new EffectTarget{PlayerIndex=targetPlayer});
        }
        private void SendLanPlay(Guid source,EffectTarget target)
        {
            if(!LanCanAct)return;
            // Lock first: on the host Send() resolves synchronously and may already have refreshed the UI
            // (e.g. "헌금이 부족합니다" rejection). Recompute afterwards instead of forcing it off.
            endButton.interactable=false;
            Network.Send(new LanCommand{kind=LanCommandKind.Play,card=source.ToString("D"),target=target.PlayerIndex,targets=target.Cards.Select(id=>id.ToString("D")).ToArray()});
            endButton.interactable=LanCanAct;
        }
        private void OpenLanPicker(Guid source)
        {
            if(!LanCanAct)return;
            var selection=LanView.selections.FirstOrDefault(s=>s.source==source.ToString("D"));
            if(selection==null){Feedback("현재 이 카드를 선택할 수 없습니다. 코스트와 사용 조건을 확인하세요.");return;}
            pendingRemovalSource=source;pendingCardTargets.Clear();ClearChildren(pickerContent);
            var def=CardCatalog.Get(LanView.hand.First(c=>c.guid==source.ToString("D")).id);
            pickerTitle.text=def.Name+(def.Id==CardId.Edict?" · 최대 2장 선택":" · 신도 1장 선택");
            pickerContent.sizeDelta=new Vector2(1400,Mathf.Max(660,selection.cards.Length*70));
            for(int i=0;i<selection.cards.Length;i++) {
                var c=selection.cards[i];var id=Guid.Parse(c.guid);Button choice=null;
                choice=CardChoice(pickerContent,CardCatalog.Get(c.id).Name+" · "+selection.piles[i]+" · "+id.ToString("N").Substring(0,8),new Vector2(0,-35-i*70),new Vector2(1300,60),()=>{ToggleCardTarget(id);choice.GetComponent<Image>().color=pendingCardTargets.Contains(id)?new Color(.75f,.85f,.75f):Color.white;});AnchorTop((RectTransform)choice.transform);
            }
            cardPicker.gameObject.SetActive(true);cardPicker.SetAsLastSibling();
        }
        private void ConfirmLanTargets()
        {
            if(!pendingRemovalSource.HasValue)return;
            Guid source=pendingRemovalSource.Value;var ids=pendingCardTargets.ToArray();CloseCardPicker();SendLanPlay(source,new EffectTarget{PlayerIndex=LocalSeat,CardInstanceIds=ids});
        }
        private void LanDetail(CardInstance card)
        {
            var data=LanView.hand.First(c=>c.guid==card.InstanceId.ToString("D"));string text=CardPresentation.Detail(card.Definition);
            if(card.Definition.Id==CardId.RefusingFollower)text+="\n\n변환까지 남은 정산: "+data.refusingTurns;
            if(card.Definition.Id==CardId.HeresyJudgment||card.Definition.Id==CardId.AggressiveMission)text+="\n\n현재 헌금 비용: "+LanView.EffectiveCost(data,targetPlayer);
            OpenDetail(text);
        }
        private void ShowLanStats(int relative)
        {
            var p=LanView.players[SeatAt(relative)];OpenDetail(p.name+$"\n\n신앙심 {p.faith} / 100 · 교리 정합성 {p.doctrine} / 100\n헌금 {p.offering} · 신도 {p.followers}\n손패 {p.handCount} · 덱 {p.deckCount} · 버린 더미 {p.discardCount}\n교회 재산 {p.property} · 다음 수입 {p.nextIncome}\n\n{p.effects}");
        }
        private IEnumerator AnimateLanReveal(LanReveal reveal)
        {
            try {
                Reveal(CardCatalog.Get(reveal.id),LanView.players[reveal.actor].ai,Guid.Parse(reveal.guid));
                yield return ThrowSubmittedCard(reveal.actor,reveal.travelSeconds);
                if(reveal.cutInSeconds>0&&(!reveal.completed||reveal.succeeded))Visuals.BeginCutIn(CardCatalog.Get(reveal.id),reveal.cutInSeconds/.8f);
            } finally {if(isActiveAndEnabled&&LanView!=null)endButton.interactable=LanCanAct;}
        }
        private IEnumerator PresentLanQueue()
        {
            IsAnimating=true;
            try{
                while(lanPresentations.Count>0){var reveal=lanPresentations.Dequeue();yield return AnimateLanReveal(reveal);
                    while(Visuals.CutIn.IsPlaying)yield return null;
                    Visuals.ReleaseReveal(reveal.guid,reveal.serial);
                    while(Network!=null&&!lanResolved.ContainsKey(reveal.serial))yield return null;
                    if(Network==null)yield break;
                    if(lanResolved[reveal.serial])while(!Visuals.HasPresented(reveal.guid,reveal.serial))yield return null;
                    lanResolved.Remove(reveal.serial);
                }
            }finally{IsAnimating=false;lanPresentationRoutine=null;if(isActiveAndEnabled&&LanView!=null)RefreshLanGame();}
        }
        private void UpdateLanInput()
        {
            var keys=UnityEngine.InputSystem.Keyboard.current;
            if(IsCardPickerOpen){if(keys!=null&&keys.escapeKey.wasPressedThisFrame)CloseCardPicker();return;}
            if(IsDetailOpen){if((keys!=null&&(keys.escapeKey.wasPressedThisFrame||keys.spaceKey.wasPressedThisFrame))||(UnityEngine.InputSystem.Mouse.current!=null&&UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame))CloseDetail();return;}
            if(!CanInteract||keys==null)return;
            if(keys.leftArrowKey.wasPressedThisFrame)SelectRelative(-1);if(keys.rightArrowKey.wasPressedThisFrame)SelectRelative(1);
            if(keys.spaceKey.wasPressedThisFrame)ToggleDetail();if(keys.enterKey.wasPressedThisFrame||keys.numpadEnterKey.wasPressedThisFrame)PlaySelected();
        }
    }
}
