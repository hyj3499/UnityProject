using UnityEngine;

namespace FarmMVP
{
    /// <summary>Farm2(두 번째 농장) 맵 생성 로직.</summary>
    internal static class Farm2Builder
    {
        public static void Build(GameLocation loc, GameData data)
        {
            loc.SetDefaultSize(20, 15);   // 씬에 "Location_Farm2"를 칠해 뒀으면 그 크기가 우선

            for (int x = 0; x < loc.width; x++)
                for (int y = 0; y < loc.height; y++)
                    loc.PlaceTile(AssetLibrary.Grass, x, y, GameLocation.GroundOrder);
            loc.TryPlaceGroundTilemap(LocationId.Farm2);

            // 네 변을 모두 막는다. 출구는 아래에서(또는 "Obj_ExitFarm1" 마커가) 다시 뚫어 준다.
            for (int x = 0; x < loc.width; x++) { loc.SetBlocked(x, 0, true); loc.SetBlocked(x, loc.height - 1, true); }
            for (int y = 0; y < loc.height; y++) { loc.SetBlocked(0, y, true); loc.SetBlocked(loc.width - 1, y, true); }

            var locData = data.GetLocation(LocationId.Farm2);
            if (loc.HasObjectMarkers)
            {
                loc.ApplyObjectMarkers(locData);
                loc.RestoreFeatures(locData);
                return;
            }

            // 왼쪽 가장자리 가운데 3칸이 Farm1로 가는 출구 ("Obj_ExitFarm1"을 칠하면 대체된다)
            for (int y = loc.height / 2 - 1; y < loc.height / 2 + 2; y++)
                loc.AddExit(0, y, LocationId.Farm1);

            // a couple of decorative trees
            if (!locData.initialized)
            {
                GameLocation.AddDefaultTree(locData, 6, 9);
                GameLocation.AddDefaultTree(locData, 14, 5);
                locData.initialized = true;
            }
            if (!locData.rocksInitialized)
            {
                GameLocation.AddRock(locData, 4, 4, 0);
                GameLocation.AddRock(locData, 9, 11, 4);
                GameLocation.AddRock(locData, 12, 8, 2);
                GameLocation.AddRock(locData, 16, 3, 3);
                GameLocation.AddRock(locData, 7, 2, 1);
                locData.rocksInitialized = true;
            }
            loc.RestoreFeatures(locData);
        }
    }
}
