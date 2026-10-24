using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>책 UI의 슬롯 그림, 둥근 마스크, 선택 애니메이션을 공유한다.</summary>
    public static class BookInventoryLayout
    {
        private static Sprite[] _art;
        private static Sprite[] _selector;
        private static Font _countFont;
        private static Font CountFont => _countFont ?? (_countFont = Resources.Load<Font>("Sprites/UI/BoldPixels")
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        private static Sprite[] Art => _art ?? (_art = Resources.LoadAll<Sprite>(BookMenuLayout.ArtRoot + "Book Inventory")
            .OrderBy(s => s.name).ToArray());
        private static Sprite[] Selector => _selector ?? (_selector = Resources.LoadAll<Sprite>(BookMenuLayout.ArtRoot + "Book Inventory Selector")
            .OrderBy(s => int.TryParse(s.name.Split('_').Last(), out int n) ? n : 0).ToArray());
        public static void ApplyMenu(BookMenuView view, int unlocked)
        {
            if (Art.Length < 4 || view.slots == null || view.slots.Length == 0) return;
            var root = view.inventorySlotsRoot;
            if (root == null)
            {
                var node = view.slots[0].button.transform as RectTransform;
                while (node != null && node.name != "InventorySlots") node = node.parent as RectTransform;
                root = node;
                view.inventorySlotsRoot = root;
            }
            if (root == null) return;
            view.inventoryBackground = root.GetComponent<Image>();
            if (view.inventoryBackground != null)
            {
                view.inventoryBackground.sprite = null;
                view.inventoryBackground.enabled = false;
            }
            // FitInParent drives anchors across its entire parent. Keep the page area
            // fixed and fit a separate child grid inside it instead.
            var fitter = root.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.enabled = false;
            // Writing RectTransform values, even when unchanged, marks the layout
            // dirty.  RefreshStatus runs when a slot is clicked, so avoid causing a
            // delayed fitter pass that visibly drops the inventory after the click.
            SetIfChanged(root, new Vector2(0, .025f), new Vector2(1, .34f), Vector2.one * .5f);
            // Node() resets anchors. Calling it every status refresh made the fitter
            // re-place the grid on each click, so only create this fixed child once.
            var grid = root.Find("Grid") as RectTransform;
            if (grid == null) grid = BookMenuLayout.Node(root, "Grid", 0, 0, 1, 1);
            var gridFitter = BookMenuLayout.Component<AspectRatioFitter>(grid);
            if (gridFitter.aspectMode != AspectRatioFitter.AspectMode.FitInParent)
                gridFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            float aspectRatio = 289f / Height(30);
            if (!Mathf.Approximately(gridFitter.aspectRatio, aspectRatio))
                gridFitter.aspectRatio = aspectRatio;
            if (!gridFitter.enabled) gridFitter.enabled = true;
            foreach (var slot in view.slots)
            {
                if (slot.button.transform.parent != grid)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEditor.Undo.SetTransformParent(slot.button.transform, grid, "Update Menu Book Inventory");
                    else
#endif
                        slot.button.transform.SetParent(grid, false);
                }
            }
            foreach (var locked in view.lockedRows)
                if (locked != null) locked.SetActive(false);
            ArrangeSlots(view.slots, unlocked, Height(30), true, true);
        }

        public static float Height(int unlocked) => 11 + 28 * Mathf.Clamp(Mathf.CeilToInt(unlocked / 10f), 1, 3);

        private static void SetIfChanged(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            if (rect.anchorMin != anchorMin) rect.anchorMin = anchorMin;
            if (rect.anchorMax != anchorMax) rect.anchorMax = anchorMax;
            if (rect.pivot != pivot) rect.pivot = pivot;
            if (rect.offsetMin != Vector2.zero) rect.offsetMin = Vector2.zero;
            if (rect.offsetMax != Vector2.zero) rect.offsetMax = Vector2.zero;
        }

        private static void StyleCount(Text count)
        {
            count.font = CountFont;
            count.material = null;
            count.color = Color.white;
            count.canvasRenderer.SetColor(Color.white);
            var countBackground = count.GetComponent<Image>();
            if (countBackground != null) countBackground.enabled = false;
            count.fontSize = 10;
            count.fontStyle = FontStyle.Normal;
            count.supportRichText = false;
            count.alignment = TextAnchor.LowerRight;
            count.horizontalOverflow = HorizontalWrapMode.Overflow;
            count.verticalOverflow = VerticalWrapMode.Overflow;
            count.rectTransform.anchorMin = new Vector2(.06f, .06f);
            count.rectTransform.anchorMax = new Vector2(.94f, .94f);
            count.rectTransform.offsetMin = count.rectTransform.offsetMax = Vector2.zero;
            var outline = BookMenuLayout.Component<Outline>(count.transform);
            foreach (var effect in count.GetComponents<Shadow>()) effect.enabled = effect == outline;
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(.5f, -.5f);
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }

        public static void Apply(Image background, BookMenuView.Slot[] slots, int unlocked)
        {
            if (Art.Length < 4) return;
            int rows = Mathf.Clamp(Mathf.CeilToInt(unlocked / 10f), 1, 3);
            background.sprite = Art[rows - 1];
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.preserveAspect = false;
            background.raycastTarget = false;
            ArrangeSlots(slots, unlocked, Height(unlocked), false, false);
        }

        private static void ArrangeSlots(BookMenuView.Slot[] slots, int unlocked, float height,
            bool showSlotGraphic, bool menuRowSprites)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var rect = (RectTransform)slot.button.transform;
                // 원본은 27px 정사각 슬롯을 28px 간격으로 배치해 슬롯 사이에 1px 여백을 둔다.
                float x = 4 + i % 10 * 28;
                float top = 5 + i / 10 * 28;
                rect.anchorMin = new Vector2(x / 289f, 1 - (top + 27) / height);
                rect.anchorMax = new Vector2((x + 27) / 289f, 1 - top / height);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                int artIndex = menuRowSprites ? (i < 10 ? 4 : 5) : 3;
                slot.button.image.sprite = Art[Mathf.Min(artIndex, Art.Length - 1)];
                slot.button.image.type = Image.Type.Simple;
                slot.button.image.color = Color.white;
                var mask = slot.button.GetComponent<Mask>();
                if (mask == null)
                {
                    mask = BookMenuLayout.Component<Mask>(slot.button.transform);
                    slot.icon.rectTransform.anchorMin = Vector2.zero;
                    slot.icon.rectTransform.anchorMax = Vector2.one;
                    slot.icon.rectTransform.offsetMin = slot.icon.rectTransform.offsetMax = Vector2.zero;
                    // 수량은 잘리는 모서리에서 조금 띄운다.
                    slot.count.rectTransform.anchorMin = new Vector2(.08f, .08f);
                    slot.count.rectTransform.anchorMax = new Vector2(.92f, .92f);
                    slot.count.rectTransform.offsetMin = slot.count.rectTransform.offsetMax = Vector2.zero;
                }
                mask.showMaskGraphic = showSlotGraphic;
                // Older serialized scenes can have an extra Count Text below the
                // one referenced by Slot.count. Style every Count child so a stale
                // duplicate cannot render the visible number in the old gray ink.
                foreach (var count in slot.button.GetComponentsInChildren<Text>(true)
                    .Where(text => text == slot.count || text.name == "Count"))
                    StyleCount(count);
                EnsureSelection(slot);
                slot.button.gameObject.SetActive(i < unlocked);
            }
        }

        public static BookInventorySelection EnsureSelection(BookMenuView.Slot slot)
        {
            if (slot.selected == null)
            {
                var selected = BookMenuLayout.Node(slot.button.transform, "Selected", 0, 0, 1, 1);
                slot.selected = BookMenuLayout.Component<Image>(selected);
            }
            slot.selected.sprite = null;
            slot.selected.color = Color.clear;
            slot.selected.enabled = false;
            slot.selected.raycastTarget = false;
            slot.selected.maskable = false;
            // 원본 셀렉터 한 프레임은 34x34이고 슬롯은 27x27이다. 중앙을 맞춰 원본 비율을 유지한다.
            const float selectorPadding = 3.5f / 27f;
            slot.selected.rectTransform.anchorMin = new Vector2(-selectorPadding, -selectorPadding);
            slot.selected.rectTransform.anchorMax = new Vector2(1 + selectorPadding, 1 + selectorPadding);
            slot.selected.rectTransform.offsetMin = slot.selected.rectTransform.offsetMax = Vector2.zero;
            if (!Application.isPlaying || slot.button.gameObject.activeInHierarchy)
                slot.selected.rectTransform.SetAsLastSibling();
            var animation = BookMenuLayout.Component<BookInventorySelection>(slot.selected.transform);
            animation.image = slot.selected;
            animation.frames = Selector;
            animation.framesPerSecond = 10;
            animation.corners = new[]
            {
                Corner(slot.selected.transform, "TopLeft"),
                Corner(slot.selected.transform, "TopRight"),
                Corner(slot.selected.transform, "BottomLeft"),
                Corner(slot.selected.transform, "BottomRight")
            };
            // New Images start enabled with no sprite. Always synchronize even when
            // selection is already false, including scene construction and refreshes.
            animation.RefreshVisual();
            if (!Application.isPlaying || slot.button.gameObject.activeInHierarchy)
                slot.count.rectTransform.SetAsLastSibling();
            return animation;
        }

        private static Image Corner(Transform parent, string name)
        {
            var rect = BookMenuLayout.Node(parent, name, 0, 0, 12f / 34f, 12f / 34f);
            var image = BookMenuLayout.Component<Image>(rect);
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.maskable = false;
            return image;
        }

        public static void SetSelected(BookMenuView.Slot slot, bool selected, bool restart = false)
            => EnsureSelection(slot).SetSelected(selected, restart);
    }
}
