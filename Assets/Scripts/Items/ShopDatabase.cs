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
            var shop = new ShopDefinition(DefaultShopId, "Farmer's shop") { allowSelling = true, useCategories = true };
            for (int level = 1; level <= GameManager.BackpackPrices.Length; level++)
                shop.products.Add(new ShopProduct
                {
                    id = "backpack_" + level, backpackLevel = level,
                    displayName = $"배낭 {level}단계", price = GameManager.BackpackPrices[level - 1], category = "가방",
                    description = "소지품을 담을 공간이 10칸 늘어납니다.\n\n배낭은 순서대로 확장할 수 있으며, 구매한 공간은 계속 유지됩니다."
                });
            foreach (var crop in CropDatabase.All.OrderBy(c => Seasons.AllowsNow(c.seasons) ? 0 : 1))
                shop.products.Add(new ShopProduct
                {
                    id = crop.SeedItemId, itemId = crop.SeedItemId, price = crop.seedPrice,
                    category = Seasons.Label(crop.seasons),
                    note = $"다 자라기까지 {crop.MaxGrowthStage * crop.daysPerStage}일" + (crop.regrow ? " · 다작" : "")
                        + (Seasons.AllowsNow(crop.seasons) ? "" : "\n현재 계절에는 자라지 않습니다.")
                });
            foreach (var plant in PlantDatabase.All)
            {
                if (plant.maxStage <= 0) continue;
                shop.products.Add(new ShopProduct
                {
                    id = plant.plantId + "_seed", itemId = plant.plantId + "_seed", price = 60,
                    category = "풀", note = "빈 땅에 심으면 옆으로 번집니다."
                });
            }
            foreach (var tree in TreeDatabase.All)
                shop.products.Add(new ShopProduct
                {
                    id = tree.SeedItemId, itemId = tree.SeedItemId, price = tree.seedPrice,
                    category = "나무", note = $"밭이 아닌 빈 땅에 심습니다.\n다 자라기까지 {tree.maxGrowthStage * tree.daysPerStage}일"
                });
            return shop;
        }

        // 다른 상점 예시. 이 메서드를 별도 C# 파일로 옮겨 등록해도 된다.
        private static ShopDefinition CreateMaterialsShop()
        {
            var shop = new ShopDefinition("materials", "Robin's shop") { allowSelling = false, useCategories = false };
            shop.products.Add(new ShopProduct { id = "wood", itemId = "wood", price = 20, description = "가구를 만들기 좋은 목재입니다.\n필요한 만큼 골라 보세요." });
            shop.products.Add(new ShopProduct { id = "stone", itemId = "stone", price = 15 });
            return shop;
        }
    }
}
