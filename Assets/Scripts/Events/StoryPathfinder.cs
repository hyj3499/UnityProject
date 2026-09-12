using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>작은 타일 맵용 4방향 BFS. 실패하면 null, 경로에는 시작 타일도 포함한다.</summary>
    public static class StoryPathfinder
    {
        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public static List<Vector2Int> Find(Vector2Int start, Vector2Int goal, Func<Vector2Int, bool> blocked)
        {
            if (start != goal && blocked(goal)) return null;
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            queue.Enqueue(start);
            while (queue.Count > 0 && previous.Count <= 65536)
            {
                var current = queue.Dequeue();
                if (current == goal)
                {
                    var path = new List<Vector2Int> { current };
                    while (current != start) { current = previous[current]; path.Add(current); }
                    path.Reverse();
                    return path;
                }
                foreach (var step in Steps)
                {
                    var next = current + step;
                    if (previous.ContainsKey(next) || blocked(next)) continue;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }
            return null;
        }
    }
}
