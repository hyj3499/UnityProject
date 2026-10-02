using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 삽마법으로 잔디땅을 파내 만든 흙땅을 다루는 GameLocation의 반쪽.
    ///
    /// 저장하는 것은 <b>삽질한 칸</b>뿐이다. 그림은 거기서 계산한다 — 삽질한 칸에는 온전한 흙이,
    /// 그 둘레 여덟 칸에는 이웃 구덩이에서 넘어오는 테두리 조각이 놓인다 (<see cref="DirtTileset"/>).
    /// 그래서 한 칸만 파면 3x3 덩어리가 통째로 놓이고, 옆 칸을 더 파면 두 덩어리가 저절로 이어진다.
    /// 한 칸 띄우고 판 두 구덩이는 이어지지 않는다 — 사이 칸에는 양쪽의 테두리가 겹쳐 놓일 뿐이다.
    ///
    /// 대지마법은 <b>직접 삽질한 칸</b>에만 쓸 수 있다.
    /// </summary>
    public partial class GameLocation
    {
        public HashSet<Vector2Int> dugTiles = new HashSet<Vector2Int>();

        /// <summary>칸마다 겹쳐 놓은 조각들. 필요한 수만큼만 켜 둔다.</summary>
        private readonly Dictionary<Vector2Int, List<SpriteRenderer>> _dirtRenderers = new Dictionary<Vector2Int, List<SpriteRenderer>>();
        private readonly Sprite[] _dirtLayers = new Sprite[DirtTileset.MaxLayers];
        private readonly List<DirtData> _outOfBoundsDirts = new List<DirtData>();

        /// <summary>흙 구역인지 — 삽질한 칸이거나 그 둘레 한 칸 (테두리 그림이 놓이는 칸까지 포함).</summary>
        private bool IsDirtArea(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (dugTiles.Contains(new Vector2Int(x + dx, y + dy))) return true;
            return false;
        }

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
            int count = 0;
            if (dugTiles.Contains(pos))
            {
                _dirtLayers[count++] = DirtTileset.Center;
            }
            else if (InBounds(pos.x, pos.y) && IsDirtArea(pos.x, pos.y))
            {
                count = DirtTileset.GetEdges(
                    IsDirt(pos.x, pos.y + 1), IsDirt(pos.x + 1, pos.y),
                    IsDirt(pos.x, pos.y - 1), IsDirt(pos.x - 1, pos.y),
                    IsDirt(pos.x + 1, pos.y + 1), IsDirt(pos.x + 1, pos.y - 1),
                    IsDirt(pos.x - 1, pos.y - 1), IsDirt(pos.x - 1, pos.y + 1), _dirtLayers);
            }

            if (!_dirtRenderers.TryGetValue(pos, out var layers))
            {
                if (count == 0) return;
                layers = new List<SpriteRenderer>();
                _dirtRenderers[pos] = layers;
            }

            for (int i = layers.Count; i < count; i++)
            {
                // 겹친 조각끼리 순서가 같으면 누가 위에 그려질지 Unity가 그때그때 정해서, 맵을 다시 지을
                // 때마다(하루가 지날 때마다) 모양이 달라진다. 한 장씩 순서를 달리해 고정한다.
                var sr = PlaceTile(null, pos.x, pos.y, Depth.Dirt + i, _featureRoot);
                sr.gameObject.name = $"dirt_{pos.x}_{pos.y}";
                layers.Add(sr);
            }
            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].enabled = i < count;
                if (i < count) layers[i].sprite = _dirtLayers[i];
            }
        }

        /// <summary>한 칸을 파거나 메우면 둘레 여덟 칸의 테두리도 달라지므로 같이 다시 그린다.</summary>
        private void RefreshDirtAround(Vector2Int pos)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    RenderDirt(new Vector2Int(pos.x + dx, pos.y + dy));
        }

        // ---------- 저장 / 복원 ----------

        private void RestoreDirt(LocationData loc)
        {
            foreach (var d in loc.dirts)
            {
                if (!InBounds(d.x, d.y)) { _outOfBoundsDirts.Add(d); continue; }
                dugTiles.Add(new Vector2Int(d.x, d.y));
            }

            // 이웃 모양을 보려면 전부 채운 뒤에 그려야 한다. 겹치는 칸은 같은 그림으로 다시 놓일 뿐이다.
            foreach (var pos in dugTiles)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        RenderDirt(new Vector2Int(pos.x + dx, pos.y + dy));
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
