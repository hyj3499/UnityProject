using UnityEngine;

namespace FarmMVP
{
    /// <summary>32x32 눈 시트: 위에서 두 종류, 왼쪽부터 정면/감음/측면/감음.</summary>
    public static class FarmerEyes
    {
        public const string FirstStyle = "Base/eyes#0";
        public const string SecondStyle = "Base/eyes#1";
        private const string GuidePath = "Sprites/Farmer/Base/base_animations_headguide";
        private const int Size = 32;
        // 눈 그림을 그린 기준 자세의 목 픽셀 중심 (좌상단 기준 16,18).
        private static readonly Vector2 ReferenceAnchor = new Vector2(16.5f, 13.5f);
        private static Vector2?[,] _anchors;
        private static Sprite[,] _sprites;
        private static bool _loaded;

        public static bool TryAnchor(int row, int frame, out Vector2 position)
        {
            Load();
            position = default;
            if (_anchors == null || row < 0 || row >= _anchors.GetLength(0)
                || frame < 0 || frame >= _anchors.GetLength(1) || !_anchors[row, frame].HasValue)
                return false;
            position = _anchors[row, frame].Value;
            return true;
        }

        public static Sprite SpriteFor(string style, int row, int frame, bool blinking)
        {
            Load();
            if (_sprites == null || row % 3 == 2) return null;
            bool closed = blinking || (row / 3 == 3 && frame == 2)
                          || (row / 3 == 4 && (frame == 2 || frame == 3));
            int expression = row % 3 == 1 ? (closed ? 3 : 2) : (closed ? 1 : 0);
            return _sprites[style == SecondStyle ? 1 : 0, expression];
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            var guide = Resources.Load<Texture2D>(GuidePath);
            var eyes = Resources.Load<Texture2D>("Sprites/Farmer/Base/eyes");
            if (guide == null || eyes == null || !guide.isReadable
                || guide.width != FarmerPoses.Columns * Size || guide.height != FarmerPoses.RowCount * Size
                || eyes.width != Size * 4 || eyes.height != Size * 2)
            {
                Debug.LogWarning("[FarmerEyes] 읽을 수 있는 192x480 헤드 가이드와 128x64 눈 시트가 필요합니다.");
                return;
            }

            _anchors = new Vector2?[FarmerPoses.RowCount, FarmerPoses.Columns];
            var pixels = guide.GetPixels32();
            for (int row = 0; row < FarmerPoses.RowCount; row++)
            for (int col = 0; col < FarmerPoses.Columns; col++)
            {
                int count = 0;
                Vector2 point = default;
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var p = pixels[(guide.height - (row + 1) * Size + y) * guide.width + col * Size + x];
                    // 현재 가이드의 빨강은 #AC3232. 순수 빨강으로 다시 내보내도 인식한다.
                    if (p.a == 0 || p.r < 128 || p.r < p.g * 2 || p.r < p.b * 2) continue;
                    point = new Vector2(x + 0.5f, y + 0.5f);
                    count++;
                }
                if (count == 1) _anchors[row, col] = point;
                else if (count > 1)
                    Debug.LogWarning($"[FarmerEyes] 가이드 {row}행 {col}열에 빨간 점이 {count}개입니다. 눈을 숨깁니다.");
            }

            // pivot과 앵커 모두 같은 픽셀 중심을 사용하므로 최종 눈 픽셀은 몸과 정렬된다.
            _sprites = new Sprite[2, 4];
            for (int style = 0; style < 2; style++)
            for (int expression = 0; expression < 4; expression++)
            {
                var rect = new Rect(expression * Size, eyes.height - (style + 1) * Size, Size, Size);
                var sprite = Sprite.Create(eyes, rect, ReferenceAnchor / Size,
                                           FarmerPoses.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = $"eyes_{style}_{expression}";
                _sprites[style, expression] = sprite;
            }
        }

    }
}
