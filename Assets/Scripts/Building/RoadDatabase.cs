using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 길(바닥 타일) 카탈로그. 종류를 늘리려면 여기에 Register 한 덩어리만 더하면 된다.
    ///
    /// Road.png(9x12칸)에서 나무 널빤지는 4~5행에 모여 있다:
    ///
    ///        col0        col1        col2        col3
    ///   row4 가로 널빤지  세로 널빤지  가로(변형)  가로+세로(교차)
    ///   row5 (대각 장식)  (대각 장식)  가로(변형)  세로(변형)
    ///
    /// 이 시트는 흙길 타일셋이 아니라 <b>흩어 놓는 널빤지 장식</b>이라 ㄱ자 모서리 그림이 따로 없다.
    /// 그래서 길은 "세로로 이어지면 세로 널빤지, 가로로 이어지면 가로 널빤지, 둘이 만나면 교차 널빤지"
    /// 세 가지로 나눈다. 모서리 그림이 있는 시트를 나중에 넣으면 이 표만 채우면 된다.
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
                blocks = false,
                dropTableId = "placed_" + WoodRoadId,
                tiles = WoodRoadTiles(),
            });
        }

        private static AutoTileMap WoodRoadTiles()
        {
            const int N = Connect.N, E = Connect.E, S = Connect.S, W = Connect.W;
            var map = new AutoTileMap()
                .Set(0, 0, 4)          // 홀로 — 가로 널빤지
                .Set(E, 0, 4)
                .Set(W, 0, 4)
                .Set(E | W, 0, 4)      // 가로로 이어짐
                .Set(N, 1, 4)
                .Set(S, 1, 4)
                .Set(N | S, 1, 4);     // 세로로 이어짐

            // 가로와 세로가 만나는 모든 조합(ㄱ자·T자·십자)은 교차 널빤지 한 칸으로 처리한다.
            int[] junctions =
            {
                N | E, N | W, E | S, S | W,
                N | E | S, N | S | W, N | E | W, E | S | W,
                N | E | S | W,
            };
            return map.SetMany(junctions, 3, 4);
        }

        private static void Register(PlaceableDef def)
        {
            _all.Add(def);
            PlaceableDatabase.Register(def);
        }
    }
}
