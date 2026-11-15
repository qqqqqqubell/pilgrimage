using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pilgrimage.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Pilgrimage.Tests
{
    public sealed class RestartInteractionTests
    {
        private DemoController demo;
        [UnitySetUp] public IEnumerator Setup(){yield return SceneUITestFactory.Load(value=>demo=value);yield return null;}
        [UnityTearDown] public IEnumerator Cleanup(){if(demo!=null)Object.Destroy(demo.gameObject);yield return null;yield return null;}

        private static void Finish(GameSession game)
        {
            for(int guard=0;guard<5000&&game.Phase!=GamePhase.Finished;guard++)
            {if(game.Phase==GamePhase.RewardChoice)game.AcknowledgeRewards();else game.EndActionTurn(game.ActivePlayerIndex);}
            Assert.That(game.Phase,Is.EqualTo(GamePhase.Finished));
        }
        private IEnumerator WaitForResult(){for(int i=0;i<900&&!demo.IsResultOpen;i++)yield return null;Assert.That(demo.IsResultOpen,Is.True);}
        private Button ResultButton(string name)=>demo.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        private static string Deal(GameSession game)=>string.Join(",",game.Players.SelectMany(p=>p.Hand.Concat(p.Deck)).Select(c=>(int)c.Definition.Id));

        [UnityTest] public IEnumerator SingleRestartCreatesFreshSessionWithSameFactionAndMenuButtonReturnsToMenu()
        {
            demo.SelectSinglePlayer();Assert.That(demo.ChooseFaction(FactionId.Gold),Is.True);demo.AiConfiguration.autoPlay=false;
            var first=demo.Game;Finish(first);demo.SendMessage("Refresh");
            yield return WaitForResult();
            ResultButton("결과 · 다시 하기").onClick.Invoke();yield return null;
            Assert.That(demo.IsResultOpen,Is.False);Assert.That(demo.Game,Is.Not.Null.And.Not.SameAs(first));
            Assert.That(demo.Game.HumanFaction,Is.EqualTo(FactionId.Gold));Assert.That(demo.Game.Round,Is.EqualTo(1));
            Assert.That(demo.Game.Phase,Is.EqualTo(GamePhase.Actions));Assert.That(demo.Game.Winners,Is.Empty);
            Assert.That(demo.IsAiRunning,Is.False);Assert.That(demo.IsAnimating,Is.False);Assert.That(demo.SelectedCardId,Is.Null);
            Assert.That(demo.IsFactionSelectionOpen,Is.False);Assert.That(demo.IsModeSelectionOpen,Is.False);
            Assert.That(demo.Game.Players.All(p=>p.Hand.Count==4&&p.TotalCardCount==15&&p.Offering==5&&p.Faith==50&&p.Doctrine==50),Is.True);
            Assert.That(demo.HandViews.Count,Is.EqualTo(4));
            Finish(demo.Game);demo.SendMessage("Refresh");
            yield return WaitForResult();
            ResultButton("결과 · 메인 메뉴").onClick.Invoke();yield return null;
            Assert.That(demo.IsModeSelectionOpen,Is.True);Assert.That(demo.Game,Is.Null);Assert.That(demo.IsResultOpen,Is.False);
            Assert.That(demo.HandViews.Count,Is.Zero);Assert.That(demo.Network,Is.Null);
            demo.SelectSinglePlayer();Assert.That(demo.ChooseFaction(FactionId.Fur),Is.True);Assert.That(demo.Game.Round,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator EverySinglePlayerMatchDealsFromANewSeed()
        {
            demo.SelectSinglePlayer();Assert.That(demo.ChooseFaction(FactionId.Poverty),Is.True);demo.AiConfiguration.autoPlay=false;
            string first=Deal(demo.Game);
            Assert.That(demo.RestartSingleGame(),Is.True);yield return null;
            Assert.That(Deal(demo.Game),Is.Not.EqualTo(first));
        }
    }
}
