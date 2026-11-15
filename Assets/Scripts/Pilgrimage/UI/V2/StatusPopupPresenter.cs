using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Pilgrimage.UI
{
    public sealed class StatusPopupPresenter : MonoBehaviour
    {
        private sealed class Slot { internal TMP_Text text;internal Vector2 origin;internal float start,duration;internal int seat,row;internal bool active; }
        private readonly List<Slot> pool=new List<Slot>();
        public int PoolSize=>pool.Count;
        public int ShownCount {get;private set;}
        public readonly List<string> RecentTexts=new List<string>();
        private DemoController owner;private RectTransform root;private Func<int,Vector2> anchor;
        public void Initialize(DemoController controller,RectTransform parent,Func<int,Vector2> locate){owner=controller;root=parent;anchor=locate;pool.Clear();
            if(controller.IsAuthoringUI) for(int i=0;i<96;i++){var element=owner.Text(root,"Status Popup", "상태 변화",Vector2.zero,new Vector2(390,40),25);element.raycastTarget=false;element.gameObject.SetActive(false);}
            foreach(Transform child in root) if(child.name=="Status Popup"){var element=child.GetComponent<TMP_Text>();pool.Add(new Slot{text=element});element.gameObject.SetActive(false);}
        }
        public static string Label(StatusDelta d)
        {
            string sign=d.amount.ToString("+#;-#;0");
            switch(d.kind){case StatusKind.Offering:return "헌금 "+sign;case StatusKind.Faith:return "신앙심 "+sign;case StatusKind.Doctrine:return "교리 정합성 "+sign;
                case StatusKind.Followers:return "신도 "+sign;case StatusKind.Property:return "교회 재산 "+sign;case StatusKind.Atheists:return "무신론자 "+sign;
                case StatusKind.HereticConversion:return "신도 "+d.amount+"명 이단화";case StatusKind.FollowerConversion:return "이단 "+d.amount+"명 신도화";
                case StatusKind.FaithfulConversion:return "신실한 신도 "+d.amount+"명 변환";case StatusKind.RefusingConversion:return "거부하는 신도 "+d.amount+"명 변환";
                case StatusKind.Draw:return "카드 "+d.amount+"장 드로우";case StatusKind.Acquisition:return "카드 "+d.amount+"장 획득";
                case StatusKind.EffectStarted:return "지속 효과 적용";case StatusKind.EffectEnded:return "지속 효과 종료";case StatusKind.Defense:return "방어 성공!";default:return "";}
        }
        public void Show(GameVisualEvent e,float scale=1)
        {
            string prefix=e.cause==VisualCause.Card?"":e.cause==VisualCause.NaturalDecay?"자연 감소 · ":e.cause==VisualCause.RoundIncome?"수입 · ":e.cause==VisualCause.RoundDraw?"라운드 · ":e.cause==VisualCause.Reward?"보상 · ":e.cause==VisualCause.Delayed?"지연 효과 · ":e.cause==VisualCause.Threshold?"임계값 · ":e.cause==VisualCause.Duration?"기간 · ":"턴 종료 · ";
            foreach(var delta in e.changes){var s=pool.Find(p=>!p.active);if(s==null){s=pool[0];s.active=false;}
                int row=0;while(pool.Exists(p=>p.active&&p.seat==delta.player&&p.row==row))row++;
                s.seat=delta.player;s.row=row;s.origin=anchor(delta.player)+new Vector2((row/6)*210-100,(row%6)*32);
                s.start=Time.unscaledTime;s.duration=Mathf.Max(.04f,.8f*scale);s.active=true;s.text.gameObject.SetActive(true);
                s.text.text=prefix+Label(delta);bool bad=delta.amount<0||delta.kind==StatusKind.HereticConversion||delta.kind==StatusKind.RefusingConversion;
                s.text.color=Color.black;
                ShownCount++;RecentTexts.Add(s.text.text);if(RecentTexts.Count>64)RecentTexts.RemoveAt(0);
            }
        }
        private void Update()
        {
            foreach(var s in pool){if(!s.active)continue;float t=(Time.unscaledTime-s.start)/s.duration;if(t>=1){s.active=false;s.text.gameObject.SetActive(false);continue;}
                float size=t<.2f?Mathf.Lerp(.7f,1.15f,t/.2f):t<.4f?Mathf.Lerp(1.15f,1,(t-.2f)/.2f):1;
                s.text.rectTransform.localScale=Vector3.one*size;s.text.rectTransform.anchoredPosition=s.origin+Vector2.up*(t*40);
                var color=s.text.color;color.a=t<.55f?1:1-(t-.55f)/.45f;s.text.color=color;
            }
        }
        public void Clear(){foreach(var s in pool){s.active=false;s.text.gameObject.SetActive(false);}RecentTexts.Clear();}
        private void OnDisable()=>Clear();
    }
}
