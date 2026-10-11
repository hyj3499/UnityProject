using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>대상별 가로 시트를 한 번 재생한다. 위치의 높이가 아닌 대상의 발밑으로 정렬한다.</summary>
    public class MagicEffect : MonoBehaviour
    {
        public const float FramesPerSecond = 12f;
        // 기본 이펙트는 원래 비율(32픽셀 = 한 월드 유닛)로 유지한다.
        // 오브젝트용 magic7~10은 대상 크기에서 별도로 확대한다.
        public const float PixelsPerUnit = 32f;
        private static readonly Dictionary<int, Sprite[]> Sheets = new Dictionary<int, Sprite[]>();
        private Sprite[] _frames;
        private SpriteRenderer _renderer;
        private float _elapsed;

        public static int DefaultSheet(MagicType type)
        {
            switch (type)
            {
                case MagicType.Water: return 1;
                case MagicType.Wind: return 2;
                case MagicType.Rock: return 3;
                case MagicType.Shovel: return 4;
                case MagicType.Blade: return 5;
                case MagicType.Earth: return 6;
                default: return 0;
            }
        }

        public static Vector2Int FrameSize(int sheet)
        {
            switch (sheet)
            {
                case 5: return new Vector2Int(48, 48);
                case 7: return new Vector2Int(80, 48);
                case 8: return new Vector2Int(128, 128);
                case 10: return new Vector2Int(128, 64);
                default: return new Vector2Int(64, 64);
            }
        }

        public static Sprite[] Frames(MagicType type) => Frames(DefaultSheet(type));

        public static Sprite[] Frames(int sheet)
        {
            if (sheet < 1 || sheet > 10) return System.Array.Empty<Sprite>();
            if (Sheets.TryGetValue(sheet, out var frames)) return frames;
            string name = "magic" + sheet;
            var size = FrameSize(sheet);
            var texture = Resources.Load<Texture2D>("Sprites/MagicEffects/" + name);
            if (texture == null) return System.Array.Empty<Sprite>();
            if (texture.height != size.y || texture.width % size.x != 0)
            {
                Debug.LogError($"[MagicEffect] {name}: expected a horizontal strip of {size.x}×{size.y} frames.");
                return System.Array.Empty<Sprite>();
            }
            frames = new Sprite[texture.width / size.x];
            // 바닥 이펙트는 아래 중앙에서 발생한다. 타일 마법은 타일 중심, magic9/10은 오브젝트 밑바닥에 둔다.
            bool ground = sheet == 1 || sheet == 2 || sheet == 4 || sheet == 6 || sheet == 9 || sheet == 10;
            var pivot = ground ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            // magic5는 16×16 칸으로 나눈 3×3 시트의 오른쪽 아래 칸(3열 3행)을 타겟 타일에 겹친다.
            // 해당 칸 중심은 Unity의 왼쪽 아래 좌표로 (40,8)이며 회전해도 이 칸은 고정된다.
            if (sheet == 5) pivot = new Vector2(40f / size.x, 8f / size.y);
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Sprite.Create(texture, new Rect(i * size.x, 0, size.x, size.y),
                    pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                frames[i].name = name + "_" + i;
            }
            Sheets[sheet] = frames;
            return frames;
        }

        public static float BladeAngle(Direction direction)
        {
            // 오른쪽 시전을 기준으로 타격점을 중심 삼아 회전한다.
            switch (direction)
            {
                case Direction.Up: return 90f;
                case Direction.Left: return 180f;
                case Direction.Down: return -90f;
                default: return 0f;
            }
        }

        public static void Play(Transform parent, MagicType type, Vector3 position, float footY, Direction direction,
                                int sheet = 0, Vector2? targetSize = null)
        {
            if (sheet == 0) sheet = DefaultSheet(type);
            var frames = Frames(sheet);
            if (frames.Length == 0 || parent == null) return;
            var go = new GameObject("MagicEffect_" + type);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            if (targetSize.HasValue)
            {
                Vector2 frameSize = frames[0].rect.size / PixelsPerUnit;
                // 가로·세로 모두 대상을 덮되 원본 비율을 유지한다. 애니메이션 내내 같은 크기다.
                float scale = Mathf.Max(targetSize.Value.x / frameSize.x, targetSize.Value.y / frameSize.y);
                go.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            }
            if (type == MagicType.Blade && sheet == 5)
                go.transform.rotation = Quaternion.Euler(0, 0, BladeAngle(direction));
            var effect = go.AddComponent<MagicEffect>();
            effect._frames = frames;
            effect._renderer = go.AddComponent<SpriteRenderer>();
            effect._renderer.sprite = frames[0];
            // 같은 대상보다 앞, 한 줄 아래의 대상/이펙트보다는 뒤.
            effect._renderer.sortingOrder = Depth.YSort(footY, 10);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(_elapsed * FramesPerSecond);
            if (frame >= _frames.Length) { Destroy(gameObject); return; }
            _renderer.sprite = _frames[frame];
        }
    }
}
