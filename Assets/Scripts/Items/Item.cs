using System;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Static definition of an item type. Data-driven so new items are added by
    /// registering a new ItemDef, keeping logic separate from data (design doc §17).
    /// </summary>
    [Serializable]
    public class ItemDef
    {
        public string id;
        public string displayName;
        public ItemType type;
        public int maxStack;
        public string spriteKey;   // key into AssetLibrary
        public ToolType toolType;  // only for tools
        public string cropId;      // only for seeds -> which crop to plant
        public string treeId;      // 나무 씨앗일 때 어떤 나무가 자라는지
        public int sellPrice;      // 배송함에 넣었을 때 개당 판매 가격 (0이면 팔 수 없음)

        public Sprite GetSprite()
        {
            AssetLibrary.EnsureLoaded();
            switch (spriteKey)
            {
                case "StrawberrySeed": return AssetLibrary.StrawberrySeed;
                case "StrawberryFruit": return AssetLibrary.StrawberryFruit;
                case "Wood": return AssetLibrary.Wood;
                case "Stone": return AssetLibrary.Stone;
                case "ApricotSeed": return AssetLibrary.ApricotSeed;
                case "Hoe": return AssetLibrary.Hoe;
                case "WateringCan": return AssetLibrary.WateringCan;
                case "Axe": return AssetLibrary.Axe;
                default:
                    // "Fish/Carp" 처럼 폴더가 붙은 키는 Resources에서 직접 찾는다.
                    // 물고기처럼 종류가 많은 것을 여기 switch에 한 줄씩 늘리지 않기 위한 통로.
                    return spriteKey != null && spriteKey.Contains("/")
                        ? AssetLibrary.GetSprite("Sprites/" + spriteKey)
                        : null;
            }
        }
    }

    /// <summary>A stack of items occupying one inventory slot.</summary>
    [Serializable]
    public class ItemStack
    {
        public string itemId;
        public int count;

        public ItemStack(string itemId, int count)
        {
            this.itemId = itemId;
            this.count = count;
        }

        public ItemDef Def => ItemDatabase.Get(itemId);
        public bool IsEmpty => count <= 0 || string.IsNullOrEmpty(itemId);
    }
}
