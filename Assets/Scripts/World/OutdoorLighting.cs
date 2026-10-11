using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 기본 2D 스프라이트 위에 곱셈 조명을 그린다. 원본 스프라이트/타일 색은 건드리지 않고,
    /// ScreenSpaceOverlay HUD와 실내는 원래 색을 유지한다.
    /// </summary>
    public sealed class OutdoorLighting : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private GameManager _game;
        private Camera _camera;
        private SpriteRenderer _overlay;
        private Sprite _sprite;
        private Material _material;

        public void Init(GameManager game, Camera camera)
        {
            _game = game;
            _camera = camera;
            if (_overlay != null) return;

            // Resources 참조로 플레이어 빌드에서도 셰이더가 제거되지 않게 한다.
            var shader = Resources.Load<Shader>("OutdoorLighting");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("[OutdoorLighting] 야외 조명 셰이더를 사용할 수 없습니다.", this);
                enabled = false;
                return;
            }

            _material = new Material(shader) { name = "Outdoor Lighting (Runtime)" };
            var texture = Texture2D.whiteTexture;
            _sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), texture.width);
            var go = new GameObject("Outdoor Lighting Overlay");
            go.transform.SetParent(transform, false);
            _overlay = go.AddComponent<SpriteRenderer>();
            _overlay.sprite = _sprite;
            _overlay.sharedMaterial = _material;
            _overlay.sortingOrder = Depth.OutdoorLighting;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (_overlay == null) return;
            bool outdoor = _game != null && _game.Data != null && _game.CurrentLocation != null
                && !MapGraph.IsIndoor(_game.CurrentLocation.id) && _camera != null;
            _overlay.enabled = outdoor;
            if (!outdoor) return;

            _material.SetColor(TintId, OutdoorLightingCycle.Evaluate(_game.LightingMinutes));
            // 카메라 이동·확대·창 크기 변경을 같은 프레임에 반영한다.
            var view = _overlay.transform;
            view.position = _camera.transform.position + _camera.transform.forward * (_camera.nearClipPlane + 1f);
            view.rotation = _camera.transform.rotation;
            float height = 2f * _camera.orthographicSize;
            view.localScale = new Vector3(height * _camera.aspect + 0.02f, height + 0.02f, 1f);
        }

        private void OnDisable()
        {
            if (_overlay != null) _overlay.enabled = false;
        }

        private void OnDestroy()
        {
            if (_overlay != null) Destroy(_overlay.gameObject);
            if (_sprite != null) Destroy(_sprite);
            if (_material != null) Destroy(_material);
        }
    }
}
