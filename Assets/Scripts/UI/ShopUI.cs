using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 상점 수레 화면. 지금은 배낭 증축만 팔지만, 품목을 늘리려면 BuildRows 안의 목록만 넓히면 된다.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        private const float PanelW = 520f;
        private const float PanelH = 300f;
        private const float Pad = 26f;
        private const float RowH = 66f;
        private const float RowGap = 10f;

        private const float SlicedPpu = 100f / (16f * 3f);
        private static readonly Color TextColor = new Color(0.96f, 0.90f, 0.80f);

        private UIManager _ui;
        private GameManager _game;

        private RectTransform _root;
        private RectTransform _rowRoot;
        private Text _moneyText;
        private readonly List<GameObject> _rows = new List<GameObject>();

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Boot(UIManager ui, GameManager game, RectTransform canvas)
        {
            _ui = ui;
            _game = game;

            BuildRoot(canvas);
            _root.gameObject.SetActive(false);
        }

        public void Open()
        {
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Refresh();
        }

        public void Close() => _root.gameObject.SetActive(false);

        /// <summary>소지금과 각 품목의 구매 가능 여부를 다시 그린다.</summary>
        public void Refresh()
        {
            if (!IsOpen) return;

            _moneyText.text = $"소지금 {_game.Money:N0} G";

            foreach (var go in _rows) Destroy(go);
            _rows.Clear();

            for (int level = 1; level <= GameManager.BackpackPrices.Length; level++)
                BuildBackpackRow(level);
        }

        private void BuildBackpackRow(int level)
        {
            int price = GameManager.BackpackPrices[level - 1];
            bool owned = _game.BackpackLevel >= level;
            bool isNext = _game.BackpackLevel == level - 1;
            bool affordable = _game.Money >= price;

            var row = new GameObject($"Backpack{level}");
            var rt = row.AddComponent<RectTransform>();
            rt.SetParent(_rowRoot, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            float top = (level - 1) * (RowH + RowGap);
            rt.offsetMin = new Vector2(0, -(top + RowH));
            rt.offsetMax = new Vector2(0, -top);

            var bg = row.AddComponent<Image>();
            bg.sprite = AssetLibrary.UiFrame;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = SlicedPpu;

            // 가방 아이콘
            var iconGo = new GameObject("Icon");
            var irt = iconGo.AddComponent<RectTransform>();
            irt.SetParent(rt, false);
            irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot = new Vector2(0, 0.5f);
            irt.anchoredPosition = new Vector2(24, 0);
            irt.sizeDelta = new Vector2(44, 44);
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = level == 1 ? AssetLibrary.UiBagLv1 : AssetLibrary.UiBagLv2;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var title = _ui.Label(rt, $"배낭 {level}단계  (+10칸)", 17, Vector2.zero, TextAnchor.LowerLeft);
            title.rectTransform.anchorMin = new Vector2(0, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0.7f, 1f);
            title.rectTransform.offsetMin = new Vector2(82, 0);
            title.rectTransform.offsetMax = new Vector2(0, -8);
            title.color = TextColor;

            string state = owned ? "보유 중"
                : !isNext ? "이전 단계를 먼저 구매하세요"
                : !affordable ? "골드가 부족합니다"
                : $"{price:N0} G";

            var sub = _ui.Label(rt, state, 15, Vector2.zero, TextAnchor.UpperLeft);
            sub.rectTransform.anchorMin = new Vector2(0, 0f);
            sub.rectTransform.anchorMax = new Vector2(0.7f, 0.5f);
            sub.rectTransform.offsetMin = new Vector2(82, 8);
            sub.rectTransform.offsetMax = new Vector2(0, 0);
            sub.color = owned ? new Color(0.65f, 0.85f, 0.6f)
                : (isNext && affordable) ? new Color(1f, 0.86f, 0.45f)
                : new Color(0.8f, 0.6f, 0.55f);

            bool canBuy = isNext && affordable;
            var btn = MakeButton(rt, owned ? "구매 완료" : "구매", canBuy, () =>
            {
                if (!_game.BuyBackpack()) return;
                _ui.Toast($"배낭 {level}단계 구매! 칸이 10개 늘었습니다.");
                Refresh();
            });
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1, 0.5f);
            brt.pivot = new Vector2(1, 0.5f);
            brt.anchoredPosition = new Vector2(-14, 0);
            brt.sizeDelta = new Vector2(120, 40);

            _rows.Add(row);
        }

        private Button MakeButton(RectTransform parent, string text, bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Buy");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.sprite = AssetLibrary.UiFrame;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = SlicedPpu;
            img.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.4f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = interactable;
            btn.onClick.AddListener(onClick);

            var label = _ui.Label(rt, text, 16, Vector2.zero, TextAnchor.MiddleCenter);
            _ui.Stretch(label.rectTransform, 0, 0, 6, 6);
            return btn;
        }

        // ---------- 뼈대 ----------
        private void BuildRoot(RectTransform canvas)
        {
            var rootGo = new GameObject("ShopRoot");
            _root = rootGo.AddComponent<RectTransform>();
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

            var panelGo = new GameObject("Panel");
            var panel = panelGo.AddComponent<RectTransform>();
            panel.SetParent(_root, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelW, PanelH);

            var bg = panelGo.AddComponent<Image>();
            bg.sprite = AssetLibrary.UiFrame;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = SlicedPpu;

            var title = _ui.Label(panel, "잡화 수레", 22, Vector2.zero, TextAnchor.UpperLeft);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(0.6f, 1);
            title.rectTransform.offsetMin = new Vector2(Pad, -56);
            title.rectTransform.offsetMax = new Vector2(0, -18);
            title.color = TextColor;

            _moneyText = _ui.Label(panel, "", 17, Vector2.zero, TextAnchor.UpperRight);
            _moneyText.rectTransform.anchorMin = new Vector2(0.4f, 1);
            _moneyText.rectTransform.anchorMax = new Vector2(1, 1);
            _moneyText.rectTransform.offsetMin = new Vector2(0, -52);
            _moneyText.rectTransform.offsetMax = new Vector2(-Pad, -22);
            _moneyText.color = new Color(1f, 0.86f, 0.45f);

            _rowRoot = new GameObject("Rows").AddComponent<RectTransform>();
            _rowRoot.SetParent(panel, false);
            _rowRoot.anchorMin = new Vector2(0, 1);
            _rowRoot.anchorMax = new Vector2(1, 1);
            _rowRoot.pivot = new Vector2(0.5f, 1);
            _rowRoot.offsetMin = new Vector2(Pad, -(PanelH - Pad));
            _rowRoot.offsetMax = new Vector2(-Pad, -64);

            var hint = _ui.Label(panel, "ESC 로 닫기", 14, Vector2.zero, TextAnchor.LowerCenter);
            hint.rectTransform.anchorMin = new Vector2(0, 0);
            hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.offsetMin = new Vector2(0, 14);
            hint.rectTransform.offsetMax = new Vector2(0, 36);
            hint.color = new Color(0.8f, 0.75f, 0.7f);
        }
    }
}
