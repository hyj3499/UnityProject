using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 길(바닥 타일) 카탈로그. 울타리와 완전히 같은 방식이다 — 시트를 16x16으로 자르고
    /// 조각마다 "{접두사}_{꼬리표}"로 이름을 지으면 코드는 그대로 읽는다.
    ///
    /// Road.png 한 장에 여러 길이 섞여 있으므로 <b>접두사로 갈라 쓴다</b>:
    ///   나무 길   WoodRoad_EW, WoodRoad_NS, WoodRoad_Junction, WoodRoad_None ...
    ///   (돌길을 나중에 넣으면 같은 시트에 StoneRoad_* 로 이름 지으면 된다)
    ///
    /// 이 시트의 나무 널빤지는 원래 오토타일 셋이 아니라 흩어 놓는 장식이라 ㄱ자 모서리 그림이 없다.
    /// 그래서 가로(EW) · 세로(NS) · 갈림길(Junction) · 홀로(None) 네 장만 이름 지어 두면
    /// 나머지 조합은 Junction/NS/EW 로 알아서 물러선다. 모서리가 있는 시트를 넣게 되면
    /// 그때 NE/NW/ES/SW 이름만 더 지어 주면 코드는 그대로다.
    /// </summary>
    public static class RoadDatabase
    {
        public const string WoodRoadId = "wood_road";

        private static readonly List<PlaceableDef> _all = new List<PlaceableDef>();
        private static bool _init;

        public static IReadOnlyList<PlaceableDef> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;

            Register(new PlaceableDef
            {
                id = WoodRoadId,
                displayName = "나무 길",
                kind = PlaceableKind.Road,
                sheetPath = "Sprites/Road/Road",
                spritePrefix = "WoodRoad",   // 시트 한 장에 여러 길이 있어서 접두사로 가른다
                blocks = false,
                dropTableId = "placed_" + WoodRoadId,
            });
        }

        private static void Register(PlaceableDef def)
        {
            _all.Add(def);
            PlaceableDatabase.Register(def);
        }
    }
}
