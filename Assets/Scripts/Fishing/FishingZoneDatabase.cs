using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// "어느 맵에서 무엇이 잡히는가"를 정하는 중앙 카탈로그.
    ///
    /// 낚시터(FishingZone)와 위치(LocationId)를 <b>따로</b> 두는 이유:
    ///  - 같은 물고기를 여러 낚시터에 서로 다른 확률로 넣을 수 있다
    ///  - 나중에 한 맵 안에 여러 물웅덩이(강/바다)를 넣고 싶어지면, 물 타일맵 레이어 이름에
    ///    낚시터를 붙이는 식으로("Water_Farm1_Sea") 확장하면 되고 물고기 데이터는 그대로다
    ///  - 계절/날씨/시간대별 낚시터도 ForLocation을 바꾸는 것만으로 얹을 수 있다
    ///
    /// 새 맵을 추가할 때는 낚시터를 하나 Register 하고 _byLocation에 한 줄 넣으면 되고,
    /// 낚시 로직(FishingController)은 전혀 건드리지 않는다.
    /// </summary>
    public static class FishingZoneDatabase
    {
        private static readonly Dictionary<string, FishingZone> _zones = new Dictionary<string, FishingZone>();
        private static Dictionary<LocationId, string> _byLocation;
        private static bool _init;

        public static void Init()
        {
            if (_init) return;
            _init = true;
            FishDatabase.Init();

            // 농장 앞 얕은 개울 — 흔한 물고기 위주, 아주 낮은 확률로 황금물고기
            Register(new FishingZone("river_shallow", "얕은 개울")
                .Add("carp", 30)
                .Add("sunfish", 25)
                .Add("chub", 20)
                .Add("perch", 12)
                .Add("bullhead_catfish", 8)
                .Add("largemouth_bass", 4)
                .Add("golden_fish", 1));

            // 두 번째 농장의 깊은 강 — 크고 어려운 물고기
            Register(new FishingZone("river_deep", "깊은 강")
                .Add("chub", 15)
                .Add("perch", 15)
                .Add("pike", 14)
                .Add("walleye", 12)
                .Add("tiger_trout", 8)
                .Add("sturgeon", 6)
                .Add("dorado", 3)
                .Add("ghost_catfish", 2)
                .Add("faeries_fish", 1));

            _byLocation = new Dictionary<LocationId, string>
            {
                { LocationId.Farm1, "river_shallow" },
                { LocationId.Farm2, "river_deep" },
                // FarmHouse는 실내라 낚시터가 없다 — 물 타일을 칠해도 낚이지 않는다
            };
        }

        private static void Register(FishingZone zone) => _zones[zone.zoneId] = zone;

        public static FishingZone Get(string zoneId)
        {
            Init();
            if (string.IsNullOrEmpty(zoneId)) return null;
            return _zones.TryGetValue(zoneId, out var z) ? z : null;
        }

        /// <summary>이 맵의 낚시터. 없으면 null (= 낚시할 수 없는 곳).</summary>
        public static FishingZone ForLocation(LocationId id)
        {
            Init();
            return _byLocation.TryGetValue(id, out var zoneId) ? Get(zoneId) : null;
        }
    }
}
