using System;

namespace FarmMVP
{
    /// <summary>구매 규칙과 거래 처리. UI에서는 상태 확인과 TryBuy만 호출한다.</summary>
    public static class ShopPurchaseService
    {
        public static string UnavailableReason(GameManager game, ShopProduct product, ShopDefinition shop = null)
        {
            if (game == null || game.Inventory == null || product == null) return Localization.Get("items.shoppurchaseservice.select_a_product");
            if (product.price < 0) return Localization.Get("items.shoppurchaseservice.invalid_price");
            if (product.IsBuyback && ShopSellService.Stock(game.Data, product, shop) == 0) return Localization.Get("items.shoppurchaseservice.no_buyback_stock_remaining");
            if (product.backpackLevel > 0)
            {
                if (product.backpackLevel > GameManager.BackpackPrices.Length) return Localization.Get("items.shoppurchaseservice.invalid_backpack_level");
                if (game.BackpackLevel >= product.backpackLevel) return Localization.Get("items.shoppurchaseservice.purchased");
                if (game.BackpackLevel + 1 != product.backpackLevel) return Localization.Get("items.shoppurchaseservice.buy_the_previous_backpack_upgrade_first");
                if (product.price != game.NextBackpackPrice) return Localization.Get("items.shoppurchaseservice.check_the_backpack_price_settings");
            }
            else
            {
                if (product.Def == null) return Localization.Get("items.shoppurchaseservice.unknown_product");
                if (!HasRoom(game.Inventory, product.Def)) return Localization.Get("items.shoppurchaseservice.your_bag_is_full");
            }
            return game.Money < product.price ? Localization.Get("items.shoppurchaseservice.not_enough_gold") : null;
        }

        private static bool HasRoom(Inventory inventory, ItemDef item)
        {
            for (int i = 0; i < Math.Min(inventory.unlockedSlots, inventory.slots.Length); i++)
            {
                var stack = inventory.slots[i];
                if (stack == null || stack.IsEmpty || (stack.itemId == item.id && stack.count < item.maxStack)) return true;
            }
            return false;
        }

        public static bool TryBuy(GameManager game, ShopProduct product, int requested, out string message, ShopDefinition shop = null)
        {
            message = UnavailableReason(game, product, shop);
            if (message != null) return false;
            if (requested < 1) { message = Localization.Get("items.shoppurchaseservice.check_the_purchase_quantity"); return false; }
            if (!ShopSellService.BeginTransaction(game)) { message = Localization.Get("items.shoppurchaseservice.transaction_in_progress"); return false; }
            try { return Purchase(game, product, requested, shop, out message); }
            finally { ShopSellService.EndTransaction(game); }
        }

        private static bool Purchase(GameManager game, ShopProduct product, int requested, ShopDefinition shop, out string message)
        {
            if (product.backpackLevel > 0)
            {
                bool success = game.BuyBackpack();
                message = success ? product.Name + Localization.Get("items.shoppurchaseservice.purchased_your_bag_gained_10_slots") : Localization.Get("items.shoppurchaseservice.could_not_purchase_the_backpack");
                return success;
            }
            if (!product.SupportsQuantity) requested = 1;
            var buyback = product.IsBuyback ? ShopSellService.Find(game.Data, product, shop) : null;
            if (product.IsBuyback)
            {
                if (buyback == null) { message = Localization.Get("items.shoppurchaseservice.no_buyback_stock_remaining"); return false; }
                requested = Math.Min(requested, buyback.quantity);
            }
            int count = Math.Min(requested, product.price == 0 ? requested : game.Money / product.price);
            int bought = count - game.Inventory.Add(product.itemId, count);
            if (bought <= 0) { message = Localization.Get("items.shoppurchaseservice.your_bag_is_full"); return false; }
            if (buyback != null)
            {
                buyback.quantity -= bought;
                ShopSellService.PruneExpired(game.Data);
            }
            game.AddMoney(-product.price * bought);
            message = Localization.Format("items.shoppurchaseservice.purchased_0_x_1", product.Name, bought);
            return true;
        }
    }
}
