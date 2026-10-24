using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>누르는 즉시 한 번, 길게 누르면 일시 정지 중에도 반복하는 수량 버튼.</summary>
    public sealed class ShopQuantityButton : Button
    {
        private bool _held;
        private float _nextRepeat;

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            _held = true;
            _nextRepeat = Time.unscaledTime + .4f;
            onClick.Invoke();
        }

        // PointerDown에서 이미 처리했으므로 버튼을 놓을 때 중복 증가하지 않는다.
        public override void OnPointerClick(PointerEventData eventData) { }
        public override void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _held = false;
            base.OnPointerUp(eventData);
        }
        public override void OnPointerExit(PointerEventData eventData)
        {
            _held = false;
            base.OnPointerExit(eventData);
        }
        protected override void OnDisable()
        {
            _held = false;
            base.OnDisable();
        }
        private void OnApplicationFocus(bool focused) { if (!focused) _held = false; }
        private void Update()
        {
            if (!_held) return;
            if (!IsActive() || !IsInteractable()) { _held = false; return; }
            if (Time.unscaledTime < _nextRepeat) return;
            _nextRepeat = Time.unscaledTime + .075f;
            onClick.Invoke();
        }
    }
}
