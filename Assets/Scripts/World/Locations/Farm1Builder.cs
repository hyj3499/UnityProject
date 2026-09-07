using UnityEngine;

namespace FarmMVP
{
    /// <summary>Farm1(첫 번째 농장) 맵 생성 로직. 집/배송함/상점/나무/바위 배치를 담당한다.</summary>
    internal static class Farm1Builder
    {
        public static void Build(GameLocation loc, GameData data)
        {
            loc.TryPlaceGroundTilemap(LocationId.Farm1);
            var locData = data.GetLocation(LocationId.Farm1);

            // "Objects_Farm1" 레이어에 마커를 칠해 뒀으면 배치와 출구가 전부 거기서 온다.
            // 하나라도 칠했으면 아래 하드코딩 배치는 통째로 건너뛴다 (반반 섞이면 헷갈리기만 한다).
            if (loc.HasObjectMarkers)
            {
                loc.ApplyObjectMarkers(locData);
                loc.RestoreFeatures(locData);
                return;
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
