using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    [ExecuteAlways]
    public class StartScreenHsvBars : MonoBehaviour
    {
        public Slider hue, saturation, value;
        public RawImage hueGradient, saturationGradient, valueGradient;
        private Texture2D _h, _s, _v;
        private float _lastH = -1, _lastS = -1;
        private void OnEnable()
        {
            if (hue == null || hueGradient == null) return;
            _h = Make(); _s = Make(); _v = Make();
            hueGradient.texture = _h; saturationGradient.texture = _s; valueGradient.texture = _v;
            Paint(true);
        }
        private static Texture2D Make() => new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        private void Update() => Paint(false);
        private void Paint(bool force)
        {
            if (_h == null) return;
            if (!force && _lastH == hue.value && _lastS == saturation.value) return;
            _lastH = hue.value; _lastS = saturation.value;
            for (int i = 0; i < 128; i++)
            {
                float t = i / 127f;
                _h.SetPixel(i, 0, Color.HSVToRGB(t, 1, 1));
                _s.SetPixel(i, 0, Color.HSVToRGB(hue.value, t, 1));
                _v.SetPixel(i, 0, Color.HSVToRGB(hue.value, saturation.value, t));
            }
            _h.Apply(); _s.Apply(); _v.Apply();
        }
        private void OnDisable()
        {
            FarmerAppearancePalette.Release(_h); FarmerAppearancePalette.Release(_s); FarmerAppearancePalette.Release(_v);
            if (hueGradient != null) hueGradient.texture = null;
            if (saturationGradient != null) saturationGradient.texture = null;
            if (valueGradient != null) valueGradient.texture = null;
            _lastH = _lastS = -1;
        }
    }
}
