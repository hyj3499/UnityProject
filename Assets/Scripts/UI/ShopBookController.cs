using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    public sealed class ShopBookController : MonoBehaviour
    {
        private UIManager _ui;
        private GameManager _game;
        private ShopBookView _view;
        private ShopDefinition _shop;
        private ShopProduct _selected;
        private string _category;
        private int _categoryIndex, _quantity = 1;
        private const string SoldCategory = "\u0001buyback";
        private int _hoveredSlot = -1;
        private bool _transacting;
        private bool _open, _closing, _busy;
        private Coroutine _animation;
        private Vector2 _paperHome;
        private readonly List<ShopProductRow> _rows = new List<ShopProductRow>();
        private readonly List<ShopProduct> _visible = new List<ShopProduct>();
        private readonly List<ShopProduct> _catalog = new List<ShopProduct>();
        private readonly List<Button> _tabs = new List<Button>();
        private readonly List<string> _tabCategories = new List<string>();
        public bool IsOpen => _open;
        public ShopBookView View => _view;

        public void Boot(UIManager ui, GameManager game, RectTransform canvas)
        {
            _ui = ui;
            _game = game;
            _view = FindObjectsByType<ShopBookView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(v => v.configured && v.gameObject.scene == game.gameObject.scene);
            if (_view == null)
            {
                var sceneCanvas = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(c => c.isRootCanvas && c.name == "Canvas" && c.gameObject.scene == game.gameObject.scene);
                var root = ShopBookLayout.Node(sceneCanvas != null ? sceneCanvas.transform : canvas, "ShopBookUi", 0, 0, 1, 1);
                _view = ShopBookLayout.Build(root);
            }
            ShopBookLayout.Upgrade(_view);
            _view.controller = this;
            if (_view.productPreview != null) _view.productPreview.SetActive(false);
            if (_view.categoryPreview != null) _view.categoryPreview.SetActive(false);
            _paperHome = _view.detailPaper.anchoredPosition;
            _view.close.onClick.AddListener(Close);
            _view.buy.onClick.AddListener(Buy);
            _view.decrease.onClick.AddListener(DecreaseQuantity);
            _view.increase.onClick.AddListener(IncreaseQuantity);
            _view.cancelQuantity.onClick.AddListener(ResetQuantity);
            for (int i = 0; i < _view.slots.Length; i++)
                _view.slots[i].button.GetComponent<ShopInventorySlot>().Bind(this, i);
            _game.OnMoneyChanged += Refresh;
            _game.OnHotbarChanged += Refresh;
            _game.Inventory.OnChanged += Refresh;
            _game.OnDayChanged += DayChanged;
            _view.gameObject.SetActive(false);
        }

        public void Open(ShopDefinition shop)
        {
            shop.Validate();
            if (_animation != null) StopCoroutine(_animation);
            _shop = shop;
            _selected = null;
            _category = null;
            _categoryIndex = 0;
            _hoveredSlot = -1;
            _view.inventoryTooltip.gameObject.SetActive(false);
            _open = true;
            _closing = false;
            _view.gameObject.SetActive(true);
            _view.transform.SetAsLastSibling();
            _view.Fit();
            _view.shopTitle.text = shop.title;
            ShopBookLayout.ArrangeInventory(_view, _game.Inventory.unlockedSlots, shop.allowSelling);
            BuildCategories();
            BuildProducts();
            _animation = StartCoroutine(AnimateOpen());
        }

        public void Close()
        {
            if (!_open || _closing) return;
            if (_animation != null) StopCoroutine(_animation);
            _closing = true;
            _hoveredSlot = -1;
            _view.inventoryTooltip.gameObject.SetActive(false);
            _animation = StartCoroutine(AnimateClose());
        }

        private IEnumerator AnimateOpen()
        {
            _busy = true;
            _view.pages.alpha = 0;
            _view.pages.blocksRaycasts = false;
            _view.detailPaper.gameObject.SetActive(false);
            foreach (var sprite in _view.opening)
            {
                SetFrame(sprite);
                yield return new WaitForSecondsRealtime(1f / Mathf.Max(1, _view.framesPerSecond));
            }
            SetFrame(_view.idle);
            _view.pages.alpha = 1;
            yield return DropPaper();
            FinishAnimation();
        }

        private IEnumerator DropPaper()
        {
            _view.detailPaper.gameObject.SetActive(true);
            float duration = Mathf.Max(.1f, _view.paperDropSeconds);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = elapsed / duration;
                // 빠르게 내려온 뒤 살짝 눌러 붙는 ease-out-back.
                float q = t - 1;
                float eased = 1 + 2.1f * q * q * q + 1.1f * q * q;
                _view.detailPaper.anchoredPosition = _paperHome + Vector2.up * (650 * (1 - eased));
                yield return null;
            }
            _view.detailPaper.anchoredPosition = _paperHome;
        }

        private void FinishAnimation()
        {
            _view.pages.blocksRaycasts = true;
            _busy = false;
            _animation = null;
            Refresh();
        }

        private void ChangeCategory(string category, int index)
        {
            if (!_open || _closing || _busy || _categoryIndex == index) return;
            bool right = index > _categoryIndex;
            _categoryIndex = index;
            _category = category;
            _animation = StartCoroutine(AnimateCategory(right));
        }

        private IEnumerator AnimateCategory(bool right)
        {
            _busy = true;
            _view.pages.blocksRaycasts = false;
            _view.inventoryTooltip.gameObject.SetActive(false);
            _view.pages.alpha = 0;
            _view.detailPaper.gameObject.SetActive(false);
            foreach (var sprite in (right ? _view.flipRight : _view.flipLeft) ?? Array.Empty<Sprite>())
            {
                SetFrame(sprite);
                yield return new WaitForSecondsRealtime(1f / Mathf.Max(1, _view.framesPerSecond));
            }
            SetFrame(_view.idle);
            BuildProducts();
            _view.pages.alpha = 1;
            yield return DropPaper();
            FinishAnimation();
        }

        private IEnumerator AnimateClose()
        {
            _busy = true;
            _view.pages.alpha = 0;
            _view.pages.blocksRaycasts = false;
            foreach (var sprite in _view.closing)
            {
                SetFrame(sprite);
                yield return new WaitForSecondsRealtime(1f / Mathf.Max(1, _view.framesPerSecond));
            }
            _view.detailPaper.anchoredPosition = _paperHome;
            _view.gameObject.SetActive(false);
            _open = _closing = _busy = false;
            _animation = null;
            _ui?.SyncPaused();
        }

        private void BuildCategories()
        {
            foreach (var tab in _tabs) { tab.gameObject.SetActive(false); Destroy(tab.gameObject); }
            _tabs.Clear();
            _tabCategories.Clear();
            var names = _shop.useCategories
                ? _shop.products.Select(p => string.IsNullOrWhiteSpace(p.category) ? Localization.Get("ui.shopbookcontroller.other") : p.category).Distinct().ToList()
                : new List<string>();
            bool enabled = _shop.allowSelling || (_shop.useCategories && _shop.products.Any(p => !string.IsNullOrWhiteSpace(p.category)));
            _view.categoriesScroll.gameObject.SetActive(enabled);
            if (!enabled) return;
            names.Insert(0, Localization.Get("ui.shopbookcontroller.all"));
            if (_shop.allowSelling) names.Add(SoldCategory);
            for (int i = 0; i < names.Count; i++)
            {
                string category = i == 0 ? null : names[i];
                int index = i;
                var tab = Instantiate(_view.categoryTemplate, _view.categories);
                tab.name = "Category_" + i;
                tab.gameObject.SetActive(true);
                var rt = (RectTransform)tab.transform;
                rt.pivot = new Vector2(.5f, 1);
                rt.anchoredPosition = new Vector2(0, -i * 40);
                tab.GetComponentInChildren<Text>().text = names[i] == SoldCategory ? Localization.Get("ui.shopbookcontroller.sold") : names[i];
                tab.onClick.AddListener(() => ChangeCategory(category, index));
                _tabs.Add(tab);
                _tabCategories.Add(category);
            }
            _view.categories.sizeDelta = new Vector2(0, names.Count * 40);
            _view.categoriesScroll.verticalNormalizedPosition = 1;
        }

        private void SetFrame(Sprite sprite)
        {
            if (sprite == null || _view.idle == null) return;
            var rect = _view.background.rectTransform;
            Vector2 ratio = _view.book.rect.size / _view.idle.rect.size;
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = Vector2.Scale(sprite.rect.size, ratio);
            rect.anchoredPosition = Vector2.Scale(sprite.rect.center - _view.idle.rect.center, ratio);
            _view.background.sprite = sprite;
        }

        private void BuildProducts()
        {
            foreach (var row in _rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            _rows.Clear();
            _visible.Clear();
            _catalog.Clear();
            _catalog.AddRange(_shop.products);
            _catalog.AddRange(ShopSellService.Products(_game.Data, _shop));
            _visible.AddRange(_catalog.Where(p => _category == null || (_category == SoldCategory ? p.IsBuyback
                : !p.IsBuyback && (string.IsNullOrWhiteSpace(p.category) ? Localization.Get("ui.shopbookcontroller.other") : p.category) == _category)));
            for (int i = 0; i < _visible.Count; i++)
            {
                var product = _visible[i];
                var row = Instantiate(_view.rowTemplate, _view.products);
                row.name = "Product_" + product.id;
                row.gameObject.SetActive(true);
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0, -i * 61);
                row.title.text = product.Name;
                row.price.text = $"{product.price:N0} G";
                row.icon.sprite = Icon(product);
                row.icon.enabled = row.icon.sprite != null;
                row.button.onClick.AddListener(() => Select(product));
                _rows.Add(row);
            }
            _view.products.sizeDelta = new Vector2(0, Mathf.Max(0, _visible.Count * 61 - 6));
            _view.emptyList.gameObject.SetActive(_visible.Count == 0);
            _view.productsScroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            _view.productsScroll.verticalNormalizedPosition = 1;
            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].image.color = _tabCategories[i] == _category
                    ? Color.white : new Color(.77f, .67f, .51f);
            Select(_visible.FirstOrDefault(p => _selected != null && p.id == _selected.id && p.IsBuyback == _selected.IsBuyback) ?? _visible.FirstOrDefault());
        }

        private static Sprite Icon(ShopProduct product)
            => product.backpackLevel > 0 ? (product.backpackLevel == 1 ? AssetLibrary.UiBagLv1 : AssetLibrary.UiBagLv2) : product.Def?.GetSprite();

        private void Select(ShopProduct product)
        {
            _selected = product;
            _quantity = 1;
            bool bulk = product != null && product.SupportsQuantity;
            _view.quantityControls.SetActive(bulk);
            _view.cancelQuantity.gameObject.SetActive(bulk);
            var buyRect = (RectTransform)_view.buy.transform;
            buyRect.anchorMin = new Vector2(bulk ? .52f : .27f, .095f);
            buyRect.anchorMax = new Vector2(bulk ? .83f : .73f, .195f);
            var descriptionRect = (RectTransform)_view.descriptionScroll.transform;
            descriptionRect.anchorMin = new Vector2(descriptionRect.anchorMin.x, bulk ? .32f : .22f);
            _view.message.text = "";
            _view.productName.text = product?.Name ?? Localization.Get("ui.shopbookcontroller.select_a_product");
            _view.productIcon.sprite = product == null ? null : Icon(product);
            _view.productIcon.enabled = _view.productIcon.sprite != null;
            _view.description.text = product == null ? Localization.Get("ui.shopbookcontroller.no_products_for_sale") : product.Description
                + (string.IsNullOrWhiteSpace(product.note) ? "" : "\n\n" + product.note);
            Canvas.ForceUpdateCanvases();
            _view.descriptionScroll.content.sizeDelta = new Vector2(0, Mathf.Max(_view.descriptionScroll.viewport.rect.height, _view.description.preferredHeight + 6));
            _view.descriptionScroll.StopMovement();
            _view.descriptionScroll.verticalNormalizedPosition = 1;
            Refresh();
        }

        private void IncreaseQuantity() => ChangeQuantity(1);
        private void DecreaseQuantity() => ChangeQuantity(-1);
        private void ChangeQuantity(int delta)
        {
            if (!_open || _busy || _selected == null || !_selected.SupportsQuantity) return;
            // 그 가방에 담을 수 있는 최대 총량보다 큰 수량은 선택할 필요가 없다.
            long limit = (long)Mathf.Max(1, _selected.Def.maxStack) * Mathf.Max(1, _game.Inventory.unlockedSlots);
            if (_selected.IsBuyback) limit = Math.Min(limit, ShopSellService.Stock(_game.Data, _selected, _shop));
            _quantity = (int)Math.Max(1L, Math.Min(Math.Min(limit, int.MaxValue), (long)_quantity + delta));
            _view.message.text = "";
            Refresh();
        }
        private void ResetQuantity()
        {
            if (!_open || _busy) return;
            _quantity = 1;
            _view.message.text = "";
            Refresh();
        }

        public void Refresh()
        {
            if (!_open || _shop == null || _transacting) return;
            _view.money.text = $"{_game.Money:N0} G";
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].selection.enabled = _visible[i] == _selected;
                int stock = ShopSellService.Stock(_game.Data, _visible[i], _shop);
                _rows[i].price.text = $"{_visible[i].price:N0} G" + (_visible[i].IsBuyback ? Localization.Format("ui.shopbookcontroller.0_remaining", stock) : "");
                _rows[i].price.color = ShopPurchaseService.UnavailableReason(_game, _visible[i], _shop) == null
                    ? ShopBookLayout.Ink : new Color(.61f, .28f, .2f);
            }
            int selectedStock = ShopSellService.Stock(_game.Data, _selected, _shop);
            if (_selected != null && _selected.IsBuyback) _quantity = Mathf.Clamp(_quantity, 1, Mathf.Max(1, selectedStock));
            string reason = ShopPurchaseService.UnavailableReason(_game, _selected, _shop);
            _view.buy.interactable = !_busy && reason == null;
            _view.quantity.text = _quantity.ToString();
            _view.cancelQuantity.interactable = !_busy && _quantity > 1;
            _view.decrease.interactable = !_busy && _quantity > 1;
            _view.increase.interactable = !_busy && _selected != null && _quantity < selectedStock;
            _view.productPrice.text = _selected == null ? "" : $"{(long)_selected.price * _quantity:N0} G";
            if (reason != null) _view.message.text = reason;
            else if (!_view.message.text.EndsWith("!")) _view.message.text = "";
            ShopBookLayout.ArrangeInventory(_view, _game.Inventory.unlockedSlots, _shop.allowSelling);
            if (!_shop.allowSelling) { _view.inventoryTooltip.gameObject.SetActive(false); return; }
            for (int i = 0; i < _view.slots.Length; i++)
            {
                var slot = _view.slots[i];
                bool unlocked = i < _game.Inventory.unlockedSlots && i < _game.Inventory.slots.Length;
                slot.button.gameObject.SetActive(unlocked);
                var stack = unlocked ? _game.Inventory.slots[i] : null;
                bool occupied = stack != null && !stack.IsEmpty;
                slot.icon.sprite = occupied ? ItemDatabase.Get(stack.itemId)?.GetSprite() : null;
                slot.icon.enabled = slot.icon.sprite != null;
                slot.count.text = occupied && stack.count > 1 ? stack.count.ToString() : "";
                slot.button.image.color = Color.white;
                BookInventoryLayout.SetSelected(slot, occupied && i == _hoveredSlot);
            }
            UpdateInventoryTooltip();
        }

        private void Buy()
        {
            if (!_open || _busy || _transacting || _selected == null || !_catalog.Contains(_selected)) return;
            bool success;
            string message;
            _transacting = true;
            try { success = ShopPurchaseService.TryBuy(_game, _selected, _quantity, out message, _shop); }
            finally { _transacting = false; }
            if (success) _quantity = 1;
            if (_selected.IsBuyback) BuildProducts();
            Refresh();
            _view.message.text = message;
        }

        private void DayChanged()
        {
            if (!_open || _transacting) return;
            BuildProducts();
        }

        public void HoverInventory(int index)
        {
            if (_hoveredSlot >= 0 && _hoveredSlot < _view.slots.Length)
                BookInventoryLayout.SetSelected(_view.slots[_hoveredSlot], false);
            _hoveredSlot = index;
            var stack = index >= 0 && index < Math.Min(_game.Inventory.unlockedSlots, _game.Inventory.slots.Length)
                ? _game.Inventory.slots[index] : null;
            if (index >= 0 && index < _view.slots.Length && stack != null && !stack.IsEmpty)
                BookInventoryLayout.SetSelected(_view.slots[index], true, true);
            UpdateInventoryTooltip();
        }

        public void LeaveInventory(int index)
        {
            if (_hoveredSlot != index) return;
            if (index >= 0 && index < _view.slots.Length)
                BookInventoryLayout.SetSelected(_view.slots[index], false);
            _hoveredSlot = -1;
            if (_view == null || _view.inventoryTooltip == null) return;
            _view.inventoryTooltip.gameObject.SetActive(false);
        }

        private void UpdateInventoryTooltip()
        {
            bool available = _open && !_busy && !_closing && _shop.allowSelling && _hoveredSlot >= 0
                && _hoveredSlot < Math.Min(_game.Inventory.unlockedSlots, _game.Inventory.slots.Length);
            var stack = available ? _game.Inventory.slots[_hoveredSlot] : null;
            var def = stack == null || stack.IsEmpty ? null : stack.Def;
            _view.inventoryTooltip.gameObject.SetActive(def != null);
            if (def == null) return;
            string price = def.sellPrice > 0 ? Localization.Format("ui.shopbookcontroller.sell_price_0_n0_g_each", def.sellPrice) : Localization.Get("ui.shopbookcontroller.this_item_cannot_be_sold");
            _view.inventoryTooltipText.text = $"{def.displayName}\n{price}\n\n{def.Description}"
                + (def.sellPrice > 0 ? Localization.Get("ui.shopbookcontroller.right_click_sell_one") : "");
            var tooltip = _view.inventoryTooltip;
            float height = Mathf.Clamp(_view.inventoryTooltipText.preferredHeight + 20, 90, _view.stage.rect.height - 24);
            tooltip.sizeDelta = new Vector2(tooltip.sizeDelta.x, height);
            Vector3 pos = _view.stage.InverseTransformPoint(_view.slots[_hoveredSlot].button.transform.position);
            var bounds = _view.stage.rect;
            tooltip.anchoredPosition = new Vector2(
                Mathf.Clamp(pos.x, bounds.xMin + tooltip.rect.width / 2 + 8, bounds.xMax - tooltip.rect.width / 2 - 8),
                Mathf.Clamp(pos.y + 22, bounds.yMin + 8, bounds.yMax - height - 8));
            tooltip.SetAsLastSibling();
        }

        public void SellInventory(int index)
        {
            if (!_open || _busy || _closing || _transacting || !_shop.allowSelling) return;
            bool success;
            string message;
            _transacting = true;
            try { success = ShopSellService.TrySell(_game, _shop, index, out message); }
            finally { _transacting = false; }
            if (success) BuildProducts();
            Refresh();
            _view.message.text = message;
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.OnMoneyChanged -= Refresh;
                _game.OnHotbarChanged -= Refresh;
                _game.OnDayChanged -= DayChanged;
                if (_game.Inventory != null) _game.Inventory.OnChanged -= Refresh;
            }
            if (_view == null) return;
            _view.close.onClick.RemoveListener(Close);
            _view.buy.onClick.RemoveListener(Buy);
            _view.decrease.onClick.RemoveListener(DecreaseQuantity);
            _view.increase.onClick.RemoveListener(IncreaseQuantity);
            _view.cancelQuantity.onClick.RemoveListener(ResetQuantity);
            // 런타임 복제 행/책갈피의 리스너가 이전 컨트롤러를 붙잡지 않도록 정리한다.
            foreach (var row in _rows) if (row != null) Destroy(row.gameObject);
            foreach (var tab in _tabs) if (tab != null) Destroy(tab.gameObject);
            _view.controller = null;
            _view.gameObject.SetActive(false);
        }
    }
}
