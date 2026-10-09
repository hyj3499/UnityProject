using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>64×64 가로 시트를 한 번 재생한다. 위치의 높이가 아닌 대상의 발밑으로 정렬한다.</summary>
    public class MagicEffect : MonoBehaviour
    {
        public const int FrameSize = 64;
        public const float FramesPerSecond = 12f;
        private static readonly Dictionary<MagicType, Sprite[]> Sheets = new Dictionary<MagicType, Sprite[]>();
        private Sprite[] _frames;
        private SpriteRenderer _renderer;
        private float _elapsed;

        public static Sprite[] Frames(MagicType type)
        {
            if (Sheets.TryGetValue(type, out var frames)) return frames;
            string name;
            switch (type)
            {
                case MagicType.Water: name = "magic1"; break;
                case MagicType.Wind: name = "magic2"; break;
                case MagicType.Rock: name = "magic3"; break;
                case MagicType.Shovel: name = "magic4"; break;
                case MagicType.Blade: name = "magic5"; break;
                default: return System.Array.Empty<Sprite>();
            }
            var texture = Resources.Load<Texture2D>("Sprites/MagicEffects/" + name);
            if (texture == null) return System.Array.Empty<Sprite>();
            if (texture.height != FrameSize || texture.width % FrameSize != 0)
            {
                Debug.LogError($"[MagicEffect] {name}: expected a horizontal strip of 64×64 frames.");
                return System.Array.Empty<Sprite>();
            }
            frames = new Sprite[texture.width / FrameSize];
            // 바닥 이펙트는 캔버스 아래끝을 타일 아래끝에 맞추고, 타격은 중심을 대상으로 맞춘다.
            bool ground = type == MagicType.Water || type == MagicType.Wind || type == MagicType.Shovel;
            var pivot = ground ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Sprite.Create(texture, new Rect(i * FrameSize, 0, FrameSize, FrameSize),
                    pivot, 32f, 0, SpriteMeshType.FullRect);
                frames[i].name = name + "_" + i;
            }
            Sheets[type] = frames;
            return frames;
        }

        public static float BladeAngle(Direction direction)
        {
            // 원본 magic5는 오른쪽으로 날아가는 모습이다.
            switch (direction)
            {
                case Direction.Up: return 90f;
                case Direction.Left: return 180f;
                case Direction.Down: return -90f;
                default: return 0f;
            }
        }

        public static void Play(Transform parent, MagicType type, Vector3 position, float footY, Direction direction)
        {
            var frames = Frames(type);
            if (frames.Length == 0 || parent == null) return;
            var go = new GameObject("MagicEffect_" + type);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            if (type == MagicType.Blade)
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
