using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 캐릭터 만들기에서 사용자가 고른 색(HEX)을 실제 몸 그림에 입히는 곳.
    ///
    /// 방법: 같은 자리(피부/눈/머리/옷)의 <b>서로 다른 두 프리셋 그림</b>을 픽셀 단위로 비교해
    /// "색이 바뀌는 자리"(눈동자·머리카락·옷감)와 "안 바뀌는 자리"(테두리·눈동자 검은자·공용 그림자)를
    /// 가려낸다. 그런 다음 바뀌는 자리만, 원래 그림의 밝기·채도 차이(하이라이트/그림자)는 그대로 두고
    /// 색상만 사용자가 고른 색으로 바꿔서 새 텍스처를 만든다.
    ///
    /// 그래서 아무 HEX를 골라도 원래 그림의 입체감이 유지되고, <b>동작(Idle/Walk/Pickaxe...)마다
    /// 그림을 새로 그릴 필요가 없다</b> — 이미 있는 프리셋(예: Blue, Brown) 하나를 밑그림 삼아
    /// 색만 다시 계산하기 때문에, 그 프리셋이 있는 모든 동작에 한 번에 적용된다.
    /// </summary>
    public static class PlayerPaletteSwap
    {
        private const string Root = "Sprites/Player/";

        /// <summary>한 층(폴더)에서 "색칠 가능한 자리"와 그 기준(가장 흔한 색)의 HSV.</summary>
        private class Ramp
        {
            public bool[] tintable;   // baseline 텍스처와 같은 길이 — 픽셀별로 색칠 대상인지
            public float anchorH, anchorS, anchorV;
            public string BaselineName;   // 이 층의 밑그림으로 쓴 프리셋 이름 (캐시를 다시 찾을 때 쓴다)
        }

        // "동작/층폴더" -> 색칠 가능 자리 (동작·층 조합마다 한 번만 계산한다)
        private static readonly Dictionary<string, Ramp> _ramps = new Dictionary<string, Ramp>();
        // "동작/층폴더#색상" -> 이미 색칠해 잘라 둔 방향별 프레임
        private static readonly Dictionary<string, Sprite[][]> _cache = new Dictionary<string, Sprite[][]>();

        /// <summary>
        /// 한 층을 사용자가 고른 색으로 칠한 방향별 프레임. 그 폴더에 그림이 아예 없으면 null —
        /// 부르는 쪽은 그때 프리셋 방식(PlayerSpriteLibrary)으로 대신 그리면 된다.
        /// </summary>
        /// <param name="preferredA">기준으로 삼을 프리셋 이름 (예: "Blue", "Brown", "Black", "1").</param>
        /// <param name="preferredB">색칠 대상을 가려내기 위해 대조할 다른 프리셋 이름.</param>
        public static Sprite[] Frames(string animFolder, string layerFolder,
                                      string preferredA, string preferredB,
                                      Direction dir, Color target)
        {
            var byDir = Sheet(animFolder, layerFolder, preferredA, preferredB, target);
            return byDir?[(int)dir];
        }

        private static Sprite[][] Sheet(string animFolder, string layerFolder,
                                        string preferredA, string preferredB, Color target)
        {
            string folder = animFolder + "/" + layerFolder;
            string colorKey = ColorUtility.ToHtmlStringRGB(target);
            string cacheKey = folder + "#" + colorKey;
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

            var ramp = GetOrBuildRamp(folder, preferredA, preferredB, out var baseline);
            if (ramp == null) { _cache[cacheKey] = null; return null; }

            var recolored = Recolor(baseline, ramp, target);
            recolored.name = (folder + "_" + colorKey).Replace('/', '_');

            var sliced = PlayerSpriteLibrary.Slice(recolored);
            _cache[cacheKey] = sliced;
            return sliced;
        }

        /// <summary>
        /// 두 프리셋을 비교해 "색칠 가능한 자리"를 알아낸다. 선호하는 이름이 이 동작엔 없을 수도
        /// 있으므로(예: 낚시 릴 감기 눈은 초록만 있음) 그때는 폴더 안에 있는 아무 그림으로 대신한다.
        /// </summary>
        private static Ramp GetOrBuildRamp(string folder, string preferredA, string preferredB, out Texture2D baseline)
        {
            if (_ramps.TryGetValue(folder, out var cached))
            {
                baseline = Resources.Load<Texture2D>(Root + folder + "/" + cached.BaselineName);
                return cached;
            }

            var texA = Resources.Load<Texture2D>(Root + folder + "/" + preferredA);
            var texB = Resources.Load<Texture2D>(Root + folder + "/" + preferredB);
            string baselineName = preferredA;

            if (texA == null)
            {
                // 선호하는 프리셋이 없다 — 폴더 안의 그림들 중 서로 다른 두 장을 대신 쓴다.
                var all = Resources.LoadAll<Texture2D>(Root + folder);
                if (all == null || all.Length == 0) { baseline = null; return null; }
                texA = all[0];
                texB = all.Length > 1 ? all[1] : null;
                baselineName = texA.name;
            }

            bool[] tintable;
            if (texB != null && texB.width == texA.width && texB.height == texA.height)
            {
                tintable = DiffMask(texA, texB);
            }
            else
            {
                // 대조할 그림이 없다 — 색이 있는(회색·검정·흰색이 아닌) 자리만 색칠 대상으로 본다.
                tintable = SaturationMask(texA);
            }

            var ramp = BuildRamp(texA, tintable, baselineName);
            _ramps[folder] = ramp;
            baseline = texA;
            return ramp;
        }

        /// <summary>두 그림에서 색이 다른 자리 = 색칠 대상. 같은 자리(테두리·그림자)는 그대로 둔다.</summary>
        private static bool[] DiffMask(Texture2D a, Texture2D b)
        {
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            var mask = new bool[pa.Length];
            for (int i = 0; i < pa.Length; i++)
                mask[i] = pa[i].a > 0 && !ColorsEqual(pa[i], pb[i]);
            return mask;
        }

        /// <summary>대조할 그림이 없을 때의 대체 규칙: 채도·명도가 있는 자리만 색칠 대상.</summary>
        private static bool[] SaturationMask(Texture2D a)
        {
            var pa = a.GetPixels32();
            var mask = new bool[pa.Length];
            for (int i = 0; i < pa.Length; i++)
            {
                if (pa[i].a == 0) continue;
                Color.RGBToHSV(pa[i], out _, out float s, out float v);
                mask[i] = s > 0.12f && v > 0.08f;
            }
            return mask;
        }

        private static bool ColorsEqual(Color32 x, Color32 y) => x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a;

        /// <summary>색칠 대상 중 가장 흔한 색을 기준으로 삼는다 — 다른 자리의 밝기 차이를 그 기준으로 잰다.</summary>
        private static Ramp BuildRamp(Texture2D baseline, bool[] tintable, string baselineName)
        {
            var pixels = baseline.GetPixels32();
            var counts = new Dictionary<Color32, int>();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (!tintable[i]) continue;
                counts.TryGetValue(pixels[i], out int c);
                counts[pixels[i]] = c + 1;
            }

            float anchorH = 0f, anchorS = 0f, anchorV = 0.5f;
            int best = -1;
            foreach (var kv in counts)
            {
                if (kv.Value <= best) continue;
                best = kv.Value;
                Color.RGBToHSV(kv.Key, out anchorH, out anchorS, out anchorV);
            }

            return new Ramp { tintable = tintable, anchorH = anchorH, anchorS = anchorS, anchorV = anchorV, BaselineName = baselineName };
        }

        /// <summary>
        /// 색칠 대상 픽셀만 새 색으로 바꾼다. 원래 그 자리가 기준색보다 밝았으면(하이라이트) 그만큼
        /// 더 밝게, 어두웠으면(그림자) 그만큼 더 어둡게 — 원본의 입체감을 그대로 살린다.
        /// 그 외의 자리(테두리 등)는 원본 그대로 복사한다.
        /// </summary>
        private static Texture2D Recolor(Texture2D baseline, Ramp ramp, Color target)
        {
            Color.RGBToHSV(target, out float targetH, out float targetS, out float targetV);

            var src = baseline.GetPixels32();
            var dst = new Color32[src.Length];

            for (int i = 0; i < src.Length; i++)
            {
                if (!ramp.tintable[i]) { dst[i] = src[i]; continue; }

                Color.RGBToHSV(src[i], out _, out float s, out float v);
                float newS = Mathf.Clamp01(targetS + (s - ramp.anchorS));
                float newV = Mathf.Clamp01(targetV + (v - ramp.anchorV));
                Color32 c = Color.HSVToRGB(targetH, newS, newV);
                c.a = src[i].a;
                dst[i] = c;
            }

            var tex = new Texture2D(baseline.width, baseline.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(dst);
            tex.Apply(false, false);
            return tex;
        }
    }
}
