using System;

namespace FarmMVP
{
    /// <summary>구매 규칙과 거래 처리. UI에서는 상태 확인과 TryBuy만 호출한다.</summary>
    public static class ShopPurchaseService
    {
        public static string UnavailableReason(GameManager game, ShopProduct product, ShopDefinition shop = null)
        {
            if (game == null || game.Inventory == null || product == null) return "상품을 선택하세요";
            if (product.price < 0) return "잘못된 가격입니다";
            if (product.IsBuyback && ShopSellService.Stock(game.Data, product, shop) == 0) return "재구매 재고가 없습니다";
            if (product.backpackLevel > 0)
            {
                if (product.backpackLevel > GameManager.BackpackPrices.Length) return "잘못된 배낭 단계입니다";
                if (game.BackpackLevel >= product.backpackLevel) return "구매 완료";
                if (game.BackpackLevel + 1 != product.backpackLevel) return "이전 단계 배낭을 먼저 구매하세요";
                if (product.price != game.NextBackpackPrice) return "배낭 가격 설정을 확인하세요";
            }
            else
            {
                if (product.Def == null) return "등록되지 않은 상품입니다";
                if (!HasRoom(game.Inventory, product.Def)) return "가방이 가득 찼습니다";
            }
            return game.Money < product.price ? "골드가 부족합니다" : null;
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
            if (requested < 1) { message = "구매 수량을 확인하세요"; return false; }
            if (!ShopSellService.BeginTransaction(game)) { message = "거래 처리 중입니다"; return false; }
            try { return Purchase(game, product, requested, shop, out message); }
            finally { ShopSellService.EndTransaction(game); }
        }

        private static bool Purchase(GameManager game, ShopProduct product, int requested, ShopDefinition shop, out string message)
        {
            if (product.backpackLevel > 0)
            {
                bool success = game.BuyBackpack();
                message = success ? product.Name + " 구매! 가방이 10칸 늘었습니다." : "배낭을 구매할 수 없습니다";
                return success;
            }
            if (!product.SupportsQuantity) requested = 1;
            var buyback = product.IsBuyback ? ShopSellService.Find(game.Data, product, shop) : null;
            if (product.IsBuyback)
            {
                if (buyback == null) { message = "재구매 재고가 없습니다"; return false; }
                requested = Math.Min(requested, buyback.quantity);
            }
            int count = Math.Min(requested, product.price == 0 ? requested : game.Money / product.price);
            int bought = count - game.Inventory.Add(product.itemId, count);
            if (bought <= 0) { message = "가방이 가득 찼습니다"; return false; }
            if (buyback != null)
            {
                buyback.quantity -= bought;
                ShopSellService.PruneExpired(game.Data);
            }
            game.AddMoney(-product.price * bought);
            message = $"{product.Name} {bought}개 구매!";
            return true;
        }
    }
}
