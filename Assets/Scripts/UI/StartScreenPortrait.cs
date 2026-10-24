using UnityEngine;

namespace FarmMVP
{
    public static class StartScreenPortrait
    {
        /// <summary>Composes authored body + eye pixels, then crops a true 16x16 bust for slots.</summary>
        public static Texture2D Render(StartScreenView view, PlayerAppearance appearance, int facing, bool bust)
        {
            int row = facing == 0 ? 0 : facing == 2 ? 2 : 1;
            var pixels = new Color32[32 * 32];
            var body = view.previewBody;
            if (body == null || view.previewEyes == null || view.previewHeadGuide == null) return null;
            var bodyPixels = body.GetPixels32();
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                pixels[y * 32 + x] = FarmerAppearancePalette.Recolor(bodyPixels[(body.height - (row + 1) * 32 + y) * body.width + x], appearance.skin, true);
            if (row != 2)
            {
                var guide = view.previewHeadGuide;
                var guidePixels = guide.GetPixels32();
                int dx = 0, dy = 0;
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    var p = guidePixels[(guide.height - (row + 1) * 32 + y) * guide.width + x];
                    if (p.a != 0 && p.r >= 128 && p.r >= p.g * 2 && p.r >= p.b * 2) { dx = x - 16; dy = y - 13; }
                }
                var eyes = view.previewEyes;
                var eyePixels = eyes.GetPixels32();
                int style = appearance.eyes == FarmerEyes.SecondStyle ? 1 : 0;
                int expression = row == 1 ? 2 : 0;
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    var p = eyePixels[(eyes.height - (style + 1) * 32 + y) * eyes.width + expression * 32 + x];
                    int tx = x + dx, ty = y + dy;
                    if (p.a > 0 && tx >= 0 && tx < 32 && ty >= 0 && ty < 32)
                        pixels[ty * 32 + tx] = FarmerAppearancePalette.Recolor(p, appearance.eyeColor, false);
                }
            }
            int size = bust ? 16 : 32;
            var output = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int sx = x + (bust ? 8 : 0), sy = y + (bust ? 12 : 0);
                output[y * size + x] = pixels[sy * 32 + (facing == 3 ? 31 - sx : sx)];
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = bust ? "Character bust 16x16" : "Character standing 32x32" };
            texture.SetPixels32(output); texture.Apply();
            return texture;
        }
    }
}
