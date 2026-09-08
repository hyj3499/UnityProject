using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 작업대 화면. 목록은 RecipeDatabase를 그대로 읽으므로, 만들 수 있는 것을 늘리려면
    /// 그쪽에 Register 한 줄만 더하면 여기는 손대지 않아도 된다 (ShopUI와 같은 구성).
    /// </summary>
    public class CraftingUI : MonoBehaviour
    {
        private const float PanelW = 560f;
        private const float PanelH = 360f;
        private const float Pad = 26f;
        private const float RowH = 62f;
        private const float RowGap = 8f;

        private const float SlicedPpu = 100f / (16f * 3f);
        private static readonly Color TextColor = new Color(0.96f, 0.90f, 0.80f);
        private static readonly Color OkColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color LackColor = new Color(0.8f, 0.6f, 0.55f);

        private UIManager _ui;
        private GameManager _game;

        private RectTransform _root;
        private RectTransform _rowRoot;
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

        /// <summary>재료가 줄어들면 버튼 상태가 바뀌므로 만들 때마다 다시 그린다.</summary>
        public void Refresh()
        {
            if (!IsOpen) return;

            foreach (var go in _rows) Destroy(go);
            _rows.Clear();

            var recipes = RecipeDatabase.All;
            for (int i = 0; i < recipes.Count; i++) BuildRow(recipes[i], i);
        }

        private void BuildRow(CraftingRecipe recipe, int index)
        {
            bool canCraft = recipe.CanCraft(_game.Inventory);

            var row = new GameObject($"Recipe{index}");
            var rt = row.AddComponent<RectTransform>();
            rt.SetParent(_rowRoot, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            float top = index * (RowH + RowGap);
            rt.offsetMin = new Vector2(0, -(top + RowH));
            rt.offsetMax = new Vector2(0, -top);

            var bg = row.AddComponent<Image>();
            bg.sprite = AssetLibrary.UiFrame;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = SlicedPpu;

            // 결과물 아이콘
            var iconGo = new GameObject("Icon");
            var irt = iconGo.AddComponent<RectTransform>();
            irt.SetParent(rt, false);
            irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot = new Vector2(0, 0.5f);
            irt.anchoredPosition = new Vector2(24, 0);
            irt.sizeDelta = new Vector2(40, 40);
            var icon = iconGo.AddComponent<Image>();
            var resultDef = recipe.ResultDef;
            icon.sprite = resultDef != null ? resultDef.GetSprite() : null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = icon.sprite != null;

            string countSuffix = recipe.resultCount > 1 ? $" x{recipe.resultCount}" : "";
            var title = _ui.Label(rt, recipe.DisplayName + countSuffix, 17, Vector2.zero, TextAnchor.LowerLeft);
            title.rectTransform.anchorMin = new Vector2(0, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            title.rectTransform.offsetMin = new Vector2(76, 0);
            title.rectTransform.offsetMax = new Vector2(0, -6);
            title.color = TextColor;

            var sub = _ui.Label(rt, recipe.CostText(_game.Inventory), 15, Vector2.zero, TextAnchor.UpperLeft);
            sub.rectTransform.anchorMin = new Vector2(0, 0f);
            sub.rectTransform.anchorMax = new Vector2(0.72f, 0.5f);
            sub.rectTransform.offsetMin = new Vector2(76, 6);
            sub.rectTransform.offsetMax = new Vector2(0, 0);
            sub.color = canCraft ? OkColor : LackColor;

            var btn = MakeButton(rt, "제작", canCraft, () =>
            {
                if (_game.Craft(recipe)) Refresh();
            });
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1, 0.5f);
            brt.pivot = new Vector2(1, 0.5f);
            brt.anchoredPosition = new Vector2(-14, 0);
            brt.sizeDelta = new Vector2(110, 38);

            _rows.Add(row);
        }

        private Button MakeButton(RectTransform parent, string text, bool interactable,
                                  UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Craft");
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
            var rootGo = new GameObject("CraftingRoot");
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

            var title = _ui.Label(panel, "작업대", 22, Vector2.zero, TextAnchor.UpperLeft);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(0.6f, 1);
            title.rectTransform.offsetMin = new Vector2(Pad, -56);
            title.rectTransform.offsetMax = new Vector2(0, -18);
            title.color = TextColor;

            _rowRoot = new GameObject("Rows").AddComponent<RectTransform>();
            _rowRoot.SetParent(panel, false);
            _rowRoot.anchorMin = new Vector2(0, 1);
            _rowRoot.anchorMax = new Vector2(1, 1);
            _rowRoot.pivot = new Vector2(0.5f, 1);
            _rowRoot.offsetMin = new Vector2(Pad, -(PanelH - Pad));
            _rowRoot.offsetMax = new Vector2(-Pad, -64);

            var hint = _ui.Label(panel, "만든 울타리·길은 손에 들고 우클릭하면 놓입니다 / ESC 로 닫기",
                                 14, Vector2.zero, TextAnchor.LowerCenter);
            hint.rectTransform.anchorMin = new Vector2(0, 0);
            hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.offsetMin = new Vector2(0, 14);
            hint.rectTransform.offsetMax = new Vector2(0, 36);
            hint.color = new Color(0.8f, 0.75f, 0.7f);
        }
    }
}
