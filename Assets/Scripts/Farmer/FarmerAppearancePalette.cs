using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>Shared pixel recoloring for the menu portraits and the in-game farmer.</summary>
    public sealed class FarmerAppearancePalette
    {
        public static readonly Color DefaultSkin = new Color32(250, 243, 232, 255);
        public static readonly Color DefaultEyes = new Color32(47, 37, 34, 255);
        private readonly Dictionary<Sprite, Sprite> _sprites = new Dictionary<Sprite, Sprite>();
        private readonly Dictionary<Texture2D, Texture2D> _textures = new Dictionary<Texture2D, Texture2D>();

        public static Color DisplayColor(AppearanceColor color, bool skin = false)
            => color.custom ? color.rgb : skin ? DefaultSkin : DefaultEyes;

        public static Color32 Recolor(Color32 pixel, AppearanceColor color, bool skin)
        {
            if (pixel.a == 0 || color == null || !color.custom) return pixel;
            if (skin)
            {
                // Preserve the outline and blush; replace only the authored skin palette.
                float shade;
                if (pixel.r == 250 && pixel.g == 243 && pixel.b == 232) shade = 1f;
                else if (pixel.r == 185 && pixel.g == 179 && pixel.b == 161) shade = .73f;
                else if (pixel.r == 231 && pixel.g == 213 && pixel.b == 198) shade = .89f;
                else return pixel;
                var result = color.rgb * shade; result.a = pixel.a / 255f; return result;
            }
            if (pixel.r == 255 && pixel.g == 255 && pixel.b == 255) return pixel; // eye highlight
            var eye = color.rgb; eye.a = pixel.a / 255f; return eye;
        }

        public Sprite Apply(Sprite source, AppearanceColor color, bool skin)
        {
            if (source == null || color == null || !color.custom) return source;
            if (_sprites.TryGetValue(source, out var cached)) return cached;
            if (!_textures.TryGetValue(source.texture, out var texture))
            {
                var pixels = source.texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Recolor(pixels[i], color, skin);
                texture = new Texture2D(source.texture.width, source.texture.height, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Farmer custom palette" };
                texture.SetPixels32(pixels); texture.Apply();
                _textures.Add(source.texture, texture);
            }
            var rect = source.rect;
            var sprite = Sprite.Create(texture, rect, new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            _sprites.Add(source, sprite);
            return sprite;
        }
        public void Clear()
        {
            foreach (var sprite in _sprites.Values) Release(sprite);
            foreach (var texture in _textures.Values) Release(texture);
            _sprites.Clear(); _textures.Clear();
        }
        public static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
