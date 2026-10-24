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
                    category = "판매", note = "판매한 물건입니다. 다음 주 월요일에 목록에서 사라집니다."
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
            if (shop == null || !shop.allowSelling) return "판매할 수 없는 상점입니다";
            if (game?.Data == null || game.Inventory == null || slotIndex < 0
                || slotIndex >= Math.Min(game.Inventory.unlockedSlots, game.Inventory.slots.Length)) return "아이템을 선택하세요";
            var stack = game.Inventory.slots[slotIndex];
            if (stack == null || stack.IsEmpty) return "아이템을 선택하세요";
            if (stack.Def == null || stack.Def.sellPrice <= 0) return "판매할 수 없는 아이템입니다";
            if ((long)game.Money + stack.Def.sellPrice > int.MaxValue) return "소지금 한도에 도달했습니다";
            return null;
        }

        public static bool TrySell(GameManager game, ShopDefinition shop, int slotIndex, out string message)
        {
            message = UnavailableReason(game, shop, slotIndex);
            if (message != null) return false;
            if (!BeginTransaction(game)) { message = "거래 처리 중입니다"; return false; }
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
                message = $"{def.displayName} 1개 판매! +{entry.unitPrice:N0} G";
                return true;
            }
            finally { EndTransaction(game); }
        }
    }
}
