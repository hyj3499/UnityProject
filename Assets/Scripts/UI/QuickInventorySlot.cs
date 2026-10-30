using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 퀵 인벤토리 한 칸. 클릭하면 선택하고, 다른 칸으로 끌어다 놓으면 자리를 바꾼다.
    /// 선택 시 슬롯 전체가 올라가며 밝아지고, 선택 테두리는 한 번만 재생한다.
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
        private Image _background;
        private BookInventorySelection _selection;
        private Vector2 _restPosition;
        private float _lift;
        private Color _normalTint;
        private Color _selectedTint;
        private bool _highlighted;

        private static Image _dragImage;
        private void OnEnable()
        {
            LocalizedText.InventoryCount(count);
            if (_selection != null) _selection.SetSelected(_highlighted, true);
        }

        public void ConfigurePresentation(Vector2 center, Vector2 size, float lift,
            Color normalTint, Color selectedTint, Sprite[] selectorFrames)
        {
            _restPosition = center;
            _lift = lift;
            _normalTint = normalTint;
            _selectedTint = selectedTint;
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            _background = GetComponent<Image>();
            _background.raycastTarget = true;
            _background.type = Image.Type.Simple;

            // Book의 34px 셀렉터와 네 모서리 배치/재생 방식을 그대로 사용한다.
            highlight.sprite = null;
            highlight.enabled = false;
            highlight.raycastTarget = false;
            var selectorRect = highlight.rectTransform;
            selectorRect.anchorMin = selectorRect.anchorMax = Vector2.one * .5f;
            selectorRect.anchoredPosition = Vector2.zero;
            selectorRect.sizeDelta = size * (34f / 27f);
            selectorRect.SetAsLastSibling();
            _selection = highlight.GetComponent<BookInventorySelection>();
            if (_selection == null) _selection = highlight.gameObject.AddComponent<BookInventorySelection>();
            _selection.image = highlight;
            _selection.frames = selectorFrames;
            _selection.framesPerSecond = 10f;
            _selection.repeatCount = 1;
            _selection.corners = new Image[4];
            string[] names = { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };
            for (int i = 0; i < names.Length; i++)
            {
                var child = selectorRect.Find(names[i]);
                if (child == null)
                {
                    var go = new GameObject(names[i], typeof(RectTransform), typeof(Image));
                    go.layer = gameObject.layer;
                    go.transform.SetParent(selectorRect, false);
                    child = go.transform;
                }
                var corner = child.GetComponent<Image>();
                corner.color = Color.white;
                corner.raycastTarget = false;
                _selection.corners[i] = corner;
            }
            count.rectTransform.SetAsLastSibling();
            SetHighlighted(_highlighted);
        }

        public void SetBackground(Sprite sprite)
        {
            if (_background == null) _background = GetComponent<Image>();
            _background.sprite = sprite;
        }

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

        public void SetHighlighted(bool on, bool restart = false)
        {
            _highlighted = on;
            ((RectTransform)transform).anchoredPosition = _restPosition + (on ? Vector2.up * _lift : Vector2.zero);
            if (_background != null) _background.color = on ? _selectedTint : _normalTint;
            if (icon != null) icon.color = on ? _selectedTint : _normalTint;
            if (_selection != null) _selection.SetSelected(on, restart);
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
