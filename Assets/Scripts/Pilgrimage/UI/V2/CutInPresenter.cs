using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Pilgrimage.UI
{
    public sealed class CutInPresenter : MonoBehaviour
    {
        public const float Duration=.8f, EffectMoment=.6f;
        public bool IsPlaying { get; private set; }
        public FactionId CurrentFaction { get; private set; }
        public int PlayCount { get; private set; }
        public int Frame { get; private set; }
        public event Action<float> Started;
        public event Action Stopped;
        public event Action Flash; // Optional sound hook only; no game logic.
        public event Action ExitStarted;
        private VisualAssetLibrary assets;private VisualAssetLibrary.Timeline timeline;
        [SerializeField] private RectTransform overlay,frame;[SerializeField] private Image dim,bg,mask,portrait,fg;
        public void Initialize(DemoController owner,RectTransform parent,VisualAssetLibrary library)
        {
            assets=library;if(assets!=null&&assets.cutInMetadata!=null)timeline=JsonUtility.FromJson<VisualAssetLibrary.Timeline>(assets.cutInMetadata.text);
            if(!owner.IsAuthoringUI){portrait.rectTransform.localRotation=Quaternion.Euler(0,0,11);ResetVisuals();return;}
            overlay=owner.Panel(parent,"Cut-in Overlay",Vector2.zero,Vector2.zero,Color.clear);Fill(overlay);
            dim=overlay.GetComponent<Image>();dim.raycastTarget=false;
            frame=owner.Panel(overlay,"Cut-in Frame",Vector2.zero,new Vector2(1920,1080),Color.clear);frame.GetComponent<Image>().raycastTarget=false;
            var fit=frame.gameObject.AddComponent<AspectRatioFitter>();fit.aspectRatio=16f/9;fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            bg=Layer(owner,frame,"Band BG");mask=Layer(owner,frame,"Portrait Alpha Mask");mask.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            portrait=Layer(owner,mask.rectTransform,"Faction Portrait");portrait.preserveAspect=true;
            fg=Layer(owner,frame,"Band FG");ResetVisuals();
        }
        private static Image Layer(DemoController owner,RectTransform parent,string name){var rect=owner.Panel(parent,name,Vector2.zero,Vector2.zero,Color.white);Fill(rect);var i=rect.GetComponent<Image>();i.raycastTarget=false;return i;}
        private static void Fill(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        public IEnumerator Play(CardDefinition card,float scale=1)
        {
            if(!VisualAssetLibrary.HasCutIn(card)||assets==null||assets.cutInBackground==null||assets.cutInBackground.Length!=24||assets.cutInMask==null||assets.cutInMask.Length!=24||assets.cutInForeground==null||assets.cutInForeground.Length!=24)yield break;
            IsPlaying=true;CurrentFaction=card.Faction;PlayCount++;if(assets!=null)portrait.sprite=assets.Portrait(card.Faction);overlay.gameObject.SetActive(true);
            int last=-1;float seconds=Mathf.Max(.02f,Duration*scale);Started?.Invoke(seconds);
            try {
                for(float t=0;t<seconds;t+=Time.unscaledDeltaTime){int f=Mathf.Clamp(Mathf.FloorToInt(t/seconds*24),0,23);
                    if(last<9&&f>=9)Flash?.Invoke();if(last<18&&f>=18)ExitStarted?.Invoke();
                    ApplyFrame(f);last=f;yield return null;}
                if(last<9)Flash?.Invoke();if(last<18)ExitStarted?.Invoke();
            }finally{ResetVisuals();}
        }
        public void ApplyFrame(int f)
        {
            Frame=Mathf.Clamp(f,0,23);f=Frame;
            if(assets==null||assets.cutInBackground==null||assets.cutInBackground.Length!=24||assets.cutInMask==null||assets.cutInMask.Length!=24||assets.cutInForeground==null||assets.cutInForeground.Length!=24) return;
            bg.sprite=assets.cutInBackground[f];mask.sprite=assets.cutInMask[f];fg.sprite=assets.cutInForeground[f];
            dim.color=new Color(0,0,0,timeline?.dim_alpha!=null&&f<timeline.dim_alpha.Length?timeline.dim_alpha[f]:.65f);
            var pose=timeline?.portrait!=null&&f<timeline.portrait.Length?timeline.portrait[f]:null;
            portrait.enabled=pose?.visible??(f>=3&&f<22);
            // Metadata coordinates are 960x540 frame pixels; the canvas is twice that size.
            portrait.rectTransform.anchoredPosition=pose==null?Vector2.zero:new Vector2(pose.x*2,pose.y*2);
            portrait.rectTransform.localScale=Vector3.one*(pose?.scale??1);
        }
        public void ResetVisuals(){bool wasPlaying=IsPlaying;IsPlaying=false;CurrentFaction=FactionId.None;if(overlay!=null)overlay.gameObject.SetActive(false);if(wasPlaying)Stopped?.Invoke();}
        private void OnDisable(){StopAllCoroutines();ResetVisuals();}
    }
}
