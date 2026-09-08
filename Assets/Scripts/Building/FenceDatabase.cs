using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 울타리 카탈로그. 울타리를 한 종류 더 넣으려면 <b>Register 한 덩어리</b>만 더하면 되고,
    /// 제작·설치·오토타일·철거 로직은 그대로 재사용된다 (TreeDatabase와 같은 방식).
    ///
    /// 코드에 좌표를 적지 않는다 — 그림은 <b>스프라이트 이름</b>으로 찾는다.
    /// 시트(예: Sprites/Fence/WhiteFence.png)를 스프라이트 에디터에서 16x16으로 자르고,
    /// 조각마다 아래 이름을 지어 두면 끝이다 (이름은 북→동→남→서 순서로 이어진 방향만 적는다):
    ///
    ///   WhiteFence_None  홀로        WhiteFence_E   ╴동만      WhiteFence_W   ╶서만
    ///   WhiteFence_NS  │ 세로        WhiteFence_EW  ─ 가로
    ///   WhiteFence_NE  └ 북+동       WhiteFence_NW  ┘ 북+서
    ///   WhiteFence_ES  ┌ 동+남       WhiteFence_SW  ┐ 남+서
    ///   WhiteFence_NES ├             WhiteFence_NEW ┴
    ///   WhiteFence_NSW ┤             WhiteFence_ESW ┬
    ///   WhiteFence_NESW ✚ 십자       WhiteFence_GateOpen  열린 문
    ///
    /// 없는 이름은 알아서 물러선다 — 딱 맞는 것이 없으면 Junction, 곧게 이어져 있으면 EW/NS,
    /// 그다음엔 이어진 방향을 하나씩 빼 가며 가장 비슷한 그림을 쓴다. 그래서 16장을 다 그릴 필요가 없다
    /// (돌담·철제 울타리는 여섯 장뿐이지만 모든 조합이 그림으로 이어진다).
    ///
    /// 이름 뒤에 계절을 더 붙이면 그 계절에만 쓰인다: WoodFence_EW_Winter (눈 덮인 나무 울타리).
    /// 다른 울타리(Fence Moon...)도 접두사만 바꿔 같은 꼬리표를 쓰면 된다.
    /// </summary>
    public static class FenceDatabase
    {
        public const string WhiteFenceId = "white_fence";
        public const string WhiteGateId = "white_fence_gate";
        public const string WoodFenceId = "wood_fence";
        public const string StoneFenceId = "stone_fence";
        public const string IronFenceId = "iron_fence";
        public const string WoodGateId = "wood_fence_gate";
        public const string StoneGateId = "stone_fence_gate";
        public const string IronGateId = "iron_fence_gate";

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
            });

            Register(new PlaceableDef
            {
                id = WhiteGateId,
                displayName = "하얀 울타리 문",
                kind = PlaceableKind.Fence,
                sheetPath = sheet,
                blocks = true,
                isGate = true,
                // 닫혀 있는 동안에는 울타리와 똑같이 이어진다 (같은 꼬리표를 그대로 쓴다).
                // 그래서 아이콘만 열린 문으로 두어야 인벤토리에서 구분된다.
                iconSuffix = Connect.GateOpenSuffix,
                dropTableId = "placed_" + WhiteGateId,
                offset = new Vector2(0f, 0.15f),
            });

            // 아래 셋은 시트만 다르고 나머지는 같다. 새 울타리도 이 다섯 줄이면 끝난다.
            RegisterSimple(WoodFenceId, "나무 울타리", "Sprites/Fence/WoodFence");
            RegisterSimple(StoneFenceId, "돌담", "Sprites/Fence/StoneFence");
            RegisterSimple(IronFenceId, "철제 울타리", "Sprites/Fence/IronFence");

            // 문. 두 칸짜리인지 한 칸짜리인지는 시트에 GateClosedL 조각이 있는지로 저절로 갈린다.
            RegisterGate(WoodGateId, "나무 울타리 문", "Sprites/Fence/WoodFence");
            RegisterGate(StoneGateId, "돌담 문", "Sprites/Fence/StoneFence");
            RegisterGate(IronGateId, "철제 울타리 문", "Sprites/Fence/IronFence");
        }

        /// <summary>울타리 문 하나. 시트의 문 조각 이름만 있으면 나머지는 다 따라온다.</summary>
        private static void RegisterGate(string id, string displayName, string sheetPath)
        {
            Register(new PlaceableDef
            {
                id = id,
                displayName = displayName,
                kind = PlaceableKind.Fence,
                sheetPath = sheetPath,
                blocks = true,
                isGate = true,
                iconSuffix = Connect.GateClosedSuffix,
                dropTableId = "placed_" + id,
                offset = new Vector2(0f, 0.15f),
            });
        }

        /// <summary>문이 없는 평범한 울타리 하나. 시트 이름이 곧 조각 이름의 앞부분이 된다.</summary>
        private static void RegisterSimple(string id, string displayName, string sheetPath)
        {
            Register(new PlaceableDef
            {
                id = id,
                displayName = displayName,
                kind = PlaceableKind.Fence,
                sheetPath = sheetPath,
                blocks = true,
                dropTableId = "placed_" + id,
                offset = new Vector2(0f, 0.15f),
            });
        }

        private static void Register(PlaceableDef def)
        {
            _all.Add(def);
            PlaceableDatabase.Register(def);
        }
    }
}
