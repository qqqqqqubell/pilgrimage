using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pilgrimage.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Pilgrimage.Tests
{
    public sealed class VisualInteractionTests
    {
        private DemoController demo;
        [UnitySetUp]public IEnumerator Setup(){yield return SceneUITestFactory.Load(value=>demo=value);yield return null;demo.SelectSinglePlayer();demo.ChooseFaction(FactionId.Poverty);demo.AiConfiguration.autoPlay=false;yield return null;}
        [UnityTearDown]public IEnumerator Cleanup(){if(demo!=null)Object.Destroy(demo.gameObject);yield return null;}
        private IEnumerator Idle(){float end=Time.realtimeSinceStartup+6;do{yield return null;}while((demo.IsAnimating||demo.Visuals.IsBusy)&&Time.realtimeSinceStartup<end);Assert.That(demo.IsAnimating||demo.Visuals.IsBusy,Is.False);}
        [UnityTest]public IEnumerator AuthoredPlaceholdersRemainEditableAndNeverSpawnAnotherCanvas()
        {
            Assert.That(demo.VisualAssets,Is.Null);
            var background=demo.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Background");
            Assert.That(background.sprite,Is.Null);Assert.That(background.color,Is.EqualTo(Color.white));
            Assert.That(background.raycastTarget,Is.False);
            Assert.That(demo.GetComponentsInChildren<Canvas>(true).Length,Is.EqualTo(1));
            var table=demo.transform.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name=="Shared Table");
            table.anchoredPosition+=new Vector2(17,23);var authored=table.anchoredPosition;
            demo.SendMessage("Refresh");yield return null;
            Assert.That(table.anchoredPosition,Is.EqualTo(authored));
            Assert.That(demo.GetComponentsInChildren<Button>().Single(b=>b.name=="행동 종료").targetGraphic.raycastTarget,Is.True);
        }
        [UnityTest]public IEnumerator FourPortraitTimelinesPlayOnceWithoutMutatingGameOrTimeScale()
        {
            float scale=Time.timeScale;var cut=demo.Visuals.CutIn;int flashes=0,exits=0;cut.Flash+=()=>flashes++;cut.ExitStarted+=()=>exits++;
            foreach(var id in new[]{CardId.HumbleTemple,CardId.Indulgence,CardId.Archbishop,CardId.WildRoar}){
                int money=demo.Game.Players[0].Offering;int count=cut.PlayCount;var c=CardCatalog.Get(id);
                yield return cut.Play(c);
                Assert.That(cut.PlayCount,Is.EqualTo(count+1));Assert.That(cut.IsPlaying,Is.False);Assert.That(demo.Game.Players[0].Offering,Is.EqualTo(money));Assert.That(Time.timeScale,Is.EqualTo(scale));
            }
            Assert.That(flashes,Is.EqualTo(4));Assert.That(exits,Is.EqualTo(4));int before=cut.PlayCount;
            yield return cut.Play(CardCatalog.Get(CardId.Evangelism));yield return cut.Play(CardCatalog.Get(CardId.FaithfulFollower));Assert.That(cut.PlayCount,Is.EqualTo(before));
        }
        [UnityTest]public IEnumerator SuccessfulFactionCardsPopActualResultsAndFailedCardsNeverStartCutIn()
        {
            demo.AiConfiguration.presentationTimeScale=.15f;demo.EnterCardLab();
            foreach(var id in new[]{CardId.HumbleTemple,CardId.Indulgence,CardId.Archbishop,CardId.WildRoar}){
                demo.SetLabHand(new[]{id});GameDebugTools.SetResources(demo.Game,0,200,50,50);demo.SendMessage("Refresh");
                var source=demo.Game.Players[0].Hand.Single();int plays=demo.Visuals.CutIn.PlayCount,serial=demo.Game.LastVisualEvent.serial;
                demo.Select(source.InstanceId);demo.PlaySelected();demo.PlaySelected();Assert.That(demo.IsAnimating,Is.True);yield return Idle();
                Assert.That(demo.Visuals.CutIn.PlayCount,Is.EqualTo(plays+1));Assert.That(demo.Game.FindCard(source.InstanceId).Pile,Is.EqualTo(CardPile.Discard));Assert.That(demo.Game.VisualEvents.Count(e=>e.cause==VisualCause.Card&&e.source==source.InstanceId.ToString("D")),Is.EqualTo(1));
                Assert.That(demo.Game.LastVisualEvent.serial,Is.GreaterThan(serial));Assert.That(demo.CanInteract,Is.True);Assert.That(demo.GetComponentsInChildren<Button>().Single(b=>b.name=="행동 종료").interactable,Is.True);
                foreach(var delta in demo.Game.LastVisualEvent.changes)Assert.That(demo.Visuals.Popups.RecentTexts,Does.Contain(StatusPopupPresenter.Label(delta)));
            }
            demo.SetLabHand(new[]{CardId.HumbleTemple});GameDebugTools.SetResources(demo.Game,0,0,50,50);int before=demo.Visuals.CutIn.PlayCount;
            demo.Select(demo.Game.Players[0].Hand.Single().InstanceId);demo.PlaySelected();yield return null;Assert.That(demo.IsAnimating,Is.False);Assert.That(demo.Visuals.CutIn.PlayCount,Is.EqualTo(before));
        }
        [UnityTest]public IEnumerator SnapshotReplayDoesNotDuplicatePopupsAndRefreshDoesNotDestroyPresenters()
        {
            demo.AiConfiguration.presentationTimeScale=.05f;demo.EnterCardLab();demo.SetLabHand(new[]{CardId.RequestOffering});var cut=demo.Visuals.CutIn;var pop=demo.Visuals.Popups;
            demo.Select(demo.Game.Players[0].Hand.Single().InstanceId);demo.PlaySelected();yield return Idle();int shown=pop.ShownCount,pool=pop.PoolSize;
            demo.Visuals.Submit(demo.Game.VisualEvents);demo.Visuals.Submit(demo.Game.VisualEvents);demo.SendMessage("Refresh");yield return null;
            Assert.That(pop.ShownCount,Is.EqualTo(shown));Assert.That(pop.PoolSize,Is.EqualTo(pool));Assert.That(demo.Visuals.CutIn,Is.SameAs(cut));Assert.That(demo.Visuals.Popups,Is.SameAs(pop));
            Assert.That(pop.RecentTexts,Does.Contain("헌금 +5"));Assert.That(cut.PlayCount,Is.Zero);
        }
        [UnityTest]public IEnumerator ImpactClassificationUsesActualVictimsAndNeverTreatsRoundDecayAsAttack()
        {
            var blocked=new GameVisualEvent{cause=VisualCause.Card,actor=0,card=CardId.FalseRevelation,changes=new[]{new StatusDelta{player=1,kind=StatusKind.Defense,amount=1}}};
            Assert.That(ImpactVfxPresenter.Classify(blocked,1),Is.EqualTo(ImpactKind.Defense));
            blocked.changes=new[]{new StatusDelta{player=1,kind=StatusKind.Faith,amount=-10}};Assert.That(ImpactVfxPresenter.Classify(blocked,1),Is.EqualTo(ImpactKind.Hit2));
            blocked.card=CardId.AreaCleansing;Assert.That(ImpactVfxPresenter.Classify(blocked,1),Is.EqualTo(ImpactKind.Explosion));
            blocked.cause=VisualCause.NaturalDecay;Assert.That(ImpactVfxPresenter.Classify(blocked,1),Is.EqualTo(ImpactKind.None));
            demo.Visuals.Impact.Show(new GameVisualEvent{cause=VisualCause.Card,actor=0,card=CardId.PublicDenunciation,changes=new[]{new StatusDelta{player=3,kind=StatusKind.Followers,amount=-1}}});
            Assert.That(demo.Visuals.Impact.LastTarget,Is.EqualTo(3));Assert.That(demo.Visuals.Impact.LastKind,Is.EqualTo(ImpactKind.Hit1));yield return null;
        }
        [UnityTest]public IEnumerator BurstResultsWaitForTheirOwnRevealAndReusedCardGuidsUseDistinctEventNumbers()
        {
            var v=demo.Visuals;v.ResetSession();v.TimeScale=.05f;string source=System.Guid.NewGuid().ToString("D");int shown=v.Popups.ShownCount;
            v.ReserveReveal(source,10);v.ReserveReveal(source,11);
            v.Submit(new[]{new GameVisualEvent{serial=1000,presentation=10,source=source,cause=VisualCause.Card,changes=new[]{new StatusDelta{player=0,kind=StatusKind.Faith,amount=3}}},
                new GameVisualEvent{serial=1001,presentation=11,source=source,cause=VisualCause.Card,changes=new[]{new StatusDelta{player=0,kind=StatusKind.Faith,amount=4}}}});
            yield return new WaitForSecondsRealtime(.1f);Assert.That(v.Popups.ShownCount,Is.EqualTo(shown));
            v.ReleaseReveal(source,10);yield return new WaitForSecondsRealtime(.2f);Assert.That(v.HasPresented(source,10),Is.True);Assert.That(v.HasPresented(source,11),Is.False);Assert.That(v.Popups.ShownCount,Is.EqualTo(shown+1));
            v.ReleaseReveal(source,11);yield return new WaitForSecondsRealtime(.2f);Assert.That(v.HasPresented(source,11),Is.True);Assert.That(v.Popups.ShownCount,Is.EqualTo(shown+2));
        }
    }
}
