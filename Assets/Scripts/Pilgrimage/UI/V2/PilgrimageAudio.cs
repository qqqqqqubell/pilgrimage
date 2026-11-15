using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Pilgrimage.UI
{
    /// <summary>Client-local audio. Uses presentation events only; never sends commands or applies effects.</summary>
    public sealed class PilgrimageAudio : MonoBehaviour
    {
        public static PilgrimageAudio Instance {get;private set;}
        [Range(0,1)] public float bgmVolume=.45f;
        [Range(0,1)] public float sfxVolume=.8f;
        public float BgmVolume {get=>bgmVolume;set{bgmVolume=Mathf.Clamp01(value);PlayerPrefs.SetFloat("Pilgrimage.BGM",bgmVolume);}}
        public float SfxVolume {get=>sfxVolume;set{sfxVolume=Mathf.Clamp01(value);PlayerPrefs.SetFloat("Pilgrimage.SFX",sfxVolume);}}
        private sealed class Voice {public AudioSource source;public string key;public float gain,start;}
        private readonly List<Voice> voices=new List<Voice>();
        private readonly Dictionary<string,List<AudioClip>> bank=new Dictionary<string,List<AudioClip>>();
        private readonly Dictionary<string,float> lastPlayed=new Dictionary<string,float>();
        private readonly HashSet<CutInPresenter> cutIns=new HashSet<CutInPresenter>();
        private readonly System.Random random=new System.Random();
        private PilgrimageAudioSettings settings;
        private AudioSource current,next;
        private AudioListener fallbackListener;
        private int track,round=-1,actor=-1;
        private bool finished;
        private double nextStart;
        private float fade=1.5f,duck=1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatic()=>Instance=null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap(){if(Instance==null)new GameObject("Pilgrimage Audio").AddComponent<PilgrimageAudio>();}
        private AudioSource Source()
        {
            var s=gameObject.AddComponent<AudioSource>();Configure(s);return s;
        }
        private static void Configure(AudioSource s)
        {s.playOnAwake=false;s.loop=false;s.spatialBlend=0;s.ignoreListenerPause=true;s.dopplerLevel=0;}
        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;DontDestroyOnLoad(gameObject);
            settings=Resources.Load<PilgrimageAudioSettings>("PilgrimageAudioSettings");
            bgmVolume=PlayerPrefs.GetFloat("Pilgrimage.BGM",settings!=null?settings.bgmVolume:.45f);
            sfxVolume=PlayerPrefs.GetFloat("Pilgrimage.SFX",settings!=null?settings.sfxVolume:.8f);
            foreach(var clip in Resources.LoadAll<AudioClip>("sfx"))
            {
                string key=clip.name;int at=key.LastIndexOf('_');
                if(at>0&&int.TryParse(key.Substring(at+1),out _))key=key.Substring(0,at);
                if(!bank.TryGetValue(key,out var clips))bank[key]=clips=new List<AudioClip>();clips.Add(clip);
            }
            current=Source();next=Source();current.priority=next.priority=0;
            if(settings!=null&&settings.playlist!=null&&settings.playlist.Length>0&&settings.playlist.All(c=>c!=null))
            {
                fade=Mathf.Max(.01f,settings.crossfadeSeconds);
                current.clip=settings.playlist[0];double start=AudioSettings.dspTime+.1;
                current.PlayScheduled(start);ScheduleNext(start+current.clip.length);
            }
            SceneManager.sceneLoaded+=SceneLoaded;
        }
        private void SceneLoaded(Scene scene,LoadSceneMode mode)=>EnsureListener();
        private void EnsureListener()
        {
            bool external=FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Any(l=>l!=fallbackListener&&l.isActiveAndEnabled);
            if(fallbackListener==null&&!external)fallbackListener=gameObject.AddComponent<AudioListener>();
            if(fallbackListener!=null)fallbackListener.enabled=!external;
        }
        private void ScheduleNext(double end)
        {
            int following=(track+1)%settings.playlist.Length;
            next.clip=settings.playlist[following];
            fade=Mathf.Min(Mathf.Max(.01f,settings.crossfadeSeconds),current.clip.length*.5f,next.clip.length*.5f);
            nextStart=Math.Max(end-fade,AudioSettings.dspTime+.02);next.volume=0;next.PlayScheduled(nextStart);
        }
        private void Update()
        {
            cutIns.RemoveWhere(c=>c==null||!c.IsPlaying);
            duck=Mathf.MoveTowards(duck,cutIns.Count>0?(settings!=null?settings.cutInDuck:.35f):1,Time.unscaledDeltaTime*4);
            if(current.clip!=null)
            {
                float t=Mathf.Clamp01((float)((AudioSettings.dspTime-nextStart)/fade));
                float volume=Mathf.Clamp01(bgmVolume)*duck;
                current.volume=volume*Mathf.Cos(t*Mathf.PI*.5f);next.volume=volume*Mathf.Sin(t*Mathf.PI*.5f);
                if(t>=1)
                {
                    double end=nextStart+next.clip.length;
                    current.Stop();var old=current;current=next;next=old;track=(track+1)%settings.playlist.Length;
                    ScheduleNext(end);
                }
            }
            foreach(var voice in voices)if(voice.source!=null)voice.source.volume=voice.gain*Mathf.Clamp01(sfxVolume);
        }
        /// <summary>Reuse the presenter's existing AudioSource as a pooled local voice.</summary>
        public void Register(AudioSource source)
        {
            if(source==null||voices.Any(v=>v.source==source))return;
            Configure(source);voices.RemoveAll(v=>v.source==null);
            int limit=settings!=null?Mathf.Clamp(settings.voices,1,32):12;
            if(voices.Count<limit)voices.Insert(0,new Voice{source=source});
        }
        public void Play(string key,float gain=1,float minInterval=.07f)
        {
            if(!bank.TryGetValue(key,out var clips)||clips.Count==0)return;
            PlayClip(key,clips[random.Next(clips.Count)],gain,minInterval);
        }
        public void PlayClip(string key,AudioClip clip,float gain=1,float minInterval=.07f)
        {
            if(clip==null)return;float now=Time.unscaledTime;
            if(lastPlayed.TryGetValue(key,out float last)&&now-last<minInterval)return;
            voices.RemoveAll(v=>v.source==null);
            if(voices.Count(v=>v.key==key&&v.source.isPlaying)>=2)return;
            var voice=voices.FirstOrDefault(v=>!v.source.isPlaying);
            int limit=settings!=null?Mathf.Clamp(settings.voices,1,32):12;
            if(voice==null&&voices.Count<limit){voice=new Voice{source=Source()};voices.Add(voice);}
            if(voice==null)voice=voices.OrderBy(v=>v.start).First();
            lastPlayed[key]=now;voice.key=key;voice.gain=Mathf.Clamp01(gain);voice.start=now;
            voice.source.Stop();voice.source.clip=clip;voice.source.volume=voice.gain*Mathf.Clamp01(sfxVolume);
            voice.source.pitch=1+(float)(random.NextDouble()*.08-.04);voice.source.Play();
        }
        public void BeginCutIn(CutInPresenter cutIn,float seconds)
        {cutIns.Add(cutIn);StartCoroutine(Whoosh(cutIn,seconds));}
        private IEnumerator Whoosh(CutInPresenter cutIn,float seconds)
        {yield return new WaitForSecondsRealtime(seconds*.125f);if(cutIn!=null&&cutIn.IsPlaying)Play("cutin_whoosh");}
        public void EndCutIn(CutInPresenter cutIn)=>cutIns.Remove(cutIn);
        public void Present(GameVisualEvent e,bool customCoins=false,bool customImpact=false)
        {
            var changes=e.changes??Array.Empty<StatusDelta>();
            if(e.cause==VisualCause.Card)Play("card_play");
            if(changes.Any(c=>c.kind==StatusKind.Draw))Play("card_draw");
            if(changes.Any(c=>c.kind==StatusKind.Acquisition))Play(e.cause==VisualCause.Reward?"card_reward":"card_place");
            if(!customCoins&&changes.Any(c=>c.kind==StatusKind.Offering&&c.amount>0))Play("coin_gain");
            if(changes.Any(c=>c.kind==StatusKind.Offering&&c.amount<0))Play("coin_spend");
            if(changes.Any(c=>c.kind==StatusKind.HereticConversion))Play("heresy");
            if(changes.Any(c=>c.kind==StatusKind.EffectStarted||c.kind==StatusKind.Defense||c.kind==StatusKind.FaithfulConversion||c.kind==StatusKind.FollowerConversion))Play("buff");
            if(changes.Any(c=>c.kind==StatusKind.Faith||c.kind==StatusKind.Doctrine))
            {
                if(changes.Any(c=>(c.kind==StatusKind.Faith||c.kind==StatusKind.Doctrine)&&c.amount>0)){Play("buff");Play("buff_sparkle",.5f);}
                if(changes.Any(c=>(c.kind==StatusKind.Faith||c.kind==StatusKind.Doctrine)&&c.amount<0))Play("debuff");
            }
            bool hit=e.cause==VisualCause.Card&&changes.Any(c=>c.player!=e.actor&&c.amount<0&&(c.kind==StatusKind.Faith||c.kind==StatusKind.Doctrine||c.kind==StatusKind.Followers));
            if(hit&&!customImpact){Play("hit_heavy");Play("hit_slash",.7f);}
        }
        public void ResetGameplay(){round=actor=-1;finished=false;}
        public void Observe(int nextRound,int nextActor,GamePhase phase,bool localWinner)
        {
            if(round<0)Play("deck_open");
            if(round!=nextRound)Play("round_start");
            if(phase==GamePhase.Actions&&actor!=nextActor&&actor>=0)Play("turn_change");
            if(phase==GamePhase.Finished&&!finished){Play(localWinner?"jingle_win":"jingle_lose");finished=true;}
            round=nextRound;actor=nextActor;
        }
        private void OnDestroy()
        {
            if(Instance!=this)return;
            PlayerPrefs.SetFloat("Pilgrimage.BGM",Mathf.Clamp01(bgmVolume));PlayerPrefs.SetFloat("Pilgrimage.SFX",Mathf.Clamp01(sfxVolume));
            SceneManager.sceneLoaded-=SceneLoaded;Instance=null;
        }
    }
}
