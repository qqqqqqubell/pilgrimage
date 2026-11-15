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
