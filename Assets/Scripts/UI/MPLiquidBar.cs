using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>Pixel liquid MP gauge. The frame stays fixed while the liquid drains.</summary>
    [DisallowMultipleComponent]
    public sealed class MPLiquidBar : MonoBehaviour
    {
        private const string Folder = "Sprites/UI/Bars/";
        private static readonly Color[] WaveColors =
        {
            new Color32(145, 219, 105, 255), new Color32(251, 255, 134, 255),
            new Color32(251, 185, 84, 255), new Color32(245, 125, 74, 255),
            new Color32(234, 79, 54, 255)
        };

        [Header("색상 전환 (남은 MP 비율 이하)")]
        [Range(0f, 1f)] public float yellowAt = 0.70f;
        [Range(0f, 1f)] public float orangeAt = 0.50f;
        [Range(0f, 1f)] public float deepOrangeAt = 0.30f;
        [Range(0f, 1f)] public float redAt = 0.20f;
        [Header("MP 소모 애니메이션")]
        [Min(0.01f)] public float drainDuration = 0.45f;
        [Min(1f)] public float animationFps = 18f;
        [Range(0f, 2f)] public float wavePixels = 1f;

        private Image _fill, _background, _frame, _wave;
        private MPLiquidWobble _fillWobble, _waveWobble;
        private Sprite[] _colors, _animation;
        private float _displayed, _target, _start, _elapsed;
        private bool _initialized, _draining;

        public float DisplayedRatio => _displayed;
        public float TargetRatio => _target;
        public bool IsDraining => _draining;

        private void Awake() => Configure();

        /// <summary>Reuses the scene's MPBar and preserves its overall position/size.</summary>
        public void Configure()
        {
            _colors = LoadOrdered("MP bar frame", "MPFill_");
            _animation = LoadOrdered("MP Bar_animation", "MPWave_");
            _background = Layer("Background");
            _fill = Layer("Fill");
            _wave = Layer("LiquidAnimation");
            _frame = Layer("Frame");

            _background.sprite = LoadNamed("MP bar", "MPBackground");
            _frame.sprite = LoadNamed("MP bar frame", "MPFrame");
            _background.type = _frame.type = Image.Type.Simple;
            Interior(_background.rectTransform);
            Interior(_fill.rectTransform);
            Interior(_wave.rectTransform);
            Stretch(_frame.rectTransform, Vector2.zero, Vector2.one);
            foreach (var image in new[] { _fill, _wave })
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
                image.preserveAspect = false;
            }
            _background.transform.SetAsFirstSibling();
            _fill.transform.SetSiblingIndex(1);
            _wave.transform.SetSiblingIndex(2);
            _frame.transform.SetSiblingIndex(3);
            _fillWobble = Wobble(_fill);
            _waveWobble = Wobble(_wave);
            ApplyVisuals();
        }

        public void SetMp(int current, int maximum, bool immediate = false)
        {
            if (_fill == null) Configure();
            float ratio = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
            if (!_initialized || immediate)
            {
                _initialized = true;
                _target = _displayed = ratio;
                _draining = false;
                _elapsed = 0f;
            }
            else if (ratio < _target)
            {
                // A second cast starts at the visible liquid level, never at the old target.
                _start = _displayed;
                _target = ratio;
                _elapsed = 0f;
                _draining = true;
            }
            else if (ratio > _target)
            {
                // Recovery and loading a new character must not continue an old drain.
                _target = _displayed = ratio;
                _draining = false;
            }
            ApplyVisuals();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        internal void Advance(float deltaTime)
        {
            if (!_draining) return;
            _elapsed += Mathf.Max(0f, deltaTime);
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, drainDuration));
            _displayed = Mathf.Lerp(_start, _target, t * t * (3f - 2f * t));
            if (t >= 1f) { _displayed = _target; _draining = false; }
            ApplyVisuals();
        }

        public int ColorIndex(float ratio)
        {
            if (ratio <= redAt) return 4;
            if (ratio <= deepOrangeAt) return 3;
            if (ratio <= orangeAt) return 2;
            if (ratio <= yellowAt) return 1;
            return 0;
        }

        private void ApplyVisuals()
        {
            if (_fill == null) return;
            // Color follows actual MP immediately, even while the visible liquid catches up.
            int color = ColorIndex(_target);
            if (_colors.Length > 0) _fill.sprite = _colors[Mathf.Min(color, _colors.Length - 1)];
            _fill.color = Color.white;
            _fill.fillAmount = _displayed;
            float strength = _draining ? 1f - Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, drainDuration)) : 0f;
            _fillWobble.SetWave(strength * wavePixels, _elapsed * 24f);
            _waveWobble.SetWave(strength * wavePixels, _elapsed * 24f);
            _wave.enabled = _draining && _displayed > 0f && _animation.Length > 0;
            if (_wave.enabled)
            {
                int index = Mathf.Min((int)(_elapsed * animationFps), _animation.Length - 1);
                _wave.sprite = _animation[index];
                _wave.fillAmount = _displayed;
                var tint = WaveColors[color];
                tint.a = 0.65f * strength;
                _wave.color = tint;
            }
        }

        private static MPLiquidWobble Wobble(Image image)
        {
            var effect = image.GetComponent<MPLiquidWobble>();
            return effect != null ? effect : image.gameObject.AddComponent<MPLiquidWobble>();
        }

        private Image Layer(string layerName)
        {
            var child = transform.Find(layerName);
            if (child == null)
            {
                var go = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                child = go.transform;
            }
            var image = child.GetComponent<Image>();
            if (image == null) image = child.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void Interior(RectTransform rect)
            => Stretch(rect, new Vector2(2f / 26f, 3f / 9f), new Vector2(24f / 26f, 7f / 9f));

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Sprite LoadNamed(string sheet, string spriteName)
        {
            foreach (var sprite in Resources.LoadAll<Sprite>(Folder + sheet))
                if (sprite.name == spriteName) return sprite;
            return null;
        }

        private static Sprite[] LoadOrdered(string sheet, string prefix)
        {
            var sprites = Array.FindAll(Resources.LoadAll<Sprite>(Folder + sheet), s => s.name.StartsWith(prefix, StringComparison.Ordinal));
            Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }
    }
}
