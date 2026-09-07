using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// Central catalogue of all item definitions. Add new content here without
    /// touching gameplay logic.
    /// </summary>
    public static class ItemDatabase
    {
        private static readonly Dictionary<string, ItemDef> _defs = new Dictionary<string, ItemDef>();
        private static bool _init;

        public static void Init()
        {
            if (_init) return;
            _init = true;

            Register(new ItemDef
            {
                id = "strawberry_seed",
                displayName = "딸기 씨앗",
                type = ItemType.Seed,
                maxStack = 99,
                spriteKey = "StrawberrySeed",
                cropId = "strawberry",
                sellPrice = 20
            });

            Register(new ItemDef
            {
                id = "strawberry",
                displayName = "딸기",
                type = ItemType.Crop,
                maxStack = 99,
                spriteKey = "StrawberryFruit",
                sellPrice = 80
            });

            Register(new ItemDef
            {
                id = "wood",
                displayName = "나무",
                type = ItemType.Resource,
                maxStack = 99,
                spriteKey = "Wood",
                sellPrice = 12
            });

            Register(new ItemDef
            {
                id = "stone",
                displayName = "돌",
                type = ItemType.Resource,
                maxStack = 99,
                spriteKey = "Stone",
                sellPrice = 10
            });

            Register(new ItemDef
            {
                id = "apricot_seed",
                displayName = "살구나무 씨앗",
                type = ItemType.Seed,
                maxStack = 99,
                spriteKey = "ApricotSeed",
                treeId = "apricot",
                sellPrice = 30
            });

            Register(new ItemDef
            {
                id = "hoe",
                displayName = "괭이",
                type = ItemType.Tool,
                maxStack = 1,
                spriteKey = "Hoe",
                toolType = ToolType.Hoe
            });

            Register(new ItemDef
            {
                id = "watering_can",
                displayName = "물뿌리개",
                type = ItemType.Tool,
                maxStack = 1,
                spriteKey = "WateringCan",
                toolType = ToolType.WateringCan
            });

            Register(new ItemDef
            {
                id = "axe",
                displayName = "도끼",
                type = ItemType.Tool,
                maxStack = 1,
                spriteKey = "Axe",
                toolType = ToolType.Axe
            });
        }

        private static void Register(ItemDef def) => _defs[def.id] = def;

        public static ItemDef Get(string id)
        {
            Init();
            return _defs.TryGetValue(id, out var d) ? d : null;
        }
    }
}
