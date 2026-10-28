using System;
using System.Collections.Generic;
using System.Linq;

namespace FarmMVP
{
    [Serializable]
    public sealed class ShopBuybackData
    {
        public string id, shopId, itemId;
        public int unitPrice, quantity, expiresOnDay;
    }

    /// <summary>인벤토리 매입과 상점별 재구매 재고. UI 수명과 무관하게 GameData에 저장한다.</summary>
    public static class ShopSellService
    {
        private static readonly HashSet<GameManager> Transactions = new HashSet<GameManager>();
        internal static bool BeginTransaction(GameManager game) => game != null && Transactions.Add(game);
        internal static void EndTransaction(GameManager game) => Transactions.Remove(game);

        public static int NextMonday(int day) => Math.Max(1, day) + Seasons.DaysPerWeek - Seasons.DayOfWeek(day);

        public static void PruneExpired(GameData data)
        {
            if (data == null) return;
            if (data.shopBuybacks == null) data.shopBuybacks = new List<ShopBuybackData>();
            data.shopBuybacks.RemoveAll(e => e == null || e.quantity <= 0 || e.unitPrice <= 0
                || string.IsNullOrEmpty(e.id) || string.IsNullOrEmpty(e.shopId) || string.IsNullOrEmpty(e.itemId)
                || data.currentDay >= e.expiresOnDay);
        }

        public static List<ShopProduct> Products(GameData data, ShopDefinition shop)
        {
            PruneExpired(data);
            if (data == null || shop == null || !shop.allowSelling) return new List<ShopProduct>();
            return data.shopBuybacks.Where(e => e.shopId == shop.id && ItemDatabase.Get(e.itemId) != null)
                .Select(e => new ShopProduct
                {
                    id = "buyback:" + e.id, itemId = e.itemId, price = e.unitPrice,
                    buybackId = e.id, buybackShopId = e.shopId,
                    category = Localization.Get("items.shopsellservice.sold"), note = Localization.Get("items.shopsellservice.a_sold_item_it_will_disappear_from")
                }).ToList();
        }

        internal static ShopBuybackData Find(GameData data, ShopProduct product, ShopDefinition shop)
        {
            PruneExpired(data);
            if (data == null || product == null || !product.IsBuyback || shop == null || !shop.allowSelling
                || product.buybackShopId != shop.id) return null;
            return data.shopBuybacks.FirstOrDefault(e => e.id == product.buybackId && e.shopId == shop.id
                && e.itemId == product.itemId && e.unitPrice == product.price);
        }

        public static int Stock(GameData data, ShopProduct product, ShopDefinition shop)
            => product != null && !product.IsBuyback ? int.MaxValue : Find(data, product, shop)?.quantity ?? 0;

        public static string UnavailableReason(GameManager game, ShopDefinition shop, int slotIndex)
        {
            if (shop == null || !shop.allowSelling) return Localization.Get("items.shopsellservice.this_shop_does_not_buy_items");
            if (game?.Data == null || game.Inventory == null || slotIndex < 0
                || slotIndex >= Math.Min(game.Inventory.unlockedSlots, game.Inventory.slots.Length)) return Localization.Get("items.shopsellservice.select_an_item");
            var stack = game.Inventory.slots[slotIndex];
            if (stack == null || stack.IsEmpty) return Localization.Get("items.shopsellservice.select_an_item");
            if (stack.Def == null || stack.Def.sellPrice <= 0) return Localization.Get("items.shopsellservice.this_item_cannot_be_sold");
            if ((long)game.Money + stack.Def.sellPrice > int.MaxValue) return Localization.Get("items.shopsellservice.you_have_reached_the_gold_limit");
            return null;
        }

        public static bool TrySell(GameManager game, ShopDefinition shop, int slotIndex, out string message)
        {
            message = UnavailableReason(game, shop, slotIndex);
            if (message != null) return false;
            if (!BeginTransaction(game)) { message = Localization.Get("items.shopsellservice.transaction_in_progress"); return false; }
            try
            {
                PruneExpired(game.Data);
                var stack = game.Inventory.slots[slotIndex];
                var def = stack.Def;
                int expires = NextMonday(game.Data.currentDay);
                var entry = game.Data.shopBuybacks.FirstOrDefault(e => e.shopId == shop.id && e.itemId == stack.itemId
                    && e.unitPrice == def.sellPrice && e.expiresOnDay == expires && e.quantity < int.MaxValue);
                if (entry == null)
                {
                    entry = new ShopBuybackData { id = Guid.NewGuid().ToString("N"), shopId = shop.id,
                        itemId = stack.itemId, unitPrice = def.sellPrice, expiresOnDay = expires };
                    game.Data.shopBuybacks.Add(entry);
                }
                entry.quantity++;
                stack.count--;
                if (stack.count == 0) game.Inventory.slots[slotIndex] = null;
                game.AddMoney(entry.unitPrice);
                game.Inventory.RaiseChanged();
                message = Localization.Format("items.shopsellservice.sold_0_x1_1_n0_g", def.displayName, entry.unitPrice);
                return true;
            }
            finally { EndTransaction(game); }
        }
    }
}
