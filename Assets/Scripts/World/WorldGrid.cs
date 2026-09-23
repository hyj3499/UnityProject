using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// <b>모든 맵</b>의 지도를 한 장씩 가볍게 떠 둔 것. 어디가 막혔는지, 어디가 길인지, 이름 붙은
    /// 자리와 출구가 어디인지만 들어 있다 — 그림도, 오브젝트도, 저장 데이터도 없다.
    ///
    /// 왜 필요한가: <see cref="GameLocation"/>은 한 번에 하나만 만들어 두므로 "지금 플레이어가
    /// 없는 맵"에는 물어볼 데가 없다. 그런데 NPC는 그런 맵에서도 걸어 다녀야 한다
    /// (<see cref="NpcScheduler"/>). 그래서 씬에 이미 들어 있는 타일맵들을 <b>처음 한 번</b>
    /// 훑어서 이 표를 만든다.
    ///
    /// <b>어림값이다.</b> 칠해 둔 레이어만 본다 — 플레이어가 베어 낸 나무나 새로 놓은 울타리는
    /// 모른다. 화면 밖에서 벌어지는 일이라 그 정도면 충분하고, 플레이어가 그 맵에 들어서는 순간
    /// 부터는 진짜 <see cref="GameLocation"/>을 보므로 어긋남이 눈에 띄지 않는다.
    /// </summary>
    public static class WorldGrid
    {
        private class Map
        {
            public Vector2Int size;
            public bool[,] blocked;
            public bool[,] path;
            public readonly Dictionary<string, Vector2Int> waypoints = new Dictionary<string, Vector2Int>();
            public readonly Dictionary<Vector2Int, LocationId> exits = new Dictionary<Vector2Int, LocationId>();
        }

        private static Dictionary<LocationId, Map> _maps;

        /// <summary>씬을 고쳤을 때 다시 훑게 한다 (에디터에서 맵을 새로 칠했을 때 등).</summary>
        public static void Invalidate() => _maps = null;

        public static Vector2Int Size(LocationId id)
        {
            var map = Get(id);
            return map != null ? map.size : new Vector2Int(30, 25);
        }

        public static bool InBounds(LocationId id, Vector2Int tile)
        {
            var size = Size(id);
            return tile.x >= 0 && tile.y >= 0 && tile.x < size.x && tile.y < size.y;
        }

        public static bool Blocked(LocationId id, Vector2Int tile)
        {
            var map = Get(id);
            if (map == null) return false;              // 아직 안 칠한 맵 — 아무 데나 걸을 수 있다고 본다
            if (!InBounds(id, tile)) return true;
            return map.blocked[tile.x, tile.y];
        }

        public static bool IsPath(LocationId id, Vector2Int tile)
        {
            var map = Get(id);
            return map != null && map.path != null && InBounds(id, tile) && map.path[tile.x, tile.y];
        }

        public static bool TryGetWaypoint(LocationId id, string name, out Vector2Int tile)
        {
            tile = default;
            var map = Get(id);
            if (map == null || string.IsNullOrEmpty(name)) return false;
            if (map.waypoints.TryGetValue(name, out tile)) return true;
            foreach (var kv in map.waypoints)
                if (string.Equals(kv.Key, name, System.StringComparison.OrdinalIgnoreCase))
                { tile = kv.Value; return true; }
            return false;
        }

        /// <summary>target 맵으로 나가는 칸 중 from에서 가장 가까운 것. 칠해 둔 출구 칸이 없으면 맵의 변.</summary>
        public static bool TryGetExitTile(LocationId id, LocationId target, Vector2Int from, out Vector2Int tile)
        {
            tile = default;
            var map = Get(id);
            if (map != null)
            {
                int best = int.MaxValue;
                foreach (var kv in map.exits)
                {
                    if (kv.Value != target) continue;
                    int distance = Mathf.Abs(kv.Key.x - from.x) + Mathf.Abs(kv.Key.y - from.y);
                    if (distance >= best) continue;
                    best = distance;
                    tile = kv.Key;
                }
                if (best != int.MaxValue) return true;
            }

            if (!MapGraph.TryGetSide(id, target, out var side)) return false;
            return TryGetEdgeTile(id, side, from, out tile);
        }

        /// <summary>origin 맵에서 넘어왔을 때 내려설 자리.</summary>
        public static Vector2Int EntryFrom(LocationId id, LocationId origin, Vector2Int hint)
        {
            var map = Get(id);
            if (map != null)
            {
                foreach (var kv in map.exits)
                {
                    if (kv.Value != origin) continue;
                    // 출구 칸 위에 서면 곧바로 되돌아가므로 그 옆 칸에 내려놓는다.
                    foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                    {
                        var n = kv.Key + d;
                        if (!Blocked(id, n) && !map.exits.ContainsKey(n)) return n;
                    }
                }
            }

            if (MapGraph.TryGetSide(id, origin, out var side) && TryGetEdgeTile(id, side, hint, out var edge))
                return edge;

            var size = Size(id);
            return NearestWalkable(id, new Vector2Int(size.x / 2, size.y / 2));
        }

        /// <summary>그 변에서 hint와 가장 가까운, 걸을 수 있는 칸 (가장자리 한 칸 안쪽).</summary>
        public static bool TryGetEdgeTile(LocationId id, MapSide side, Vector2Int hint, out Vector2Int tile)
        {
            tile = default;
            var size = Size(id);
            bool vertical = side == MapSide.Left || side == MapSide.Right;
            int count = vertical ? size.y : size.x;
            int best = int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                int x, y;
                switch (side)
                {
                    case MapSide.Left: x = 0; y = i; break;
                    case MapSide.Right: x = size.x - 1; y = i; break;
                    case MapSide.Bottom: x = i; y = 0; break;
                    default: x = i; y = size.y - 1; break;
                }
                var candidate = new Vector2Int(x, y);
                if (Blocked(id, candidate)) continue;

                int distance = Mathf.Abs(x - hint.x) + Mathf.Abs(y - hint.y);
                if (distance >= best) continue;
                best = distance;
                tile = candidate;
            }
            return best != int.MaxValue;
        }

        /// <summary>그 칸이 막혀 있으면 가장 가까운 걸을 수 있는 칸.</summary>
        public static Vector2Int NearestWalkable(LocationId id, Vector2Int wanted)
        {
            if (!Blocked(id, wanted)) return wanted;
            var size = Size(id);
            for (int r = 1; r < Mathf.Max(size.x, size.y); r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        var candidate = wanted + new Vector2Int(dx, dy);
                        if (!Blocked(id, candidate)) return candidate;
                    }
            return wanted;
        }

        // ---------- 훑기 ----------
        private static Map Get(LocationId id)
        {
            Scan();
            return _maps.TryGetValue(id, out var map) ? map : null;
        }

        private static void Scan()
        {
            if (_maps != null) return;
            _maps = new Dictionary<LocationId, Map>();

            var byName = new Dictionary<string, Tilemap>();
            // 꺼 둔 레이어까지 찾아야 한다 — 지금 맵이 아닌 레이어는 전부 꺼져 있다.
            foreach (var tm in Object.FindObjectsOfType<Tilemap>(true)) byName[tm.name] = tm;

            int scanned = 0;
            foreach (LocationId id in System.Enum.GetValues(typeof(LocationId)))
            {
                var ground = Find(byName, id, "Location") ?? Find(byName, id, null, $"{id}Ground");
                if (ground == null) continue;

                ground.CompressBounds();
                var bounds = ground.cellBounds;
                if (bounds.size.x <= 0 || bounds.size.y <= 0) continue;

                var map = new Map
                {
                    size = new Vector2Int(bounds.size.x, bounds.size.y),
                    blocked = new bool[bounds.size.x, bounds.size.y],
                    path = new bool[bounds.size.x, bounds.size.y],
                };

                var blockers = new[]
                {
                    Find(byName, id, "Blocked"), Find(byName, id, "Water"), Find(byName, id, "Cliff"),
                    Find(byName, id, "Fixed"), Find(byName, id, "Breakable"),
                };
                var pathMask = Find(byName, id, "Path");
                var objects = Find(byName, id, "Objects");

                for (int x = 0; x < map.size.x; x++)
                    for (int y = 0; y < map.size.y; y++)
                    {
                        var cell = new Vector3Int(bounds.xMin + x, bounds.yMin + y, bounds.zMin);
                        var world = ground.GetCellCenterWorld(cell);

                        foreach (var layer in blockers)
                            if (layer != null && layer.HasTile(layer.WorldToCell(world))) { map.blocked[x, y] = true; break; }

                        if (pathMask != null && pathMask.HasTile(pathMask.WorldToCell(world))) map.path[x, y] = true;
                        if (objects != null) ReadMarker(map, objects, world, new Vector2Int(x, y));
                    }

                _maps[id] = map;
                scanned++;
            }

            Debug.Log($"[WorldGrid] 맵 {scanned}개의 지도를 떠 두었습니다 (NPC가 화면 밖에서도 걸어 다닙니다).");
        }

        /// <summary>"Objects_{맵}"에 칠한 마커 중 길 찾기에 필요한 것만 읽는다.</summary>
        private static void ReadMarker(Map map, Tilemap objects, Vector3 world, Vector2Int tile)
        {
            var asset = objects.GetTile(objects.WorldToCell(world));
            if (asset == null) return;

            string name = ObjectMarkerDatabase.StripPrefix(asset.name);
            if (string.IsNullOrEmpty(name)) return;

            if (name.StartsWith("Spot", System.StringComparison.OrdinalIgnoreCase) && name.Length > 4)
            { map.waypoints[name.Substring(4)] = tile; return; }

            if (name.StartsWith("Exit", System.StringComparison.OrdinalIgnoreCase) && name.Length > 4)
            {
                if (System.Enum.TryParse(name.Substring(4), out LocationId target)) map.exits[tile] = target;
                return;
            }

            // 집·나무·바위 같은 것들 — 무엇인지까지는 몰라도 되고, 지나갈 수 없다는 것만 안다.
            map.blocked[tile.x, tile.y] = true;
        }

        private static Tilemap Find(Dictionary<string, Tilemap> byName, LocationId id, string prefix, string legacy = null)
        {
            if (prefix != null)
            {
                if (byName.TryGetValue($"{prefix}_{id}", out var tm)) return tm;
                foreach (Season s in System.Enum.GetValues(typeof(Season)))
                    if (byName.TryGetValue($"{Seasons.Key(s)}_{prefix}_{id}", out var seasonal)) return seasonal;
                if (byName.TryGetValue($"{id}{prefix}", out var old)) return old;
            }
            return legacy != null && byName.TryGetValue(legacy, out var byLegacy) ? byLegacy : null;
        }
    }
}
