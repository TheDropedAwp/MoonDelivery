using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoonDelivery
{
    public sealed class MoonPointButton : MonoBehaviour, IPointerClickHandler
    {
        private MoonCanvasUI ui;
        private MapPoint point;

        public void Initialize(MoonCanvasUI owner, MapPoint value)
        {
            ui = owner;
            point = value;
        }

        public void OnPointerClick(PointerEventData eventData) =>
            ui.SelectPoint(point, eventData.button == PointerEventData.InputButton.Right);
    }

    public sealed class MoonCardTint
        : MonoBehaviour,
            IPointerDownHandler,
            IPointerUpHandler,
            IPointerExitHandler
    {
        private static readonly Color32 Normal = new Color32(0xB7, 0xB7, 0xB7, 0xFF);
        private Image image;
        private bool selected;

        public void Initialize(Image target, bool isSelected)
        {
            image = target;
            selected = isSelected;
            ApplyBase();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Button button = GetComponent<Button>();
            if (button != null && button.interactable && image != null)
                image.color = Color.white;
        }

        public void OnPointerUp(PointerEventData eventData) => ApplyBase();

        public void OnPointerExit(PointerEventData eventData) => ApplyBase();

        private void ApplyBase()
        {
            if (image != null)
                image.color = selected ? Color.white : Normal;
        }
    }

    public sealed class MoonBrokenButton : MonoBehaviour, IPointerClickHandler
    {
        private MoonCanvasUI ui;
        private Delivery delivery;

        public void Initialize(MoonCanvasUI owner, Delivery value)
        {
            ui = owner;
            delivery = value;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (delivery.status == DeliveryStatus.Broken)
                ui.ShowBroken(delivery, eventData.position);
        }
    }
}
