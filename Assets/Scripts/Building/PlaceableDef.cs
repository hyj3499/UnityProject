using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>설치물의 큰 갈래. 울타리는 막고, 길은 밟고 지나간다.</summary>
    public enum PlaceableKind { Fence, Road }

    /// <summary>
    /// 이웃 연결 상태를 나타내는 비트. 같은 <b>종류끼리만</b> 이어진 것으로 본다 —
    /// 울타리 옆의 길은 이웃으로 치지 않는다.
    /// </summary>
    public static class Connect
    {
        public const int N = 1, E = 2, S = 4, W = 8;
        public const int Count = 16;   // 4비트 조합 전부
    }

    /// <summary>
    /// 시트에서 잘라 쓸 칸 하나. 좌우/상하 뒤집기를 함께 담아 두기 때문에, 그림이 한쪽
    /// 방향밖에 없는 시트에서도 나머지 방향을 만들어 쓸 수 있다 (SpriteRenderer.flipX/Y).
    /// </summary>
    public struct AutoTileCell
    {
        public int col, row;
        public bool flipX, flipY;
        public bool valid;

        public AutoTileCell(int col, int row, bool flipX = false, bool flipY = false)
        {
            this.col = col;
            this.row = row;
            this.flipX = flipX;
            this.flipY = flipY;
            valid = true;
        }
    }

    /// <summary>
    /// "이웃이 이렇게 붙어 있으면 시트의 이 칸을 쓴다"를 담는 16칸짜리 표.
    /// 하나만 놓았을 때(0), 한쪽만 이어졌을 때, 일자, ㄱ자, T자, 십자가 모두 여기서 갈린다.
    /// 표를 채우지 않은 조합은 가장 비슷한 조합으로 대신한다(Fallback).
    /// </summary>
    public class AutoTileMap
    {
        private readonly AutoTileCell[] _cells = new AutoTileCell[Connect.Count];

        /// <summary>mask는 Connect.N/E/S/W의 조합.</summary>
        public AutoTileMap Set(int mask, int col, int row, bool flipX = false, bool flipY = false)
        {
            _cells[mask & 0xF] = new AutoTileCell(col, row, flipX, flipY);
            return this;
        }

        /// <summary>여러 조합에 같은 칸을 한 번에 지정한다.</summary>
        public AutoTileMap SetMany(int[] masks, int col, int row, bool flipX = false, bool flipY = false)
        {
            foreach (int m in masks) Set(m, col, row, flipX, flipY);
            return this;
        }

        /// <summary>
        /// 이 연결 상태에 쓸 칸. 비어 있으면 비슷한 것으로 대신한다 —
        /// 세로로만 이어졌으면 세로(N|S), 그 밖에는 가로(E|W), 그것도 없으면 홀로(0).
        /// </summary>
        public AutoTileCell Get(int mask)
        {
            mask &= 0xF;
            if (_cells[mask].valid) return _cells[mask];

            bool vertical = (mask & (Connect.N | Connect.S)) != 0;
            int fallback = vertical ? (Connect.N | Connect.S) : (Connect.E | Connect.W);
            if (_cells[fallback].valid) return _cells[fallback];
            return _cells[0];
        }
    }

    /// <summary>
    /// 설치물 한 종류의 정의. 울타리든 길이든 <b>이 표 하나만 채우면</b> 제작·설치·오토타일·
    /// 철거가 전부 따라온다 (FenceDatabase / RoadDatabase 참고).
    /// </summary>
    public class PlaceableDef
    {
        /// <summary>저장과 아이템에 함께 쓰는 id. 아이템 id도 같은 값을 쓴다.</summary>
        public string id;
        public string displayName;
        public PlaceableKind kind;

        /// <summary>잘라 쓸 시트의 Resources 경로 (예: "Sprites/Fence/WhiteFence").</summary>
        public string sheetPath;

        /// <summary>지나갈 수 없는지. 울타리는 true, 길은 false.</summary>
        public bool blocks;

        /// <summary>문이면 true — 우클릭으로 여닫을 수 있고, 열려 있는 동안에는 지나갈 수 있다.</summary>
        public bool isGate;

        /// <summary>문이 열렸을 때 쓸 칸. isGate일 때만 본다.</summary>
        public AutoTileCell openCell;

        /// <summary>이웃 연결에 따라 고를 칸들.</summary>
        public AutoTileMap tiles = new AutoTileMap();

        /// <summary>철거했을 때 떨어지는 것 (LootTableDatabase의 id).</summary>
        public string dropTableId;

        /// <summary>그림을 칸 중심에서 얼마나 올려 그릴지. 울타리처럼 키가 있는 것에 쓴다.</summary>
        public Vector2 offset = Vector2.zero;

        /// <summary>
        /// 인벤토리 아이콘으로 쓸 칸. 비워 두면 홀로 놓았을 때의 모습을 쓴다.
        /// 문처럼 닫힌 모습이 울타리와 똑같은 것은 여기에 알아보기 쉬운 칸을 지정한다.
        /// </summary>
        public AutoTileCell iconCell;

        /// <summary>인벤토리에 보여 줄 그림.</summary>
        public Sprite GetIcon()
            => PlaceableSheet.Get(sheetPath, iconCell.valid ? iconCell : tiles.Get(0));

        /// <summary>이 종류끼리 이어진 것으로 볼지 판단한다. 울타리는 울타리끼리, 길은 길끼리.</summary>
        public bool ConnectsTo(PlaceableDef other) => other != null && other.kind == kind;

        /// <summary>지금 상태에 쓸 시트 칸 (뒤집기 정보까지 들어 있다).</summary>
        public AutoTileCell CellFor(int mask, bool open)
            => isGate && open ? openCell : tiles.Get(mask);

        public Sprite GetSprite(int mask, bool open)
            => PlaceableSheet.Get(sheetPath, CellFor(mask, open));

        /// <summary>지금 상태에서 지나갈 수 없는지 (열린 문은 지나갈 수 있다).</summary>
        public bool BlocksNow(bool open) => blocks && !(isGate && open);
    }

    /// <summary>
    /// 설치물 시트를 런타임에 16px 칸으로 잘라 쓰는 캐시 (SoilTileset과 같은 방식).
    /// 시트를 계절별로 나누거나 칸을 미리 잘라 둘 필요가 없다.
    /// </summary>
    public static class PlaceableSheet
    {
        public const int Tile = 16;

        private static readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        public static Sprite Get(string sheetPath, AutoTileCell cell)
        {
            if (string.IsNullOrEmpty(sheetPath) || !cell.valid) return null;

            string key = $"{sheetPath}#{cell.col}_{cell.row}";
            if (_sprites.TryGetValue(key, out var cached)) return cached;

            var tex = LoadTexture(sheetPath);
            if (tex == null) { _sprites[key] = null; return null; }

            // 텍스처 좌표는 왼쪽 아래가 원점이라 행 번호를 뒤집는다.
            var rect = new Rect(cell.col * Tile, tex.height - (cell.row + 1) * Tile, Tile, Tile);
            if (rect.x < 0 || rect.y < 0 || rect.xMax > tex.width || rect.yMax > tex.height)
            {
                Debug.LogWarning($"[PlaceableSheet] {sheetPath}: ({cell.col},{cell.row}) 칸이 시트 밖입니다.");
                _sprites[key] = null;
                return null;
            }

            var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), Tile, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            _sprites[key] = sprite;
            return sprite;
        }

        private static Texture2D LoadTexture(string sheetPath)
        {
            if (_textures.TryGetValue(sheetPath, out var cached)) return cached;

            var tex = Resources.Load<Texture2D>(sheetPath);
            if (tex == null)
                Debug.LogWarning($"[PlaceableSheet] 시트를 찾지 못했습니다: {sheetPath}");
            _textures[sheetPath] = tex;
            return tex;
        }
    }
}
