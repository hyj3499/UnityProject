using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>Moves the draining edge in four pixel rows without moving the frame.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class MPLiquidWobble : BaseMeshEffect
    {
        private float _pixels, _phase;
        private readonly UIVertex[] _corners = new UIVertex[4];
        private readonly UIVertex[] _row = new UIVertex[4];

        public void SetWave(float pixels, float phase)
        {
            if (_pixels == pixels && _phase == phase) return;
            _pixels = pixels;
            _phase = phase;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || _pixels <= 0f || vh.currentVertCount != 4) return;
            for (int i = 0; i < 4; i++) vh.PopulateUIVertex(ref _corners[i], i);
            // Filled horizontal Image produces BL, TL, TR, BR.
            var bounds = graphic.rectTransform.rect;
            float left = _corners[0].position.x;
            float right = _corners[3].position.x;
            float width = right - left;
            if (width <= 0f) return;
            float pixelWidth = bounds.width / 22f;
            vh.Clear();
            for (int y = 0; y < 4; y++)
            {
                float bottom = y / 4f, top = (y + 1f) / 4f;
                // Quantized horizontal displacement keeps the wobble on the source pixel grid.
                float offset = Mathf.Round(Mathf.Sin(_phase + y * 1.7f) * _pixels) * pixelWidth;
                float edge = Mathf.Clamp(right + offset, left, bounds.xMax);
                float u = (edge - left) / width;
                _row[0] = Interpolate(_corners[0], _corners[1], bottom);
                _row[1] = Interpolate(_corners[0], _corners[1], top);
                _row[2] = Interpolate(_corners[3], _corners[2], top);
                _row[3] = Interpolate(_corners[3], _corners[2], bottom);
                _row[2].position.x = _row[3].position.x = edge;
                _row[2].uv0.x = _row[3].uv0.x = Mathf.LerpUnclamped(_corners[0].uv0.x, _corners[3].uv0.x, u);
                vh.AddUIVertexQuad(_row);
            }
        }

        private static UIVertex Interpolate(UIVertex a, UIVertex b, float t)
        {
            a.position = Vector3.LerpUnclamped(a.position, b.position, t);
            a.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
            return a;
        }
    }
}
