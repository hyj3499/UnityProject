using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// Enables drag & drop between inventory/hotbar slots and click-to-select on
    /// the hotbar (design doc §12).
    /// </summary>
    public class SlotDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
    {
        private UIManager _ui;
        private SlotView _view;
        private static GameObject _dragIcon;
        private static Image _dragImage;
        private Canvas _canvas;

        public void Init(UIManager ui, SlotView view)
        {
            _ui = ui;
            _view = view;
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerClick(PointerEventData e)
        {
            // 쉬프트+클릭: 배송함이 열려 있으면 반대편 인벤토리로 한 번에 옮긴다.
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                if (_ui.QuickTransfer(_view)) return;
            }

            if (_view.isHotbar)
                _ui.OnHotbarClicked(_view.index);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            var icon = _view.icon;
            if (icon == null || !icon.enabled || icon.sprite == null) return;

            if (_dragIcon == null)
            {
                _dragIcon = new GameObject("DragIcon");
                _dragImage = _dragIcon.AddComponent<Image>();
                _dragImage.raycastTarget = false;
                _dragImage.preserveAspect = true;
                var rt = _dragIcon.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(40, 40);
            }
            _dragIcon.transform.SetParent(_canvas.transform, false);
            _dragIcon.transform.SetAsLastSibling();
            _dragImage.sprite = icon.sprite;
            _dragImage.enabled = true;
            _dragImage.color = Color.white;
            MoveDragIcon(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragImage != null && _dragImage.enabled)
                MoveDragIcon(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_dragImage != null)
                _dragImage.enabled = false;
        }

        public void OnDrop(PointerEventData e)
        {
            var from = e.pointerDrag != null ? e.pointerDrag.GetComponent<SlotDragHandler>() : null;
            if (from != null && from != this)
                _ui.OnSlotDrop(from._view, _view);
        }

        private void MoveDragIcon(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, e.position, e.pressEventCamera, out Vector2 local);
            _dragIcon.GetComponent<RectTransform>().localPosition = local;
        }
    }
}
