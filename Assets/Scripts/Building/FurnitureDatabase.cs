using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 가구 카탈로그 — 상점 수레·작업대·침대처럼 <b>그림 한 장짜리로 홀로 서 있는</b> 설치물.
    ///
    /// 울타리·길과 같은 PlaceableDef를 쓰기 때문에 설치·철거·저장·드랍이 전부 그대로 재사용된다.
    /// 그래서 여기 Register 한 줄이면 그 가구는 자동으로
    ///   · 바위마법으로 걷어낼 수 있고 (걷어내면 아이템으로 떨어진다)
    ///   · 주워서 다른 자리에 다시 놓을 수 있고
    ///   · "Breakable_{맵}" 레이어에 칠해 두면 처음부터 그 자리에 놓인 채로 시작한다.
    ///
    /// 그림은 시트를 자르지 않는다 — sheetPath가 곧 그림 한 장의 Resources 경로다
    /// (Sprites/Environment/ShopCart.png). 계절판(ShopCart_Winter.png)이 있으면 그게 쓰인다.
    ///
    /// <b>id는 그림 파일 이름과 같은 뜻이 되도록 짓는다</b> — 오브젝트 팔레트에 칠한 타일
    /// ("ShopCart")로 어느 가구인지 되짚을 때 밑줄과 대소문자를 무시하고 맞춰 보기 때문이다
    /// (shop_cart ↔ ShopCart).
    /// </summary>
    public static class FurnitureDatabase
    {
        public const string ShopCartId = "shop_cart";
        public const string WorkbenchId = "workbench";
        public const string BedId = "bed";
        public const string FireplaceId = "fireplace";
        public const string PlantId = "plant";
        public const string RugId = "rug";

        private static readonly List<PlaceableDef> _all = new List<PlaceableDef>();
        private static bool _init;

        public static IReadOnlyList<PlaceableDef> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;

            Register(ShopCartId, "잡화 수레", "Sprites/Environment/ShopCart", 400,
                     role: PlaceableRole.Shop);

            Register(WorkbenchId, "작업대", "Sprites/Environment/Workbench", 150,
                     role: PlaceableRole.Workbench);

            Register(BedId, "침대", "Sprites/Interior/Bed", 300,
                     role: PlaceableRole.Bed);

            Register(FireplaceId, "벽난로", "Sprites/Interior/Fireplace", 200);
            Register(PlantId, "화분", "Sprites/Interior/Plant", 60);

            // 러그는 바닥에 깔리는 장식이라 지나갈 수 있다.
            Register(RugId, "러그", "Sprites/Interior/Rug", 80, blocks: false);
        }

        private static void Register(string id, string displayName, string spritePath, int sellPrice,
                                     PlaceableRole role = PlaceableRole.None, bool blocks = true)
        {
            var def = new PlaceableDef
            {
                id = id,
                displayName = displayName,
                kind = PlaceableKind.Furniture,
                sheetPath = spritePath,
                blocks = blocks,
                role = role,
                sellPrice = sellPrice,
                dropTableId = "placed_" + id,
            };
            _all.Add(def);
            PlaceableDatabase.Register(def);
        }
    }
}
