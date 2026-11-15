using UnityEngine;
using UnityEngine.EventSystems;

namespace Pilgrimage.UI
{
    public sealed class CoverScreen : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform title;
        [SerializeField] private float floatHeight = 12f;
        [SerializeField] private float floatPeriod = 3.5f;
        private GameObject menu;
        private Vector2 titleOrigin;
        private float elapsed;

        public void Initialize(GameObject nextScreen)
        {
            menu = nextScreen;
            titleOrigin = title.anchoredPosition;
            menu.SetActive(false);
            transform.SetAsLastSibling();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            title.anchoredPosition = titleOrigin + Vector2.up *
                (Mathf.Sin(elapsed * Mathf.PI * 2f / Mathf.Max(.1f, floatPeriod)) * floatHeight);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || menu == null) return;
            menu.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
