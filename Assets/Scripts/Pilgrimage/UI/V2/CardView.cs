using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Pilgrimage.UI
{
    public enum CardDisplayFace { Front, PrivateBack, Detail }

    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private CardVisualCatalog catalog;
        [SerializeField] private RectTransform visual;
        [SerializeField] private GameObject frontFace, backFace;
        [SerializeField] private Image frontImage, backImage;
        [SerializeField] private TMP_Text nameLabel, descriptionLabel, flavorLabel, costLabel, missingArtworkLabel;
        [Header("Hover / keyboard selection")]
        [SerializeField, Range(1.1f,3f)] private float hoverScale=1.5f;
        [SerializeField] private float hoverLift=240f;
        [SerializeField, Range(.05f,.6f)] private float flipDuration=.22f;
        public CardInstance Card {get;private set;}
        public CardDefinition Definition {get;private set;}
        public CardDisplayFace Face {get;private set;}
        public RectTransform Rect {get;private set;}
        public bool Highlighted {get;private set;}
        public CardVisualCatalog Catalog=>catalog;
        private DemoController owner;
        private Vector2 restPosition,visualOrigin;
        private Quaternion visualRotation;
        private Vector3 visualScale;
        private float restAngle,hoverProgress,flipProgress;
        private bool showReverse;
        private int handIndex;
        private bool readOnly;

        public void Bind(DemoController controller,CardInstance card,int index,float width)
        {
            Card=card;readOnly=false;handIndex=index;
            BindDefinition(controller,card.Definition);
            var slot=HandFanLayout.Calculate(index,owner.LocalHandCount,width,Rect.sizeDelta.x);
            restPosition=new Vector2(slot.X,slot.Y);restAngle=slot.Angle;
            RenderHover();
        }
        public void BindPublic(DemoController controller,CardDefinition definition)
        {
            Card=null;readOnly=true;
            BindDefinition(controller,definition);
            restPosition=Rect.anchoredPosition;restAngle=0;
            RenderHover();
        }
        private void BindDefinition(DemoController controller,CardDefinition definition)
        {
            if(hoverProgress>0||flipProgress>0){hoverProgress=flipProgress=0;showReverse=false;Highlighted=false;RenderHover();}
            owner=controller;Definition=definition;Rect=(RectTransform)transform;
            visualOrigin=visual.anchoredPosition;visualRotation=visual.localRotation;visualScale=visual.localScale;
            hoverProgress=flipProgress=0;showReverse=false;Highlighted=false;
            var entry=catalog!=null?catalog.Get(definition.Id):null;
            frontImage.sprite=entry?.front;backImage.sprite=entry?.back;
            missingArtworkLabel.text=definition.Name;
            missingArtworkLabel.gameObject.SetActive(frontImage.sprite==null);
            nameLabel.text=CardPresentation.PlayerText(definition.Name);
            descriptionLabel.text=CardPresentation.PlayerText(definition.Description);
            flavorLabel.text=CardPresentation.PlayerText(definition.FlavorText);
            costLabel.text=CardPresentation.CostLabel(definition);
            costLabel.gameObject.SetActive(!string.IsNullOrEmpty(costLabel.text));
            foreach(var label in GetComponentsInChildren<TMP_Text>(true)) owner.ApplyFont(label);
        }
        public void SetHighlight(bool selected)
        {
            Highlighted=selected;
            if(!selected)showReverse=false;
            if(Rect==null)return;
            if(!readOnly){Rect.anchoredPosition=restPosition;Rect.localRotation=Quaternion.Euler(0,0,restAngle);}
            if(selected)Rect.SetAsLastSibling();
        }
        private void Update()
        {
            if(Rect==null)return;
            float target=Highlighted?1:0;
            float reverseTarget=showReverse?1:0;
            if(Mathf.Approximately(hoverProgress,target)&&Mathf.Approximately(flipProgress,reverseTarget))return;
            float step=Time.unscaledDeltaTime/Mathf.Max(.05f,flipDuration);
            hoverProgress=Mathf.MoveTowards(hoverProgress,target,step);
            flipProgress=Mathf.MoveTowards(flipProgress,reverseTarget,step);
            RenderHover();
        }
        private void RenderHover()
        {
            bool reverse=flipProgress>=.5f;
            var face=reverse?CardDisplayFace.Detail:CardDisplayFace.Front;
            if(Face!=face)PilgrimageAudio.Instance?.Play("card_flip",.7f);
            Face=face;
            frontFace.SetActive(!reverse);backFace.SetActive(reverse);
            float amount=Mathf.SmoothStep(0,1,hoverProgress);
            visual.anchoredPosition=visualOrigin+Vector2.up*((readOnly?0:hoverLift)*amount);
            visual.localRotation=visualRotation*Quaternion.Euler(0,Mathf.Sin(flipProgress*Mathf.PI)*90,-restAngle*amount);
            visual.localScale=visualScale*Mathf.Lerp(1,hoverScale,amount);
        }
        public void RestoreSiblingOrder(){if(Rect!=null&&!readOnly)Rect.SetSiblingIndex(handIndex);}
        public void OnPointerEnter(PointerEventData eventData)
        {
            if(readOnly){SetHighlight(true);return;}
            if(owner.CanInteract)owner.SelectFromMouse(Card.InstanceId);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            showReverse=false;
            if(readOnly){SetHighlight(false);return;}
            owner.ReleaseMouseSelection(Card.InstanceId);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if(readOnly)
            {
                if(eventData.button==PointerEventData.InputButton.Right)ToggleFace();
                return;
            }
            if(!owner.CanInteract)return;
            owner.SelectFromMouse(Card.InstanceId);
            if(eventData.button==PointerEventData.InputButton.Right)ToggleFace();
            else if(eventData.button==PointerEventData.InputButton.Left&&eventData.clickCount==2)owner.PlaySelected();
        }
        public void ToggleFace(){showReverse=!showReverse;}
        public static Color CategoryColor(CardCategory category)
        {
            switch(category){case CardCategory.Follower:return new Color(.18f,.37f,.31f);case CardCategory.Heretic:return new Color(.43f,.16f,.24f);case CardCategory.Faction:return new Color(.41f,.32f,.15f);default:return new Color(.2f,.3f,.46f);}
        }
    }
}
