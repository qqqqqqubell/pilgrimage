using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pilgrimage.UI;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Pilgrimage.Tests
{
    public sealed class SceneUIInteractionTests
    {
        [UnityTest] public IEnumerator StartupPreservesAuthoredGeometrySpritesAndStaticObjects()
        {
            DemoController demo=null;yield return SceneUITestFactory.Load(value=>demo=value);
            Assert.That(demo.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1), "Canvas must exist before Start");
            var title=demo.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Title");
            var image=demo.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Shared Table");
            title.rectTransform.anchoredPosition=new Vector2(123,456);title.fontSize=29;
            var texture=new Texture2D(2,2);var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);
            image.sprite=sprite;
            var staticIds=demo.GetComponentsInChildren<RectTransform>(true).Select(r=>r.GetEntityId()).ToArray();
            yield return null;
            Assert.That(title.rectTransform.anchoredPosition,Is.EqualTo(new Vector2(123,456)));
            Assert.That(title.fontSize,Is.EqualTo(29));Assert.That(image.sprite,Is.SameAs(sprite));
            Assert.That(demo.GetComponentsInChildren<RectTransform>(true).Select(r=>r.GetEntityId()),Is.EquivalentTo(staticIds));
            demo.SelectSinglePlayer();Assert.That(demo.ChooseFaction(FactionId.Poverty),Is.True);
            Assert.That(demo.HandViews.Count,Is.EqualTo(4));
            var cardView=demo.HandViews[0];var visual=(RectTransform)cardView.transform.Find("Card Border");
            visual.anchoredPosition=new Vector2(7,9);visual.localScale=new Vector3(.9f,.8f,1);
            var art=visual.Find("Front Face").GetComponent<Image>();var expectedArt=cardView.Catalog.Get(cardView.Card.Definition.Id).front;
            int childCount=cardView.GetComponentsInChildren<RectTransform>(true).Length;
            cardView.Bind(demo,cardView.Card,0,1300);cardView.SetHighlight(true);cardView.SetHighlight(false);
            Assert.That(visual.anchoredPosition,Is.EqualTo(new Vector2(7,9)));
            Assert.That(visual.localScale,Is.EqualTo(new Vector3(.9f,.8f,1)));
            Assert.That(art.sprite,Is.SameAs(expectedArt));
            Assert.That(cardView.GetComponentsInChildren<RectTransform>(true).Length,Is.EqualTo(childCount));
            demo.EnterCardLab();demo.SendMessage("OpenLabPanel");
            var rows=demo.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="Card Lab Entry").ToArray();
            var rowIds=rows.Select(t=>t.GetEntityId()).ToArray();
            var decoration=new GameObject("Authored laboratory decoration",typeof(RectTransform),typeof(Image));
            decoration.transform.SetParent(rows[0].transform.parent,false);
            var search=demo.GetComponentsInChildren<TMP_InputField>(true).Single(f=>f.name=="카드 이름 검색");
            search.text="포고령";yield return null;search.text="";yield return null;
            Assert.That(demo.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="Card Lab Entry").Select(t=>t.GetEntityId()),Is.EquivalentTo(rowIds));
            Assert.That(image.sprite,Is.SameAs(sprite));
            Assert.That(decoration.activeSelf,Is.True,"Refreshing data must preserve added scene decorations");
            Object.Destroy(demo.gameObject);Object.Destroy(sprite);Object.Destroy(texture);yield return null;
        }
    }
}
