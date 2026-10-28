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
        [UnityEngine.SerializeField, UnityEngine.Serialization.FormerlySerializedAs("displayName")] private string _displayName;
        public string displayName { get => Localization.Resolve(_displayName); set => _displayName = value; }
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
            displayName = _displayName,
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
                    case FishRarity.Common: return Localization.Get("items.fish.common");
                    case FishRarity.Uncommon: return Localization.Get("items.fish.uncommon");
                    case FishRarity.Rare: return Localization.Get("items.fish.rare");
                    case FishRarity.Epic: return Localization.Get("items.fish.epic");
                    case FishRarity.Legendary: return Localization.Get("items.fish.legendary");
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
