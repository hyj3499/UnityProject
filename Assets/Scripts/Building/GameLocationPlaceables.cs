using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// GameLocation의 설치물(울타리·길) 담당 부분. 나무·바위와 나란한 개념이지만 <b>플레이어가
    /// 놓고 걷어내는</b> 것이라 규칙이 제법 달라서 파일을 나눠 두었다.
    ///
    /// 이웃과 이어진 모양(오토타일)은 저장하지 않는다 — 저장하는 것은 "어디에 무엇이 있는지"뿐이고,
    /// 모양은 그릴 때마다 이웃을 보고 다시 고른다. 그래서 옆 칸을 놓거나 걷어내면 저절로 맞춰진다.
    /// </summary>
    public partial class GameLocation
    {
        /// <summary>플레이어가 설치한 울타리·길. 칸 하나에 하나씩.</summary>
        public readonly Dictionary<Vector2Int, PlacedFeature> placed = new Dictionary<Vector2Int, PlacedFeature>();

        private readonly Dictionary<Vector2Int, SpriteRenderer> _placedRenderers =
            new Dictionary<Vector2Int, SpriteRenderer>();

        // 맵 밖으로 밀려난 설치물. 나무·바위와 같은 이유로 지우지 않고 들고 있다가 그대로 저장한다.
        private readonly List<PlacedData> _outOfBoundsPlaced = new List<PlacedData>();

        /// <summary>작업대 칸 ("Obj_Workbench" 마커). 우클릭하면 제작 화면이 열린다.</summary>
        public Vector2Int? workbenchTile;

        // ---------- 조회 ----------
        public PlacedFeature GetPlaced(int x, int y)
            => placed.TryGetValue(new Vector2Int(x, y), out var p) ? p : null;

        /// <summary>이 칸에 걷어낼 수 있는 설치물이 있는지 (바위마법의 대상).</summary>
        public bool HasPlaced(int x, int y) => placed.ContainsKey(new Vector2Int(x, y));

        /// <summary>이 칸에 여닫을 수 있는 문이 있는지.</summary>
        public bool HasGate(int x, int y)
        {
            var p = GetPlaced(x, y);
            return p != null && p.IsGate;
        }

        // ---------- 설치 ----------
        /// <summary>
        /// 지금 이 칸에 이 설치물을 놓을 수 있는지. 이미 무언가 있는 자리, 물·절벽, 막힌 자리,
        /// 갈아 둔 밭, 다른 맵으로 가는 칸에는 놓지 못한다.
        /// </summary>
        public bool CanPlace(int x, int y, PlaceableDef def)
        {
            if (def == null || !InBounds(x, y)) return false;

            var pos = new Vector2Int(x, y);
            if (placed.ContainsKey(pos)) return false;
            if (hoeDirts.ContainsKey(pos) || trees.ContainsKey(pos) || rocks.ContainsKey(pos)) return false;
            if (IsWater(x, y) || IsCliff(x, y)) return false;
            if (_exits.ContainsKey(pos)) return false;             // 출구를 막아 버리면 갇힌다
            if (doorExitTile.HasValue && doorExitTile.Value == pos) return false;
            if (IsBlocked(x, y)) return false;                     // 집·오브젝트 발판, 칠해 둔 충돌
            return true;
        }

        /// <summary>설치물을 놓는다. 성공하면 true.</summary>
        public bool PlaceAt(int x, int y, string defId)
        {
            var def = PlaceableDatabase.Get(defId);
            if (!CanPlace(x, y, def)) return false;

            var pos = new Vector2Int(x, y);
            placed[pos] = new PlacedFeature(x, y, defId);
            if (def.BlocksNow(false)) SetBlocked(x, y, true);
            RefreshPlacedAround(pos);
            return true;
        }

        /// <summary>
        /// 설치물을 걷어낸다. 걷어냈으면 true이고 dropTableId에 떨어뜨릴 것이 담긴다
        /// (실제 스폰은 호출자가 ItemDropSpawner로 한다 — 나무·바위와 같은 흐름).
        /// </summary>
        public bool RemovePlaced(int x, int y, out string dropTableId)
        {
            dropTableId = null;
            var pos = new Vector2Int(x, y);
            if (!placed.TryGetValue(pos, out var feature)) return false;

            dropTableId = feature.Def != null ? feature.Def.dropTableId : null;
            placed.Remove(pos);

            if (_placedRenderers.TryGetValue(pos, out var sr))
            {
                Destroy(sr.gameObject);
                _placedRenderers.Remove(pos);
            }
            // 오브젝트 발판 위에 놓여 있었다면 그 막힘은 그대로 둔다.
            ClearFeatureBlock(pos);
            RefreshPlacedAround(pos);
            return true;
        }

        /// <summary>문을 여닫는다. 문이 없으면 false.</summary>
        public bool ToggleGate(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            if (!placed.TryGetValue(pos, out var feature) || !feature.IsGate) return false;

            feature.open = !feature.open;
            if (feature.BlocksNow) SetBlocked(x, y, true);
            else ClearFeatureBlock(pos);

            RenderPlaced(pos);
            return true;
        }

        // ---------- 저장 / 복원 ----------
        internal void RestorePlaced(LocationData loc)
        {
            foreach (var p in loc.placed)
            {
                if (!InBounds(p.x, p.y)) { _outOfBoundsPlaced.Add(p); continue; }
                var def = PlaceableDatabase.Get(p.defId);
                if (def == null) { _outOfBoundsPlaced.Add(p); continue; }   // 모르는 종류는 건드리지 않고 보관

                var pos = new Vector2Int(p.x, p.y);
                placed[pos] = new PlacedFeature(p.x, p.y, p.defId, p.open);
                if (def.BlocksNow(p.open)) SetBlocked(p.x, p.y, true);
            }

            // 이웃을 다 채운 뒤에 그려야 이어지는 모양이 맞다 (경작지 오토타일과 같은 이유).
            foreach (var key in new List<Vector2Int>(placed.Keys)) RenderPlaced(key);
        }

        internal void SavePlacedInto(LocationData loc)
        {
            loc.placed.Clear();
            foreach (var kv in placed)
            {
                var f = kv.Value;
                loc.placed.Add(new PlacedData { x = f.x, y = f.y, defId = f.defId, open = f.open });
            }
            loc.placed.AddRange(_outOfBoundsPlaced);
        }

        // ---------- 그리기 ----------
        /// <summary>이 칸과 붙어 있는 네 칸을 다시 그린다 (이어진 모양이 바뀌므로).</summary>
        private void RefreshPlacedAround(Vector2Int pos)
        {
            RenderPlaced(pos);
            RenderPlaced(pos + Vector2Int.up);
            RenderPlaced(pos + Vector2Int.down);
            RenderPlaced(pos + Vector2Int.left);
            RenderPlaced(pos + Vector2Int.right);
        }

        public void RenderPlaced(Vector2Int pos)
        {
            bool alive = placed.TryGetValue(pos, out var feature);
            var def = alive ? feature.Def : null;

            if (!alive || def == null)
            {
                if (_placedRenderers.TryGetValue(pos, out var old))
                {
                    Destroy(old.gameObject);
                    _placedRenderers.Remove(pos);
                }
                return;
            }

            var cell = def.CellFor(PlacedMask(pos, def), feature.open);
            var sprite = PlaceableSheet.Get(def.sheetPath, cell);

            if (!_placedRenderers.TryGetValue(pos, out var sr))
            {
                // 울타리는 서 있는 물건이라 y정렬(뒤로 돌아가면 가려진다), 길은 바닥에 깔린다.
                sr = PlaceObject(sprite, pos.x + def.offset.x, pos.y + def.offset.y, pos.y);
                if (def.kind == PlaceableKind.Road) sr.sortingOrder = Depth.Road;
                sr.gameObject.name = $"placed_{def.id}_{pos.x}_{pos.y}";
                _placedRenderers[pos] = sr;
            }

            sr.sprite = sprite;
            sr.flipX = cell.flipX;
            sr.flipY = cell.flipY;
        }

        /// <summary>붙어 있는 같은 종류의 설치물을 비트로 모은다 (울타리는 울타리끼리만 이어진다).</summary>
        private int PlacedMask(Vector2Int pos, PlaceableDef def)
        {
            int mask = 0;
            if (Connects(pos + Vector2Int.up, def)) mask |= Connect.N;
            if (Connects(pos + Vector2Int.right, def)) mask |= Connect.E;
            if (Connects(pos + Vector2Int.down, def)) mask |= Connect.S;
            if (Connects(pos + Vector2Int.left, def)) mask |= Connect.W;
            return mask;
        }

        private bool Connects(Vector2Int pos, PlaceableDef def)
            => placed.TryGetValue(pos, out var other) && def.ConnectsTo(other.Def);
    }
}
