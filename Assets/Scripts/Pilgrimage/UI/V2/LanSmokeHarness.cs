using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Pilgrimage.UI
{
    // Explicit opt-in automation. Sends exactly the same intents as the real UI.
    // Never grants resources/cards, changes turns or accesses a remote authority model.
    public sealed class LanSmokeHarness : MonoBehaviour
    {
        [Serializable] private sealed class Result {
            public string role, outcome, error; public int humans, seat=-1, round, seed, played, reveals, snapshots;
            public bool assignedUnique, debugBlocked, privateViews=true, aiTakeover, menuReturned;
            public bool peopleConserved=true, identitiesConserved=true;
            public string publicState;
            public int[] assignment; public string[] violations;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Boot()
        {
            // Editor and Development Build only (runtime check replaces the deprecated DEVELOPMENT_BUILD define).
            if((!Application.isEditor&&!Debug.isDebugBuild)||Value("-pilgrimageLanSmoke")==null)return;
            var demo=FindAnyObjectByType<DemoController>();if(demo!=null)demo.gameObject.AddComponent<LanSmokeHarness>();
        }
        private static string Value(string flag){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,flag);return i>=0&&i<a.Length-1?a[i+1]:null;}
        private readonly Result result=new Result();private readonly HashSet<string> rejected=new HashSet<string>();
        private DemoController demo;private bool done;private int lastRound=-1,lastActor=-1,lastReveal;private float next;private string mode,output;private int peerNumber;
        private IEnumerator Start()
        {
            yield return null;
            demo=GetComponent<DemoController>();mode=Value("-pilgrimageLanScenario")??"finish";output=Value("-pilgrimageLanResult");
            result.role=Value("-pilgrimageLanSmoke");result.humans=int.Parse(Value("-pilgrimageLanHumans")??"2");peerNumber=int.Parse(Value("-pilgrimageLanPeer")??"0");
            demo.SelectLanMode();demo.AiConfiguration.revealInterval=.1f;demo.AiConfiguration.travelDuration=.05f;demo.AiConfiguration.presentationTimeScale=.05f;
            if(!demo.OpenLan(result.role=="host",Value("-pilgrimageLanIp")??"127.0.0.1",ushort.Parse(Value("-pilgrimageLanPort")??"17777"),"LAN automation","Peer "+peerNumber)) {Finish("failed",demo.LanError);yield break;}
            demo.Network.Changed+=OnView;
            demo.Network.Closed+=reason=>{result.menuReturned=demo.IsModeSelectionOpen;Finish(mode=="close"&&result.role=="client"?"closed":"disconnected",reason);};
            next=Time.realtimeSinceStartup+.3f;
        }
        private void OnView(LanEnvelope envelope)
        {
            if(done)return;var lobby=envelope.lobby;var g=envelope.game;
            if(lobby.assignment!=null){result.assignment=lobby.assignment.Select(f=>(int)f).ToArray();result.assignedUnique=lobby.assignment.Distinct().Count()==4;result.seed=lobby.seed;}
            if(!string.IsNullOrEmpty(envelope.error)&&g!=null&&g.activePlayer==g.localSeat) foreach(var c in g.hand) if(c.guid==lastCard)rejected.Add(c.guid);
            if(g==null)return;
            result.snapshots++;result.seat=g.localSeat;result.round=g.round;
            result.publicState=g.round+":"+g.phase+":"+g.atheists+":"+string.Join("|",g.players.Select(p=>p.seat+","+p.faction+","+p.offering+","+p.faith+","+p.doctrine+","+p.followers+","+p.handCount+","+p.deckCount+","+p.discardCount+","+p.property+","+p.judgmentUses+","+p.effects));
            if(g.reveal!=null&&g.reveal.serial>lastReveal){lastReveal=g.reveal.serial;result.reveals++;}
            if(result.role=="host") {
                var authority=demo.Network.Authority;
                foreach(var member in authority.Members) {
                    var recipient=authority.ViewFor(member.Connection);string serialized=JsonUtility.ToJson(recipient);
                    var published=recipient.rewards.Select(r=>r.guid).Concat(recipient.events.Where(e=>e.cause==VisualCause.Card).Select(e=>e.source)).ToHashSet();if(recipient.reveal!=null)published.Add(recipient.reveal.guid);
                    if(authority.Game.Players.Where((p,i)=>i!=recipient.localSeat).SelectMany(p=>p.Hand.Concat(p.Deck)).Any(c=>!published.Contains(c.InstanceId.ToString("D"))&&serialized.Contains(c.InstanceId.ToString("D"))))result.privateViews=false;
                }
                result.peopleConserved &= authority.Game.PopulationIsConserved;
                var ids=authority.Game.Players.SelectMany(p=>p.Hand.Concat(p.Deck).Concat(p.Discard).Concat(p.Removed)).Select(c=>c.InstanceId).ToArray();
                result.identitiesConserved &= ids.Distinct().Count()==ids.Length;
                if(g.players.Count(p=>p.ai)>4-result.humans)result.aiTakeover=true;
            }
            var before=demo.Game;demo.EnterCardLab();result.debugBlocked=!demo.IsCardLab&&!demo.DevelopmentToolsAvailable&&ReferenceEquals(demo.Game,before);
        }
        private string lastCard;
        private void Update()
        {
            if(done||demo==null)return;
            if(Time.realtimeSinceStartup>240){Finish("timeout",demo.LanError);return;}
            if(demo.Network==null)return;
            if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.07f;
            var lobby=demo.Network.View?.lobby;if(lobby==null||demo.Network.Pending)return;
            if(lobby.phase==LanPhase.Lobby){
                var mine=lobby.members.First(m=>m.connection==lobby.localConnection);
                if(!mine.ready){demo.Network.Send(new LanCommand{kind=LanCommandKind.Ready,ready=true});return;}
                if(result.role=="host"&&lobby.members.Length==result.humans&&lobby.members.All(m=>m.ready))demo.Network.Send(new LanCommand{kind=LanCommandKind.Prepare});return;
            }
            if(lobby.phase==LanPhase.Assignment){if(!lobby.members.First(m=>m.connection==lobby.localConnection).confirmed)demo.Network.Send(new LanCommand{kind=LanCommandKind.ConfirmAssignment});return;}
            var g=demo.LanView;if(g==null)return;
            if(mode=="disconnect"&&result.role=="client"&&g.round>=2){Finish("left",null);demo.Network.Leave();return;}
            if(mode=="close"&&result.role=="host"&&g.round>=2){Finish("host-closed",null);demo.Network.Leave();return;}
            if(g.phase==GamePhase.Finished){Finish("finished",null);return;}
            if(g.phase==GamePhase.RewardChoice){if(!g.acknowledged)demo.Network.Send(new LanCommand{kind=LanCommandKind.Acknowledge});return;}
            if(g.busy||demo.IsAnimating||g.activePlayer!=g.localSeat)return;
            if(lastRound!=g.round||lastActor!=g.activePlayer){rejected.Clear();lastRound=g.round;lastActor=g.activePlayer;}
            // A small test driver, not a second rules engine. The host rejects all invalid attempts.
            foreach(var card in g.hand) {
                var def=CardCatalog.Get(card.id);if(rejected.Contains(card.guid)||!def.IsPlayable||def.Implementation!=EffectImplementation.Implemented||g.EffectiveCost(card,(g.localSeat+1)%4)>g.players[g.localSeat].offering)continue;
                string[] targets=Array.Empty<string>();int target=(g.localSeat+1)%4;
                if(def.Target==TargetKind.Card){var choice=g.selections.FirstOrDefault(s=>s.source==card.guid);if(choice==null)continue;target=g.localSeat;targets=choice.cards.Take(def.Id==CardId.Edict?2:1).Select(c=>c.guid).ToArray();}
                lastCard=card.guid;rejected.Add(card.guid);result.played++;
                demo.Network.Send(new LanCommand{kind=LanCommandKind.Play,card=card.guid,target=target,targets=targets});return;
            }
            demo.Network.Send(new LanCommand{kind=LanCommandKind.EndTurn});
        }
        private void Finish(string outcome,string error)
        {
            if(done)return;done=true;result.outcome=outcome;result.error=error;
            if(output!=null)File.WriteAllText(output,JsonUtility.ToJson(result,true));
            Debug.Log("LAN_SMOKE "+JsonUtility.ToJson(result));
            if(!Application.isEditor)StartCoroutine(Exit());
        }
        private IEnumerator Exit(){yield return new WaitForSecondsRealtime(1);Application.Quit();}
    }
}
