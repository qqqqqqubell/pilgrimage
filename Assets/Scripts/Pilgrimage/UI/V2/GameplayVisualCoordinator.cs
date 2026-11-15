using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pilgrimage.UI
{
    public sealed class GameplayVisualCoordinator : MonoBehaviour
    {
        public CutInPresenter CutIn {get;private set;}
        public ImpactVfxPresenter Impact {get;private set;}
        public StatusPopupPresenter Popups {get;private set;}
        public bool IsBusy=>processingCard||pendingReveals.Count>0||CutIn!=null&&CutIn.IsPlaying;
        public float TimeScale {get;set;}=1;
        public int LastEventSerial {get;private set;}
        public event Action Completed;
        public event Action<string> SoundCue;
        public AudioClip cutInFlashSound,impactSound,coinsSound;
        private AudioSource audioSource;private bool processingCard,resetting;private Coroutine drain;
        private readonly Queue<GameVisualEvent> queue=new Queue<GameVisualEvent>();
        private readonly HashSet<string> pendingReveals=new HashSet<string>(),processedCards=new HashSet<string>();
        private static string Key(string source,int serial)=>source+"#"+serial;
        public void ReserveReveal(string source,int serial)=>pendingReveals.Add(Key(source,serial));
        public void ReleaseReveal(string source,int serial)=>pendingReveals.Remove(Key(source,serial));
        public bool HasPresented(string source,int serial)=>processedCards.Contains(Key(source,serial));
        public void Initialize(DemoController owner,RectTransform parent,VisualAssetLibrary assets,Func<int,Vector2> anchor)
        {
            CutIn=GetComponent<CutInPresenter>()??gameObject.AddComponent<CutInPresenter>();
            Impact=GetComponent<ImpactVfxPresenter>()??gameObject.AddComponent<ImpactVfxPresenter>();
            Popups=GetComponent<StatusPopupPresenter>()??gameObject.AddComponent<StatusPopupPresenter>();
            var effects=owner.Panel(parent,"Persistent VFX Layer",Vector2.zero,Vector2.zero,Color.clear);effects.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            if(owner.IsAuthoringUI){effects.anchorMin=Vector2.zero;effects.anchorMax=Vector2.one;effects.offsetMin=effects.offsetMax=Vector2.zero;}
            Impact.Initialize(owner,effects,assets,anchor);Popups.Initialize(owner,effects,anchor);CutIn.Initialize(owner,parent,assets);
            audioSource=GetComponent<AudioSource>()??gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;
            if(!owner.IsAuthoringUI)
            {
                PilgrimageAudio.Instance?.Register(audioSource);
                CutIn.Started+=seconds=>PilgrimageAudio.Instance?.BeginCutIn(CutIn,seconds);
                CutIn.Stopped+=()=>PilgrimageAudio.Instance?.EndCutIn(CutIn);
                CutIn.Flash+=()=>PlaySound("cutin.flash",cutInFlashSound);
            }
        }
        private void PlaySound(string cue,AudioClip clip)
        {
            SoundCue?.Invoke(cue);
            if(clip!=null)PilgrimageAudio.Instance?.PlayClip(cue,clip);
            else if(cue=="cutin.flash")PilgrimageAudio.Instance?.Play("cutin_impact");
        }
        public void BeginCutIn(CardDefinition card,float scale)
        {if(VisualAssetLibrary.HasCutIn(card)){transform.SetAsLastSibling();StartCoroutine(CutIn.Play(card,scale));}}
        public void Submit(IEnumerable<GameVisualEvent> events)
        {
            if(events==null)return;
            foreach(var e in events.Where(e=>e.serial>LastEventSerial).OrderBy(e=>e.serial)) {queue.Enqueue(e);LastEventSerial=e.serial;}
            if(drain==null&&queue.Count>0)drain=StartCoroutine(Drain());
        }
        private IEnumerator Drain()
        {
            // Yield first so the coroutine handle is assigned even for non-card-only batches.
            yield return null;
            try {
                while(queue.Count>0){var e=queue.Dequeue();processingCard=e.cause==VisualCause.Card;
                    if(processingCard){while(pendingReveals.Contains(Key(e.source,e.presentation))||CutIn.IsPlaying)yield return null;}
                    Impact.Show(e,TimeScale);Popups.Show(e,TimeScale);
                    PilgrimageAudio.Instance?.Present(e,coinsSound!=null,impactSound!=null);
                    if(e.changes.Any(d=>d.kind==StatusKind.Offering&&d.amount>0))PlaySound("coins",coinsSound);
                    if(e.cause==VisualCause.Card)PlaySound("card.impact",impactSound);
                    if(processingCard)yield return new WaitForSecondsRealtime(Mathf.Max(.04f,.8f*TimeScale));
                    if(processingCard&&e.source!=null)processedCards.Add(Key(e.source,e.presentation));
                    processingCard=false;
                }
            }finally {processingCard=false;drain=null;if(!resetting)Completed?.Invoke();}
        }
        public void ResetSession(int serial=0){PilgrimageAudio.Instance?.ResetGameplay();resetting=true;try{StopAllCoroutines();drain=null;queue.Clear();pendingReveals.Clear();processedCards.Clear();processingCard=false;LastEventSerial=serial;CutIn?.ResetVisuals();Impact?.Clear();Popups?.Clear();}finally{resetting=false;}}
        private void OnDisable()=>ResetSession(LastEventSerial);
    }
}
