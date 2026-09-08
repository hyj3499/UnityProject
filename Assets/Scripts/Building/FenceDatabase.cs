using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 울타리 카탈로그. 울타리를 한 종류 더 넣으려면 <b>여기에 Register 한 덩어리</b>만 더하면 되고,
    /// 제작·설치·오토타일·철거 로직은 그대로 재사용된다 (TreeDatabase와 같은 방식).
    ///
    /// 시트 칸 번호는 왼쪽 위가 (0,0)이고 16px 단위다. WhiteFence.png(5x5칸)의 배치:
    ///
    ///        col0        col1        col2        col3        col4
    ///   row0 ┌ 동+남     문(열림)    ┐ 서+남      -           -
    ///   row1 │ 세로       -          │ 세로       (가로 표본)  (가로 표본)
    ///   row2 └ 북+동     ─ 동+서     ┘ 북+서      (끝 표본)    (끝 표본)
    ///   row3 홀로        ╴ 동만      ╶ 서만       -           -
    ///   row4 ┬ 동서남    ┴ 북동서    ├ 북동남     ┤ 북서남     -
    ///
    /// 십자(북동남서)와 세로 끝맺음 칸은 이 시트에 없어서 가장 가까운 칸으로 대신한다.
    /// </summary>
    public static class FenceDatabase
    {
        public const string WhiteFenceId = "white_fence";
        public const string WhiteGateId = "white_fence_gate";

        private static readonly List<PlaceableDef> _all = new List<PlaceableDef>();
        private static bool _init;

        public static IReadOnlyList<PlaceableDef> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;

            const string sheet = "Sprites/Fence/WhiteFence";

            Register(new PlaceableDef
            {
                id = WhiteFenceId,
                displayName = "하얀 울타리",
                kind = PlaceableKind.Fence,
                sheetPath = sheet,
                blocks = true,
                dropTableId = "placed_" + WhiteFenceId,
                // 울타리는 칸보다 키가 커 보이도록 살짝 올려 그린다.
                offset = new Vector2(0f, 0.15f),
                tiles = WhiteFenceTiles(),
            });

            Register(new PlaceableDef
            {
                id = WhiteGateId,
                displayName = "하얀 울타리 문",
                kind = PlaceableKind.Fence,
                sheetPath = sheet,
                blocks = true,
                isGate = true,
                openCell = new AutoTileCell(1, 0),   // 활짝 열린 문
                iconCell = new AutoTileCell(1, 0),   // 닫힌 문은 울타리와 똑같이 생겨서 아이콘은 열린 모습
                dropTableId = "placed_" + WhiteGateId,
                offset = new Vector2(0f, 0.15f),
                // 닫혀 있는 동안에는 울타리와 똑같이 이어진다 — 줄 한복판에 두어도 어긋나지 않는다.
                tiles = WhiteFenceTiles(),
            });
        }

        /// <summary>WhiteFence.png의 연결 표. 문도 닫혀 있을 때는 이 표를 그대로 쓴다.</summary>
        private static AutoTileMap WhiteFenceTiles()
        {
            const int N = Connect.N, E = Connect.E, S = Connect.S, W = Connect.W;
            return new AutoTileMap()
                .Set(0, 0, 3)                    // 홀로
                .Set(E, 1, 3)                    // 동쪽으로만
                .Set(W, 2, 3)                    // 서쪽으로만
                .Set(E | W, 1, 2)                // ─ 가로
                .SetMany(new[] { N, S, N | S }, 0, 1)   // │ 세로 (세로 끝맺음 칸이 없어 세로 레일로 대신)
                .Set(E | S, 0, 0)                // ┌
                .Set(S | W, 2, 0)                // ┐
                .Set(N | E, 0, 2)                // └
                .Set(N | W, 2, 2)                // ┘
                .Set(E | S | W, 0, 4)            // ┬
                .Set(N | E | W, 1, 4)            // ┴
                .Set(N | E | S, 2, 4)            // ├
                .Set(N | S | W, 3, 4)            // ┤
                .Set(N | E | S | W, 0, 4);       // ✚ 전용 칸이 없어 ┬로 대신
        }

        private static void Register(PlaceableDef def)
        {
            _all.Add(def);
            PlaceableDatabase.Register(def);
        }
    }
}
