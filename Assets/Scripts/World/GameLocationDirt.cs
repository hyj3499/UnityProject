using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 삽마법으로 잔디땅을 파내 만든 흙땅을 다루는 GameLocation의 반쪽.
    ///
    /// 저장하는 것은 <b>삽질한 칸</b>뿐이다. 그림은 경작지와 같은 47-blob 오토타일로 계산하므로,
    /// 이웃한 개간 칸과 자연스럽게 이어지고 이웃이 바뀐 8칸만 다시 그린다.
    ///
    /// 대지마법은 <b>직접 삽질한 칸</b>에만 쓸 수 있다.
    /// </summary>
    public partial class GameLocation
    {
        public HashSet<Vector2Int> dugTiles = new HashSet<Vector2Int>();

        private readonly Dictionary<Vector2Int, SpriteRenderer> _dirtRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly List<DirtData> _outOfBoundsDirts = new List<DirtData>();

        /// <summary>삽마법으로 직접 파낸 칸인지. 대지마법은 이런 칸에만 쓸 수 있다.</summary>
        public bool IsDirt(int x, int y) => dugTiles.Contains(new Vector2Int(x, y));

        /// <summary>지금 이 칸을 삽마법으로 팔 수 있는지 (실제로 파지는 않는다 — 조준 표시가 쓴다).</summary>
        public bool CanDig(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            if (IsBlocked(x, y)) return false;
            if (!IsTillable(x, y)) return false;    // "Tillable_{맵}"을 칠해 둔 칸만 팔 수 있다
            if (dugTiles.Contains(pos) || hoeDirts.ContainsKey(pos)) return false;
            if (HasPlaced(x, y)) return false;      // 깔아 둔 길 밑은 파지 않는다
            return true;
        }

        /// <summary>
        /// 잔디땅을 파서 흙땅으로 만든다. 그 칸에 풀이 나 있었으면 함께 걷어내고 드랍 테이블을 돌려준다.
        /// </summary>
        public bool Dig(int x, int y, out string dropTableId)
        {
            dropTableId = null;
            if (!CanDig(x, y)) return false;

            CutPlant(x, y, out dropTableId);
            var pos = new Vector2Int(x, y);
            dugTiles.Add(pos);
            RefreshDirtAround(pos);
            return true;
        }

        /// <summary>지금 이 칸의 밭을 걷어내 흙땅으로 되돌릴 수 있는지. 작물이 심긴 밭은 건드리지 않는다.</summary>
        public bool CanUntill(int x, int y)
            => hoeDirts.TryGetValue(new Vector2Int(x, y), out var d) && !d.HasCrop;

        /// <summary>갈아 둔 밭을 걷어낸다 (바위마법). 밑의 흙땅은 그대로 남는다.</summary>
        public bool Untill(int x, int y)
        {
            if (!CanUntill(x, y)) return false;
            var pos = new Vector2Int(x, y);
            hoeDirts.Remove(pos);

            DestroyRenderer(_hoeRenderers, pos);
            DestroyRenderer(_wetRenderers, pos);
            DestroyRenderer(_cropRenderers, pos);
            RefreshSoilAround(pos);   // 이웃 밭의 테두리가 이 칸 쪽으로 닫힌다
            return true;
        }

        private static void DestroyRenderer(Dictionary<Vector2Int, SpriteRenderer> renderers, Vector2Int pos)
        {
            if (!renderers.TryGetValue(pos, out var sr)) return;
            renderers.Remove(pos);
            if (sr != null) Destroy(sr.gameObject);
        }

        /// <summary>지금 이 칸의 흙땅을 메워 삽질하기 전으로 되돌릴 수 있는지 (밭이 얹혀 있으면 먼저 걷어내야 한다).</summary>
        public bool CanFillDirt(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            return dugTiles.Contains(pos) && !hoeDirts.ContainsKey(pos);
        }

        /// <summary>삽질한 칸을 메운다 (바위마법). 둘레의 테두리도 함께 물러난다.</summary>
        public bool FillDirt(int x, int y)
        {
            if (!CanFillDirt(x, y)) return false;
            var pos = new Vector2Int(x, y);
            dugTiles.Remove(pos);
            RefreshDirtAround(pos);
            return true;
        }

        private void RenderDirt(Vector2Int pos)
        {
            if (!dugTiles.Contains(pos))
            {
                if (_dirtRenderers.TryGetValue(pos, out var oldRenderer))
                    oldRenderer.enabled = false;
                return;
            }

            if (!_dirtRenderers.TryGetValue(pos, out var renderer))
            {
                renderer = PlaceTile(AssetLibrary.Dirt, pos.x, pos.y, Depth.Dirt, _featureRoot);
                renderer.gameObject.name = $"dirt_{pos.x}_{pos.y}";
                _dirtRenderers[pos] = renderer;
            }

            renderer.sprite = SoilTileset.GetDug(DirtNeighborMask(pos)) ?? AssetLibrary.Dirt;
            renderer.enabled = true;
        }

        /// <summary>한 칸을 파거나 메우면 맞닿은 8칸의 오토타일 모양도 달라지므로 같이 다시 그린다.</summary>
        private void RefreshDirtAround(Vector2Int pos)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    RenderDirt(new Vector2Int(pos.x + dx, pos.y + dy));
        }

        /// <summary>개간한 땅끼리의 이웃 8칸을 47-blob 오토타일 마스크로 만든다.</summary>
        private int DirtNeighborMask(Vector2Int p)
        {
            int m = 0;
            if (IsDirt(p.x, p.y + 1)) m |= SoilTileset.N;
            if (IsDirt(p.x + 1, p.y)) m |= SoilTileset.E;
            if (IsDirt(p.x, p.y - 1)) m |= SoilTileset.S;
            if (IsDirt(p.x - 1, p.y)) m |= SoilTileset.W;
            if (IsDirt(p.x + 1, p.y + 1)) m |= SoilTileset.NE;
            if (IsDirt(p.x + 1, p.y - 1)) m |= SoilTileset.SE;
            if (IsDirt(p.x - 1, p.y - 1)) m |= SoilTileset.SW;
            if (IsDirt(p.x - 1, p.y + 1)) m |= SoilTileset.NW;
            return SoilTileset.Normalize(m);
        }

        // ---------- 저장 / 복원 ----------

        private void RestoreDirt(LocationData loc)
        {
            foreach (var d in loc.dirts)
            {
                if (!InBounds(d.x, d.y)) { _outOfBoundsDirts.Add(d); continue; }
                dugTiles.Add(new Vector2Int(d.x, d.y));
            }

            // 이웃 모양을 보려면 전부 채운 뒤에 그려야 한다.
            foreach (var pos in dugTiles)
                RenderDirt(pos);
        }

        private void SaveDirtInto(LocationData loc)
        {
            loc.dirts.Clear();
            foreach (var pos in dugTiles)
                loc.dirts.Add(new DirtData { x = pos.x, y = pos.y });
            loc.dirts.AddRange(_outOfBoundsDirts);
        }
    }
}
