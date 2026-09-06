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

        public Sprite GetSprite()
        {
            AssetLibrary.EnsureLoaded();
            switch (spriteKey)
            {
                case "StrawberrySeed": return AssetLibrary.StrawberrySeed;
                case "StrawberryFruit": return AssetLibrary.StrawberryFruit;
                case "Wood": return AssetLibrary.Wood;
                case "Hoe": return AssetLibrary.Hoe;
                case "WateringCan": return AssetLibrary.WateringCan;
                case "Axe": return AssetLibrary.Axe;
                default: return null;
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
