using UnityEngine;

namespace FarmMVP
{
    /// <summary>FarmHouse(실내) 맵 생성 로직.</summary>
    internal static class FarmHouseBuilder
    {
        public static void Build(GameLocation loc, GameData data)
        {
            loc.width = 12;
            loc.height = 9;
            loc.ResetBlocked();

            // wall row at top, wood floor elsewhere
            for (int x = 0; x < loc.width; x++)
                for (int y = 0; y < loc.height; y++)
                {
                    if (y >= loc.height - 2)
                        loc.PlaceTile(AssetLibrary.Wall, x, y, GameLocation.GroundOrder);
                    else
                        loc.PlaceTile(AssetLibrary.Floor, x, y, GameLocation.GroundOrder);
                }

            // border walls
            for (int x = 0; x < loc.width; x++) { loc.SetBlocked(x, 0, true); loc.SetBlocked(x, loc.height - 1, true); loc.SetBlocked(x, loc.height - 2, true); }
            for (int y = 0; y < loc.height; y++) { loc.SetBlocked(0, y, true); loc.SetBlocked(loc.width - 1, y, true); }

            // rug
            var rug = loc.PlaceObject(AssetLibrary.Rug, loc.width / 2f - 0.5f, 3f, 50);
            rug.sortingOrder = -50;

            // bed (top-left interior)
            loc.bedTile = new Vector2Int(2, loc.height - 3);
            loc.PlaceObject(AssetLibrary.Bed, 2f, loc.height - 3.0f, 500);
            loc.SetBlocked(2, loc.height - 3, true);
            loc.SetBlocked(2, loc.height - 4, true);

            // fireplace decor
            loc.PlaceObject(AssetLibrary.Fireplace, 6f, loc.height - 3.0f, 500);
            loc.SetBlocked(6, loc.height - 3, true);

            // door (bottom) -> back to Farm1
            loc.doorExitTile = new Vector2Int(loc.width / 2, 1);
            loc.PlaceObject(AssetLibrary.Door, loc.width / 2f, 0.6f, 500);
            loc.SetBlocked(loc.doorExitTile.Value.x, 0, false);
        }
    }
}
