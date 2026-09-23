using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 작은 타일 맵용 4방향 길 찾기. 실패하면 null, 경로에는 시작 타일도 포함한다.
    ///
    /// 두 가지가 있다.
    ///   <see cref="Find(Vector2Int, Vector2Int, Func{Vector2Int, bool})"/>
    ///     칸 수만 세는 BFS — <b>가장 짧은</b> 길. 이벤트 연출이 쓴다.
    ///   <see cref="Find(Vector2Int, Vector2Int, Func{Vector2Int, bool}, Func{Vector2Int, int})"/>
    ///     칸마다 값이 다른 다익스트라 — <b>가장 싼</b> 길. NPC가 길(Path)을 따라 다닐 때 쓴다
    ///     (길 위는 싸고 잔디는 비싸게 매기면, 조금 돌아가더라도 길을 타고 간다).
    /// </summary>
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
                if (current == goal) return Rebuild(previous, start, current);
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

        /// <summary>
        /// 칸마다 밟는 값이 다른 길 찾기. <paramref name="stepCost"/>는 <b>그 칸에 들어설 때</b>의 값이고
        /// 1 이상이어야 한다. 전부 같은 값이면 결과는 BFS와 같다.
        /// </summary>
        public static List<Vector2Int> Find(Vector2Int start, Vector2Int goal,
            Func<Vector2Int, bool> blocked, Func<Vector2Int, int> stepCost)
        {
            if (start == goal) return new List<Vector2Int> { start };
            if (blocked(goal)) return null;

            var best = new Dictionary<Vector2Int, int> { [start] = 0 };
            var previous = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            var queue = new MinHeap();
            queue.Push(0, start);

            while (queue.Count > 0 && best.Count <= 65536)
            {
                queue.Pop(out int cost, out var current);
                if (cost > best[current]) continue;          // 더 싼 길로 이미 다녀간 칸
                if (current == goal) return Rebuild(previous, start, current);

                foreach (var step in Steps)
                {
                    var next = current + step;
                    if (blocked(next)) continue;
                    int total = cost + Math.Max(1, stepCost(next));
                    if (best.TryGetValue(next, out int known) && known <= total) continue;
                    best[next] = total;
                    previous[next] = current;
                    queue.Push(total, next);
                }
            }
            return null;
        }

        private static List<Vector2Int> Rebuild(Dictionary<Vector2Int, Vector2Int> previous,
            Vector2Int start, Vector2Int current)
        {
            var path = new List<Vector2Int> { current };
            while (current != start) { current = previous[current]; path.Add(current); }
            path.Reverse();
            return path;
        }

        /// <summary>가장 싼 칸을 먼저 꺼내는 작은 힙. 맵이 작아 이 정도면 충분하다.</summary>
        private class MinHeap
        {
            private readonly List<(int cost, Vector2Int tile)> _items = new List<(int, Vector2Int)>();

            public int Count => _items.Count;

            public void Push(int cost, Vector2Int tile)
            {
                _items.Add((cost, tile));
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_items[parent].cost <= _items[i].cost) break;
                    (_items[parent], _items[i]) = (_items[i], _items[parent]);
                    i = parent;
                }
            }

            public void Pop(out int cost, out Vector2Int tile)
            {
                (cost, tile) = _items[0];
                _items[0] = _items[_items.Count - 1];
                _items.RemoveAt(_items.Count - 1);

                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1, right = left + 1, smallest = i;
                    if (left < _items.Count && _items[left].cost < _items[smallest].cost) smallest = left;
                    if (right < _items.Count && _items[right].cost < _items[smallest].cost) smallest = right;
                    if (smallest == i) break;
                    (_items[smallest], _items[i]) = (_items[i], _items[smallest]);
                    i = smallest;
                }
            }
        }
    }
}
