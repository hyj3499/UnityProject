using UnityEngine;

namespace FarmMVP
{
    /// <summary>Farm2(두 번째 농장) 맵 생성 로직.</summary>
    internal static class Farm2Builder
    {
        public static void Build(GameLocation loc, GameData data)
        {
            if (!loc.TryPlaceGroundTilemap(LocationId.Farm2))
            {
                for (int x = 0; x < loc.width; x++)
                    for (int y = 0; y < loc.height; y++)
                        loc.PlaceTile(AssetLibrary.Grass, x, y, -100);
            }

            for (int x = 0; x < loc.width; x++) { loc.SetBlocked(x, 0, true); loc.SetBlocked(x, loc.height - 1, true); }
            for (int y = 0; y < loc.height; y++) { loc.SetBlocked(loc.width - 1, y, true); }

            // left exit -> Farm1
            loc.leftExit = new RectInt(0, loc.height / 2 - 1, 1, 3);
            for (int y = 0; y < loc.height; y++)
            {
                bool open = y >= loc.leftExit.Value.yMin && y < loc.leftExit.Value.yMax;
                loc.SetBlocked(0, y, !open);
            }

            // a couple of decorative trees
            var locData = data.GetLocation(LocationId.Farm2);
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
