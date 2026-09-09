using System.Collections.Generic;
using UnityEngine;

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

            // 물고기는 FishDatabase에 한 번만 정의하고 아이템 정의는 여기서 자동 생성한다 —
            // 물고기를 추가할 때 두 군데를 고치지 않도록.
            foreach (var fish in FishDatabase.All)
                Register(fish.ToItemDef());

            // 작물도 마찬가지 — CropDatabase에 한 줄 추가하면 씨앗과 수확물 아이템이 따라 생긴다.
            foreach (var crop in CropDatabase.All)
            {
                Register(SeedItem(crop));
                foreach (var harvest in crop.Harvests) Register(HarvestItem(crop, harvest));
            }

            // 설치물(울타리·길·가구)도 표 하나만 보면 되도록 여기서 만든다. 아이템 id와 설치물 id가
            // 같은 값이라, 가구를 하나 더 넣어도 아이템·드랍·재설치가 저절로 따라온다.
            foreach (var p in PlaceableDatabase.All) Register(PlaceableItem(p));
        }

        /// <summary>
        /// 설치물 아이템 하나. 그림은 그 설치물이 홀로 놓였을 때의 모습을 그대로 쓴다
        /// (가구는 그림이 한 장뿐이라 그것이 곧 아이콘이다).
        /// </summary>
        private static ItemDef PlaceableItem(PlaceableDef def) => new ItemDef
        {
            id = def.id,
            displayName = def.displayName,
            type = ItemType.Resource,
            maxStack = def.IsFurniture ? 1 : 99,   // 가구는 부피가 커서 한 칸에 하나만
            spriteKey = "Placeable/" + def.id,
            sellPrice = def.sellPrice
        };

        /// <summary>씨앗 아이템. 아이콘은 All Crops.png 안의 씨앗 봉지("{시트}Seed")를 쓴다.</summary>
        private static ItemDef SeedItem(CropDef crop) => new ItemDef
        {
            id = crop.SeedItemId,
            displayName = crop.name + " 씨앗",
            type = ItemType.Seed,
            maxStack = 99,
            spriteKey = "Crops/" + crop.SeedSpriteName,
            cropId = crop.cropId,
            sellPrice = Mathf.Max(1, crop.seedPrice / 3)   // 되팔면 산 값의 1/3
        };

        /// <summary>
        /// 수확물 아이템. 인벤토리 아이콘은 작물 시트의 마지막 번호를 그대로 쓰고,
        /// 바닥에 떨어졌을 때만 Sprites/Crops/Drops/ 의 그림으로 바뀐다 (있을 때).
        /// </summary>
        private static ItemDef HarvestItem(CropDef crop, CropVariant harvest) => new ItemDef
        {
            id = harvest.id,
            displayName = harvest.name,
            type = ItemType.Crop,
            maxStack = 99,
            spriteKey = $"Crops/{crop.sheet}_{harvest.fruitIndex}",
            //dropSpriteKey = "Crops/Drops/" + (harvest.dropSpriteName ?? crop.DefaultDropSpriteName),
            dropSpriteKey = $"Crops/{crop.sheet}_Drop",
            sellPrice = crop.sellPrice
        };

        private static void Register(ItemDef def) => _defs[def.id] = def;

        public static ItemDef Get(string id)
        {
            Init();
            return _defs.TryGetValue(id, out var d) ? d : null;
        }
    }
}
