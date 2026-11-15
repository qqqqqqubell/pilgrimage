using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Pilgrimage.UI
{
    public enum ImpactKind { None, Hit1, Hit2, Explosion, Coins, Buff, Defense }
    public sealed class ImpactVfxPresenter : MonoBehaviour
    {
        private sealed class Slot {internal Image image;internal Sprite[] frames;internal float start,duration;internal bool active;internal ImpactKind kind;}
        private readonly List<Slot> pool=new List<Slot>();private DemoController owner;private RectTransform root;private VisualAssetLibrary assets;private Func<int,Vector2> anchor;
        public int PoolSize=>pool.Count;public int LastTarget {get;private set;}=-1;public ImpactKind LastKind {get;private set;}
        public void Initialize(DemoController controller,RectTransform parent,VisualAssetLibrary library,Func<int,Vector2> locate){owner=controller;root=parent;assets=library;anchor=locate;pool.Clear();
            if(controller.IsAuthoringUI) for(int i=0;i<96;i++){var element=owner.Panel(root,"Impact VFX",Vector2.zero,new Vector2(260,260),Color.white).GetComponent<Image>();element.raycastTarget=false;element.gameObject.SetActive(false);}
            foreach(Transform child in root) if(child.name=="Impact VFX"){var element=child.GetComponent<Image>();pool.Add(new Slot{image=element});element.gameObject.SetActive(false);}
        }
        public static ImpactKind Classify(GameVisualEvent e,int player)
        {
            var changes=e.changes.Where(d=>d.player==player).ToArray();
            bool damage=changes.Any(d=>(d.kind==StatusKind.Faith||d.kind==StatusKind.Doctrine||d.kind==StatusKind.Followers)&&d.amount<0||d.kind==StatusKind.HereticConversion||d.kind==StatusKind.RefusingConversion);
            if(e.cause==VisualCause.Card&&((player!=e.actor&&damage)||(e.card==CardId.AreaCleansing&&damage)||(e.card==CardId.WildRoar&&player!=e.actor&&changes.Any(d=>d.kind==StatusKind.Acquisition)))){
                if(e.card==CardId.MassAgitation||e.card==CardId.AreaCleansing||e.card==CardId.FurRally)return ImpactKind.Explosion;
                if(e.card==CardId.Reaper||e.card==CardId.HeresyJudgment||e.card==CardId.FalseRevelation||e.card==CardId.WildRoar)return ImpactKind.Hit2;
                return ImpactKind.Hit1;
            }
            if(changes.Any(d=>d.kind==StatusKind.Defense))return ImpactKind.Defense;
            if(changes.Any(d=>d.kind==StatusKind.Offering&&d.amount>0))return ImpactKind.Coins;
            if(changes.Any(d=>d.amount>0&&(d.kind==StatusKind.Faith||d.kind==StatusKind.Doctrine||d.kind==StatusKind.FaithfulConversion||d.kind==StatusKind.EffectStarted)))return ImpactKind.Buff;
            return ImpactKind.None;
        }
        public void Show(GameVisualEvent e,float scale=1)
        {

            foreach(int player in e.changes.Select(d=>d.player).Where(i=>i>=0).Distinct()){
                var kind=Classify(e,player);if(kind==ImpactKind.None)continue;var s=pool.Find(p=>!p.active);
                if(s==null){s=pool[0];s.active=false;}
                s.frames=assets==null?null:kind==ImpactKind.Hit1?assets.hit1:kind==ImpactKind.Hit2?assets.hit2:kind==ImpactKind.Explosion?assets.explosion1:kind==ImpactKind.Coins?assets.coins:null;
                s.kind=kind;s.start=Time.unscaledTime;s.duration=Mathf.Max(.04f,(kind==ImpactKind.Coins ? .8f : .5f)*scale);s.active=true;
                s.image.rectTransform.anchoredPosition=anchor(player);s.image.rectTransform.sizeDelta=new Vector2(kind==ImpactKind.Explosion?360:240,kind==ImpactKind.Explosion?360:240);
                s.image.gameObject.SetActive(true);s.image.preserveAspect=true;
                s.image.color=s.frames!=null?Color.white:kind==ImpactKind.Defense?new Color(.25f,.65f,1,.22f):new Color(.25f,1,.55f,.18f);
                s.image.sprite=s.frames?.FirstOrDefault();LastTarget=player;LastKind=kind;
            }
        }
        private void Update(){foreach(var s in pool){if(!s.active)continue;float t=(Time.unscaledTime-s.start)/s.duration;if(t>=1){s.active=false;s.image.gameObject.SetActive(false);continue;}
            if(s.frames!=null&&s.frames.Length>0){s.image.sprite=s.frames[Mathf.Clamp((int)(t*s.frames.Length),0,s.frames.Length-1)];s.image.rectTransform.localScale=Vector3.one;}
            else {s.image.rectTransform.localScale=Vector3.one*Mathf.Lerp(.65f,1.4f,t);var c=s.image.color;c.a=(1-t)*.2f;s.image.color=c;}
        }}
        public void Clear(){foreach(var s in pool){s.active=false;s.image.gameObject.SetActive(false);}}
        private void OnDisable()=>Clear();
    }
}
