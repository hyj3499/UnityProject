using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>Connections come exclusively from reciprocal, painted Exit tiles.</summary>
    public static class MapGraph
    {
        public static List<LocationId> Neighbors(LocationId id)
        {
            var neighbors = new List<LocationId>();
            foreach (LocationId candidate in System.Enum.GetValues(typeof(LocationId)))
                if (AreConnected(id, candidate)) neighbors.Add(candidate);
            return neighbors;
        }

        public static bool AreConnected(LocationId a, LocationId b)
            => a != b && WorldGrid.HasExitTo(a, b) && WorldGrid.HasExitTo(b, a);

        /// <summary>
        /// from에서 to까지 걸어가려면 <b>먼저 어느 맵으로</b> 나가야 하는지. 이웃이 아니어도 된다 —
        /// 통로를 따라 가장 적은 맵을 거치는 길을 찾아 그 첫 걸음을 돌려준다.
        /// NPC가 마을에서 산 정상까지 가는 것처럼 여러 맵을 건너는 이동에 쓴다.
        /// </summary>
        public static bool TryGetNextHop(LocationId from, LocationId to, out LocationId next)
        {
            next = to;
            if (from == to) return false;

            // 너비 우선 — from에서 한 걸음씩 퍼져 나가며 to에 닿는 순간 그 길의 첫 걸음을 돌려준다.
            var first = new Dictionary<LocationId, LocationId>();
            var queue = new Queue<LocationId>();
            foreach (var neighbor in Neighbors(from))
            {
                if (first.ContainsKey(neighbor)) continue;
                first[neighbor] = neighbor;   // 첫 걸음은 자기 자신
                queue.Enqueue(neighbor);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == to) { next = first[current]; return true; }
                foreach (var neighbor in Neighbors(current))
                {
                    if (neighbor == from || first.ContainsKey(neighbor)) continue;
                    first[neighbor] = first[current];
                    queue.Enqueue(neighbor);
                }
            }
            return false;   // 이어져 있지 않다 (실내 맵 등)
        }

        /// <summary>로그와 에디터 창에 쓰는 한글 이름. 적어 두지 않은 맵은 영어 id 그대로.</summary>
        public static string DisplayName(LocationId id)
        {
            switch (id)
            {
                case LocationId.Farm:         return Localization.Get("world.mapgraph.farm");
                case LocationId.FarmHouse:     return Localization.Get("world.mapgraph.farmhouse");
                case LocationId.Valley:        return Localization.Get("world.mapgraph.valley");
                case LocationId.Campsite1: return Localization.Get("world.mapgraph.campsite_1");
                case LocationId.Campsite2:   return Localization.Get("world.mapgraph.campsite_2");
                case LocationId.Meadow:        return Localization.Get("world.mapgraph.meadow");
                case LocationId.DeepForest:    return Localization.Get("world.mapgraph.deep_forest");
                case LocationId.Sea:           return Localization.Get("world.mapgraph.sea");
                case LocationId.Mountain1:     return Localization.Get("world.mapgraph.mountain_1");
                case LocationId.Mountain2:     return Localization.Get("world.mapgraph.mountain_2");
                case LocationId.MountainPeak:  return Localization.Get("world.mapgraph.mountain_summit");
                default:                       return id.ToString();
            }
        }

        public static bool IsIndoor(LocationId id) => id == LocationId.FarmHouse;
    }
}
