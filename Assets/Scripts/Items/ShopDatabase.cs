using System;
using System.Collections.Generic;
using System.Linq;

namespace FarmMVP
{
    /// <summary>상점 등록부. 새 상점의 팩토리를 등록하면 같은 ShopBookUi를 사용한다.</summary>
    public static class ShopDatabase
    {
        public const string DefaultShopId = "general";
        private static readonly Dictionary<string, Func<ShopDefinition>> Factories =
            new Dictionary<string, Func<ShopDefinition>>(StringComparer.Ordinal)
            {
                { DefaultShopId, CreateGeneralShop },
                { "materials", CreateMaterialsShop }
            };

        public static void Register(string id, Func<ShopDefinition> factory)
        {
            if (string.IsNullOrWhiteSpace(id) || factory == null) throw new ArgumentException("상점 id와 factory가 필요합니다.");
            Factories[id] = factory;
        }

        public static ShopDefinition Get(string id)
        {
            if (id == null || !Factories.TryGetValue(id, out var factory)) throw new ArgumentException("등록되지 않은 상점: " + id);
            var shop = factory();
            if (shop == null || shop.id != id) throw new ArgumentException("등록 id와 정의의 id가 다릅니다: " + id);
            shop.Validate();
            return shop;
        }

        private static ShopDefinition CreateGeneralShop()
        {
            var shop = new ShopDefinition(DefaultShopId, Localization.Get("ui.shop.general")) { allowSelling = true, useCategories = true };
            for (int level = 1; level <= GameManager.BackpackPrices.Length; level++)
                shop.products.Add(new ShopProduct
                {
                    id = "backpack_" + level, backpackLevel = level,
                    displayName = Localization.Format("items.shopdatabase.backpack_level_0", level), price = GameManager.BackpackPrices[level - 1], category = Localization.Get("items.shopdatabase.bags"),
                    description = Localization.Get("items.shopdatabase.adds_10_inventory_slots_backpacks_must_be")
                });
            foreach (var crop in CropDatabase.All.OrderBy(c => Seasons.AllowsNow(c.seasons) ? 0 : 1))
                shop.products.Add(new ShopProduct
                {
                    id = crop.SeedItemId, itemId = crop.SeedItemId, price = crop.seedPrice,
                    category = Seasons.Label(crop.seasons),
                    note = Localization.Format("items.shopdatabase.matures_in_0_days", crop.MaxGrowthStage * crop.daysPerStage) + (crop.regrow ? Localization.Get("items.shopdatabase.regrows") : "")
                        + (Seasons.AllowsNow(crop.seasons) ? "" : Localization.Get("items.shopdatabase.matures_in_0_days"))
                });
            foreach (var plant in PlantDatabase.All)
            {
                if (plant.maxStage <= 0) continue;
                shop.products.Add(new ShopProduct
                {
                    id = plant.plantId + "_seed", itemId = plant.plantId + "_seed", price = 60,
                    category = Localization.Get("items.shopdatabase.grass"), note = Localization.Get("items.shopdatabase.plant_on_empty_ground_to_let_it")
                });
            }
            foreach (var tree in TreeDatabase.All)
                shop.products.Add(new ShopProduct
                {
                    id = tree.SeedItemId, itemId = tree.SeedItemId, price = tree.seedPrice,
                    category = Localization.Get("items.shopdatabase.trees"), note = Localization.Format("items.shopdatabase.plant_on_empty_untilled_ground_matures_in", tree.maxGrowthStage * tree.daysPerStage)
                });
            return shop;
        }

        // 다른 상점 예시. 이 메서드를 별도 C# 파일로 옮겨 등록해도 된다.
        private static ShopDefinition CreateMaterialsShop()
        {
            var shop = new ShopDefinition("materials", Localization.Get("ui.shop.materials")) { allowSelling = false, useCategories = false };
            shop.products.Add(new ShopProduct { id = "wood", itemId = "wood", price = 20, description = Localization.Get("items.shopdatabase.wood_for_making_furniture_choose_as_much") });
            shop.products.Add(new ShopProduct { id = "stone", itemId = "stone", price = 15 });
            return shop;
        }
    }
}
