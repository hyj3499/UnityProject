using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 물고기 종류의 중앙 카탈로그 (ItemDatabase/CropDatabase와 같은 패턴).
    /// "어떤 물고기가 존재하는가"만 여기서 정하고, "어디서 잡히는가"는 FishingZoneDatabase가 정한다.
    /// 이 둘을 나눠 두면 같은 물고기를 여러 낚시터에 다른 확률로 넣을 수 있다.
    ///
    /// 새 물고기 추가:
    ///   1) Assets/Resources/Sprites/Fish/ 에 16x16 아이콘을 넣고
    ///   2) 아래에 Register 한 줄
    ///   3) FishingZoneDatabase의 원하는 낚시터에 Add 한 줄
    /// 아이템 정의(ItemDatabase)는 여기서 자동으로 만들어지므로 따로 적지 않는다.
    /// </summary>
    public static class FishDatabase
    {
        private static readonly Dictionary<string, FishDef> _defs = new Dictionary<string, FishDef>();
        private static readonly List<FishDef> _all = new List<FishDef>();
        private static bool _init;

        public static IReadOnlyList<FishDef> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;

            //       id                 이름            등급                   난이도  스프라이트           가격
            Register("carp", "@items.fishdatabase.carp", FishRarity.Common, 1, "Carp", 30);
            Register("sunfish", "@items.fishdatabase.bluegill", FishRarity.Common, 1, "Sunfish", 35);
            Register("chub", "@items.fishdatabase.dace", FishRarity.Common, 2, "Chub", 45);
            Register("perch", "@items.fishdatabase.perch", FishRarity.Uncommon, 2, "Perch", 60);
            Register("bullhead_catfish", "@items.fishdatabase.bullhead", FishRarity.Uncommon, 3, "BullheadCatfish", 90);
            Register("largemouth_bass", "@items.fishdatabase.bass", FishRarity.Rare, 3, "LargeMouthBass", 140);
            Register("pike", "@items.fishdatabase.pike", FishRarity.Uncommon, 3, "PikeFish", 110);
            Register("walleye", "@items.fishdatabase.walleye", FishRarity.Uncommon, 3, "Walleye", 120);
            Register("tiger_trout", "@items.fishdatabase.tiger_trout", FishRarity.Rare, 3, "TigerTrout", 160);
            Register("sturgeon", "@items.fishdatabase.sturgeon", FishRarity.Rare, 4, "Sturgeon", 220);
            Register("dorado", "@items.fishdatabase.dorado", FishRarity.Epic, 4, "Dorado", 320);
            Register("ghost_catfish", "@items.fishdatabase.ghost_catfish", FishRarity.Epic, 5, "GhostCatfish", 400);
            Register("golden_fish", "@items.fishdatabase.golden_fish", FishRarity.Legendary, 5, "GoldenFish", 500);
            Register("faeries_fish", "@items.fishdatabase.fairy_fish", FishRarity.Legendary, 5, "FaeriesFish", 650);
        }

        private static void Register(string id, string name, FishRarity rarity, int difficulty,
                                     string spriteName, int sellPrice)
        {
            var def = new FishDef
            {
                fishId = id,
                displayName = name,
                rarity = rarity,
                difficulty = difficulty,
                spriteName = spriteName,
                sellPrice = sellPrice
            };
            _defs[id] = def;
            _all.Add(def);
        }

        public static FishDef Get(string fishId)
        {
            Init();
            if (string.IsNullOrEmpty(fishId)) return null;
            return _defs.TryGetValue(fishId, out var d) ? d : null;
        }
    }
}
