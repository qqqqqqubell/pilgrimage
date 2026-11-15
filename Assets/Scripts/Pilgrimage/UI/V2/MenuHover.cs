using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pilgrimage.UI
{
    /// <summary>
    /// Menu hover: a red plate fills across the item while the pointer is over it (or while the item is
    /// the selected choice), and listed graphics cross-fade to their highlight colors.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private struct Tint { public Graphic graphic; public Color normal, hover; }
        public MenuShape fillShape;
        public float fillSpeed = 7f;
        private readonly List<Tint> tints = new List<Tint>();
        private Button button;
        private CanvasGroup group;
        private bool hovered, selected;
        private float amount;

        public bool Selected { get => selected; set => selected = value; }
        public bool Highlighted => (hovered && Interactable) || selected;
        private bool Interactable => button == null || button.IsInteractable();

        public void AddTint(Graphic graphic, Color normal, Color hover)
        {
            if (graphic == null) return;
            tints.Add(new Tint { graphic = graphic, normal = normal, hover = hover });
            graphic.color = Color.Lerp(normal, hover, amount);
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnDisable() { hovered = false; Apply(selected ? 1 : 0); }

        private void Update()
        {
            float target = Highlighted ? 1 : 0;
            if (!Mathf.Approximately(amount, target)) Apply(Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * fillSpeed));
            float alpha = Interactable ? 1 : .42f;
            if (!Mathf.Approximately(group.alpha, alpha)) group.alpha = alpha;
        }

        private void Apply(float value)
        {
            amount = value;
            // Ease-out so the red plate "pours" in quickly and settles.
            if (fillShape != null) fillShape.Fill = 1 - (1 - value) * (1 - value);
            foreach (Tint tint in tints) if (tint.graphic != null) tint.graphic.color = Color.Lerp(tint.normal, tint.hover, value);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!hovered && Interactable) PilgrimageAudio.Instance?.Play("ui_hover", .55f, .05f);
            hovered = true;
        }
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable && eventData.button == PointerEventData.InputButton.Left) PilgrimageAudio.Instance?.Play("ui_click", .8f, .05f);
        }
    }
}
