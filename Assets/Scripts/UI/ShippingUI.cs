using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 배송함 화면. 위쪽이 배송함, 아래쪽이 내 가방이고 드래그&amp;드롭 또는 쉬프트+클릭으로
    /// 두 인벤토리 사이에서 아이템을 옮긴다. 배송함에 담긴 물건의 예상 판매금액이
    /// 오른쪽 위에 표시되고, 잠을 자면 그만큼 소지금이 늘어난다.
    /// </summary>
    public class ShippingUI : MonoBehaviour
    {
        private const int Cols = Inventory.HotbarSize; // 퀵바와 같은 10칸 폭
        private const float Slot = 40f;
        private const float Pitch = 46f;
        // 9-slice 나무 테두리(약 18px)에 칸이나 글자가 물리지 않도록 넉넉히 띄운다.
        private const float TitleH = 40f;
        private const float PanelPad = 26f;

        // 9-slice 프레임을 아트 3배 크기로 그리기 위한 보정값
        private const float SlicedPpu = 100f / (16f * 3f);

        private static readonly Color TextColor = new Color(0.96f, 0.90f, 0.80f);

        private UIManager _ui;
        private GameManager _game;

        private RectTransform _root;
        private Text _valueText;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Boot(UIManager ui, GameManager game, RectTransform canvas)
        {
            _ui = ui;
            _game = game;

            int boxRows = Mathf.CeilToInt(Inventory.ShippingSlots / (float)Cols);
            int bagRows = Mathf.CeilToInt(Inventory.TotalSlots / (float)Cols);

            float panelW = Cols * Pitch - (Pitch - Slot) + PanelPad * 2f;
            float boxH = TitleH + boxRows * Pitch - (Pitch - Slot) + PanelPad;
            float bagH = TitleH + bagRows * Pitch - (Pitch - Slot) + PanelPad;
            float totalH = boxH + 14f + bagH;

            BuildRoot(canvas);

            var boxPanel = Panel("BoxPanel", panelW, boxH, totalH / 2f - boxH / 2f);
            PanelTitle(boxPanel, "배송함");
            _valueText = _ui.Label(boxPanel, "예상 판매금액 0 G", 16, new Vector2(-PanelPad, -8), TextAnchor.UpperRight);
            _valueText.rectTransform.anchorMin = new Vector2(0.4f, 1f);
            _valueText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _valueText.rectTransform.offsetMin = new Vector2(0, -TitleH);
            _valueText.rectTransform.offsetMax = new Vector2(-PanelPad, -14);
            _valueText.color = new Color(1f, 0.86f, 0.45f);
            BuildGrid(boxPanel, Inventory.ShippingSlots, isShipping: true);

            var bagPanel = Panel("BagPanel", panelW, bagH, -(totalH / 2f - bagH / 2f));
            PanelTitle(bagPanel, "내 가방");
            BuildGrid(bagPanel, Inventory.TotalSlots, isShipping: false);

            var hint = _ui.Label(_root, "드래그로 옮기기 · Shift+클릭으로 한 번에 옮기기 · ESC 로 닫기", 14, Vector2.zero, TextAnchor.MiddleCenter);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hint.rectTransform.anchoredPosition = new Vector2(0, -(totalH / 2f) - 22f);
            hint.rectTransform.sizeDelta = new Vector2(600, 24);
            hint.color = new Color(0.85f, 0.85f, 0.85f);

            _root.gameObject.SetActive(false);
        }

        public void Open()
        {
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        public void Close() => _root.gameObject.SetActive(false);

        public void SetExpectedValue(int value)
        {
            if (_valueText != null) _valueText.text = $"예상 판매금액 {value:N0} G";
        }

        // ---------- 뼈대 ----------
        private void BuildRoot(RectTransform canvas)
        {
            var go = new GameObject("ShippingRoot");
            _root = go.AddComponent<RectTransform>();
            _root.SetParent(canvas, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            var dim = new GameObject("Dim").AddComponent<Image>();
            dim.transform.SetParent(_root, false);
            dim.color = new Color(0, 0, 0, 0.5f);
            var drt = dim.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
        }

        private RectTransform Panel(string name, float w, float h, float y)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.AddComponent<Image>();
            img.sprite = AssetLibrary.UiFrame;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = SlicedPpu;
            return rt;
        }

        private void PanelTitle(RectTransform panel, string title)
        {
            var t = _ui.Label(panel, title, 20, Vector2.zero, TextAnchor.UpperLeft);
            t.rectTransform.anchorMin = new Vector2(0, 1);
            t.rectTransform.anchorMax = new Vector2(0.6f, 1);
            t.rectTransform.offsetMin = new Vector2(PanelPad, -TitleH);
            t.rectTransform.offsetMax = new Vector2(0, -14);
            t.color = TextColor;
        }

        private void BuildGrid(RectTransform panel, int slotCount, bool isShipping)
        {
            var grid = new GameObject("Grid").AddComponent<RectTransform>();
            grid.SetParent(panel, false);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.anchoredPosition = new Vector2(0, -TitleH);
            grid.sizeDelta = new Vector2(Cols * Pitch, Mathf.Ceil(slotCount / (float)Cols) * Pitch);

            for (int i = 0; i < slotCount; i++)
            {
                int c = i % Cols;
                int r = i / Cols;
                float x = -((Cols - 1) * Pitch) / 2f + c * Pitch;
                float y = -r * Pitch;

                if (isShipping) _ui.CreateShippingSlot(grid, i, x, y, Slot);
                else _ui.CreatePlayerSlot(grid, i, x, y, Slot);
            }
        }
    }
}
