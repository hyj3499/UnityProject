using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 몸 위에 얹는 것들 — 상의·하의·머리카락. farmer_base 시트에는 옷도 머리카락도 들어 있지
    /// 않아 (부츠만 신은 대머리다) 전부 여기서 따로 가져온다.
    ///
    /// 두 종류로 갈린다:
    ///   · <b>바지</b>는 몸과 똑같은 16x32 프레임이라 번호만 맞춰 그대로 겹치면 된다.
    ///   · <b>셔츠·머리카락</b>은 방향마다 그림이 하나씩뿐이라, 프레임마다 몸이 흔들린 만큼
    ///     옮겨 찍어야 한다. 그 흔들림은 FarmerFrames가 알고 있다. 여기서는 미리 16x32 칸 안에
    ///     찍어 두어 몸·팔과 똑같이 다룰 수 있게 만든다 (그래야 왼쪽을 볼 때 통째로 뒤집기만
    ///     하면 된다).
    ///
    /// 색은 시트를 고쳐 만들지 않고 <b>SpriteRenderer.color로 곱해</b> 입힌다. 세 시트 모두
    /// 흰색·회색으로만 그려져 있어서 곱하면 그 색이 되고, 색이 바뀌어도 그림은 그대로다.
    /// 그래서 색을 아무리 많이 골라도 만들어 두는 그림은 늘지 않는다.
    /// </summary>
    public static class FarmerClothes
    {
        private const string PantsSheet = "Sprites/Farmer/pants";
        private const string ShirtSheet = "Sprites/Farmer/shirts";
        private const string HairSheet = "Sprites/Farmer/hairstyles";

        private const float PixelsPerUnit = 16f;

        // 셔츠: 한 벌이 8x32 (정면·우측·좌측·후면 8x8 넉 장), 한 줄에 16벌.
        private const int ShirtSize = 8, ShirtColumns = 16, ShirtRowHeight = 32;
        private const int ShirtBaseX = 4, ShirtBaseY = 15;

        // 머리카락: 한 벌이 16x96 (정면·우측·후면 16x32 석 장), 한 줄에 8벌.
        private const int HairColumns = 8, HairRowHeight = 96;
        private const int HairBaseX = 0, HairBaseY = 1;

        /// <summary>기본 셔츠 — 무늬 없는 흰 티셔츠라 어떤 색으로 물들여도 깔끔하다.</summary>
        public const int DefaultShirt = 41;

        /// <summary>고를 수 있는 머리 모양 수 (8열 x 7줄).</summary>
        public const int HairCount = 56;

        private static Sprite[] _pants;
        private static readonly Dictionary<int, Sprite[]> _shirts = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> _hair = new Dictionary<int, Sprite[]>();

        /// <summary>프레임 번호에 맞는 바지. 흰 그림이므로 렌더러 색으로 물들여 쓴다.</summary>
        public static Sprite Pants(int frame)
        {
            if (_pants == null)
            {
                var tex = Resources.Load<Texture2D>(PantsSheet);
                if (tex == null)
                {
                    Debug.LogError($"[FarmerClothes] {PantsSheet} 를 찾지 못했습니다.");
                    _pants = new Sprite[0];
                }
                else _pants = SliceFrames(tex);
            }
            return Pick(_pants, frame);
        }

        /// <summary>프레임 번호에 맞는 셔츠. 이미 16x32 칸 안 제자리에 찍혀 있다.</summary>
        public static Sprite Shirt(int style, int frame) => Pick(Overlay(_shirts, style, ShirtKind), frame);

        /// <summary>프레임 번호에 맞는 머리카락. 이미 16x32 칸 안 제자리에 찍혀 있다.</summary>
        public static Sprite Hair(int style, int frame) => Pick(Overlay(_hair, style, HairKind), frame);

        private static Sprite Pick(Sprite[] frames, int frame)
            => frames != null && frame >= 0 && frame < frames.Length ? frames[frame] : null;

        // ---------- 얹는 그림 만들기 ----------

        /// <summary>한 시트가 어떻게 생겼는지 — 얹는 그림들의 공통 규격.</summary>
        private struct OverlayKind
        {
            public string path;
            public int width, height;      // 한 방향 그림의 크기
            public int columns;            // 한 줄에 든 벌 수
            public int rowHeight;          // 한 벌이 차지하는 높이
            public int baseX, baseY;       // 기준 프레임에서 찍을 자리 (칸 왼쪽 위 기준)
            public int[] subRows;          // 정면·우측·후면이 그 벌 안에서 몇 번째 장인지
        }

        private static readonly OverlayKind ShirtKind = new OverlayKind
        {
            path = ShirtSheet,
            width = ShirtSize, height = ShirtSize,
            columns = ShirtColumns, rowHeight = ShirtRowHeight,
            baseX = ShirtBaseX, baseY = ShirtBaseY,
            subRows = new[] { 0, 1, 3 },        // 시트 순서: 정면·우측·좌측·후면 (좌측은 안 쓴다)
        };

        private static readonly OverlayKind HairKind = new OverlayKind
        {
            path = HairSheet,
            width = FarmerSheet.CellWidth, height = FarmerSheet.CellHeight,
            columns = HairColumns, rowHeight = HairRowHeight,
            baseX = HairBaseX, baseY = HairBaseY,
            subRows = new[] { 0, 1, 2 },        // 시트 순서: 정면·우측·후면
        };

        private static Sprite[] Overlay(Dictionary<int, Sprite[]> cache, int style, OverlayKind kind)
        {
            if (cache.TryGetValue(style, out var frames)) return frames;
            frames = Build(style, kind);
            cache[style] = frames;
            return frames;
        }

        /// <summary>
        /// 한 벌(방향 석 장)을 126칸짜리 그림판에 미리 찍어 둔다. 칸마다 그 프레임의 방향에 맞는
        /// 장을 골라, 몸이 흔들린 만큼 옮겨 찍는다. 칸 밖으로 나가는 부분은 잘라 낸다.
        /// </summary>
        private static Sprite[] Build(int style, OverlayKind kind)
        {
            var src = Resources.Load<Texture2D>(kind.path);
            if (src == null)
            {
                Debug.LogError($"[FarmerClothes] {kind.path} 를 찾지 못했습니다.");
                return null;
            }

            var srcPixels = src.GetPixels32();
            int sx = (style % kind.columns) * kind.width;
            int sy = (style / kind.columns) * kind.rowHeight;

            var byFacing = new Color32[3][];
            for (int i = 0; i < 3; i++)
                byFacing[i] = ReadBlock(srcPixels, src.width, src.height,
                                        sx, sy + kind.subRows[i] * kind.height,
                                        kind.width, kind.height);

            int cols = FarmerSheet.Columns;
            int rows = (FarmerFrames.Count + cols - 1) / cols;
            int w = cols * FarmerSheet.CellWidth, h = rows * FarmerSheet.CellHeight;
            var canvas = new Color32[w * h];

            for (int f = 0; f < FarmerFrames.Count; f++)
            {
                var info = FarmerFrames.Info(f);
                var stamp = byFacing[(int)info.facing];

                int cellX = (f % cols) * FarmerSheet.CellWidth;
                int cellY = (f / cols) * FarmerSheet.CellHeight;

                for (int y = 0; y < kind.height; y++)
                {
                    for (int x = 0; x < kind.width; x++)
                    {
                        var c = stamp[y * kind.width + x];
                        if (c.a == 0) continue;

                        int px = cellX + kind.baseX + info.dx + x;
                        int py = cellY + kind.baseY + info.dy + y;      // 칸 안에서 위가 0
                        if (px < cellX || px >= cellX + FarmerSheet.CellWidth) continue;
                        if (py < cellY || py >= cellY + FarmerSheet.CellHeight) continue;

                        canvas[(h - 1 - py) * w + px] = c;              // 텍스처 좌표는 아래가 0
                    }
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = src.name + "_" + style
            };
            tex.SetPixels32(canvas);
            tex.Apply(false, false);
            return SliceFrames(tex);
        }

        /// <summary>시트에서 한 장을 위가 0인 순서로 읽어 온다.</summary>
        private static Color32[] ReadBlock(Color32[] px, int texW, int texH, int x, int yFromTop, int w, int h)
        {
            var block = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int srcY = texH - 1 - (yFromTop + y);
                if (srcY < 0 || srcY >= texH) continue;
                for (int i = 0; i < w; i++)
                {
                    int srcX = x + i;
                    if (srcX < 0 || srcX >= texW) continue;
                    block[y * w + i] = px[srcY * texW + srcX];
                }
            }
            return block;
        }

        /// <summary>몸과 같은 규격(16x32, 한 줄 6칸)으로 자른다.</summary>
        private static Sprite[] SliceFrames(Texture2D tex)
        {
            int rows = tex.height / FarmerSheet.CellHeight;
            var list = new Sprite[rows * FarmerSheet.Columns];
            var pivot = new Vector2(0.5f, 0.5f);

            for (int f = 0; f < list.Length; f++)
            {
                int col = f % FarmerSheet.Columns, row = f / FarmerSheet.Columns;
                var rect = new Rect(col * FarmerSheet.CellWidth,
                                    tex.height - (row + 1) * FarmerSheet.CellHeight,
                                    FarmerSheet.CellWidth, FarmerSheet.CellHeight);
                list[f] = Sprite.Create(tex, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                list[f].name = $"{tex.name}_{f}";
            }
            return list;
        }
    }
}
