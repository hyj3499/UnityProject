using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// "Fixed_{맵}" / "Breakable_{맵}" 레이어를 읽어 오브젝트를 놓는다.
    ///
    /// ObjectMarkerPlacer와 달리 <b>등록할 표가 없다</b> — 칠한 타일의 그림을 그대로 쓴다.
    /// 그래서 오브젝트 png를 팔레트에 끌어다 넣고 칠하기만 하면 되고, 코드는 손대지 않는다.
    ///
    ///   Fixed_{맵}      부술 수 없는 배경 오브젝트. 그림을 놓고 그 칸을 막는다.
    ///                   (지나갈 수 있는 바닥 장식은 이 레이어가 아니라 "Decor_{맵}"에 칠한다.)
    ///   Breakable_{맵}  부술 수 있는 오브젝트. 칠한 타일 이름으로 어느 설치물인지 되짚어
    ///                   <b>진짜 설치물</b>로 만든다 — 그래서 바위마법으로 걷어내면 아이템으로
    ///                   떨어지고, 주워서 다른 자리에 다시 놓을 수 있다.
    ///
    /// 왜 타일맵을 그대로 보여 주지 않는가: 타일맵 하나는 앞뒤 순서가 하나뿐이라, 칸마다 달라야 하는
    /// y정렬(아래에 있는 것이 앞)을 할 수 없다. 그래서 칠한 것을 스프라이트로 옮겨 놓고 타일맵
    /// 렌더러는 꺼 둔다 — 에디터에서는 칠한 그대로 보이고, 게임에서는 플레이어와 제대로 겹친다.
    /// </summary>
    internal static class PaintedObjectPlacer
    {
        public static void Apply(GameLocation loc, Tilemap fixedMap, Tilemap breakableMap, LocationData locData)
        {
            if (loc == null) return;
            PlaceableDatabase.Init();

            ApplyFixed(loc, fixedMap);
            ApplyBreakable(loc, breakableMap, locData);
        }

        /// <summary>
        /// 부술 수 없는 오브젝트. 저장할 것이 없다 — 맵에 칠해 둔 그대로라 들어올 때마다 다시 놓으면 된다.
        /// </summary>
        private static void ApplyFixed(GameLocation loc, Tilemap map)
        {
            if (map == null) return;

            int placed = 0;
            for (int x = 0; x < loc.width; x++)
                for (int y = 0; y < loc.height; y++)
                {
                    var cell = map.WorldToCell(new Vector3(x, y, 0f));
                    var sprite = map.GetSprite(cell);
                    if (sprite == null) continue;

                    // 그림이 한 칸보다 크면 밑동이 칸 바닥에 붙도록 올려 그리고, 앞뒤는 칠한 칸으로 정한다.
                    loc.PlaceObject(sprite, x, y + GameLocation.BottomAlignLift(sprite), y);

                    // 칠한 칸만 막는다. 두 칸을 차지하는 오브젝트는 두 칸에 칠하거나,
                    // "Blocked_{맵}" 레이어로 막을 자리를 따로 칠하면 된다.
                    loc.SetObjectBlocked(x, y);
                    placed++;
                }

            if (placed > 0)
                Debug.Log($"[PaintedObjects] {map.name}: 부술 수 없는 오브젝트 {placed}개를 놓았습니다.");
        }

        /// <summary>
        /// 부술 수 있는 오브젝트. 나무·바위와 같은 규칙으로 <b>칸마다 딱 한 번만</b> 만든다 —
        /// 그러지 않으면 걷어낸 울타리가 맵에 다시 들어올 때마다 되살아난다.
        /// 어느 칸을 이미 만들었는지는 LocationData.spawnedMarkers에 남고, 만들고 난 뒤로는
        /// 평범한 설치물이라 저장·철거·재설치가 전부 기존 흐름을 그대로 탄다.
        /// </summary>
        private static void ApplyBreakable(GameLocation loc, Tilemap map, LocationData locData)
        {
            if (map == null || locData == null) return;

            var spawned = new HashSet<Vector2Int>();
            foreach (var m in locData.spawnedMarkers) spawned.Add(new Vector2Int(m.x, m.y));

            int created = 0;
            var unknown = new HashSet<string>();

            for (int x = 0; x < loc.width; x++)
                for (int y = 0; y < loc.height; y++)
                {
                    var cell = map.WorldToCell(new Vector3(x, y, 0f));
                    var tile = map.GetTile(cell);
                    if (tile == null) continue;

                    var def = PlaceableDatabase.FindByTileName(tile.name);
                    if (def == null) { unknown.Add(tile.name); continue; }

                    var pos = new Vector2Int(x, y);
                    if (!spawned.Add(pos)) continue;   // 이미 한 번 만든 칸

                    locData.spawnedMarkers.Add(new MarkerSpawnData { x = x, y = y });
                    locData.placed.Add(new PlacedData { x = x, y = y, defId = def.id });
                    created++;
                }

            if (created > 0)
                Debug.Log($"[PaintedObjects] {map.name}: 부술 수 있는 오브젝트 {created}개를 새로 놓았습니다.");

            if (unknown.Count > 0)
            {
                Debug.LogWarning($"[PaintedObjects] {map.name}: 설치물로 알아볼 수 없는 타일 — " +
                                 string.Join(", ", unknown) + ". 이 레이어에는 <b>인벤토리에 들어갈 수 있는 것</b>만 " +
                                 "칠할 수 있습니다 (울타리·길·가구). 타일 이름은 그림 조각 이름 그대로면 됩니다 " +
                                 "(ShopCart, WhiteFence_NS, WoodRoad_EW). 부술 수 없는 장식이라면 " +
                                 $"\"Fixed_{loc.id}\" 레이어에 칠하세요.");
            }
        }
    }
}
