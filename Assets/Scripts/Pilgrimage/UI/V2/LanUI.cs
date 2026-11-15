using System;
using System.Collections;
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
        private RectTransform modeScreen, lanScreen, lobbyContent;
        private TMP_InputField lanName, lanRoomName, lanAddress, lanPort;
        private TMP_Text lanStatus;
        private Button labEntryButton, leaveLanButton;
        private int displayedReveal;

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
            modeScreen.SetAsLastSibling();
        }
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
        }
        private void StartLan(bool host)
        {
            if (!ushort.TryParse(lanPort.text, out ushort port) || port == 0) { lanStatus.text = "포트는 1~65535 정수입니다."; return; }
            OpenLan(host, lanAddress.text.Trim(), port, lanRoomName.text, lanName.text);
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
            StopAllCoroutines(); IsAnimating = IsAiRunning = false;
            Game = null; LanView = null; IsLanMode = false; LanError = reason; SelectedCardId = null; displayedReveal = 0;
            CloseCardPicker(); CloseDetail(); CloseLabPanel(); rewardRoot.gameObject.SetActive(false); revealedCard.gameObject.SetActive(false);
            RevealedCardInstanceId = null; RevealedAiCard = null;
            ClearChildren(handRoot); handViews.Clear(); leaveLanButton.gameObject.SetActive(false);
            lanScreen.gameObject.SetActive(false); factionSelection.gameObject.SetActive(false); modeScreen.gameObject.SetActive(true); modeScreen.SetAsLastSibling();
            modeScreen.Find("Menu Feedback").GetComponent<TMP_Text>().text = reason;
            foreach(var input in new[]{lanName,lanRoomName,lanAddress,lanPort}) input.interactable=true;
        }
        private void OnLanChanged(LanEnvelope envelope)
        {
            LanView = envelope.game;
            if (Network != null && Network.IsHost) Game = Network.Authority?.Game;
            if (!string.IsNullOrEmpty(envelope.error)) { LanError = envelope.error; Feedback(envelope.error); }
            if (envelope.lobby.phase == LanPhase.Playing && LanView != null) {
                lanScreen.gameObject.SetActive(false); leaveLanButton.gameObject.SetActive(true);
                if (targetPlayer == LocalSeat) targetPlayer = (LocalSeat + 1) % 4;
                RefreshLanGame();
                if (LanView.reveal != null && LanView.reveal.serial > displayedReveal) {
                    displayedReveal = LanView.reveal.serial;
                    StartCoroutine(AnimateLanReveal(LanView.reveal));
                }
                if (!string.IsNullOrEmpty(envelope.error)) Feedback(envelope.error);
            } else RenderLobby(envelope.lobby, envelope.error);
        }
        private void RenderLobby(LanLobbyView lobby, string error)
        {
            ClearChildren(lobbyContent); lanScreen.gameObject.SetActive(true); lanScreen.SetAsLastSibling();
            string endpoint = Network.Address + ":" + Network.Port;
            lanStatus.text = lobby.room + "  ·  " + endpoint + "\n인간 " + lobby.members.Length + "/4 · AI " + (4-lobby.members.Length) + (string.IsNullOrEmpty(error) ? "" : "\n"+error);
            bool assignment = lobby.phase == LanPhase.Assignment;
            for (int i=0;i<4;i++) {
                var member = i<lobby.members.Length ? lobby.members[i] : null;
                string state = member == null ? "AI 빈자리" : member.name + (member.connection==lobby.localConnection ? " (나)" : "") + " · " + (assignment ? member.confirmed ? "확인 완료" : "확인 대기" : member.ready ? "준비 완료" : "준비 중");
                string faction = assignment ? FactionSetup.Name(lobby.assignment[i]) : member==null ? "시작 시 배정" : FactionSetup.Name(member.desired);
                Text(lobbyContent,"Lobby Seat "+i,state+"\n"+faction,new Vector2(-600+i*400,100),new Vector2(385,120),23);
            }
            if (assignment) {
                Text(lobbyContent,"Assignment Seed","최종 교단 배정 · 시드 "+lobby.seed+"\n모두 확인하면 게임을 시작합니다.",new Vector2(0,-50),new Vector2(1500,85),25);
                Button(lobbyContent,"배정 확인",new Vector2(0,-180),new Vector2(400,60),()=>Network.Send(new LanCommand {kind=LanCommandKind.ConfirmAssignment}));
            } else {
                for(int i=0;i<4;i++) { var f=(FactionId)(i+1); Button(lobbyContent,FactionSetup.Name(f),new Vector2(-600+i*400,-45),new Vector2(380,60),()=>Network.Send(new LanCommand {kind=LanCommandKind.Faction,faction=f})); }
                var mine=lobby.members.First(m=>m.connection==lobby.localConnection);
                Button(lobbyContent,mine.ready ? "준비 취소" : "준비",new Vector2(-240,-160),new Vector2(360,60),()=>Network.Send(new LanCommand {kind=LanCommandKind.Ready,ready=!mine.ready}));
                if(lobby.host) { var start=Button(lobbyContent,"게임 시작 · 배정",new Vector2(240,-160),new Vector2(400,60),()=>Network.Send(new LanCommand {kind=LanCommandKind.Prepare})); start.interactable=lobby.members.Length>=2&&lobby.members.All(m=>m.ready); }
            }
            foreach(var input in new[]{lanName,lanRoomName,lanAddress,lanPort}) input.interactable=false;
        }
        private int SeatAt(int relative) => (LocalSeat+relative)%4;
        private void RefreshLanGame()
        {
            if(LanView==null) return;
            for(int relative=0;relative<4;relative++) {
                var p=LanView.players[SeatAt(relative)];
                playerLabels[relative].text=(p.seat==LanView.activePlayer&&LanView.phase==GamePhase.Actions?"▶ ":"") + p.name + (relative==0?" (나)":p.ai?" · AI":"");
                if(relative==0) { int[]v={p.offering,p.faith,p.doctrine,p.followers}; for(int j=0;j<4;j++)ownStats[j].text=v[j].ToString(); }
                else {
                    opponentStats[relative-1,0].text=p.followers.ToString(); opponentStats[relative-1,1].text=p.offering.ToString(); opponentStats[relative-1,2].text=p.handCount.ToString();
                    RenderLanBacks(relative,p.handCount);
                }
            }
            roundText.text=$"ROUND {LanView.round:00} / 40     무신론자 {LanView.atheists}";
            activeText.text=LanView.phase==GamePhase.Finished?"승리\n"+string.Join(" · ",LanView.winners.Select(i=>LanView.players[i].name)):LanView.phase==GamePhase.RewardChoice?"카드 획득 결과":LanView.players[LanView.activePlayer].name+(LanView.activePlayer==LocalSeat?"\n내 차례":LanView.players[LanView.activePlayer].ai?"\nAI 차례":"\n상대 차례");
            activeText.enableAutoSizing=true; activeText.fontSizeMin=14;activeText.fontSizeMax=26;
            targetLabel.text=LanView.players[targetPlayer].name;
            endButton.interactable=LanCanAct;
            foreach(var view in handViews){view.gameObject.SetActive(false);Destroy(view.gameObject);} handViews.Clear();
            for(int i=0;i<LanView.hand.Length;i++) {
                var card=Panel(handRoot,"My Card "+i,Vector2.zero,new Vector2(200,280),Color.black);
                var view=card.gameObject.AddComponent<CardView>(); view.Bind(this,LanView.hand[i].DisplayCard(),i,1300);handViews.Add(view);
            }
            if(SelectedCardId.HasValue&&!handViews.Any(v=>v.Card.InstanceId==SelectedCardId))SelectedCardId=null;
            ApplySelection(); BuildLanRewards();
            if(LanView.reveal!=null&&!string.IsNullOrEmpty(LanView.reveal.result)) Feedback(LanView.reveal.result);
            if(LanView.phase==GamePhase.Finished)Feedback("최다 신도 "+LanView.players.Max(p=>p.followers)+"명 · 전체 인원 "+LanView.population+"명");
        }
        private bool LanCanAct => LanView!=null&&LanView.phase==GamePhase.Actions&&LanView.activePlayer==LocalSeat&&!LanView.busy&&!IsAnimating&&Network!=null&&!Network.Pending;
        private void RenderLanBacks(int relative,int count)
        {
            var area=opponentHands[relative-1];ClearChildren(area);
            for(int i=0;i<count;i++) {
                float spacing=count<=1?0:Mathf.Min(56,(area.sizeDelta.x-65)/(count-1));
                var back=Panel(area,"Private Card Back",new Vector2((i-(count-1)/2f)*spacing,0),new Vector2(62,82),new Color(.3f,.32f,.35f));back.GetComponent<Image>().raycastTarget=false;
                if(settings!=null&&settings.cardBack!=null)back.GetComponent<Image>().sprite=settings.cardBack;
                else Text(back,"Back Symbol","?",Vector2.zero,new Vector2(54,70),30);
            }
        }
        private void BuildLanRewards()
        {
            ClearChildren(rewardRoot);rewardRoot.gameObject.SetActive(LanView.phase==GamePhase.RewardChoice);
            if(!rewardRoot.gameObject.activeSelf)return;rewardRoot.SetAsLastSibling();
            Text(rewardRoot,"Reward Title",LanView.round+"라운드 · 무작위 카드 획득 결과",new Vector2(0,210),new Vector2(1070,55),25);
            for(int i=0;i<LanView.rewards.Length;i++) {var r=LanView.rewards[i];Text(rewardRoot,"Award "+i,LanView.players[r.owner].name+" · "+CardCatalog.Get(r.id).Name+" → "+PileName(r.destination),new Vector2(0,125-i*75),new Vector2(1050,60),24);}
            var button=Button(rewardRoot,LanView.acknowledged?"다른 참가자 확인 대기":"획득 확인 · 계속",new Vector2(0,-190),new Vector2(500,55),()=>Network.Send(new LanCommand{kind=LanCommandKind.Acknowledge})); button.interactable=!LanView.acknowledged&&!Network.Pending;
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
            Network.Send(new LanCommand{kind=LanCommandKind.Play,card=source.ToString("D"),target=target.PlayerIndex,targets=target.Cards.Select(id=>id.ToString("D")).ToArray()});
            endButton.interactable=false;
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
                choice=Button(pickerContent,CardCatalog.Get(c.id).Name+" · "+selection.piles[i]+" · "+id.ToString("N").Substring(0,8),new Vector2(0,-35-i*70),new Vector2(1300,60),()=>{ToggleCardTarget(id);choice.GetComponent<Image>().color=pendingCardTargets.Contains(id)?new Color(.2f,.5f,.38f):new Color(.13f,.3f,.31f);});AnchorTop((RectTransform)choice.transform);
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
            IsAnimating=true;
            try {
                Reveal(CardCatalog.Get(reveal.id),LanView.players[reveal.actor].ai,Guid.Parse(reveal.guid));
                int relative=(reveal.actor-LocalSeat+4)%4;
                Vector2 start=relative==0?new Vector2(0,-350):relative==1?new Vector2(0,211):relative==2?new Vector2(-755,-19):new Vector2(755,-19);
                float travel=Mathf.Clamp(aiSettings.travelDuration,.05f,2);
                for(float t=0;t<travel;t+=Time.unscaledDeltaTime){revealedCard.anchoredPosition=Vector2.Lerp(start,new Vector2(0,6),t/travel);revealedCard.localScale=Vector3.one*Mathf.Lerp(.45f,1,t/travel);yield return null;}
                revealedCard.anchoredPosition=new Vector2(0,6);revealedCard.localScale=Vector3.one;
            } finally {IsAnimating=false;if(isActiveAndEnabled&&LanView!=null)endButton.interactable=LanCanAct;}
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
