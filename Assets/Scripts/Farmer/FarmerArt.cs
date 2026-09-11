using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 한 부위를 어떤 색으로 칠할지.
    ///
    /// 이 에셋의 색칠은 <b>LUT</b>(색 대응표)로 한다. 그림은 정해진 표식 색으로만 그려져 있고,
    /// 그 픽셀의 <b>빨강 값</b>이 표의 몇 번째 줄인지를 가리킨다. 표의 열을 바꾸면 통째로 다른
    /// 색이 된다 — 피부는 36가지, 눈은 26가지가 이미 그려져 있다.
    ///
    /// 그 위에 <b>색상/채도/명도</b>를 더 틀어 쓸 수 있게 했다. 표에 없는 색도 만들 수 있고,
    /// 표가 만들어 둔 음영은 그대로 살아난다.
    /// </summary>
    public struct FarmerTint
    {
        public string lutPath;      // 없으면 그림 색을 그대로 쓴다
        public int column;          // 표의 몇 번째 색인지 (1부터)
        public float hueShift;      // -180 ~ 180
        public float saturation;    // 1 = 그대로
        public float value;         // 1 = 그대로

        /// <summary>
        /// 소매를 옷 색으로 칠할 때, 그 색이 적힌 <b>옷 그림</b>의 경로.
        ///
        /// 소매 그림은 갈래마다 한 벌뿐이고 검정·갈색 두 색으로만 그려져 있다. 옷 그림 왼쪽 위
        /// 첫 픽셀이 소매 본색, 그 옆이 테두리색이라, 그 둘을 갈색·검정 자리에 갈아 끼우면
        /// 옷에 맞는 소매가 된다. 소매가 아닌 부위에서는 비어 있다.
        /// </summary>
        public string sleevePalette;

        public static FarmerTint None => new FarmerTint { saturation = 1f, value = 1f };

        public bool IsPlain => string.IsNullOrEmpty(lutPath)
                               && string.IsNullOrEmpty(sleevePalette)
                               && Mathf.Approximately(hueShift, 0f)
                               && Mathf.Approximately(saturation, 1f)
                               && Mathf.Approximately(value, 1f);

        // 슬라이더를 끌면 값이 잘게 바뀌는데, 그때마다 다른 색이라고 보면 그림을 끝없이 새로
        // 만들게 된다. 눈에 띄지 않을 만큼 단위를 굵게 잡아 같은 색으로 묶는다.
        private static float Snap(float v, float step) => Mathf.Round(v / step) * step;

        public string Key => $"{lutPath}#{column}#{Snap(hueShift, 3f):F0}"
                           + $"#{Snap(saturation, 0.05f):F2}#{Snap(value, 0.05f):F2}#{sleevePalette}";
    }

    /// <summary>
    /// 부위 시트를 읽어 프레임 단위 그림으로 잘라 주는 곳.
    ///
    /// 시트는 가로 한 줄이고 칸은 모두 같은 크기(16x32)다. 폭을 칸 너비로 나누면 프레임 수가
    /// 나온다. 부위마다 그림이 따로 있어도 <b>같은 칸에 자리가 맞도록</b> 그려져 있어서,
    /// 축을 칸 한가운데로 똑같이 잡아 두면 층을 전부 같은 자리에 놓기만 해도 몸이 된다.
    /// </summary>
    public static class FarmerArt
    {
        public const string Root = "Sprites/Farmer/";

        private static readonly Dictionary<string, Sprite[]> _frames = new Dictionary<string, Sprite[]>();
        private static readonly Dictionary<string, Texture2D> _luts = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> _missing = new HashSet<string>();

        /// <summary>색칠해서 새로 만든 그림들. 캐릭터 만들기 화면에서 색을 이리저리 돌려 보면
        /// 계속 쌓이므로, 일정 수를 넘으면 통째로 비우고 다시 만든다.</summary>
        private static readonly List<Texture2D> _made = new List<Texture2D>();
        private const int MaxMade = 240;

        /// <summary>
        /// "Tops/spr_player_top_adventurer" 처럼 부위 이름을 뺀 경로와 부위를 주면 그 부위의
        /// 프레임들을 돌려준다. 그런 파일이 없으면 null (그 옷에 그 부위가 없다는 뜻이다).
        /// </summary>
        public static Sprite[] Frames(string itemPath, FarmerSlot slot, FarmerTint tint)
        {
            if (string.IsNullOrEmpty(itemPath)) return null;

            string path = PathFor(itemPath, slot);
            string key = path + "|" + tint.Key;

            if (_frames.TryGetValue(key, out var cached)) return cached;
            if (_missing.Contains(path)) return null;

            var src = Resources.Load<Texture2D>(path);
            if (src == null)
            {
                _missing.Add(path);
                return null;
            }

            if (src.height != FarmerSlots.FrameHeight)
            {
                Debug.LogWarning($"[FarmerArt] {path} 는 높이가 {src.height}입니다. " +
                                 $"규격({FarmerSlots.FrameWidth}x{FarmerSlots.FrameHeight})으로 다시 만들어야 그려집니다.");
                _missing.Add(path);
                return null;
            }

            var tex = Prepare(src, tint, FarmerSlots.Info(slot).paletteHeader);
            if (!ReferenceEquals(tex, src))
            {
                if (_made.Count >= MaxMade) DropMade();
                _made.Add(tex);
            }

            var frames = Slice(tex);
            _frames[key] = frames;
            return frames;
        }

        /// <summary>
        /// 그림 파일 경로. 보통은 "품목경로_부위"지만 두 가지 예외가 있다.
        ///   · 경로가 폴더로 끝나면(Base/) 부위 이름만으로 파일이 된다 — Base/chest.png
        ///   · 부위에 붙일 이름이 없으면(소매) 경로가 이미 완성된 것이다 — Sleeves/arm_left_overshirts
        /// </summary>
        public static string PathFor(string itemPath, FarmerSlot slot)
        {
            string suffix = FarmerSlots.Info(slot).suffix;
            if (string.IsNullOrEmpty(suffix)) return Root + itemPath;
            return Root + itemPath + (itemPath.EndsWith("/") ? "" : "_") + suffix;
        }

        /// <summary>그 옷이 이 부위를 가지고 있는지.</summary>
        public static bool Has(string itemPath, FarmerSlot slot)
        {
            if (string.IsNullOrEmpty(itemPath)) return false;
            string path = PathFor(itemPath, slot);
            if (_missing.Contains(path)) return false;
            if (Resources.Load<Texture2D>(path) != null) return true;
            _missing.Add(path);
            return false;
        }

        /// <summary>이 옷에 딸린 색 대응표 경로. 없으면 null.</summary>
        public static string LutPath(string itemPath)
        {
            if (string.IsNullOrEmpty(itemPath)) return null;
            string path = itemPath + "_lut";
            return Lut(path) != null ? path : null;
        }

        /// <summary>
        /// 이 옷 전용 대응표가 있으면 그것을, 없으면 갈래가 함께 쓰는 대응표를 쓴다.
        /// 머리카락처럼 55가지 색이 한 표에 들어 있어 모든 머리 모양이 같이 쓰는 경우가 있다.
        /// </summary>
        public static string LutPathOr(string itemPath, string shared)
        {
            var own = LutPath(itemPath);
            if (own != null) return own;
            return Lut(shared) != null ? shared : null;
        }

        /// <summary>색 대응표에 들어 있는 색의 개수 (0번은 원본 색이라 세지 않는다).</summary>
        public static int PaletteCount(string lutPath)
        {
            var lut = Lut(lutPath);
            return lut == null ? 0 : Mathf.Max(0, lut.width - 1);
        }

        private static Texture2D Lut(string lutPath)
        {
            if (string.IsNullOrEmpty(lutPath)) return null;
            if (_luts.TryGetValue(lutPath, out var cached)) return cached;

            var tex = Resources.Load<Texture2D>(Root + lutPath);
            _luts[lutPath] = tex;
            return tex;
        }

        /// <summary>새로 만들어 둔 그림을 모두 버린다. 다음에 필요해지면 그때 다시 만든다.</summary>
        private static void DropMade()
        {
            foreach (var tex in _made) if (tex != null) Object.Destroy(tex);
            _made.Clear();

            // 버린 그림을 가리키던 것들도 같이 지운다 (원본 그대로 쓰던 것은 남겨 둔다).
            var stale = new List<string>();
            foreach (var pair in _frames)
            {
                var first = pair.Value != null && pair.Value.Length > 0 ? pair.Value[0] : null;
                if (first == null || first.texture == null) stale.Add(pair.Key);
            }
            foreach (var k in stale) _frames.Remove(k);
        }

        // ---------- 색칠 ----------

        /// <summary>
        /// 그림이 아니라 <b>표시</b>로 찍혀 있는 색. 오른팔·오른소매 몇 프레임에 네 픽셀씩 들어
        /// 있는데(손이 어디쯤인지 같은 표시로 보인다) 그대로 두면 새파란 초록 점으로 튀어나온다.
        /// </summary>
        private static readonly Color32 Marker = new Color32(0x1F, 0xFF, 0x00, 255);

        /// <summary>
        /// 소매 그림에 쓰인 두 색. 소매는 갈래마다 한 벌뿐이라 이 두 색으로만 그려져 있고,
        /// 입은 옷이 적어 둔 색을 이 자리에 갈아 끼워 옷마다 다른 소매를 만든다.
        /// </summary>
        private static readonly Color32 SleeveFill = new Color32(0x8F, 0x56, 0x3B, 255);   // 갈색 = 소매 본색
        private static readonly Color32 SleeveLine = new Color32(0x00, 0x00, 0x00, 255);   // 검정 = 테두리

        private static Texture2D Prepare(Texture2D src, FarmerTint tint, bool paletteHeader)
        {
            var lut = Lut(tint.lutPath);
            bool shift = !Mathf.Approximately(tint.hueShift, 0f)
                         || !Mathf.Approximately(tint.saturation, 1f)
                         || !Mathf.Approximately(tint.value, 1f);
            bool sleeve = SleeveColors(tint.sleevePalette, out var fill, out var line);

            // 칠할 것도 지울 표시도 없으면 원본을 그대로 쓴다 (그림을 새로 만들지 않는다).
            if (lut == null && !shift && !sleeve && !paletteHeader && !HasMarker(src)) return src;

            var px = src.GetPixels32();

            Color32[] map = null;
            if (lut != null)
            {
                // 표에서 한 열만 뽑아 둔다. 줄 번호가 곧 픽셀의 빨강 값이라 256칸이면 충분하다.
                int col = Mathf.Clamp(tint.column, 0, lut.width - 1);
                var lutPx = lut.GetPixels32();
                map = new Color32[256];
                for (int r = 0; r < 256 && r < lut.height; r++)
                {
                    // 텍스처는 아래가 0이므로 줄 번호를 뒤집어 읽는다.
                    map[r] = lutPx[(lut.height - 1 - r) * lut.width + col];
                }
            }

            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;

                if (Same(px[i], Marker))
                {
                    px[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                if (sleeve)
                {
                    if (Same(px[i], SleeveFill)) px[i] = Keep(fill, px[i].a);
                    else if (Same(px[i], SleeveLine)) px[i] = Keep(line, px[i].a);
                }

                if (map != null)
                {
                    var to = map[px[i].r];
                    if (to.a != 0) { byte a = px[i].a; px[i] = to; px[i].a = a; }
                }

                if (shift) px[i] = Shift(px[i], tint);
            }

            // 색 견본은 그려야 할 그림이 아니라 적어 둔 값이라 화면에 나오면 안 된다.
            if (paletteHeader)
            {
                int top = (src.height - 1) * src.width;
                for (int x = 0; x < 2 && x < src.width; x++) px[top + x] = new Color32(0, 0, 0, 0);
            }

            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = src.name + "_" + tint.Key
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        /// <summary>원래 픽셀의 투명도는 그대로 두고 색만 갈아 끼운다.</summary>
        private static Color32 Keep(Color32 to, byte alpha) { to.a = alpha; return to; }

        /// <summary>
        /// 옷 그림 왼쪽 위 두 픽셀에서 소매 색을 꺼낸다 — 첫 픽셀이 본색, 그 옆이 테두리색.
        /// 텍스처는 아래가 0이라 맨 윗줄은 배열의 마지막 줄이다.
        /// </summary>
        private static bool SleeveColors(string shirtPath, out Color32 fill, out Color32 line)
        {
            fill = line = default;
            if (string.IsNullOrEmpty(shirtPath)) return false;

            var shirt = Resources.Load<Texture2D>(shirtPath);
            if (shirt == null || shirt.width < 2) return false;

            var sp = shirt.GetPixels32();
            int top = (shirt.height - 1) * shirt.width;
            fill = sp[top];
            line = sp[top + 1];
            return fill.a != 0 && line.a != 0;
        }

        private static readonly Dictionary<Texture2D, bool> _hasMarker = new Dictionary<Texture2D, bool>();

        /// <summary>이 그림에 지워야 할 표시 픽셀이 들어 있는지 (한 번만 확인하고 기억해 둔다).</summary>
        private static bool HasMarker(Texture2D src)
        {
            if (_hasMarker.TryGetValue(src, out var known)) return known;

            bool found = false;
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                if (Same(px[i], Marker)) { found = true; break; }
            }
            _hasMarker[src] = found;
            return found;
        }

        private static Color32 Shift(Color32 c, FarmerTint tint)
        {
            Color.RGBToHSV(new Color32(c.r, c.g, c.b, 255), out float h, out float s, out float v);
            h = Mathf.Repeat(h + tint.hueShift / 360f, 1f);
            s = Mathf.Clamp01(s * tint.saturation);
            v = Mathf.Clamp01(v * tint.value);
            Color32 o = Color.HSVToRGB(h, s, v);
            o.a = c.a;
            return o;
        }

        // ---------- 자르기 ----------

        /// <summary>
        /// 가로로 늘어선 프레임들을 한 칸씩 잘라 낸다. 부위가 달라도 칸 크기가 같고 그림이
        /// 이미 서로 자리가 맞게 그려져 있으니, 축은 칸 한가운데로 모두 똑같이 잡으면 된다.
        /// </summary>
        private static Sprite[] Slice(Texture2D tex)
        {
            int fw = FarmerSlots.FrameWidth, fh = FarmerSlots.FrameHeight;
            int n = Mathf.Max(1, tex.width / fw);
            var list = new Sprite[n];
            var pivot = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < n; i++)
            {
                var rect = new Rect(i * fw, 0f, fw, fh);
                list[i] = Sprite.Create(tex, rect, pivot, FarmerSlots.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                list[i].name = $"{tex.name}_{i}";
            }
            return list;
        }
    }
}
