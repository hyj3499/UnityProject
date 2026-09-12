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

        /// <summary>
        /// 바닥에 떨어져 있을 때만 쓰는 그림. 인벤토리 아이콘(spriteKey)과 다르게 보이고 싶을 때
        /// 채운다. 비었거나 그 그림이 아직 없으면 인벤토리 아이콘을 그대로 쓴다.
        /// </summary>
        public string dropSpriteKey;

        public Sprite GetSprite()
        {
            AssetLibrary.EnsureLoaded();
            switch (spriteKey)
            {
                case "Wood": return AssetLibrary.Wood;
                case "Stone": return AssetLibrary.Stone;
                case "Hoe": return AssetLibrary.Hoe;
                case "WateringCan": return AssetLibrary.WateringCan;
                case "Axe": return AssetLibrary.Axe;
                default:
                    // "Placeable/white_fence" — 설치물 아이콘은 그 설치물을 홀로 놓았을 때의 모습을 쓴다.
                    // 그림이 시트 안에 있어서 파일 하나로 떼어 둘 필요가 없다.
                    if (spriteKey != null && spriteKey.StartsWith("Placeable/"))
                        return PlaceableDatabase.Get(spriteKey.Substring(10))?.GetIcon();

                    // "Fish/Carp" 처럼 폴더가 붙은 키는 Resources에서 직접 찾는다.
                    // 물고기처럼 종류가 많은 것을 여기 switch에 한 줄씩 늘리지 않기 위한 통로.
                    return spriteKey != null && spriteKey.Contains("/")
                        ? AssetLibrary.GetSprite("Sprites/" + spriteKey)
                        : null;
            }
        }

        /// <summary>
        /// 바닥에 떨어졌을 때 보일 그림. 따로 정해 둔 것이 없거나 그 그림이 아직 프로젝트에
        /// 없으면 인벤토리 아이콘으로 돌아간다 — 그래서 드랍 그림은 준비된 것부터 하나씩
        /// 넣어도 아무것도 깨지지 않는다.
        /// </summary>
        public Sprite GetDropSprite()
        {
            if (!string.IsNullOrEmpty(dropSpriteKey))
            {
                AssetLibrary.EnsureLoaded();
                var s = AssetLibrary.GetSpriteOptional("Sprites/" + dropSpriteKey);
                if (s != null) return s;
            }
            return GetSprite();
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
