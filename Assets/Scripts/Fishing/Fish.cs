using System;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>물고기 등급. 높을수록 드물고, 잡기 어렵고, 비싸다.</summary>
    public enum FishRarity { Common, Uncommon, Rare, Epic, Legendary }

    /// <summary>
    /// 물고기 한 종류의 정의. ItemDef와 마찬가지로 순수한 데이터라서,
    /// 새 물고기를 추가할 때 게임플레이 코드는 건드릴 필요가 없다 (FishDatabase에 한 줄).
    /// </summary>
    [Serializable]
    public class FishDef
    {
        public string fishId;       // 아이템 id로도 그대로 쓰인다
        public string displayName;
        public FishRarity rarity;
        public int difficulty;      // 1~5. 미니게임의 속도/존 크기/필요 성공 횟수를 정한다
        public string spriteName;   // Resources의 Sprites/Fish/{spriteName}
        public int sellPrice;

        public Sprite GetSprite() => AssetLibrary.GetSprite("Sprites/Fish/" + spriteName);

        /// <summary>
        /// 인벤토리에 넣으려면 아이템이기도 해야 한다. 물고기 정의를 두 군데 적지 않도록
        /// ItemDef는 여기서 만들어 ItemDatabase가 가져가게 한다.
        /// </summary>
        public ItemDef ToItemDef() => new ItemDef
        {
            id = fishId,
            displayName = displayName,
            type = ItemType.Fish,
            maxStack = 99,
            spriteKey = "Fish/" + spriteName,
            sellPrice = sellPrice
        };

        public string RarityLabel
        {
            get
            {
                switch (rarity)
                {
                    case FishRarity.Common: return "흔함";
                    case FishRarity.Uncommon: return "제법 귀함";
                    case FishRarity.Rare: return "희귀";
                    case FishRarity.Epic: return "매우 희귀";
                    case FishRarity.Legendary: return "전설";
                    default: return "";
                }
            }
        }

        /// <summary>등급별 이름 색 — 잡았을 때 토스트/배너에 쓴다.</summary>
        public Color RarityColor
        {
            get
            {
                switch (rarity)
                {
                    case FishRarity.Uncommon: return new Color(0.55f, 0.9f, 0.55f);
                    case FishRarity.Rare: return new Color(0.45f, 0.7f, 1f);
                    case FishRarity.Epic: return new Color(0.8f, 0.55f, 1f);
                    case FishRarity.Legendary: return new Color(1f, 0.8f, 0.25f);
                    default: return Color.white;
                }
            }
        }
    }
}
