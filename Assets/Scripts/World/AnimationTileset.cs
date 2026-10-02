using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// Resources/Sprites/Tiles/Animation 아래의 이미지 한 장을 애니메이션 하나로 취급한다.
    /// 프레임은 직접 만들거나 재분할하지 않고 임포트된 Sprite를 그대로 사용한다.
    /// 슬라이스 위치 순서(위쪽 줄부터, 각 줄은 왼쪽부터)로 반복 재생한다.
    /// </summary>
    public static class AnimationTileset
    {
        public const string ResourceRoot = "Sprites/Tiles/Animation";
        public const float FramesPerSecond = 8f;

        // 이름이 같은 다른 폴더의 이미지/타일도 구분하기 위해 실제 오브젝트를 키로 쓴다.
        private static Dictionary<Sprite, Sprite[]> _frames;
        private static readonly Dictionary<Tile, SlicedAnimationTile> Tiles =
            new Dictionary<Tile, SlicedAnimationTile>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            // Domain Reload를 끈 에디터에서도 다음 Play 때 변경된 슬라이스를 다시 읽는다.
            _frames = null;
            Tiles.Clear();
        }

        public static int Apply(Tilemap map)
        {
            if (map == null) return 0;
            EnsureFramesLoaded();
            int converted = 0;

            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var source = map.GetTile(cell) as Tile;
                if (source == null || source is SlicedAnimationTile || source.sprite == null) continue;
                if (!_frames.TryGetValue(source.sprite, out var frames)) continue;

                if (!Tiles.TryGetValue(source, out var animated) || animated == null)
                {
                    animated = ScriptableObject.CreateInstance<SlicedAnimationTile>();
                    animated.name = source.name;
                    animated.hideFlags = HideFlags.DontSave;
                    animated.sprite = frames[0];
                    animated.frames = frames;
                    animated.color = source.color;
                    animated.transform = source.transform;
                    animated.flags = source.flags;
                    animated.colliderType = source.colliderType;
                    animated.gameObject = source.gameObject;
                    Tiles[source] = animated;
                }

                // 칠할 때 지정한 셀별 회전/반전/색상/잠금도 보존한다. 원본 Tile 에셋은 변경하지 않는다.
                var transform = map.GetTransformMatrix(cell);
                var color = map.GetColor(cell);
                var flags = map.GetTileFlags(cell);
                map.SetTile(cell, animated);
                map.SetTileFlags(cell, TileFlags.None);
                map.SetTransformMatrix(cell, transform);
                map.SetColor(cell, color);
                map.SetTileFlags(cell, flags);
                converted++;
            }
            return converted;
        }

        private static void EnsureFramesLoaded()
        {
            if (_frames != null) return;

            _frames = new Dictionary<Sprite, Sprite[]>();
            var sheets = new Dictionary<Texture2D, List<Sprite>>();
            foreach (var sprite in Resources.LoadAll<Sprite>(ResourceRoot))
            {
                if (sprite == null || sprite.texture == null) continue;
                if (!sheets.TryGetValue(sprite.texture, out var slices))
                {
                    slices = new List<Sprite>();
                    sheets.Add(sprite.texture, slices);
                }
                slices.Add(sprite);
            }

            foreach (var slices in sheets.Values)
            {
                // Single 모드/한 조각뿐인 이미지는 움직이지 않는다.
                if (slices.Count < 2) continue;
                slices.Sort(CompareSlices);
                var frames = slices.ToArray();
                foreach (var sprite in frames) _frames.Add(sprite, frames);
            }
        }

        private static int CompareSlices(Sprite a, Sprite b)
        {
            // rect는 원본 이미지의 픽셀 좌표다. 파일명 번호나 고정 셀 크기에 의존하지 않는다.
            int row = b.rect.yMax.CompareTo(a.rect.yMax);
            if (row != 0) return row;
            int column = a.rect.xMin.CompareTo(b.rect.xMin);
            return column != 0 ? column : string.CompareOrdinal(a.name, b.name);
        }
    }
}
