using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>가로 폭 × 전방 길이. 조준과 실제 시전이 같은 칸 목록을 쓴다.</summary>
    public static class MagicCharge
    {
        public const float SecondsPerStage = 0.5f;
        private static readonly Vector2Int[] Sizes =
        {
            new Vector2Int(1, 1), new Vector2Int(3, 1), new Vector2Int(3, 3),
            new Vector2Int(3, 6), new Vector2Int(6, 6), new Vector2Int(6, 9)
        };

        public static int Stage(float seconds, MagicType type)
            => type == MagicType.Fishing ? 0
                : Mathf.Clamp(Mathf.FloorToInt(seconds / SecondsPerStage), 0, Sizes.Length - 1);

        public static Vector2Int Size(int stage) => Sizes[Mathf.Clamp(stage, 0, Sizes.Length - 1)];

        public static Vector2Int Forward(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return Vector2Int.up;
                case Direction.Left: return Vector2Int.left;
                case Direction.Right: return Vector2Int.right;
                default: return Vector2Int.down;
            }
        }

        public static void FillTiles(Vector2Int first, Direction direction, int stage, List<Vector2Int> tiles)
        {
            tiles.Clear();
            var size = Size(stage);
            var forward = Forward(direction);
            var right = new Vector2Int(forward.y, -forward.x);
            // 짝수 폭은 바라보는 방향의 오른쪽에 한 칸 더 둔다. 모든 단계는 이전 범위를 포함한다.
            int left = -(size.x - 1) / 2;
            for (int depth = 0; depth < size.y; depth++)
                for (int side = 0; side < size.x; side++)
                    tiles.Add(first + forward * depth + right * (left + side));
        }
    }
}
