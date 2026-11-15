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
        [SerializeField] private VisualAssetLibrary presentationAssets;
        private void BuildGameplayVisuals()
        {
            VisualAssets = presentationAssets != null ? presentationAssets : Resources.Load<VisualAssetLibrary>("PilgrimageVisualAssets");
            GameObject visualObject;
            if (buildingSceneUI)
            {
                visualObject = new GameObject("Gameplay Visual Presenters", typeof(RectTransform), typeof(GameplayVisualCoordinator), typeof(CutInPresenter), typeof(ImpactVfxPresenter), typeof(StatusPopupPresenter), typeof(AudioSource));
                visualObject.transform.SetParent(root, false);
                Stretch((RectTransform)visualObject.transform);
            }
            else visualObject = root.Find("Gameplay Visual Presenters").gameObject;
            Visuals = visualObject.GetComponent<GameplayVisualCoordinator>();
            Visuals.Initialize(this, (RectTransform)visualObject.transform, VisualAssets, VisualAnchor);
            Visuals.Completed+=()=>{if(isActiveAndEnabled&&(Game!=null||LanView!=null))Refresh();};
        }
        private Vector2 VisualAnchor(int player)
        {
            if(player<0)return new Vector2(20,130);
            int relative=IsLanMode?(player-LocalSeat+4)%4:player;
            return relative==0?new Vector2(820,-150):relative==1?new Vector2(820,410):relative==2?new Vector2(-820,410):new Vector2(-820,-150);
        }
        private float VisualScale=>Mathf.Clamp(aiSettings.presentationTimeScale,.02f,2);
        // Presentation only: game effects and LAN timing remain controlled by the caller.
        private IEnumerator ThrowSubmittedCard(int actor,float duration)
        {
            int relative=IsLanMode?(actor-LocalSeat+4)%4:actor;
            Rect bounds=root.rect;
            Vector2 outside=relative==0?new Vector2(bounds.width*.22f,bounds.yMin-320):
                relative==1?new Vector2(bounds.xMax+320,bounds.yMax*.45f):
                relative==2?new Vector2(bounds.xMin-320,bounds.yMax*.45f):
                new Vector2(bounds.xMin-320,bounds.yMin*.45f);
            var parent=(RectTransform)revealedCard.parent;
            Vector2 start=parent.InverseTransformPoint(root.TransformPoint(outside));
            Vector2 landing=new Vector2(0,6);
            float spin=relative==0||relative==1?-36:36;
            duration=Mathf.Max(.001f,duration);
            try
            {
                for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
                {
                    float progress=elapsed/duration;
                    if(progress<.8f)
                    {
                        float flight=progress/.8f;
                        float eased=1-Mathf.Pow(1-flight,3);
                        revealedCard.anchoredPosition=Vector2.Lerp(start,landing,eased)+Vector2.up*(Mathf.Sin(flight*Mathf.PI)*120);
                        revealedCard.localRotation=Quaternion.Euler(55*(1-eased),spin*(1-eased),spin*(1-eased));
                        revealedCard.localScale=Vector3.one*Mathf.Lerp(1.8f,1,eased);
                    }
                    else
                    {
                        float bounce=Mathf.Sin((progress-.8f)/.2f*Mathf.PI);
                        revealedCard.anchoredPosition=landing+Vector2.up*(bounce*4);
                        revealedCard.localRotation=Quaternion.identity;
                        revealedCard.localScale=new Vector3(1+.08f*bounce,1-.06f*bounce,1);
                    }
                    yield return null;
                }
            }
            finally
            {
                if(revealedCard!=null)
                {
                    revealedCard.anchoredPosition=landing;
                    revealedCard.localRotation=Quaternion.identity;
                    revealedCard.localScale=Vector3.one;
                }
            }
        }
        private IEnumerator PlayLocalCardWithVisuals(Guid id,EffectTarget target)
        {
            IsAnimating=true;endButton.interactable=false;
            try {
                var card=Game.Players[0].Hand.First(c=>c.InstanceId==id);var definition=card.Definition;
                Reveal(definition,false,id);
                var view=handViews.FirstOrDefault(v=>v.Card.InstanceId==id);
                float travel=Mathf.Clamp(aiSettings.travelDuration,.05f,2)*VisualScale;
                if(view!=null)view.gameObject.SetActive(false);
                yield return ThrowSubmittedCard(0,travel);
                if(VisualAssetLibrary.HasCutIn(definition)){Visuals.BeginCutIn(definition,VisualScale);yield return new WaitForSecondsRealtime(CutInPresenter.EffectMoment*VisualScale);}
                if(!Game.TryPlay(0,id,out string message,target)){Feedback(message);yield break;}
                messageText.text="";Log(message);Visuals.TimeScale=VisualScale;Visuals.Submit(Game.VisualEvents);Refresh();
                // Coordinator drains only the matching authoritative result; no callbacks apply effects.
                yield return null;while(Visuals.IsBusy)yield return null;
            }finally{IsAnimating=false;if(isActiveAndEnabled&&Game!=null)Refresh();}
        }
    }
}
