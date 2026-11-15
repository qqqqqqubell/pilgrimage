using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Pilgrimage.UI;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Pilgrimage.Tests
{
    public sealed class LanInteractionTests
    {
        private DemoController demo;
        [UnitySetUp] public IEnumerator Setup(){demo=new GameObject("LAN UI Test").AddComponent<DemoController>();yield return null;}
        [UnityTearDown] public IEnumerator Cleanup(){if(demo!=null)Object.Destroy(demo.gameObject);yield return null;yield return null;}
        [UnityTest] public IEnumerator ModeSelectionKeepsOfflineGameUnstartedAndSinglePlayerRetainsFactionAndLab()
        {
            Assert.That(demo.IsModeSelectionOpen,Is.True);Assert.That(demo.Game,Is.Null);Assert.That(demo.IsAiRunning,Is.False);
            demo.SelectSinglePlayer();Assert.That(demo.IsFactionSelectionOpen,Is.True);Assert.That(demo.Network,Is.Null);
            Assert.That(demo.ChooseFaction(FactionId.Gold),Is.True);Assert.That(demo.LocalSeat,Is.Zero);Assert.That(demo.LocalHandCount,Is.EqualTo(4));
            demo.EnterCardLab();Assert.That(demo.IsCardLab,Is.True);demo.ExitCardLab();Assert.That(demo.Game.HumanFaction,Is.EqualTo(FactionId.Gold));yield return null;
        }
        [UnityTest] public IEnumerator LanHostLobbyBlocksFactionStartAiAndEveryLabEntryThenReturnsToMenu()
        {
            demo.SelectLanMode();Assert.That(demo.IsLanMode,Is.True);Assert.That(demo.DevelopmentToolsAvailable,Is.False);
            Assert.That(demo.OpenLan(true,"127.0.0.1",19377,"UI test room","호스트"),Is.True);yield return null;
            Assert.That(demo.Network.IsHost,Is.True);Assert.That(demo.Network.View.lobby.members.Length,Is.EqualTo(1));
            Assert.That(demo.Game,Is.Null);Assert.That(demo.IsAiRunning,Is.False);Assert.That(demo.ChooseFaction(FactionId.Fur),Is.False);
            demo.EnterCardLab();demo.DebugOpponentStep();Assert.That(demo.IsCardLab,Is.False);Assert.That(demo.Game,Is.Null);
            Assert.That(demo.GetComponentsInChildren<Button>().Any(b=>b.name=="카드 실험"),Is.False);
            Assert.That(demo.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("AI 3")),Is.True);
            demo.LeaveLan();yield return new WaitForSecondsRealtime(.5f);
            Assert.That(demo.IsModeSelectionOpen,Is.True);Assert.That(demo.Network,Is.Null);Assert.That(demo.IsLanMode,Is.False);
            demo.SelectSinglePlayer();Assert.That(demo.ChooseFaction(FactionId.Discipline),Is.True);
        }
        [UnityTest] public IEnumerator HostRejectedPlayKeepsEndTurnAvailable()
        {
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            demo.SelectLanMode();Assert.That(demo.OpenLan(true,"127.0.0.1",19378,"reject test","호스트"),Is.True);yield return null;
            var net=demo.Network;var room=net.Authority;Assert.That(room.Join(99,"가짜",out _),Is.True);
            // Remote peer without a transport connection: its commands go through the same host Process path.
            var process=typeof(LanNetworkSession).GetMethod("Process",flags);
            process.Invoke(net,new object[]{99UL,new LanCommand{session=room.Session,sequence=1,kind=LanCommandKind.Ready,ready=true}});
            net.Send(new LanCommand{kind=LanCommandKind.Ready,ready=true});net.Send(new LanCommand{kind=LanCommandKind.Prepare});
            net.Send(new LanCommand{kind=LanCommandKind.ConfirmAssignment});
            process.Invoke(net,new object[]{99UL,new LanCommand{session=room.Session,sequence=2,kind=LanCommandKind.ConfirmAssignment,version=room.Version}});
            Assert.That(room.Phase,Is.EqualTo(LanPhase.Playing));Assert.That(demo.LanView.activePlayer,Is.EqualTo(demo.LocalSeat));
            var end=(Button)typeof(DemoController).GetField("endButton",flags).GetValue(demo);Assert.That(end.interactable,Is.True);
            // Same rejection path as "헌금이 부족합니다": the host refuses the play synchronously.
            typeof(DemoController).GetMethod("SendLanPlay",flags).Invoke(demo,new object[]{System.Guid.NewGuid(),new EffectTarget{PlayerIndex=1}});
            Assert.That(demo.LanError,Is.Not.Empty);Assert.That(room.Busy,Is.False);Assert.That(net.Pending,Is.False);
            Assert.That(end.interactable,Is.True,"a rejected play must not lock the end-turn button");
            end.onClick.Invoke();Assert.That(room.Game.ActivePlayerIndex,Is.Not.EqualTo(demo.LocalSeat));
            demo.LeaveLan();yield return new WaitForSecondsRealtime(.5f);
        }
        [UnityTest] public IEnumerator InvalidClientAddressAndPortShowErrorWithoutCreatingGame()
        {
            demo.SelectLanMode();Assert.That(demo.OpenLan(false,"invalid",7777,"room","name"),Is.False);yield return null;
            Assert.That(demo.LanError,Does.Contain("IPv4"));Assert.That(demo.Network,Is.Null);Assert.That(demo.Game,Is.Null);
            Assert.That(demo.OpenLan(true,"127.0.0.1",0,"room","name"),Is.False);yield return null;
            Assert.That(demo.Network,Is.Null);demo.LeaveLan();Assert.That(demo.IsModeSelectionOpen,Is.True);
        }
    }
}
