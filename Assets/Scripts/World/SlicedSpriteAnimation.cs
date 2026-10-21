using UnityEngine;

namespace FarmMVP
{
    /// <summary>Animation 폴더의 슬라이스를 개별 월드 오브젝트에서도 반복 재생한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SlicedSpriteAnimation : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private Sprite _lastSprite;
        private float _elapsed;

        public static void Apply(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            var animation = renderer.GetComponent<SlicedSpriteAnimation>();
            if (!AnimationTileset.TryGetFrames(renderer.sprite, out var frames))
            {
                if (animation != null)
                {
                    animation._frames = null;
                    animation.enabled = false;
                }
                return;
            }

            if (animation == null) animation = renderer.gameObject.AddComponent<SlicedSpriteAnimation>();
            animation._renderer = renderer;
            animation._frames = frames;
            animation._elapsed = 0f;
            animation._lastSprite = renderer.sprite = frames[0];
            animation.enabled = true;
        }

        // 씬에 직접 놓은 SpriteRenderer에는 이 컴포넌트를 붙여도 사용할 수 있다.
        private void Start()
        {
            if (_renderer == null) Apply(GetComponent<SpriteRenderer>());
        }

        private void Update() => Advance(Time.deltaTime);

        private void Advance(float deltaTime)
        {
            if (_renderer == null || _frames == null) return;

            // 문 열기/계절 변경 등 다른 코드가 바꾼 그림을 옛 애니메이션으로 덮지 않는다.
            if (_renderer.sprite != _lastSprite)
            {
                Apply(_renderer);
                if (_frames == null) return;
            }

            float duration = _frames.Length / AnimationTileset.FramesPerSecond;
            _elapsed = (_elapsed + deltaTime) % duration;
            int frame = Mathf.Min((int)(_elapsed * AnimationTileset.FramesPerSecond), _frames.Length - 1);
            _lastSprite = _renderer.sprite = _frames[frame];
        }
    }
}
