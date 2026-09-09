using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// GameLocation의 설치물(울타리·길) 담당 부분. 나무·바위와 나란한 개념이지만 <b>플레이어가
    /// 놓고 걷어내는</b> 것이라 규칙이 제법 달라서 파일을 나눠 두었다.
    ///
    /// 이웃과 이어진 모양(오토타일)도, 문이 짝을 이뤘는지도 <b>저장하지 않는다</b> — 저장하는 것은
    /// "어디에 무엇이 있는지"뿐이고 나머지는 그릴 때마다 이웃을 보고 다시 정한다.
    /// 그래서 옆 칸을 놓거나 걷어내면 저절로 맞춰진다.
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

        /// <summary>이 칸에 놓인 설치물이 맡은 역할 (상점·작업대·침대). 없으면 None.</summary>
        public PlaceableRole RoleAt(int x, int y)
        {
            var def = GetPlaced(x, y)?.Def;
            return def != null ? def.role : PlaceableRole.None;
        }

        /// <summary>
        /// 우클릭했을 때 이 칸이 그 역할을 하는지. 맵에 마커로 박아 둔 것과 플레이어가 놓은 가구를
        /// <b>함께</b> 본다 — 그래서 침대를 걷어 다른 자리에 다시 놓아도 그 자리에서 잘 수 있다.
        /// </summary>
        public bool IsShopAt(int x, int y)
            => shopTile == new Vector2Int(x, y) || RoleAt(x, y) == PlaceableRole.Shop;

        public bool IsWorkbenchAt(int x, int y)
            => workbenchTile == new Vector2Int(x, y) || RoleAt(x, y) == PlaceableRole.Workbench;

        public bool IsBedAt(int x, int y)
            => bedTile == new Vector2Int(x, y) || RoleAt(x, y) == PlaceableRole.Bed;

        /// <summary>지금 이 맵에서 잘 수 있는 자리 (마커 침대가 없으면 놓아 둔 침대를 찾는다).</summary>
        public Vector2Int? FindBedTile()
        {
            if (bedTile.HasValue) return bedTile;
            foreach (var kv in placed)
                if (kv.Value.Def != null && kv.Value.Def.role == PlaceableRole.Bed) return kv.Key;
            return null;
        }

        /// <summary>이 칸에 문이 있는지 (열 수 있는지와는 별개 — 짝이 없으면 못 연다).</summary>
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

        /// <summary>설치물을 놓는다. 문도 <b>한 칸씩</b> 놓인다. 성공하면 true.</summary>
        public bool PlaceAt(int x, int y, string defId)
        {
            var def = PlaceableDatabase.Get(defId);
            if (!CanPlace(x, y, def)) return false;

            var pos = new Vector2Int(x, y);
            placed[pos] = new PlacedFeature(x, y, defId);
            if (def.BlocksNow(false)) SetBlocked(x, y, true);

            RefreshPlacedAround(pos);
            RefreshGateRun(pos, defId);   // 옆에 문이 있으면 이제 짝이 될 수 있다
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
            string defId = feature.defId;

            placed.Remove(pos);
            if (_placedRenderers.TryGetValue(pos, out var sr))
            {
                Destroy(sr.gameObject);
                _placedRenderers.Remove(pos);
            }
            // 오브젝트 발판 위에 놓여 있었다면 그 막힘은 그대로 둔다.
            ClearFeatureBlock(pos);

            RefreshPlacedAround(pos);
            RefreshGateRun(pos, defId);   // 짝을 잃은 문은 다시 홀로 선 모습이 된다
            return true;
        }

        // ---------- 문 ----------
        /// <summary>
        /// 문을 여닫는다. <b>나란히 놓인 두 짝이 함께</b> 움직이고, 열려 있는 동안에는 두 칸 다
        /// 지나갈 수 있다. 짝이 없는 문(혼자 서 있는 문)은 열리지 않으므로 false를 돌려준다.
        /// </summary>
        public bool ToggleGate(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            if (!placed.TryGetValue(pos, out var feature) || !feature.IsGate) return false;

            var def = feature.Def;
            bool paired = TryGetGatePartner(pos, feature, out var partner);
            if (def != null && def.IsWideGate && !paired) return false;   // 짝이 있어야 열 수 있다

            bool open = !feature.open;
            SetGateOpen(pos, feature, open);
            if (paired) SetGateOpen(partner, placed[partner], open);
            return true;
        }

        private void SetGateOpen(Vector2Int pos, PlacedFeature feature, bool open)
        {
            feature.open = open;
            ApplyBlocked(pos, feature);
            RenderPlaced(pos);
        }

        /// <summary>
        /// 나란히 붙은 같은 문끼리 <b>왼쪽부터 둘씩</b> 짝을 짓는다. 왼쪽 짝이면 0, 오른쪽 짝이면 1,
        /// 짝이 없으면 -1 (혼자 서 있는 문은 울타리를 홀로 놓은 모습이 된다).
        /// 한 칸짜리 문 그림만 있는 시트는 짝이 필요 없으므로 언제나 -1이다.
        /// </summary>
        private int GatePart(Vector2Int pos, PlacedFeature feature)
        {
            var def = feature.Def;
            if (def == null || !def.isGate || !def.IsWideGate) return -1;

            int start = pos.x;
            while (IsSameGate(new Vector2Int(start - 1, pos.y), feature.defId)) start--;

            if ((pos.x - start) % 2 == 1) return 1;   // 짝의 오른쪽
            return IsSameGate(new Vector2Int(pos.x + 1, pos.y), feature.defId) ? 0 : -1;
        }

        private bool IsSameGate(Vector2Int pos, string defId)
            => placed.TryGetValue(pos, out var other) && other.defId == defId;

        /// <summary>짝이 되는 칸을 찾는다.</summary>
        private bool TryGetGatePartner(Vector2Int pos, PlacedFeature feature, out Vector2Int partner)
        {
            int part = GatePart(pos, feature);
            partner = pos + (part == 0 ? Vector2Int.right : Vector2Int.left);
            return part >= 0 && IsSameGate(partner, feature.defId);
        }

        /// <summary>
        /// 가로로 이어진 문 한 줄을 통째로 다시 정리한다. 한 칸을 놓거나 걷어내면 줄 전체의
        /// 짝이 밀리기 때문에, 옆 네 칸만 다시 그려서는 모자란다.
        /// </summary>
        private void RefreshGateRun(Vector2Int pos, string defId)
        {
            var def = PlaceableDatabase.Get(defId);
            if (def == null || !def.isGate) return;

            int left = pos.x, right = pos.x;
            while (IsSameGate(new Vector2Int(left - 1, pos.y), defId)) left--;
            while (IsSameGate(new Vector2Int(right + 1, pos.y), defId)) right++;

            for (int x = left; x <= right; x++)
            {
                var p = new Vector2Int(x, pos.y);
                if (!placed.TryGetValue(p, out var f)) continue;

                // 짝을 잃은 문은 다시 닫힌 것으로 본다 — 혼자 열려 있는 문은 없다.
                if (GatePart(p, f) < 0 && f.open) f.open = false;
                ApplyBlocked(p, f);
                RenderPlaced(p);
            }
        }

        /// <summary>지금 상태에 맞게 막힘을 다시 정한다 (오브젝트 발판 위라면 그 막힘은 남긴다).</summary>
        private void ApplyBlocked(Vector2Int pos, PlacedFeature feature)
        {
            if (feature.BlocksNow) SetBlocked(pos.x, pos.y, true);
            else ClearFeatureBlock(pos);
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
            }

            // 이웃과 짝을 다 채운 뒤에 정리해야 이어지는 모양과 여닫힘이 맞는다.
            foreach (var key in new List<Vector2Int>(placed.Keys))
            {
                var f = placed[key];
                if (f.IsGate && GatePart(key, f) < 0) f.open = false;
                ApplyBlocked(key, f);
                RenderPlaced(key);
            }
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

            var sprite = def.GetSprite(PlacedMask(pos, def), feature.open,
                                       GatePart(pos, feature), VerticalSide(pos, def));

            // 가구는 그림 크기가 제각각이라 올려 그릴 양을 그림에서 바로 구한다 (울타리·길은 정해진 값).
            float lift = def.offset.y + (def.IsFurniture ? BottomAlignLift(sprite) : 0f);

            if (!_placedRenderers.TryGetValue(pos, out var sr))
            {
                // 울타리·가구는 서 있는 물건이라 y정렬(뒤로 돌아가면 가려진다), 길은 바닥에 깔린다.
                sr = PlaceObject(sprite, pos.x + def.offset.x, pos.y + lift, pos.y);
                if (def.kind == PlaceableKind.Road) sr.sortingOrder = Depth.Road;
                // 러그처럼 밟고 지나가는 가구는 바닥에 깔린 것이라 아무도 가리지 않는다.
                else if (def.IsFurniture && !def.blocks) sr.sortingOrder = Depth.FloorDecor;
                sr.gameObject.name = $"placed_{def.id}_{pos.x}_{pos.y}";
                _placedRenderers[pos] = sr;
            }

            sr.sprite = sprite;
            // 계절이 바뀌면 그림도 바뀔 수 있으므로 자리도 그때마다 다시 맞춘다.
            sr.transform.position = new Vector3(pos.x + def.offset.x, pos.y + lift, 0f);
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

        /// <summary>
        /// 세로 담이 어느 쪽 판을 써야 하는지. 위나 아래 이웃이 <b>동쪽으로만</b> 꺾이면(┌ └)
        /// 왼쪽 담(-1), <b>서쪽으로만</b> 꺾이면(┐ ┘) 오른쪽 담(+1)이다.
        /// 판단할 수 없으면 0 — 둘 중 아무거나 쓴다 (담 두 칸만 이어 놓았을 때가 그렇다).
        /// </summary>
        private int VerticalSide(Vector2Int pos, PlaceableDef def)
        {
            for (int i = 0; i < 2; i++)
            {
                var n = pos + (i == 0 ? Vector2Int.up : Vector2Int.down);
                if (!placed.TryGetValue(n, out var other) || !def.ConnectsTo(other.Def)) continue;

                int m = PlacedMask(n, other.Def);
                bool east = (m & Connect.E) != 0, west = (m & Connect.W) != 0;
                if (east && !west) return -1;
                if (west && !east) return 1;
            }
            return 0;
        }
    }
}
