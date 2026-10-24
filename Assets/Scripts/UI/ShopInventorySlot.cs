using UnityEngine;
using UnityEngine.EventSystems;

namespace FarmMVP
{
    /// <summary>상점 인벤토리의 호버 설명과 우클릭 판매 입력만 전달한다.</summary>
    public sealed class ShopInventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private ShopBookController _controller;
        private int _index;
        public void Bind(ShopBookController controller, int index) { _controller = controller; _index = index; }
        public void OnPointerEnter(PointerEventData eventData) => _controller?.HoverInventory(_index);
        public void OnPointerExit(PointerEventData eventData) => _controller?.LeaveInventory(_index);
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) _controller?.SellInventory(_index);
        }
        private void OnDisable() => _controller?.LeaveInventory(_index);
    }
}
