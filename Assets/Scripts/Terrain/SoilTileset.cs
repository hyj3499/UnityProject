using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// "Soil_spring.png"(192x192, 16px 타일)를 런타임에 잘라 쓰는 47-blob 오토타일 시트.
    /// 위에서부터 12x4 블록씩 개간한 땅, 경작한 땅, 젖은 땅이 같은 배치로 들어 있다.
    ///
    /// 이웃 8칸의 연결 상태를 비트마스크로 만들어 대각선/가로선/세로선/T자/3x3 등
    /// 모든 조합에서 테두리가 자연스럽게 이어지도록 한 칸을 골라 준다.
    /// </summary>
    public static class SoilTileset
    {
        public const int Tile = 16;                       // 타일 한 변 픽셀 수 (= PPU)
        private const int Cols = 12;                      // 한 블록의 가로 칸 수
        private const int SheetCols = Cols;               // 시트 한 줄의 전체 칸 수
        private const int RowsPerState = 4;                // 한 상태 블록의 세로 칸 수
        private const int DugRowOffset = 0;                // 개간한 땅은 첫 번째 블록
        private const int DryRowOffset = RowsPerState;     // 경작한 땅은 두 번째 블록
        private const int WetRowOffset = RowsPerState * 2; // 젖은 땅은 세 번째 블록
        private const string SheetPath = "Sprites/Tiles/Soil_spring";

        // 이웃 비트. 대각선 비트는 양옆 두 직교 이웃이 모두 이어져 있을 때만 의미가 있다.
        public const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;

        /// <summary>정규화된 마스크 -> 시트의 (열, 행). 47개 blob 조합 전부를 덮는다.</summary>
        private static readonly int[,] MaskToCell =
        {
            //  mask, tx, ty
            {   0, 0, 3 }, {   1, 0, 2 }, {   4, 1, 3 }, {   5, 1, 2 }, {   7, 8, 3 }, {  16, 0, 0 },
            {  17, 0, 1 }, {  20, 1, 0 }, {  21, 1, 1 }, {  23, 4, 2 }, {  28, 8, 0 }, {  29, 4, 1 },
            {  31, 8, 1 }, {  64, 3, 3 }, {  65, 3, 2 }, {  68, 2, 3 }, {  69, 2, 2 }, {  71, 5, 3 },
            {  80, 3, 0 }, {  81, 3, 1 }, {  84, 2, 0 }, {  85, 2, 1 }, {  87, 7, 0 }, {  92, 5, 0 },
            {  93, 7, 3 }, {  95, 8, 2 }, { 112,11, 0 }, { 113, 7, 1 }, { 116, 6, 0 }, { 117, 4, 3 },
            { 119, 9, 1 }, { 124,10, 0 }, { 125, 9, 0 }, { 127, 5, 1 }, { 193,11, 3 }, { 197, 6, 3 },
            { 199, 9, 3 }, { 209, 7, 2 }, { 213, 4, 0 }, { 215,10, 3 }, { 221,10, 2 }, { 223, 5, 2 },
            { 241,11, 2 }, { 245,11, 1 }, { 247, 6, 2 }, { 253, 6, 1 }, { 255, 9, 2 },
        };

        private static Sprite[] _dug;   // [정규화 마스크] -> 개간한 땅
        private static Sprite[] _dry;   // [정규화 마스크] -> 마른 경작지
        private static Sprite[] _wet;   // [정규화 마스크] -> 젖은 흙
        private static bool _loaded;

        /// <summary>시트를 찾지 못했으면 false — 호출부는 예전 단일 스프라이트로 대체한다.</summary>
        public static bool Available
        {
            get { EnsureLoaded(); return _dug != null && _dry != null && _wet != null; }
        }

        /// <summary>이웃 8칸 비트마스크에 맞는 개간한 땅 스프라이트.</summary>
        public static Sprite GetDug(int mask)
        {
            EnsureLoaded();
            return _dug?[Normalize(mask)];
        }

        /// <summary>이웃 8칸 비트마스크에 맞는 마른 경작지 스프라이트.</summary>
        public static Sprite GetDry(int mask)
        {
            EnsureLoaded();
            return _dry?[Normalize(mask)];
        }

        /// <summary>이웃 8칸 비트마스크에 맞는 젖은 흙 스프라이트 (경작지 위에 덧그린다).</summary>
        public static Sprite GetWet(int mask)
        {
            EnsureLoaded();
            return _wet?[Normalize(mask)];
        }

        /// <summary>대각선은 양옆이 모두 이어져 있을 때만 남긴다 (47개 blob 조합으로 축약).</summary>
        public static int Normalize(int mask)
        {
            if ((mask & (N | E)) != (N | E)) mask &= ~NE;
            if ((mask & (S | E)) != (S | E)) mask &= ~SE;
            if ((mask & (S | W)) != (S | W)) mask &= ~SW;
            if ((mask & (N | W)) != (N | W)) mask &= ~NW;
            return mask & 0xFF;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            var tex = Resources.Load<Texture2D>(SheetPath);
            if (tex == null)
            {
                Debug.LogWarning($"[SoilTileset] Missing texture: {SheetPath}");
                return;
            }

            var dug = new Sprite[256];
            var dry = new Sprite[256];
            var wet = new Sprite[256];
            var cache = new Dictionary<int, Sprite>();

            int count = MaskToCell.GetLength(0);
            for (int i = 0; i < count; i++)
            {
                int mask = MaskToCell[i, 0];
                int tx = MaskToCell[i, 1];
                int ty = MaskToCell[i, 2];
                dug[mask] = Slice(tex, tx, ty + DugRowOffset, cache);
                dry[mask] = Slice(tex, tx, ty + DryRowOffset, cache);
                wet[mask] = Slice(tex, tx, ty + WetRowOffset, cache);
            }

            // 표에 없는 마스크(정규화되지 않은 값)는 홀로 떨어진 타일로 대체해 둔다.
            for (int m = 0; m < 256; m++)
            {
                if (dug[m] == null) dug[m] = dug[0];
                if (dry[m] == null) dry[m] = dry[0];
                if (wet[m] == null) wet[m] = wet[0];
            }

            _dug = dug;
            _dry = dry;
            _wet = wet;
        }

        private static Sprite Slice(Texture2D tex, int tx, int ty, Dictionary<int, Sprite> cache)
        {
            int key = ty * SheetCols + tx;
            if (cache.TryGetValue(key, out var cached)) return cached;

            // 텍스처 좌표는 왼쪽 아래가 원점이라 행 번호를 뒤집는다.
            var rect = new Rect(tx * Tile, tex.height - (ty + 1) * Tile, Tile, Tile);
            var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), Tile, 0, SpriteMeshType.FullRect);
            sprite.name = $"soil_{tx}_{ty}";
            cache[key] = sprite;
            return sprite;
        }
    }
}
