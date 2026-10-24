using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>Book Inventory Selector 스프라이트 시트를 반복 재생한다.</summary>
    public sealed class BookInventorySelection : MonoBehaviour
    {
        public Image image;
        public Sprite[] frames;
        public Image[] corners;
        [Range(4, 30)] public float framesPerSecond = 10;
        private bool _selected;
        private float _startedAt;

        public bool Selected => _selected;
        public int CurrentFrame { get; private set; }

        public void SetSelected(bool selected, bool restart = false)
        {
            if (_selected == selected && !restart)
            {
                RefreshVisual();
                return;
            }
            _selected = selected;
            _startedAt = Time.unscaledTime;
            if (image == null) image = GetComponent<Image>();
            if (image != null) image.enabled = false;
            CurrentFrame = 0;
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (image != null) image.enabled = false;
            ApplyFrame(CurrentFrame);
            SetCornersVisible(_selected && frames != null && frames.Length >= 8);
        }

        private void Update()
        {
            if (!_selected || frames == null || frames.Length < 8 || corners == null || corners.Length < 4) return;
            int frame = Mathf.FloorToInt((Time.unscaledTime - _startedAt) * framesPerSecond) % 2;
            if (frame == CurrentFrame) return;
            CurrentFrame = frame;
            ApplyFrame(frame);
        }

        private void OnDisable()
        {
            if (image != null) image.enabled = false;
            SetCornersVisible(false);
            _selected = false;
        }

        private void SetCornersVisible(bool visible)
        {
            if (corners == null) return;
            foreach (var corner in corners) if (corner != null) corner.enabled = visible;
        }

        private void ApplyFrame(int frame)
        {
            if (corners == null || corners.Length < 4 || frames == null || frames.Length < 8) return;
            // 실제 PNG는 두 프레임이며, 프레임마다 좌상·우상·좌하·우하 네 조각으로 구성된다.
            int start = (frame & 1) * 4;
            for (int i = 0; i < 4; i++) corners[i].sprite = frames[start + i];

            // 두 번째 프레임은 네 조각이 모두 1px 안쪽으로 이동한다.
            // 크기는 12px 그대로 유지되어 가운데 공간만 넓어졌다 좁아지는 원본 떨림이 재현된다.
            float inset = (frame & 1) / 34f;
            const float size = 12f / 34f;
            Place(corners[0].rectTransform, inset, 1f - inset - size);
            Place(corners[1].rectTransform, 1f - inset - size, 1f - inset - size);
            Place(corners[2].rectTransform, inset, inset);
            Place(corners[3].rectTransform, 1f - inset - size, inset);
        }

        private static void Place(RectTransform rect, float x, float y)
        {
            const float size = 12f / 34f;
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(x + size, y + size);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
