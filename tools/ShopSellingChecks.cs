#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

namespace FarmMVP
{
    public static class ShopSellingChecks
    {
        public static void Run(GameManager game)
        {
            var previousData = game.Data;
            var previousInventory = game.Inventory;
            var wood = ItemDatabase.Get("wood");
            int previousPrice = wood.sellPrice;
            var data = new GameData { currentDay = 1 };
            var inv = new Inventory { unlockedSlots = 10 };
            typeof(GameManager).GetProperty("Data").SetValue(game, data);
            typeof(GameManager).GetProperty("Inventory").SetValue(game, inv);
            try
            {
                var shop = new ShopDefinition("test_sell", "Test shop") { allowSelling = true };
                var other = new ShopDefinition("other", "Other shop");
                var off = new ShopDefinition(shop.id, shop.title) { allowSelling = false };
                data.farmer.money = 100;
                inv.slots[0] = new ItemStack("wood", 2);
                inv.slots[1] = new ItemStack("wood", 3);
                inv.slots[2] = new ItemStack("hoe", 1);
                inv.slots[10] = new ItemStack("wood", 1);
                Check(!ShopSellService.TrySell(game, off, 1, out _) && inv.slots[1].count == 3 && game.Money == 100, "Selling disabled rejects transaction");
                Check(!ShopSellService.TrySell(game, shop, 2, out _) && !ShopSellService.TrySell(game, shop, 3, out _)
                    && !ShopSellService.TrySell(game, shop, 10, out _), "Unsellable empty and locked slots rejected");
                Check(ShopSellService.TrySell(game, shop, 1, out _) && inv.slots[1].count == 2
                    && inv.slots[0].count == 2 && game.Money == 100 + previousPrice, "Sell removes exactly one from clicked slot");
                ShopSellService.TrySell(game, shop, 1, out _);
                Check(data.shopBuybacks.Count == 1 && data.shopBuybacks[0].quantity == 2, "Same shop price and week merge quantities");
                var original = ShopSellService.Products(data, shop).Single();
                wood.sellPrice = previousPrice + 5;
                ShopSellService.TrySell(game, shop, 1, out _);
                Check(inv.slots[1] == null && data.shopBuybacks.Count == 2 && original.price == previousPrice, "Last item clears slot and changed sale price gets separate lot");
                wood.sellPrice = previousPrice;
                Check(ShopSellService.Products(data, other).Count == 0 && ShopSellService.Products(data, off).Count == 0,
                    "Other and disabled shops cannot list buybacks");
                Check(!ShopPurchaseService.TryBuy(game, original, 1, out _, other)
                    && !ShopPurchaseService.TryBuy(game, original, 1, out _, off)
                    && !ShopPurchaseService.TryBuy(game, original, 1, out _), "Wrong shop disabled shop and missing shop reject buyback");
                Check(SaveSystem.TrySave(data), "Buyback state saves to isolated save file");
                var loaded = SaveSystem.Load();
                Check(loaded.shopBuybacks.Count == 2 && loaded.shopBuybacks[0].quantity == 2
                    && loaded.shopBuybacks[0].unitPrice == previousPrice && loaded.shopBuybacks[0].expiresOnDay == 8,
                    "Save load preserves shop quantity price and expiry");
                var legacy = JsonUtility.FromJson<GameData>("{\"currentDay\":3}");
                ShopSellService.PruneExpired(legacy);
                Check(legacy.shopBuybacks != null && legacy.shopBuybacks.Count == 0, "Legacy save without shop state remains valid");
                data.farmer.money = 1000;
                int before = inv.CountOf("wood");
                bool reentered = false;
                Action nested = () => { reentered |= ShopPurchaseService.TryBuy(game, original, 1, out _, shop); };
                inv.OnChanged += nested;
                bool bought = ShopPurchaseService.TryBuy(game, original, 99, out _, shop);
                inv.OnChanged -= nested;
                Check(bought && inv.CountOf("wood") == before + 2 && game.Money == 1000 - 2 * previousPrice,
                    "Buyback caps quantity to stock and charges original sale price");
                Check(!reentered && !ShopPurchaseService.TryBuy(game, original, 1, out _, shop)
                    && ShopSellService.Products(data, shop).Count == 1, "Reentrant purchases and stale sold-out entries cannot duplicate stock");
                ShopSellService.TrySell(game, shop, 0, out _);
                ShopSellService.TrySell(game, shop, 0, out _);
                var partial = ShopSellService.Products(data, shop).Single(p => p.price == previousPrice);
                for (int i = 0; i < 10; i++) inv.slots[i] = new ItemStack("wood", 99);
                inv.slots[0].count = 98;
                int balance = game.Money;
                Check(ShopPurchaseService.TryBuy(game, partial, 2, out _, shop) && game.Money == balance - previousPrice
                    && ShopSellService.Stock(data, partial, shop) == 1, "Partial bag capacity only consumes delivered buyback stock");
                Check(!ShopPurchaseService.TryBuy(game, partial, 1, out _, shop) && ShopSellService.Stock(data, partial, shop) == 1,
                    "Full inventory leaves buyback stock intact");
                data.farmer.money = int.MaxValue;
                Check(!ShopSellService.TrySell(game, shop, 0, out _) && inv.slots[0].count == 99, "Sale prevents currency overflow");
                data.farmer.money = 100;
                data.currentDay = 7;
                ShopSellService.TrySell(game, other, 0, out _);
                Check(ShopSellService.NextMonday(1) == 8 && ShopSellService.NextMonday(7) == 8
                    && ShopSellService.NextMonday(8) == 15 && ShopSellService.NextMonday(28) == 29
                    && ShopSellService.NextMonday(112) == 113, "Monday boundary works across weeks seasons and years");
                data.currentDay = 8;
                ShopSellService.PruneExpired(data);
                Check(data.shopBuybacks.Count == 0 && !ShopPurchaseService.TryBuy(game, partial, 1, out _, shop), "Monday clears all shops and rejects expired selection");
                ShopSellService.TrySell(game, shop, 0, out _);
                Check(data.shopBuybacks.Single().expiresOnDay == 15, "Monday sale lasts until following Monday");
                data.currentDay = 15;
                SaveSystem.TrySave(data);
                Check(SaveSystem.Load().shopBuybacks.Count == 0, "Loading expired saved stock prunes it immediately");
            }
            finally
            {
                wood.sellPrice = previousPrice;
                typeof(GameManager).GetProperty("Data").SetValue(game, previousData);
                typeof(GameManager).GetProperty("Inventory").SetValue(game, previousInventory);
            }
        }
        private static void Check(bool condition, string name) => ShopBookChecks.Check(condition, name);
    }
}
#endif
