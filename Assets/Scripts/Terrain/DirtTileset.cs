using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 삽마법으로 파낸 흙땅의 그림. "Tileset_spring.png"(16px 타일)의 3x3 덩어리 하나를 런타임에 잘라 쓴다.
    ///
    ///   ┌ ─ ┐      가운데 한 칸이 온전한 흙이고, 둘레 여덟 칸은 잔디와 맞닿는 테두리다.
    ///   │ ■ │      한 칸만 파도 이 3x3이 통째로 놓여 "폭 파인" 모양이 되고, 여러 칸을 파면
    ///   └ ─ ┘      가운데가 늘어나며 테두리가 바깥으로 밀려난다 (9-슬라이스와 같은 원리).
    ///
    /// 3x3 조각은 흙 바깥이 <b>투명</b>하다. 그래서 한 칸에 여러 장을 겹쳐 놓을 수 있다 — 한 칸 띄우고 판
    /// 두 구덩이 사이 칸에는 왼쪽 구덩이의 오른쪽 변과 오른쪽 구덩이의 왼쪽 변을 함께 놓는다.
    /// 둘을 한 덩어리로 합쳐 버리지 않으므로 파지 않은 칸이 저절로 파인 것처럼 보이지 않는다.
    ///
    /// 다른 계절 시트("Tileset_summer" ...)가 같은 격자로 있으면 그쪽을 쓰고, 없으면 봄 시트를 쓴다.
    /// </summary>
    public static class DirtTileset
    {
        public const int Tile = 16;                  // 타일 한 변 픽셀 수 (= PPU)

        // 3x3 덩어리의 왼쪽 위 칸. 시트에서 왼쪽부터 9~11번째 열, 맨 윗줄을 0으로 센 3~5행이다.
        private const int FirstCol = 8;
        private const int FirstRow = 3;

        private const string SheetPrefix = "Sprites/Tiles/Tileset_";
        private const string FallbackSeason = "spring";

        private const int BlockCols = 3;

        // 계절 -> 조각 ([행 * BlockCols + 열]). 시트가 없으면 null을 넣어 두고 다시 찾지 않는다.
        private static readonly Dictionary<Season, Sprite[]> _bySeason = new Dictionary<Season, Sprite[]>();

        /// <summary>한 칸에 겹쳐 놓일 수 있는 조각의 최대 수 (네 방향 변 넷, 또는 네 대각선 모서리 넷).</summary>
        public const int MaxLayers = 4;

        /// <summary>삽질한 칸에 놓는 온전한 흙. 시트를 찾지 못했으면 null.</summary>
        public static Sprite Center => Piece(1, 1);

        /// <summary>
        /// <b>파지 않은</b> 칸에 놓을 테두리 조각들을 into에 채우고 그 수를 돌려준다.
        /// 여덟 방향 이웃이 삽질한 칸인지를 받아, 이웃 구덩이마다 이 칸으로 넘어오는 테두리를 고른다.
        ///
        /// 원칙: <b>파지 않은 칸은 흙으로 채우지 않는다.</b> 이웃 구덩이들의 테두리를 따로따로 겹쳐 놓을 뿐이다.
        /// ㄱ·ㄴ자로 판 안쪽 칸도 마찬가지 — 안쪽 모서리 조각으로 메우지 않고 두 변만 놓는다.
        /// </summary>
        public static int GetEdges(bool n, bool e, bool s, bool w, bool ne, bool se, bool sw, bool nw, Sprite[] into)
        {
            int count = 0;

            if (w) into[count++] = Piece(2, 1);
            if (e) into[count++] = Piece(0, 1);
            if (n) into[count++] = Piece(1, 2);
            if (s) into[count++] = Piece(1, 0);

            // 대각선 구덩이는 이 칸에 모서리만 걸친다. 옆의 변이 이미 덮는 모서리는 뺀다.
            if (nw && !n && !w) into[count++] = Piece(2, 2);
            if (ne && !n && !e) into[count++] = Piece(0, 2);
            if (sw && !s && !w) into[count++] = Piece(2, 0);
            if (se && !s && !e) into[count++] = Piece(0, 0);
            return count;
        }

        private static Sprite Piece(int col, int row)
        {
            var pieces = Load(Seasons.Current);
            return pieces?[row * BlockCols + col];
        }

        private static Sprite[] Load(Season season)
        {
            if (_bySeason.TryGetValue(season, out var cached)) return cached;

            var tex = Resources.Load<Texture2D>(SheetPrefix + Seasons.Key(season).ToLowerInvariant())
                      ?? Resources.Load<Texture2D>(SheetPrefix + FallbackSeason);
            Sprite[] pieces = null;
            if (tex == null)
            {
                Debug.LogWarning($"[DirtTileset] Missing texture: {SheetPrefix}{FallbackSeason}");
            }
            else
            {
                pieces = new Sprite[3 * BlockCols];
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < BlockCols; col++)
                    {
                        // 텍스처 좌표는 왼쪽 아래가 원점이라 행 번호를 뒤집는다.
                        var rect = new Rect((FirstCol + col) * Tile, tex.height - (FirstRow + row + 1) * Tile, Tile, Tile);
                        var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), Tile, 0, SpriteMeshType.FullRect);
                        sprite.name = $"dirt_{col}_{row}";
                        pieces[row * BlockCols + col] = sprite;
                    }
            }

            _bySeason[season] = pieces;
            return pieces;
        }
    }
}
