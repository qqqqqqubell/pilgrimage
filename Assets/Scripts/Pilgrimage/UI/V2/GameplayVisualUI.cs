using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Pilgrimage.UI
{
    public sealed partial class DemoController
    {
        public GameplayVisualCoordinator Visuals {get;private set;}
        public VisualAssetLibrary VisualAssets {get;private set;}
        private void BuildGameplayVisuals()
        {
            VisualAssets=Resources.Load<VisualAssetLibrary>("PilgrimageVisualAssets");
            if(VisualAssets==null){Debug.LogWarning("Pilgrimage: Visual library missing. Use Pilgrimage/Visuals/에셋 설정 및 라이브러리 갱신.");}
            else if(VisualAssets.background!=null){
                var container=root.Find("Background").GetComponent<RectTransform>();
                var art=Panel(container,"Red Table Background",Vector2.zero,Vector2.zero,Color.white);art.GetComponent<Image>().sprite=VisualAssets.background;art.GetComponent<Image>().raycastTarget=false;
                var fit=art.gameObject.AddComponent<AspectRatioFitter>();fit.aspectRatio=1920f/1080;fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
                art.SetAsFirstSibling();
            }
            foreach(var image in root.GetComponentsInChildren<Image>(true))if(image.name.StartsWith("Player ")&&image.name.EndsWith("Table"))image.color=new Color(.09f,.035f,.035f,.9f);
            var table=root.Find("Shared Table").GetComponent<Image>();table.color=new Color(.035f,.015f,.02f,.25f);
            var prefab=Resources.Load<GameObject>("PilgrimageGameplayVisuals");
            var visualObject=prefab!=null?Instantiate(prefab,root,false):new GameObject("Gameplay Visual Presenters",typeof(RectTransform));
            if(prefab==null)visualObject.transform.SetParent(root,false);
            Visuals=visualObject.GetComponent<GameplayVisualCoordinator>()??visualObject.AddComponent<GameplayVisualCoordinator>();
            Visuals.Initialize(this,(RectTransform)visualObject.transform,VisualAssets,VisualAnchor);
            var r=(RectTransform)visualObject.transform;Stretch(r);
            Visuals.Completed+=()=>{if(isActiveAndEnabled&&(Game!=null||LanView!=null))Refresh();};
        }
        private Vector2 VisualAnchor(int player)
        {
            if(player<0)return new Vector2(20,130);
            int relative=IsLanMode?(player-LocalSeat+4)%4:player;
            return relative==0?new Vector2(-130,-405):relative==1?new Vector2(-80,300):relative==2?new Vector2(-730,100):new Vector2(730,100);
        }
        private float VisualScale=>Mathf.Clamp(aiSettings.presentationTimeScale,.02f,2);
        private IEnumerator PlayLocalCardWithVisuals(Guid id,EffectTarget target)
        {
            IsAnimating=true;endButton.interactable=false;
            try {
                var card=Game.Players[0].Hand.First(c=>c.InstanceId==id);var definition=card.Definition;
                Reveal(definition,false,id);
                var view=handViews.FirstOrDefault(v=>v.Card.InstanceId==id);
                float travel=Mathf.Clamp(aiSettings.travelDuration,.05f,2)*VisualScale;
                if(view!=null){var start=view.Rect.anchoredPosition;var group=view.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;view.Rect.SetAsLastSibling();
                    for(float t=0;t<travel;t+=Time.unscaledDeltaTime){float f=t/travel;view.Rect.anchoredPosition=start+Vector2.up*(f*260);group.alpha=1-f;yield return null;}}
                if(VisualAssetLibrary.HasCutIn(definition)){Visuals.BeginCutIn(definition,VisualScale);yield return new WaitForSecondsRealtime(CutInPresenter.EffectMoment*VisualScale);}
                if(!Game.TryPlay(0,id,out string message,target)){Feedback(message);yield break;}
                Log(message);Visuals.TimeScale=VisualScale;Visuals.Submit(Game.VisualEvents);Refresh();
                // Coordinator drains only the matching authoritative result; no callbacks apply effects.
                yield return null;while(Visuals.IsBusy)yield return null;
            }finally{IsAnimating=false;if(isActiveAndEnabled&&Game!=null)Refresh();}
        }
    }
}
