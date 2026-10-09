using System.Collections.Generic;
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
        private readonly List<SpriteRenderer> _tiles = new List<SpriteRenderer>();

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
            gameObject.SetActive(false);
        }

        public void Show(Vector2Int tile, Color color)
        {
            ShowTile(0, tile, color);
            Finish(1);
        }

        public void ShowMagic(IReadOnlyList<Vector2Int> tiles, MagicType type, GameLocation location)
        {
            int count = 0;
            var color = MagicSystem.Get(type).previewColor;
            foreach (var tile in tiles)
            {
                if (location == null || !location.InBounds(tile.x, tile.y)) continue;
                bool valid = MagicSystem.CanApply(type, location, tile.x, tile.y);
                ShowTile(count++, tile, valid ? color : MagicSystem.InvalidPreviewColor);
            }
            Finish(count);
        }

        private void ShowTile(int index, Vector2Int tile, Color color)
        {
            if (index == _tiles.Count)
            {
                var child = new GameObject("TargetTile");
                child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = _sprite;
                renderer.sortingOrder = Depth.Highlight;
                child.transform.localScale = Vector3.one * 0.94f;
                _tiles.Add(renderer);
            }
            var sr = _tiles[index];
            sr.transform.position = new Vector3(tile.x, tile.y, 0);
            sr.color = color;
            sr.enabled = true;
        }

        private void Finish(int count)
        {
            for (int i = count; i < _tiles.Count; i++) _tiles[i].enabled = false;
            gameObject.SetActive(count > 0);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
