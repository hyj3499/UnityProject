using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>제작에 들어가는 재료 하나.</summary>
    public struct Ingredient
    {
        public string itemId;
        public int count;

        public Ingredient(string itemId, int count)
        {
            this.itemId = itemId;
            this.count = count;
        }

        public ItemDef Def => ItemDatabase.Get(itemId);
    }

    /// <summary>작업대에서 만들 수 있는 것 하나.</summary>
    public class CraftingRecipe
    {
        public string resultItemId;
        public int resultCount = 1;
        public Ingredient[] costs;

        public ItemDef ResultDef => ItemDatabase.Get(resultItemId);

        public string DisplayName
        {
            get
            {
                var def = ResultDef;
                return def != null ? def.displayName : resultItemId;
            }
        }

        /// <summary>재료가 충분한지.</summary>
        public bool CanCraft(Inventory inv)
        {
            if (inv == null) return false;
            foreach (var c in costs)
                if (inv.CountOf(c.itemId) < c.count) return false;
            return true;
        }

        /// <summary>"나무 2개, 돌 1개" 처럼 재료를 한 줄로.</summary>
        public string CostText(Inventory inv)
        {
            var parts = new List<string>();
            foreach (var c in costs)
            {
                var def = c.Def;
                string name = def != null ? def.displayName : c.itemId;
                int have = inv != null ? inv.CountOf(c.itemId) : 0;
                parts.Add($"{name} {have}/{c.count}");
            }
            return string.Join("   ", parts);
        }
    }

    /// <summary>
    /// 작업대 제작표. 설치물을 하나 추가하면 여기에 Register 한 줄만 더하면 작업대 목록에 나온다.
    /// 재료는 인벤토리에서 그대로 빠지고 결과물은 가방으로 들어간다 (GameManager.Craft).
    /// </summary>
    public static class RecipeDatabase
    {
        private static readonly List<CraftingRecipe> _all = new List<CraftingRecipe>();
        private static bool _init;

        public static IReadOnlyList<CraftingRecipe> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;
            PlaceableDatabase.Init();

            Register(FenceDatabase.WhiteFenceId, 1, new Ingredient("wood", 2));
            Register(FenceDatabase.WhiteGateId, 1, new Ingredient("wood", 4), new Ingredient("stone", 1));
            Register(RoadDatabase.WoodRoadId, 2, new Ingredient("wood", 1), new Ingredient("stone", 1));
        }

        private static void Register(string resultItemId, int resultCount, params Ingredient[] costs)
            => _all.Add(new CraftingRecipe { resultItemId = resultItemId, resultCount = resultCount, costs = costs });
    }
}
