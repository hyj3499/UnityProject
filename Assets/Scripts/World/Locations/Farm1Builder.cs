using UnityEngine;

namespace FarmMVP
{
    /// <summary>Farm1(첫 번째 농장) 맵 생성 로직. 집/배송함/상점/나무/바위 배치를 담당한다.</summary>
    internal static class Farm1Builder
    {
        public static void Build(GameLocation loc, GameData data)
        {
            // 씬에 "Location_Farm1" 바닥을 칠해 뒀으면 그 크기가 맵 크기가 되고, 없으면 이 값을 쓴다.
            loc.SetDefaultSize(20, 15);

            // 기본 잔디를 항상 먼저 깔아 둔다 — 에디터에서 칠한 Tilemap이 맵 전체를 덮지 않아도
            // 빈 칸이 생기지 않는다. 칠한 Tilemap은 GameLocation.Build가 이 위에 얹어 준다.
            for (int x = 0; x < loc.width; x++)
                for (int y = 0; y < loc.height; y++)
                    loc.PlaceTile(AssetLibrary.Grass, x, y, GameLocation.GroundOrder);
            loc.TryPlaceGroundTilemap(LocationId.Farm1);

            // 네 변을 모두 막는다. 출구는 아래에서(또는 "Obj_ExitFarm2" 마커가) 다시 뚫어 준다.
            for (int x = 0; x < loc.width; x++) { loc.SetBlocked(x, 0, true); loc.SetBlocked(x, loc.height - 1, true); }
            for (int y = 0; y < loc.height; y++) { loc.SetBlocked(0, y, true); loc.SetBlocked(loc.width - 1, y, true); }

            var locData = data.GetLocation(LocationId.Farm1);

            // "Objects_Farm1" 레이어에 마커를 칠해 뒀으면 배치와 출구가 전부 거기서 온다.
            // 하나라도 칠했으면 아래 하드코딩 배치는 통째로 건너뛴다 (반반 섞이면 헷갈리기만 한다).
            if (loc.HasObjectMarkers)
            {
                loc.ApplyObjectMarkers(locData);
                loc.RestoreFeatures(locData);
                return;
            }

            // 오른쪽 가장자리 가운데 3칸이 Farm2로 가는 출구 ("Obj_ExitFarm2"를 칠하면 대체된다)
            for (int y = loc.height / 2 - 1; y < loc.height / 2 + 2; y++)
                loc.AddExit(loc.width - 1, y, LocationId.Farm2);

            // House structure (top-left). Door tile at bottom of house leads to FarmHouse.
            // 집은 가로 5칸(x 2~6) 세로 5칸(위에서부터)을 차지한다 — 맵이 그보다 작으면 놓지 않는다.
            if (loc.width >= 7 && loc.height >= 5)
            {
                var houseSr = loc.PlaceObject(AssetLibrary.House, 3.5f, loc.height - 3.0f, 1000);
                houseSr.sortingOrder = 500;
                // block house footprint
                for (int hx = 2; hx <= 6; hx++)
                    for (int hy = loc.height - 5; hy <= loc.height - 1; hy++)
                        loc.SetBlocked(hx, hy, true);
                // door tile (walk into it to enter house)
                loc.doorExitTile = new Vector2Int(4, loc.height - 5);
                loc.SetBlocked(loc.doorExitTile.Value.x, loc.doorExitTile.Value.y, false);
            }
            else
            {
                Debug.LogWarning($"[Farm1Builder] 맵이 {loc.width}x{loc.height}라 집을 놓지 못했습니다 " +
                                 "(최소 7x5). 집이 없으면 농가로 들어갈 수 없습니다 — 바닥을 더 칠해 주세요.");
            }

            // 배송함 (집 옆) — 우클릭해서 열고, 넣어 둔 물건은 다음 날 아침에 팔린다
            if (loc.width >= 10 && loc.height >= 5)
            {
                loc.shippingBoxTile = new Vector2Int(8, loc.height - 5);
                loc.SetShippingBoxRenderer(loc.PlaceObject(AssetLibrary.ShippingBox,
                    loc.shippingBoxTile.Value.x, loc.shippingBoxTile.Value.y + 0.15f, 600));
                loc.SetBlocked(loc.shippingBoxTile.Value.x, loc.shippingBoxTile.Value.y, true);
            }

            // 상점 수레 — 우클릭해서 배낭을 살 수 있다.
            // 오른쪽 출구 앞쪽에 두므로 맵 폭을 따라간다 (폭 20이면 예전과 같은 x=13).
            int shopX = loc.width - 7;
            if (shopX >= 1 && loc.height > 4)
            {
                loc.shopTile = new Vector2Int(shopX, 4);
                loc.PlaceObject(AssetLibrary.ShopCart, loc.shopTile.Value.x, loc.shopTile.Value.y + 0.6f, 600);
                loc.SetBlocked(loc.shopTile.Value.x, loc.shopTile.Value.y, true);
            }

            // Trees (from data or defaults)
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
