using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 마법/씨앗을 짧게 또는 길게 누르는 동안 대상 타일을 반투명 사각형으로 표시한다.
    /// 코드로만 생성되는 이 프로젝트의 다른 렌더러들과 동일한 방식(런타임 SpriteRenderer)을 따른다.
    /// </summary>
    public class TargetIndicator : MonoBehaviour
    {
        private static Sprite _sprite;
        private SpriteRenderer _sr;

        public static TargetIndicator Create()
        {
            var go = new GameObject("TargetIndicator");
            return go.AddComponent<TargetIndicator>();
        }

        private void Awake()
        {
            if (_sprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = _sprite;
            _sr.sortingOrder = Depth.Highlight; // 바닥 위, 서 있는 것들보다는 뒤
            gameObject.SetActive(false);
        }

        public void Show(Vector2Int tile, Color color)
        {
            transform.position = new Vector3(tile.x, tile.y, 0);
            _sr.color = color;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
