using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>에디터와 런타임 대체 생성이 공유하는 기본 배치. 완성된 뷰는 다시 배치하지 않는다.</summary>
    public static class ShopBookLayout
    {
        public static readonly Color Ink = new Color(.29f, .18f, .12f);
        public static ShopBookView Build(RectTransform root)
        {
            var v = root.GetComponent<ShopBookView>();
            if (v == null) v = root.gameObject.AddComponent<ShopBookView>();
            if (v.configured) { Upgrade(v); return v; }
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = UILayers.Book;
            if (!root.GetComponent<GraphicRaycaster>()) root.gameObject.AddComponent<GraphicRaycaster>();
            var dim = Picture(root, "Dim", 0, 0, 1, 1, null);
            dim.color = new Color(0, 0, 0, .65f);
            dim.raycastTarget = true;
            v.stage = Node(root, "DeskStage", .5f, .5f, .5f, .5f);
            v.stage.sizeDelta = new Vector2(820, 600);
            Picture(v.stage, "Desk", 0, 0, 1, 1, Art("Book Desk/5"));
            v.book = Node(v.stage, "Book", .5f, .5f, .5f, .5f);
            v.book.sizeDelta = new Vector2(663, 418);
            v.book.anchoredPosition = new Vector2(0, 36);
            v.idle = Art("Inventory Book/Book Idle/1");
            v.background = Picture(v.book, "Background", 0, 0, 1, 1, v.idle);
            v.opening = Frames("Open");
            v.closing = Frames("Close");
            var pages = Node(v.book, "Pages", 0, 0, 1, 1);
            v.pages = pages.gameObject.AddComponent<CanvasGroup>();
            var left = Node(pages, "LeftPage", .055f, .12f, .465f, .91f);
            v.shopTitle = Label(left, "ShopTitle", "OO's shop", 0, .88f, .66f, 1, 18);
            v.shopTitle.fontStyle = FontStyle.Bold;
            v.shopTitle.resizeTextForBestFit = true;
            v.shopTitle.resizeTextMinSize = 11;
            v.shopTitle.resizeTextMaxSize = 18;
            v.money = Label(left, "PlayerCurrency", "1,250 G", .66f, .89f, 1, .99f, 13, TextAnchor.MiddleRight);
            v.money.fontStyle = FontStyle.Bold;
            var line = Picture(left, "HeaderRule", 0, .865f, 1, .87f, null);
            line.color = new Color(.5f, .33f, .19f, .45f);
            Label(left, "ListCaption", "판매 상품", 0, .78f, 1, .86f, 10);
            var list = Node(left, "ProductList", 0, .05f, 1, .77f);
            v.productsScroll = Scroll(list, out v.products, true);
            v.rowTemplate = CreateRow(v.products);
            v.rowTemplate.gameObject.SetActive(false);
            v.emptyList = Label(left, "EmptyList", "판매 중인 상품이 없습니다.", 0, .32f, .9f, .6f, 13, TextAnchor.MiddleCenter);
            v.emptyList.gameObject.SetActive(false);
            Label(left, "Hint", "휠 / 드래그로 목록 보기", 0, -.025f, 1, .04f, 9, TextAnchor.MiddleCenter);

            var tabs = Node(pages, "CategoryBookmarks", -.105f, .13f, .052f, .9f);
            v.categoriesScroll = Scroll(tabs, out v.categories, false);
            v.categoryTemplate = Button(v.categories, "CategoryTemplate", "카테고리", 0, 1, 1, 1, "Content/8 Side tabs/2", 11);
            v.categoryTemplate.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
            v.categoryTemplate.gameObject.SetActive(false);

            v.detailPaper = Picture(pages, "DetailPaper", .51f, .095f, .98f, .96f, Art("Shop Book/detail paper")).rectTransform;
            var paper = v.detailPaper;
            Label(paper, "Caption", "상품 안내", .16f, .82f, .84f, .91f, 10, TextAnchor.MiddleCenter);
            v.productIcon = Picture(paper, "ProductIcon", .4f, .65f, .6f, .81f, null);
            v.productIcon.preserveAspect = true;
            v.productIcon.enabled = false;
            v.productName = Label(paper, "ProductName", "상품을 선택하세요", .14f, .54f, .86f, .65f, 17, TextAnchor.MiddleCenter);
            v.productName.fontStyle = FontStyle.Bold;
            v.productName.resizeTextForBestFit = true;
            v.productName.resizeTextMinSize = 11;
            v.productName.resizeTextMaxSize = 17;
            v.productPrice = Label(paper, "ProductPrice", "", .14f, .46f, .86f, .54f, 13, TextAnchor.MiddleCenter);
            var desc = Node(paper, "DescriptionScroll", .15f, .22f, .85f, .45f);
            v.descriptionScroll = Scroll(desc, out var descriptionContent, false);
            v.description = Label(descriptionContent, "Description", "왼쪽 목록에서 상품을 선택하면\n상세 설명이 표시됩니다.", 0, 0, 1, 1, 12, TextAnchor.UpperLeft);
            descriptionContent.sizeDelta = new Vector2(0, 110);
            v.buy = Button(paper, "Buy", "구매", .27f, .095f, .73f, .195f, "Content/4 Buttons/0", 15);
            v.buy.interactable = false;
            v.message = Label(paper, "PurchaseMessage", "", .1f, .035f, .9f, .09f, 9, TextAnchor.MiddleCenter);
            BuildInventory(v);
            v.close = Button(v.stage, "Close", "닫기 ×", .865f, .92f, .975f, .98f, "Content/4 Buttons/0", 12);
            BuildPreview(v);
            v.configured = true;
            Upgrade(v);
            v.Fit();
            return v;
        }

        /// <summary>기존 씬/프리팹에 새 컨트롤을 한 번 추가한다. 이후 사용자가 편집한 배치는 유지한다.</summary>
        public static void Upgrade(ShopBookView v)
        {
            if (v.layoutVersion < 2) UpgradeQuantityLayout(v);
            if (v.layoutVersion >= 10) return;
            if (v.layoutVersion >= 3)
            {
                foreach (var slot in v.slots) BookInventoryLayout.EnsureSelection(slot);
                ArrangeInventory(v, 10, true);
                v.layoutVersion = 10;
                return;
            }
            Remove(v.stage.Find("Controls"));
            Remove(v.inventory.transform.Find("Capacity"));
            v.bookCenter = Vector2.zero;
            var grid = (RectTransform)v.slots[0].button.transform.parent;
            grid.anchoredPosition = new Vector2(0, -8);
            foreach (var slot in v.slots)
            {
                slot.button.interactable = true;
                slot.button.transition = Selectable.Transition.None;
                if (slot.button.GetComponent<ShopInventorySlot>() == null)
                    slot.button.gameObject.AddComponent<ShopInventorySlot>();
                BookInventoryLayout.EnsureSelection(slot);
            }
            var tooltip = Picture(v.stage, "InventoryTooltip", .5f, .5f, .5f, .5f, Art("Content/5 Holders/7"));
            tooltip.type = Image.Type.Sliced;
            v.inventoryTooltip = tooltip.rectTransform;
            v.inventoryTooltip.pivot = new Vector2(.5f, 0);
            v.inventoryTooltip.sizeDelta = new Vector2(270, 140);
            v.inventoryTooltipText = Label(tooltip.transform, "Description", "", 0, 0, 1, 1, 12, TextAnchor.UpperLeft);
            v.inventoryTooltipText.rectTransform.offsetMin = new Vector2(12, 10);
            v.inventoryTooltipText.rectTransform.offsetMax = new Vector2(-12, -10);
            v.inventoryTooltip.gameObject.SetActive(false);
            ArrangeInventory(v, 10, true);
            v.layoutVersion = 10;
        }

        public static void ArrangeInventory(ShopBookView v, int unlocked, bool visible)
        {
            int slots = Mathf.Clamp(unlocked, 0, v.slots.Length);
            v.inventory.SetActive(visible);
            for (int i = 0; i < v.slots.Length; i++) v.slots[i].button.gameObject.SetActive(i < slots);
            var tray = (RectTransform)v.inventory.transform;
            float height = BookInventoryLayout.Height(slots) * tray.rect.width / 289f;
            tray.anchoredPosition = new Vector2(0, 8);
            tray.sizeDelta = new Vector2(tray.sizeDelta.x, height);
            var grid = (RectTransform)v.slots[0].button.transform.parent;
            grid.anchorMin = Vector2.zero;
            grid.anchorMax = Vector2.one;
            grid.offsetMin = grid.offsetMax = Vector2.zero;
            BookInventoryLayout.Apply(v.inventory.GetComponent<Image>(), v.slots, slots);
            v.book.anchoredPosition = v.bookCenter + Vector2.up * (visible ? Mathf.Max(0, height - 73) : 0);
        }

        private static void Remove(Transform node)
        {
            if (node == null) return;
            node.gameObject.SetActive(false);
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.Undo.DestroyObjectImmediate(node.gameObject); return; }
#endif
            Object.Destroy(node.gameObject);
        }

        private static void UpgradeQuantityLayout(ShopBookView v)
        {
            v.flipLeft = Resources.LoadAll<Sprite>(BookMenuLayout.ArtRoot + "Inventory Book/Book Page Flip/Flip Left")
                .OrderBy(s => int.TryParse(s.name.Split('_')[0], out int n) ? n : 0).ToArray();
            v.flipRight = Resources.LoadAll<Sprite>(BookMenuLayout.ArtRoot + "Inventory Book/Book Page Flip/Flip Right")
                .OrderBy(s => int.TryParse(s.name.Split('_')[0], out int n) ? n : 0).ToArray();
            if (v.quantityControls == null)
            {
                var group = Node(v.detailPaper, "QuantityControls", .5f, .25f, .5f, .25f);
                group.sizeDelta = new Vector2(112, 32);
                v.quantityControls = group.gameObject;
                var sprites = Resources.LoadAll<Sprite>("Sprites/UI/Button/PlusMinus").OrderBy(s => s.rect.x).ToArray();
                v.decrease = QuantityButton(group, "Minus", 0, sprites, 2);
                var box = Picture(group, "QuantityBox", .5f, .5f, .5f, .5f, Art("Content/5 Holders/7"));
                box.rectTransform.sizeDelta = new Vector2(32, 32);
                box.type = Image.Type.Sliced;
                v.quantity = Label(box.transform, "Value", "1", 0, 0, 1, 1, 13, TextAnchor.MiddleCenter);
                v.quantity.fontStyle = FontStyle.Bold;
                v.quantity.resizeTextForBestFit = true;
                v.quantity.resizeTextMinSize = 8;
                v.quantity.resizeTextMaxSize = 13;
                v.increase = QuantityButton(group, "Plus", 1, sprites, 0);
                v.cancelQuantity = Button(v.detailPaper, "CancelQuantity", "취소", .17f, .095f, .48f, .195f, "Content/4 Buttons/0", 15);
                v.cancelQuantity.interactable = false;
                var colors = v.cancelQuantity.colors;
                colors.disabledColor = new Color(.55f, .55f, .55f, .55f);
                v.cancelQuantity.colors = colors;
                v.buy.GetComponent<RectTransform>().anchorMin = new Vector2(.52f, .095f);
                v.buy.GetComponent<RectTransform>().anchorMax = new Vector2(.83f, .195f);
                var desc = (RectTransform)v.descriptionScroll.transform;
                desc.anchorMin = new Vector2(desc.anchorMin.x, .32f);
            }
            v.book.anchoredPosition += Vector2.up * 60;
            var tray = (RectTransform)v.inventory.transform;
            tray.anchorMin = tray.anchorMax = new Vector2(.5f, 0);
            tray.pivot = new Vector2(.5f, 0);
            tray.anchoredPosition = new Vector2(0, 34);
            tray.sizeDelta = new Vector2(390, 146);
            var grid = (RectTransform)v.slots[0].button.transform.parent;
            grid.anchorMin = grid.anchorMax = new Vector2(.5f, 1);
            grid.pivot = new Vector2(.5f, 1);
            grid.anchoredPosition = new Vector2(0, -28);
            grid.sizeDelta = new Vector2(360, 108);
            for (int i = 0; i < v.slots.Length; i++)
            {
                var slot = (RectTransform)v.slots[i].button.transform;
                slot.anchorMin = slot.anchorMax = new Vector2(0, 1);
                slot.pivot = new Vector2(0, 1);
                slot.anchoredPosition = new Vector2(i % 10 * 36 + 2, -(i / 10) * 36);
                slot.sizeDelta = new Vector2(32, 32);
            }
            v.layoutVersion = 2;
        }

        private static Button QuantityButton(Transform parent, string name, float x, Sprite[] sprites, int index)
        {
            var image = Picture(parent, name, x, .5f, x, .5f, sprites.Length > index ? sprites[index] : null);
            image.rectTransform.pivot = new Vector2(x, .5f);
            image.rectTransform.sizeDelta = new Vector2(32, 32);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<ShopQuantityButton>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { pressedSprite = sprites.Length > index + 1 ? sprites[index + 1] : null };
            UITheme.ApplyArrow(button, name == "Plus");
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static void BuildInventory(ShopBookView v)
        {
            var tray = Picture(v.stage, "Inventory", .135f, .065f, .865f, .215f, Art("Content/5 Holders/7"));
            tray.type = Image.Type.Sliced;
            v.inventory = tray.gameObject;
            var grid = Node(tray.transform, "Slots", .025f, .07f, .975f, .72f);
            v.slots = new BookMenuView.Slot[Inventory.TotalSlots];
            for (int i = 0; i < v.slots.Length; i++)
            {
                int col = i % 15, row = i / 15;
                var b = Button(grid, "Slot_" + i, "", col / 15f + .003f, .5f - row * .5f, (col+1)/15f - .003f, .98f - row * .5f, "Content/5 Holders/7", 9);
                b.interactable = false;
                var icon = Picture(b.transform, "Icon", .12f, .08f, .88f, .92f, null);
                icon.preserveAspect = true;
                icon.enabled = false;
                v.slots[i] = new BookMenuView.Slot
                {
                    button = b, icon = icon,
                    count = Label(b.transform, "Count", "", 0, 0, .95f, 1, 8, TextAnchor.LowerRight)
                };
            }
        }

        private static void BuildPreview(ShopBookView v)
        {
            v.products.sizeDelta = new Vector2(0, 299);
            v.productPreview = Node(v.products, "EditorPreview", 0, 0, 1, 1).gameObject;
            string[] names = { "딸기 씨앗", "나무 씨앗", "배낭 1단계", "판매 상품", "판매 상품" };
            for (int i = 0; i < names.Length; i++)
            {
                var row = Object.Instantiate(v.rowTemplate, v.productPreview.transform);
                row.name = "PreviewRow_" + i;
                row.gameObject.SetActive(true);
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0, -i * 61);
                row.title.text = names[i];
                row.price.text = $"{(i + 1) * 100:N0} G";
                row.selection.enabled = i == 0;
            }
            v.categories.sizeDelta = new Vector2(0, 160);
            v.categoryPreview = Node(v.categories, "EditorPreview", 0, 0, 1, 1).gameObject;
            string[] categories = { "전체", "씨앗", "나무", "가방" };
            for (int i = 0; i < categories.Length; i++)
            {
                var tab = Object.Instantiate(v.categoryTemplate, v.categoryPreview.transform);
                tab.name = "PreviewCategory_" + i;
                tab.gameObject.SetActive(true);
                var rt = (RectTransform)tab.transform;
                rt.pivot = new Vector2(.5f, 1);
                rt.anchoredPosition = new Vector2(0, -i * 40);
                tab.GetComponentInChildren<Text>().text = categories[i];
            }
        }

        private static ShopProductRow CreateRow(RectTransform parent)
        {
            var button = Button(parent, "ProductRowTemplate", "", 0, 1, 1, 1, "Content/5 Holders/7", 12);
            var rect = (RectTransform)button.transform;
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(0, 55);
            var row = button.gameObject.AddComponent<ShopProductRow>();
            row.button = button;
            row.selection = Picture(rect, "Selected", 0, 0, 1, 1, null);
            row.selection.color = new Color(.63f, .35f, .1f, .18f);
            row.selection.enabled = false;
            row.icon = Picture(rect, "Icon", .035f, .18f, .19f, .82f, null);
            row.icon.preserveAspect = true;
            row.icon.enabled = false;
            row.title = Label(rect, "ProductName", "상품 이름", .23f, .43f, .95f, .9f, 14);
            row.title.fontStyle = FontStyle.Bold;
            row.title.resizeTextForBestFit = true;
            row.title.resizeTextMinSize = 10;
            row.title.resizeTextMaxSize = 14;
            row.price = Label(rect, "Price", "100 G", .23f, .1f, .95f, .43f, 11);
            return row;
        }

        private static ScrollRect Scroll(RectTransform parent, out RectTransform content, bool holderBar)
        {
            var viewport = Node(parent, "Viewport", 0, 0, holderBar ? .91f : 1, 1);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(1, 1, 1, .001f);
            content = Node(viewport, "Content", 0, 1, 1, 1);
            content.pivot = new Vector2(.5f, 1);
            var scroll = parent.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            if (holderBar)
            {
                var track = Picture(parent, "HolderScrollbar", .945f, 0, .99f, 1, Art("Content/5 Holders/16"));
                track.raycastTarget = true;
                var area = Node(track.transform, "SlidingArea", 0, 0, 1, 1);
                var handle = Picture(area, "Handle", 0, 0, 1, 1, Art("Content/5 Holders/15"));
                handle.raycastTarget = true;
                var bar = track.gameObject.AddComponent<Scrollbar>();
                bar.direction = Scrollbar.Direction.BottomToTop;
                bar.handleRect = handle.rectTransform;
                bar.targetGraphic = handle;
                bar.navigation = new Navigation { mode = Navigation.Mode.None };
                UITheme.ApplyShopScrollbar(bar);
                scroll.verticalScrollbar = bar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
            return scroll;
        }

        public static RectTransform Node(Transform parent, string name, float x0, float y0, float x1, float y1)
            => BookMenuLayout.Node(parent, name, x0, y0, x1, y1);
        private static Sprite Art(string path) => BookMenuLayout.Sprite(path);
        private static Sprite[] Frames(string action) => Resources.LoadAll<Sprite>(BookMenuLayout.ArtRoot + "Inventory Book/Book Open and Close/" + action)
            .OrderBy(s => int.TryParse(s.name.Split('_')[0], out int n) ? n : 0).ToArray();
        private static Image Picture(Transform parent, string name, float x0, float y0, float x1, float y1, Sprite sprite)
        {
            var image = Node(parent, name, x0, y0, x1, y1).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }
        private static Text Label(Transform parent, string name, string value, float x0, float y0, float x1, float y1, int size, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var text = Node(parent, name, x0, y0, x1, y1).gameObject.AddComponent<Text>();
            text.font = Resources.Load<Font>("Fonts/PF스타더스트 3.0") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = align;
            text.color = Ink;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }
        private static Button Button(Transform parent, string name, string value, float x0, float y0, float x1, float y1, string sprite, int size)
        {
            var image = Picture(parent, name, x0, y0, x1, y1, Art(sprite));
            if (sprite == "Content/5 Holders/7") image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(image.transform, "Label", value, .06f, .04f, .94f, .96f, size, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(value) && name != "CategoryTemplate") UITheme.ApplyButton(button);
            if (name == "Close") UITheme.ApplyClose(button);
            return button;
        }
    }
}
