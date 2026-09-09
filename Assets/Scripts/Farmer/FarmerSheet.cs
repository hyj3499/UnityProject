using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// farmer_base 시트가 가로로 나뉘어 있는 세 구역. 값은 <b>시트에서의 가로 위치</b>다
    /// (0=0~5열, 1=6~11열, 2=12~17열).
    ///
    /// 팔이 두 벌인 것은 <b>들고 다닐 때</b> 자세가 다르기 때문이다. 같은 프레임 번호라도
    /// 손을 내린 팔(1)과 머리 위로 든 팔(2)이 따로 그려져 있어, 무엇을 들었느냐에 따라
    /// 둘 중 하나만 골라 몸 위에 얹으면 된다.
    /// </summary>
    public enum FarmerSection
    {
        Body = 0,          // 머리·몸통·다리·부츠 (옷은 들어 있지 않다)
        Arms = 1,          // 팔 — 보통 자세
        ArmsCarrying = 2,  // 팔 — 머리 위로 들어 올린 자세
    }

    /// <summary>
    /// farmer_base.png / farmer_girl_base.png 를 <b>프레임 번호</b>로 잘라 내주는 곳.
    ///
    /// 시트 규격 (스타듀밸리와 같다):
    ///   · 한 칸 16x32, 한 줄에 6칸, 21줄 → 프레임 번호 0~125
    ///   · 가로로 세 구역: 몸(0~5열) · 팔(6~11열) · 든 팔(12~17열).
    ///     같은 프레임 번호의 칸들이 서로 짝이라, 번호 하나로 몸과 팔이 한꺼번에 정해진다.
    ///   · 프레임 번호 -> 줄 = 번호/6, 칸 = 번호%6  (위키의 R3F2 = 번호 13)
    ///
    /// 이 시트에는 <b>옷이 들어 있지 않다</b> — 부츠만 신은 맨몸이다. 상·하의는 shirts/pants
    /// 시트에서 따로 가져와 위에 얹는다.
    ///
    /// 색은 <b>정해진 표식 색</b>으로 구분한다 — 스타듀밸리가 쓰는 방식 그대로다. 시트에 칠해진
    /// 피부·부츠·눈동자·옷 색은 "여기는 피부다"라는 표시일 뿐이고, 실제 색은 캐릭터를 만들 때
    /// 고른 값으로 갈아 끼운다. 그래서 색깔별 그림이 한 장도 필요 없다.
    /// </summary>
    public static class FarmerSheet
    {
        public const int CellWidth = 16, CellHeight = 32;
        public const int Columns = 6;
        public const int SectionWidth = Columns * CellWidth;   // 96px
        private const float PixelsPerUnit = 16f;

        private const string MaleSheet = "Sprites/Farmer/farmer_base";
        private const string FemaleSheet = "Sprites/Farmer/farmer_girl_base";

        // ---------- 표식 색 ----------
        // 시트에 실제로 칠해져 있는 색들. 밝은 것이 기준(anchor)이고, 나머지는 그 기준에서
        // 얼마나 어두운지로 다시 계산한다 — 그래서 아무 색을 골라도 원래 음영이 살아난다.
        private static readonly Color32[] SkinMarkers =
        { new Color32(0xF9, 0xAE, 0x89, 255), new Color32(0xE0, 0x6B, 0x65, 255), new Color32(0x6B, 0x00, 0x3A, 255) };

        private static readonly Color32[] BootMarkers =
        { new Color32(0xAD, 0x47, 0x1B, 255), new Color32(0x77, 0x29, 0x1A, 255),
          new Color32(0x5B, 0x1F, 0x24, 255), new Color32(0x3D, 0x11, 0x23, 255) };

        private static readonly Color32[] EyeMarkers =
        { new Color32(0x1C, 0x96, 0x4A, 255), new Color32(0x0E, 0x4C, 0x3B, 255) };

        /// <summary>소매(팔 구역에 칠해져 있는 옷감) 표식.</summary>
        private static readonly Color32[] ClothMarkers =
        { new Color32(0x8E, 0x1F, 0x0C, 255), new Color32(0x70, 0x17, 0x18, 255), new Color32(0x4A, 0x0C, 0x06, 255) };

        /// <summary>이 시트를 어떤 색들로 칠할지.</summary>
        public struct Palette
        {
            public Color skin, boots, eyes, pants, shirt;

            /// <summary>바지 색은 이 시트에 쓰이지 않지만(옷이 없다), 같은 묶음으로 다뤄 편하도록 둔다.</summary>
            public string Key => $"{Hex(skin)}{Hex(boots)}{Hex(eyes)}{Hex(shirt)}";
            private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        }

        // "시트#팔레트" -> 구역별·프레임별 스프라이트
        private static readonly Dictionary<string, Sprite[][]> _cache = new Dictionary<string, Sprite[][]>();

        /// <summary>프레임 번호에 해당하는 한 구역의 그림. 시트를 못 읽으면 null.</summary>
        public static Sprite Get(bool female, FarmerSection section, int frame, Palette palette)
        {
            var bySection = Sheet(female, palette);
            if (bySection == null) return null;

            var frames = bySection[(int)section];
            return frame >= 0 && frame < frames.Length ? frames[frame] : null;
        }

        /// <summary>이 시트에 들어 있는 프레임 수 (보통 126).</summary>
        public static int FrameCount(bool female, Palette palette)
        {
            var bySection = Sheet(female, palette);
            return bySection == null ? 0 : bySection[0].Length;
        }

        private static Sprite[][] Sheet(bool female, Palette palette)
        {
            string path = female ? FemaleSheet : MaleSheet;
            string key = path + "#" + palette.Key;
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var src = Resources.Load<Texture2D>(path);
            if (src == null)
            {
                Debug.LogError($"[FarmerSheet] {path} 를 찾지 못했습니다.");
                _cache[key] = null;
                return null;
            }

            var tex = Recolor(src, palette);
            var sliced = Slice(tex);
            _cache[key] = sliced;
            return sliced;
        }

        /// <summary>표식 색을 고른 색으로 갈아 끼운다. 옷감 표식은 팔에만 있으므로 셔츠 색이 된다.</summary>
        private static Texture2D Recolor(Texture2D src, Palette palette)
        {
            var px = src.GetPixels32();
            int w = src.width;

            var skin = Ramp(SkinMarkers, palette.skin);
            var boots = Ramp(BootMarkers, palette.boots);
            var eyes = Ramp(EyeMarkers, palette.eyes);
            var sleeves = Ramp(ClothMarkers, palette.shirt);

            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;

                if (TrySwap(ref px[i], skin)) continue;
                if (TrySwap(ref px[i], boots)) continue;
                if (TrySwap(ref px[i], eyes)) continue;
                TrySwap(ref px[i], sleeves);
            }

            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = src.name + "_" + palette.Key
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        private static bool TrySwap(ref Color32 p, Dictionary<int, Color32> map)
        {
            if (!map.TryGetValue(Rgb(p), out var to)) return false;
            byte a = p.a;
            p = to;
            p.a = a;
            return true;
        }

        private static int Rgb(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        /// <summary>
        /// 표식 색들 -> 바꿔 넣을 색들. 첫 표식(가장 밝은 것)을 기준 삼아, 나머지가 기준보다
        /// 얼마나 어둡고 탁했는지를 그대로 옮긴다. 그래서 한 가지 색만 골라도 음영이 따라온다.
        /// </summary>
        private static Dictionary<int, Color32> Ramp(Color32[] markers, Color target)
        {
            Color.RGBToHSV(markers[0], out _, out float anchorS, out float anchorV);
            Color.RGBToHSV(target, out float th, out float ts, out float tv);

            var map = new Dictionary<int, Color32>(markers.Length);
            foreach (var m in markers)
            {
                Color.RGBToHSV(m, out _, out float s, out float v);
                Color32 c = Color.HSVToRGB(th,
                                           Mathf.Clamp01(ts + (s - anchorS)),
                                           Mathf.Clamp01(tv + (v - anchorV)));
                map[Rgb(m)] = c;
            }
            return map;
        }

        /// <summary>시트를 구역별로, 그 안에서 프레임 번호 순서대로 자른다.</summary>
        private static Sprite[][] Slice(Texture2D tex)
        {
            int rows = tex.height / CellHeight;
            int frames = rows * Columns;
            var pivot = new Vector2(0.5f, 0.5f);
            var bySection = new Sprite[3][];

            for (int s = 0; s < 3; s++)
            {
                var list = new Sprite[frames];
                for (int f = 0; f < frames; f++)
                {
                    int col = f % Columns, row = f / Columns;
                    // 텍스처 좌표는 아래가 0이라 줄 번호를 뒤집는다.
                    var rect = new Rect(s * SectionWidth + col * CellWidth,
                                        tex.height - (row + 1) * CellHeight,
                                        CellWidth, CellHeight);
                    list[f] = Sprite.Create(tex, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    list[f].name = $"{tex.name}_{(FarmerSection)s}_{f}";
                }
                bySection[s] = list;
            }
            return bySection;
        }
    }
}
