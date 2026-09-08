using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 상점 수레 화면. 품목이 많아 한 화면에 다 들어가지 않으므로 목록은 스크롤된다
    /// (마우스 휠 / 드래그 / 오른쪽 스크롤바). 무엇을 파는지는 ShopDatabase가 정하고,
    /// 여기는 그것을 줄로 그리는 일만 한다.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        private const float PanelW = 560f;
        private const float PanelH = 420f;
        private const float Pad = 26f;
        private const float HeaderH = 64f;
        private const float FooterH = 38f;
        private const float ScrollbarW = 10f;

        private const float RowH = 62f;
        private const float RowGap = 8f;
        private const float GroupH = 30f;   // 묶음 머리글 ("봄", "여름", ...)

        private const float SlicedPpu = 100f / (16f * 3f);
        private static readonly Color TextColor = new Color(0.96f, 0.90f, 0.80f);
        private static readonly Color GoldColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color OkColor = new Color(0.65f, 0.85f, 0.6f);
        private static readonly Color BadColor = new Color(0.8f, 0.6f, 0.55f);
        private static readonly Color DimColor = new Color(0.72f, 0.68f, 0.62f);

        private UIManager _ui;
        private GameManager _game;

        private RectTransform _root;
        private ScrollRect _scroll;
        private RectTransform _content;
        private Text _moneyText;
        private readonly List<GameObject> _rows = new List<GameObject>();

        /// <summary>다시 그려도 보던 자리에 그대로 있도록 스크롤 위치를 기억한다.</summary>
        private float _scrollPos = 1f;

        private float _cursorY;      // 다음 줄이 놓일 위치 (content 위쪽에서부터)
        private string _lastGroup;

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
            _scrollPos = 1f;   // 열 때마다 맨 위에서 시작
            Refresh();
        }

        public void Close() => _root.gameObject.SetActive(false);

        /// <summary>소지금과 각 품목의 구매 가능 여부를 다시 그린다.</summary>
        public void Refresh()
        {
            if (!IsOpen) return;

            _moneyText.text = $"소지금 {_game.Money:N0} G";

            // 목록이 창보다 짧으면 스크롤 위치가 NaN으로 나올 수 있으므로 그때는 건드리지 않는다.
            if (_scroll != null && _content.sizeDelta.y > 0f)
            {
                float pos = _scroll.verticalNormalizedPosition;
                if (!float.IsNaN(pos)) _scrollPos = pos;
            }

            foreach (var go in _rows) Destroy(go);
            _rows.Clear();
            _cursorY = 0f;
            _lastGroup = null;

            Group("가방");
            for (int level = 1; level <= GameManager.BackpackPrices.Length; level++)
                BuildBackpackRow(level);

            foreach (var entry in ShopDatabase.Entries)
            {
                if (entry.Def == null) continue;   // 아직 아이템이 없는 품목은 조용히 건너뛴다
                Group(entry.group);
                BuildItemRow(entry);
            }

            _content.sizeDelta = new Vector2(0, _cursorY);
            // 줄을 다 만든 다음이라야 스크롤 범위가 정해진다.
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scrollPos);
        }

        // ---------- 줄 ----------
        /// <summary>묶음이 바뀔 때만 머리글을 한 줄 넣는다.</summary>
        private void Group(string group)
        {
            if (string.IsNullOrEmpty(group) || group == _lastGroup) return;
            _lastGroup = group;

            var rt = NewRow("Group_" + group, GroupH);
            var label = _ui.Label(rt, group, 16, Vector2.zero, TextAnchor.LowerLeft);
            _ui.Stretch(label.rectTransform, 0, 4, 4, 0);
            label.color = GoldColor;
        }

        private void BuildBackpackRow(int level)
        {
            int price = GameManager.BackpackPrices[level - 1];
            bool owned = _game.BackpackLevel >= level;
            bool isNext = _game.BackpackLevel == level - 1;
            bool affordable = _game.Money >= price;

            string state = owned ? "보유 중"
                : !isNext ? "이전 단계를 먼저 구매하세요"
                : !affordable ? "골드가 부족합니다"
                : $"{price:N0} G";

            var rt = Row($"Backpack{level}",
                level == 1 ? AssetLibrary.UiBagLv1 : AssetLibrary.UiBagLv2,
                $"배낭 {level}단계  (+10칸)", state,
                owned ? OkColor : (isNext && affordable) ? GoldColor : BadColor);

            AddBuyButton(rt, owned ? "구매 완료" : "구매", isNext && affordable, () =>
            {
                if (!_game.BuyBackpack()) return;
                _ui.Toast($"배낭 {level}단계 구매! 칸이 10개 늘었습니다.");
                Refresh();
            });
        }

        private void BuildItemRow(ShopEntry entry)
        {
            var def = entry.Def;
            bool affordable = _game.Money >= entry.price;
            var crop = CropDatabase.Get(def.cropId);
            bool inSeason = crop == null || Seasons.AllowsNow(crop.seasons);

            string sub = $"{entry.price:N0} G";
            if (!string.IsNullOrEmpty(entry.note)) sub += "   " + entry.note;
            if (!inSeason) sub += "   (제철이 아님)";

            var rt = Row(def.id, def.GetSprite(), def.displayName, sub,
                !affordable ? BadColor : inSeason ? GoldColor : DimColor);

            AddBuyButton(rt, "구매", affordable, () => Buy(entry));
        }

        /// <summary>Shift를 누른 채 사면 한 번에 10개 — 밭 한 뙈기를 채우려고 서른 번 누르지 않도록.</summary>
        private const int BulkCount = 10;

        private void Buy(ShopEntry entry)
        {
            bool bulk = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // 돈이 되는 만큼만 산다 (Shift로 10개를 눌렀는데 7개어치뿐이면 7개).
            int want = bulk ? BulkCount : 1;
            int affordable = entry.price > 0 ? _game.Money / entry.price : want;
            int count = Mathf.Min(want, affordable);
            if (count <= 0) return;

            int leftover = _game.Inventory.Add(entry.itemId, count);
            int bought = count - leftover;
            if (bought <= 0)
            {
                _ui.Toast("가방이 가득 찼습니다.");
                return;
            }

            _game.AddMoney(-entry.price * bought);
            _ui.Toast(bought > 1
                ? $"{entry.Def.displayName} {bought}개 구매!"
                : $"{entry.Def.displayName} 구매!");
            Refresh();
        }

        /// <summary>아이콘 + 이름 + 설명 한 줄. 오른쪽의 구매 버튼은 호출한 쪽이 붙인다.</summary>
        private RectTransform Row(string name, Sprite icon, string title, string sub, Color subColor)
        {
            var rt = NewRow(name, RowH, RowGap);

            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = AssetLibrary.UiFrame;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = SlicedPpu;

            var iconGo = new GameObject("Icon");
            var irt = iconGo.AddComponent<RectTransform>();
            irt.SetParent(rt, false);
            irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot = new Vector2(0, 0.5f);
            irt.anchoredPosition = new Vector2(20, 0);
            irt.sizeDelta = new Vector2(40, 40);
            var img = iconGo.AddComponent<Image>();
            img.sprite = icon;
            img.enabled = icon != null;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var titleText = _ui.Label(rt, title, 17, Vector2.zero, TextAnchor.LowerLeft);
            titleText.rectTransform.anchorMin = new Vector2(0, 0.5f);
            titleText.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(72, 0);
            titleText.rectTransform.offsetMax = new Vector2(0, -6);
            titleText.color = TextColor;

            var subText = _ui.Label(rt, sub, 14, Vector2.zero, TextAnchor.UpperLeft);
            subText.rectTransform.anchorMin = new Vector2(0, 0f);
            subText.rectTransform.anchorMax = new Vector2(0.72f, 0.5f);
            subText.rectTransform.offsetMin = new Vector2(72, 6);
            subText.rectTransform.offsetMax = new Vector2(0, 0);
            subText.color = subColor;

            return rt;
        }

        /// <summary>목록에 새 줄 자리를 잡는다 (위에서 아래로 쌓인다).</summary>
        private RectTransform NewRow(string name, float height, float gap = 4f)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_content, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, -(_cursorY + height));
            rt.offsetMax = new Vector2(0, -_cursorY);

            _cursorY += height + gap;
            _rows.Add(go);
            return rt;
        }

        private void AddBuyButton(RectTransform row, string text, bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Buy");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(row, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-12, 0);
            rt.sizeDelta = new Vector2(104, 36);

            var img = go.AddComponent<Image>();
            img.sprite = AssetLibrary.UiFrame;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = SlicedPpu;
            img.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.4f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = interactable;
            btn.onClick.AddListener(onClick);

            var label = _ui.Label(rt, text, 15, Vector2.zero, TextAnchor.MiddleCenter);
            _ui.Stretch(label.rectTransform, 0, 0, 6, 6);
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
            _moneyText.color = GoldColor;

            BuildScrollArea(panel);

            var hint = _ui.Label(panel, "휠로 내려서 보기 · Shift+클릭으로 10개씩 구매 · ESC 로 닫기", 14, Vector2.zero, TextAnchor.LowerCenter);
            hint.rectTransform.anchorMin = new Vector2(0, 0);
            hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.offsetMin = new Vector2(0, 12);
            hint.rectTransform.offsetMax = new Vector2(0, 32);
            hint.color = new Color(0.8f, 0.75f, 0.7f);
        }

        /// <summary>
        /// 잘라내는 창(viewport) + 그 안을 위아래로 움직이는 판(content). 창은 패널 크기에 고정이고
        /// 판만 품목 수만큼 길어지므로, 품목이 몇 개든 창 밖으로 넘치지 않는다.
        /// </summary>
        private void BuildScrollArea(RectTransform panel)
        {
            var viewportGo = new GameObject("Viewport");
            var viewport = viewportGo.AddComponent<RectTransform>();
            viewport.SetParent(panel, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(Pad, FooterH);
            viewport.offsetMax = new Vector2(-(Pad + ScrollbarW + 6f), -HeaderH);
            viewportGo.AddComponent<RectMask2D>();

            // 드래그로도 스크롤되려면 창 전체가 클릭을 받아야 한다 (보이지는 않는 판).
            var catcher = viewportGo.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0.001f);

            var contentGo = new GameObject("Content");
            _content = contentGo.AddComponent<RectTransform>();
            _content.SetParent(viewport, false);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;

            var scrollbar = BuildScrollbar(panel);

            _scroll = panel.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 28f;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        private Scrollbar BuildScrollbar(RectTransform panel)
        {
            var barGo = new GameObject("Scrollbar");
            var barRt = barGo.AddComponent<RectTransform>();
            barRt.SetParent(panel, false);
            barRt.anchorMin = new Vector2(1, 0);
            barRt.anchorMax = new Vector2(1, 1);
            barRt.pivot = new Vector2(1, 0.5f);
            barRt.offsetMin = new Vector2(-(Pad + ScrollbarW), FooterH);
            barRt.offsetMax = new Vector2(-Pad, -HeaderH);

            var track = barGo.AddComponent<Image>();
            track.color = new Color(0, 0, 0, 0.35f);

            var areaGo = new GameObject("SlidingArea");
            var area = areaGo.AddComponent<RectTransform>();
            area.SetParent(barRt, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;

            var handleGo = new GameObject("Handle");
            var handle = handleGo.AddComponent<RectTransform>();
            handle.SetParent(area, false);
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = new Color(0.86f, 0.78f, 0.62f, 0.9f);

            var bar = barGo.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handleImg;
            return bar;
        }
    }
}
