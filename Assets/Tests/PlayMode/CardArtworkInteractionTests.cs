using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pilgrimage.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
namespace Pilgrimage.Tests
{
    public sealed class CardArtworkInteractionTests
    {
        private DemoController demo;
        [UnityTearDown] public IEnumerator Cleanup(){if(demo!=null)Object.Destroy(demo.gameObject);yield return null;}
        [UnityTest] public IEnumerator CatalogMapsAll45IdsWithNumberedGeneralPairsAndAnonymousFactionBack()
        {
            var catalog=Resources.Load<CardVisualCatalog>("PilgrimageCardVisuals");
            Assert.That(catalog,Is.Not.Null);Assert.That(catalog.cards.Length,Is.EqualTo(45));
            Assert.That(catalog.cards.Select(c=>c.id).Distinct().Count(),Is.EqualTo(45));
            Assert.That(catalog.Get(CardId.Follower).number,Is.Zero);
            Assert.That(catalog.Get(CardId.Evangelism).number,Is.EqualTo(3));
            Assert.That(catalog.Get(CardId.Paradise).number,Is.EqualTo(21));
            foreach(var definition in CardCatalog.All)
            {
                var entry=catalog.Get(definition.Id);Assert.That(entry,Is.Not.Null,definition.Name);
                Assert.That(entry.back,Is.Not.Null,definition.Name);
                if(definition.Faction!=FactionId.None) Assert.That(entry.back,Is.SameAs(catalog.anonymousBack));
                if(definition.Id!=CardId.FurRally) Assert.That(entry.front,Is.Not.Null,definition.Name);
                else Assert.That(entry.front,Is.Null);
                if(entry.number>=0)
                {
                    Assert.That(entry.front.name,Is.EqualTo("General_"+entry.number.ToString("00")+"_Front"));
                    Assert.That(entry.back.name,Is.EqualTo("General_"+entry.number.ToString("00")+"_Back"));
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LongDescriptionsFitAndMissingFactionArtUsesNamedPlaceholder()
        {
            yield return SceneUITestFactory.Load(value=>demo=value);yield return null;
            demo.SelectSinglePlayer();demo.ChooseFaction(FactionId.Poverty);demo.AiConfiguration.autoPlay=false;demo.EnterCardLab();
            demo.SetLabHand(new[]{CardId.HeresyJudgment,CardId.HumbleTemple,CardId.FurRally});
            foreach(var view in demo.HandViews)
            {
                foreach(var other in demo.HandViews)other.SetHighlight(false);
                view.SetHighlight(true);yield return new WaitForSecondsRealtime(.3f);
                foreach(var label in view.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();Assert.That(label.isTextOverflowing,Is.False,view.Definition.Name+" / "+label.name);
                }
                if(view.Definition.Id==CardId.HeresyJudgment) SceneUITestFactory.Capture(demo,"-cardHoverScreenshot");
            }
            var missing=demo.HandViews.Single(v=>v.Definition.Id==CardId.FurRally);
            missing.SetHighlight(false);yield return new WaitForSecondsRealtime(.3f);
            Assert.That(missing.GetComponentsInChildren<TMP_Text>().Single().text,Is.EqualTo("털가죽 집회"));
        }
        [UnityTest] public IEnumerator HoverFlipsAndEnlargesCardThenRestoresFrontWithoutChangingIdentity()
        {
            yield return SceneUITestFactory.Load(value=>demo=value);yield return null;demo.SelectSinglePlayer();demo.ChooseFaction(FactionId.Poverty);demo.AiConfiguration.autoPlay=false;
            // Initial status presentation refreshes the hand after its first player-loop update.
            yield return null;yield return null;
            var view=demo.HandViews[0];var card=view.Card;var visual=(RectTransform)view.transform.Find("Card Border");var scale=visual.localScale;
            Assert.That(view.Face,Is.EqualTo(CardDisplayFace.Front));
            Assert.That(demo.CanInteract,Is.True,"Pointer input must be available before hover");
            view.OnPointerEnter(new PointerEventData(EventSystem.current));
            Assert.That(demo.SelectedCardId,Is.EqualTo(card.InstanceId),"Pointer enter must select the card");
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(demo.SelectedCardId,Is.EqualTo(card.InstanceId),"Pointer selection must remain while hovering");
            Assert.That(view==null,Is.False,"Hover target must remain the live hand view");
            Assert.That(view.Face,Is.Not.EqualTo(CardDisplayFace.Front),"Hovered card must show its description side");
            Assert.That(visual.localScale.x,Is.GreaterThan(scale.x*1.5f));
            var labels=view.GetComponentsInChildren<TMP_Text>();
            Assert.That(labels.Any(t=>t.text==card.Definition.Name),Is.True);
            Assert.That(labels.Any(t=>t.text==CardPresentation.PlayerText(card.Definition.Description)),Is.True);
            Assert.That(labels.Any(t=>t.text==CardPresentation.PlayerText(card.Definition.FlavorText)),Is.True);
            Assert.That(view.Card,Is.SameAs(card));
            view.OnPointerExit(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.35f);
            Assert.That(view.Face,Is.EqualTo(CardDisplayFace.Front));Assert.That(visual.localScale.x,Is.EqualTo(scale.x).Within(.001));
            Assert.That(view.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==card.Definition.Name),Is.False,"Front must remain illustration-only");
        }
    }
}
