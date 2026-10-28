using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>Each dropdown row has its own fill, selection and keyboard/pointer highlight.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DropdownRowStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Color normal = new Color32(76, 43, 53, 255);
        public Color highlighted = new Color32(108, 77, 87, 255);
        private Toggle _toggle;
        private Image _background;
        private bool _hovered, _focused;
        private void OnEnable()
        {
            _toggle = GetComponent<Toggle>(); _background = GetComponent<Image>();
            if (_toggle != null) _toggle.onValueChanged.AddListener(Changed);
            Refresh();
        }
        private void OnDisable()
        {
            if (_toggle != null) _toggle.onValueChanged.RemoveListener(Changed);
            _hovered = _focused = false;
        }
        private void Changed(bool value) => Refresh();
        private void Refresh()
        { if (_background != null) _background.color = _hovered || _focused || (_toggle != null && _toggle.isOn) ? highlighted : normal; }
        public void OnPointerEnter(PointerEventData data) { _hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData data) { _hovered = false; Refresh(); }
        public void OnSelect(BaseEventData data) { _focused = true; Refresh(); }
        public void OnDeselect(BaseEventData data) { _focused = false; Refresh(); }
    }
}
