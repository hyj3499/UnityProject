using UnityEngine;

namespace FarmMVP
{
    /// <summary>Farm1(첫 번째 농장) 맵 생성 로직. 집/배송함/상점/나무/바위 배치를 담당한다.</summary>
    internal static class Farm1Builder
    {
        public static void Build(GameLocation loc, GameData data)
        {
            // grass ground (에디터에서 칠한 Tilemap 프리팹이 있으면 그걸 쓰고, 없으면 단색 잔디로 대체)
            if (!loc.TryPlaceGroundTilemap(LocationId.Farm1))
            {
                for (int x = 0; x < loc.width; x++)
                    for (int y = 0; y < loc.height; y++)
                        loc.PlaceTile(AssetLibrary.Grass, x, y, -100);
            }

            // border blocking (except right exit)
            for (int x = 0; x < loc.width; x++) { loc.SetBlocked(x, 0, true); loc.SetBlocked(x, loc.height - 1, true); }
            for (int y = 0; y < loc.height; y++) { loc.SetBlocked(0, y, true); }

            // right exit region -> Farm2
            loc.rightExit = new RectInt(loc.width - 1, loc.height / 2 - 1, 1, 3);
            for (int y = 0; y < loc.height; y++)
            {
                bool open = y >= loc.rightExit.Value.yMin && y < loc.rightExit.Value.yMax;
                loc.SetBlocked(loc.width - 1, y, !open);
            }

            // House structure (top-left). Door tile at bottom of house leads to FarmHouse.
            var houseSr = loc.PlaceObject(AssetLibrary.House, 3.5f, loc.height - 3.0f, 1000);
            houseSr.sortingOrder = 500;
            // block house footprint
            for (int hx = 2; hx <= 6; hx++)
                for (int hy = loc.height - 5; hy <= loc.height - 1; hy++)
                    loc.SetBlocked(hx, hy, true);
            // door tile (walk into it to enter house)
            loc.doorExitTile = new Vector2Int(4, loc.height - 5);
            loc.SetBlocked(loc.doorExitTile.Value.x, loc.doorExitTile.Value.y, false);

            // 배송함 (집 옆) — 우클릭해서 열고, 넣어 둔 물건은 다음 날 아침에 팔린다
            loc.shippingBoxTile = new Vector2Int(8, loc.height - 5);
            loc.SetShippingBoxRenderer(loc.PlaceObject(AssetLibrary.ShippingBox,
                loc.shippingBoxTile.Value.x, loc.shippingBoxTile.Value.y + 0.15f, 600));
            loc.SetBlocked(loc.shippingBoxTile.Value.x, loc.shippingBoxTile.Value.y, true);

            // 상점 수레 — 우클릭해서 배낭을 살 수 있다
            loc.shopTile = new Vector2Int(13, 4);
            loc.PlaceObject(AssetLibrary.ShopCart, loc.shopTile.Value.x, loc.shopTile.Value.y + 0.6f, 600);
            loc.SetBlocked(loc.shopTile.Value.x, loc.shopTile.Value.y, true);

            // Trees (from data or defaults)
            var locData = data.GetLocation(LocationId.Farm1);
            if (!locData.initialized)
            {
                GameLocation.AddDefaultTree(locData, 12, 10);
                GameLocation.AddDefaultTree(locData, 15, 8);
                GameLocation.AddDefaultTree(locData, 10, 4);
                locData.initialized = true;
            }
            if (!locData.rocksInitialized)
            {
                // 바위를 맵 곳곳에 흩어 둔다
                GameLocation.AddRock(locData, 3, 3, 0);
                GameLocation.AddRock(locData, 6, 6, 2);
                GameLocation.AddRock(locData, 16, 3, 4);
                GameLocation.AddRock(locData, 17, 11, 1);
                GameLocation.AddRock(locData, 11, 2, 3);
                GameLocation.AddRock(locData, 2, 8, 1);
                locData.rocksInitialized = true;
            }
            loc.RestoreFeatures(locData);
        }
    }
}
