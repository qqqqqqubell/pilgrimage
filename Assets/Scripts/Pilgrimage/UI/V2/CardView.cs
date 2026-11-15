using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Pilgrimage.UI
{
    public enum CardDisplayFace { Front, PrivateBack, Detail }

    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public CardInstance Card { get; private set; }
        public CardDisplayFace Face { get; private set; }
        public RectTransform Rect { get; private set; }
        public bool Highlighted { get; private set; }
        private DemoController owner;
        private Image border;
        private RectTransform visual;
        private Vector2 restPosition;
        private float restAngle;
        private int handIndex;

        public void Bind(DemoController controller, CardInstance card, int index, float width)
        {
            owner = controller; Card = card; handIndex = index; Face = CardDisplayFace.Front;
            Rect = (RectTransform)transform;
            CardDefinition def = card.Definition;
            Color tint = CategoryColor(def.Category);
            GetComponent<Image>().color = Color.clear;
            visual = owner.Panel(Rect, "Card Border", Vector2.zero, new Vector2(200, 280), Color.black);
            border = visual.GetComponent<Image>();
            border.color = new Color(0.2f, 0.27f, 0.31f);
            RectTransform frame = owner.Panel(visual, "Card Frame", new Vector2(0, 0), new Vector2(188, 268), tint);
            Image frameImage = frame.GetComponent<Image>();
            frameImage.sprite = owner.FrameSprite(def.Category);
            if (frameImage.sprite != null) frameImage.color = Color.white;
            frameImage.raycastTarget = false;
            owner.Text(frame, "Card Number", $"#{(int)def.Id + 1:00}", new Vector2(0, 111), new Vector2(163, 25), 18);
            owner.Text(frame, "Card Name", def.Name, new Vector2(0, 76), new Vector2(172, 55), 25);
            RectTransform icon = owner.Panel(frame, "Illustration Slot", new Vector2(0, 4), new Vector2(155, 85), new Color(0, 0, 0, 0.25f));
            icon.GetComponent<Image>().raycastTarget = false;
            if (owner.Illustration != null) { icon.GetComponent<Image>().sprite = owner.Illustration; icon.GetComponent<Image>().color = Color.white; }
            else owner.Text(icon, "Placeholder Icon", def.CountsAsFollower ? "人" : "+", Vector2.zero, new Vector2(140, 75), 48);
            if (!(def.CountsAsFollower && def.CostKind == CardCostKind.None))
            {
                owner.Text(frame, "Category", def.Category == CardCategory.Faction ? "교단" : def.Category == CardCategory.Heretic ? "이단" : "일반", new Vector2(0, -57), new Vector2(175, 28), 17);
                if (CardPresentation.CostLabel(def) != "") owner.Text(frame, "Cost", CardPresentation.CostLabel(def), new Vector2(0, -88), new Vector2(177, 30), 20);
                if (CardPresentation.Availability(def) != "") owner.Text(frame, "Play State", "사용 불가", new Vector2(0, -116), new Vector2(180, 24), 13);
            }
            FanSlot slot = HandFanLayout.Calculate(index, owner.LocalHandCount, width, 200);
            restPosition = new Vector2(slot.X, slot.Y);
            restAngle = slot.Angle;
            SetHighlight(false);
        }

        public void SetHighlight(bool selected)
        {
            Highlighted = selected;
            if (Rect == null) return;
            Rect.anchoredPosition = restPosition;
            Rect.localRotation = Quaternion.Euler(0, 0, restAngle);
            // The transparent base remains a hit target while the visual lifts,
            // avoiding pointer enter/exit oscillation near the card's lower edge.
            visual.anchoredPosition = selected ? Vector2.up * 42 : Vector2.zero;
            visual.localRotation = Quaternion.Euler(0, 0, selected ? -restAngle : 0);
            visual.localScale = Vector3.one * (selected ? 1.04f : 1);
            border.color = selected ? new Color(0.62f, 1f, 0.15f) : new Color(0.2f, 0.27f, 0.31f);
            if (selected) Rect.SetAsLastSibling();
        }

        public void RestoreSiblingOrder() { if (Rect != null) Rect.SetSiblingIndex(handIndex); }
        public void OnPointerEnter(PointerEventData eventData) { if (owner.CanInteract) owner.SelectFromMouse(Card.InstanceId); }
        public void OnPointerExit(PointerEventData eventData) { owner.ReleaseMouseSelection(Card.InstanceId); }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!owner.CanInteract) return;
            owner.SelectFromMouse(Card.InstanceId);
            if (eventData.button == PointerEventData.InputButton.Right) owner.ToggleDetail();
            else if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2) owner.PlaySelected();
        }

        public static Color CategoryColor(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Follower: return new Color(0.18f, 0.37f, 0.31f);
                case CardCategory.Heretic: return new Color(0.43f, 0.16f, 0.24f);
                case CardCategory.Faction: return new Color(0.41f, 0.32f, 0.15f);
                default: return new Color(0.2f, 0.3f, 0.46f);
            }
        }
    }
}
