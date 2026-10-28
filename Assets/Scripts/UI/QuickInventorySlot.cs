using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 퀵 인벤토리 한 칸. 클릭하면 선택하고, 다른 칸으로 끌어다 놓으면 자리를 바꾼다.
    /// 위치와 크기는 에디터에서 자유롭게 옮겨도 된다 (QuickInventoryHUD가 건드리지 않는다).
    /// </summary>
    public class QuickInventorySlot : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private QuickInventoryHUD hud;
        [Tooltip("퀵바 안에서의 칸 번호 (0이 숫자키 1번).")]
        [SerializeField] private int slotNumber;
        [SerializeField] private Image highlight;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text count;

        private static Image _dragImage;
        private void OnEnable() => LocalizedText.InventoryCount(count);

        public void Setup(QuickInventoryHUD owner, int number, Image highlightImage, Image iconImage, TMP_Text countText)
        {
            hud = owner;
            slotNumber = number;
            highlight = highlightImage;
            icon = iconImage;
            count = countText;
            LocalizedText.InventoryCount(count);
        }

        public void Show(ItemStack stack)
        {
            var sprite = stack != null && !stack.IsEmpty ? stack.Def.GetSprite() : null;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            if (count != null)
                count.text = stack != null && !stack.IsEmpty && stack.count > 1 ? stack.count.ToString() : "";
        }

        public void SetHighlighted(bool on)
        {
            if (highlight != null) highlight.enabled = on;
        }

        public void OnPointerClick(PointerEventData e) => hud.OnSlotClicked(slotNumber);

        public void OnBeginDrag(PointerEventData e)
        {
            if (hud.InputBlocked || icon == null || !icon.enabled || icon.sprite == null) return;

            if (_dragImage == null)
            {
                var go = new GameObject("QuickInventoryDragIcon", typeof(RectTransform));
                _dragImage = go.AddComponent<Image>();
                _dragImage.raycastTarget = false;
                _dragImage.preserveAspect = true;
            }
            var rt = _dragImage.rectTransform;
            rt.SetParent(hud.CanvasRoot, false);
            rt.SetAsLastSibling();
            rt.sizeDelta = ((RectTransform)transform).rect.size * transform.lossyScale.x
                           / hud.CanvasRoot.lossyScale.x;
            _dragImage.sprite = icon.sprite;
            _dragImage.enabled = true;
            MoveDragIcon(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragImage != null && _dragImage.enabled) MoveDragIcon(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_dragImage != null) _dragImage.enabled = false;
        }

        public void OnDrop(PointerEventData e)
        {
            var from = e.pointerDrag != null ? e.pointerDrag.GetComponent<QuickInventorySlot>() : null;
            if (from != null && from != this) hud.OnSlotDropped(from.slotNumber, slotNumber);
        }

        private void MoveDragIcon(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)hud.CanvasRoot, e.position, e.pressEventCamera, out Vector2 local);
            _dragImage.rectTransform.localPosition = local;
        }
    }
}
