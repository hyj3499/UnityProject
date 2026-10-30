using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 농장 바닥에 놓이는 잔해(나뭇가지·돌멩이·큰 통나무·큰 바위)를 다루는 GameLocation의 반쪽.
    /// <b>잡초</b>도 같은 날짜에 생성하며, 군락·작은 무리·떨어진 개별 칸을 섞어 깐다.
    ///
    ///   · 계절 첫날에는 빈 칸에 새로 깔린다. 해가 바뀌는 봄 1일에는 훨씬 많이 깔린다.
    ///   · 그 밖의 날에는 <b>이미 있는</b> 것이 밤마다 이웃 칸으로 번진다 — 치우지 않고
    ///     두면 농장 전체로 불어난다. 번지는 자리에 작물이나 설치물이 있으면 확률적으로 뭉개 버린다.
    ///   · 큰 것(variant 2·3)은 이 규칙 밖이다. 마커로 직접 놓은 것만 있고, 부수면 다시 생기지 않는다.
    ///
    /// 풀과 같은 이유로(<see cref="AdvancePlants"/>) 잠잘 때가 아니라 <b>그 맵에 들어올 때</b> 밀린
    /// 날수만큼 한꺼번에 처리한다 — 어디에 놓일 수 있는지는 지도가 있어야 알 수 있다.
    /// </summary>
    public partial class GameLocation
    {
        /// <summary>기준 칸(발판의 왼쪽 아래) → 잔해.</summary>
        public Dictionary<Vector2Int, DebrisFeature> debris = new Dictionary<Vector2Int, DebrisFeature>();

        /// <summary>발판 칸 → 기준 칸. 큰 잔해는 어느 칸을 쳐도 같은 것이 맞는다.</summary>
        private readonly Dictionary<Vector2Int, Vector2Int> _debrisAnchors = new Dictionary<Vector2Int, Vector2Int>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _debrisRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly List<DebrisData> _outOfBoundsDebris = new List<DebrisData>();

        /// <summary>한 번에 따라잡는 최대 날수 (한 계절). 오래 안 가 본 농장도 계절 첫날은 놓치지 않는다.</summary>
        private const int MaxDebrisCatchUpDays = Seasons.DaysPerSeason;
        /// <summary>한 맵에 있을 수 있는 작은 잔해(나뭇가지+돌멩이) 수의 상한. 잡초 상한은 PlantDatabase.</summary>
        private const int MaxDebrisPerLocation = 400;

        // ---- 새로 깔리는 양: "빈 칸 중 몇 %에" (나뭇가지·돌멩이·잡초 각각) ----
        /// <summary>계절 첫날 (여름·가을·겨울 1일).</summary>
        private const float SeasonWoodRatio = 0.007f, SeasonStoneRatio = 0.007f;
        /// <summary>새해 첫날 (봄 1일)과 처음 들어간 농장.</summary>
        private const float NewYearWoodRatio = 0.10f, NewYearStoneRatio = 0.10f, NewYearWeedRatio = 0.20f;
        /// <summary>빈 칸이 적어도 종류마다 최소 이만큼은 깐다.</summary>
        private const int SeasonMinPerKind = 1, NewYearMinPerKind = 3;

        /// <summary>잡초 한 군락의 칸 수. 상하좌우로 이어 붙여 넓고 빽빽하게 자라게 한다.</summary>
        private const int WeedPatchMinTiles = 16, WeedPatchMaxTiles = 32;

        /// <summary>작은 잔해 하나가 하룻밤에 옆 칸으로 번질 확률.</summary>
        private const float DebrisSpreadChance = 0.08f;
        /// <summary>잡초 한 칸이 밤에 한 번 이웃으로 번질 확률.</summary>
        private const float WeedSpreadChance = 0.04f;
        /// <summary>번지려는 칸에 작물·설치물이 있을 때 그것을 뭉개고 들어설 확률.</summary>
        private const float DebrisCrushChance = 0.3f;

        private static readonly Vector2Int[] Around8 =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1),
            new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
        };

        /// <summary>이번에 맵을 지으면서 잔해가 뭉갠 작물·설치물 수 (GameManager가 알림을 띄운다).</summary>
        public int DebrisDamage { get; private set; }

        /// <summary>잔해는 농장에만 깔린다.</summary>
        private bool HasDebrisField => id == LocationId.Farm;

        // ---------- 조회 / 부수기 ----------

        /// <summary>이 칸에 그 재질의 잔해가 있는지 (나무는 칼날마법, 돌은 바위마법의 대상).</summary>
        public bool HasDebris(int x, int y, DebrisKind kind)
            => _debrisAnchors.TryGetValue(new Vector2Int(x, y), out var anchor) && debris[anchor].kind == kind;

        /// <summary>잔해를 한 번 친다. 부서졌으면 broken=true와 드랍 테이블을 돌려준다.</summary>
        public bool HitDebris(int x, int y, DebrisKind kind, out bool broken, out string dropTableId)
        {
            broken = false;
            dropTableId = null;
            if (!HasDebris(x, y, kind)) return false;

            var anchor = _debrisAnchors[new Vector2Int(x, y)];
            var feature = debris[anchor];
            broken = feature.Hit();

            if (broken)
            {
                dropTableId = feature.Def.DropTableId;
                RemoveDebris(anchor);
            }
            else if (_debrisRenderers.TryGetValue(anchor, out var sr))
            {
                Wobble.Play(sr, 5f, 0.22f, 4f);
            }
            return true;
        }

        // ---------- 놓기 / 치우기 ----------

        /// <summary>
        /// 이 칸에 잔해가 놓일 수 있는지 — 아무것도 없는 맨땅이거나 삽으로 판 흙땅.
        /// 길·가구·울타리·풀이 있는 칸, 갈아 둔 밭, NPC가 다니는 길, 출입구 둘레에는 놓이지 않는다.
        /// </summary>
        private bool CanHoldDebris(int x, int y)
        {
            if (!InBounds(x, y) || IsBlocked(x, y)) return false;   // 나무·바위·울타리·물·절벽·다른 잔해
            if (!IsTillable(x, y)) return false;
            var pos = new Vector2Int(x, y);
            // Walkable holes in a mask do not free the debris's interaction/ownership footprint.
            if (_debrisAnchors.ContainsKey(pos)) return false;
            if (hoeDirts.ContainsKey(pos) || plants.ContainsKey(pos) || placed.ContainsKey(pos)) return false;
            if (IsPath(x, y)) return false;
            return !NearEntrance(pos);
        }

        /// <summary>출구·문과 그 둘레 한 칸. 여기를 막으면 맵에 들어서자마자 갇힌다.</summary>
        private bool NearEntrance(Vector2Int pos)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var p = new Vector2Int(pos.x + dx, pos.y + dy);
                    if (_exits.ContainsKey(p) || (doorExitTile.HasValue && doorExitTile.Value == p)) return true;
                }
            return false;
        }

        private void AddDebris(int x, int y, DebrisDef def, int hp)
        {
            var anchor = new Vector2Int(x, y);
            debris[anchor] = new DebrisFeature(x, y, def) { hp = hp };

            for (int dx = 0; dx < def.size.x; dx++)
                for (int dy = 0; dy < def.size.y; dy++)
                {
                    if (!InBounds(x + dx, y + dy)) continue;
                    _debrisAnchors[new Vector2Int(x + dx, y + dy)] = anchor;
                }

            // 넓은 그림은 발판의 가운데에, 밑동은 아랫줄 칸의 바닥에 맞춘다.
            var sprite = def.GetSprite();
            _debrisRenderers[anchor] = PlaceObject(sprite, x + (def.size.x - 1) * 0.5f, y + BottomAlignLift(sprite), y);
            RegisterObjectCollision(_debrisRenderers[anchor], new RectInt(x, y, def.size.x, def.size.y));
        }

        private void AddSmallDebris(Vector2Int pos, DebrisKind kind)
        {
            var def = DebrisDatabase.Get(kind, Random.Range(0, DebrisDatabase.SmallVariants));
            AddDebris(pos.x, pos.y, def, def.maxHp);
        }

        /// <summary>
        /// 마커로 칠해 둔 잔해를 저장 데이터에 넣는다 (ObjectMarkerPlacer가 칸마다 한 번만 부른다).
        /// 마커 칸은 그림의 <b>아래 가운데</b>다 — 발판은 거기서 좌우로 펼쳐진다.
        /// </summary>
        internal static void AddDebrisData(LocationData loc, int x, int y, DebrisDef def)
            => loc.debris.Add(new DebrisData
            {
                x = x - (def.size.x - 1) / 2, y = y,
                kind = (int)def.kind, variant = def.variant, hp = def.maxHp
            });

        // ---------- 잡초 ----------

        private static PlantDef WeedDef => PlantDatabase.Get(PlantDatabase.WeedId);

        /// <summary>지금 잡초가 더 날 수 있는지 (겨울에는 나지 않고, 맵마다 상한이 있다).</summary>
        private bool CanAddWeed()
            => Seasons.AllowsNow(WeedDef.seasons) && CountPlants(PlantDatabase.WeedId) < WeedDef.maxPerLocation;

        private void AddWeed(Vector2Int pos)
            => AddPlant(pos.x, pos.y, PlantDatabase.WeedId,
                Random.Range(0, Mathf.Max(1, PlantArt.VariantCount(WeedDef.sheet, Seasons.Current))));

        /// <summary>씨앗 칸에서 빈 이웃 칸을 차례로 채운다. 모든 새 칸은 군락과 변을 맞댄다.</summary>
        private int GrowWeedPatch(Vector2Int seed, int budget)
        {
            var frontier = new List<Vector2Int> { seed };
            var visited = new HashSet<Vector2Int> { seed };
            int added = 0;
            while (frontier.Count > 0 && added < budget)
            {
                int pick = Random.Range(0, frontier.Count);
                var pos = frontier[pick];
                frontier[pick] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                if (!CanHoldDebris(pos.x, pos.y)) continue;

                AddWeed(pos);
                added++;
                for (int i = 0; i < 4; i++)
                {
                    var next = pos + Neighbor(i);
                    if (InBounds(next.x, next.y) && visited.Add(next)) frontier.Add(next);
                }
            }
            return added;
        }

        private void SowWeedsNaturally(List<Vector2Int> free, int budget)
        {
            budget = Mathf.Min(budget, Mathf.Max(0, WeedDef.maxPerLocation - CountPlants(PlantDatabase.WeedId)));
            if (budget <= 0) return;

            // 생성 칸의 약 45%는 큰 군락, 25%는 작은 무리, 나머지는 떨어진 개별 칸.
            // 개별 칸은 마지막에 놓아 뒤에 생성되는 군락에 흡수되지 않게 한다.
            int largeBudget = Mathf.RoundToInt(budget * 0.45f);
            int smallBudget = Mathf.RoundToInt(budget * 0.25f);
            int added = SowWeedGroups(free, largeBudget, WeedPatchMinTiles, WeedPatchMaxTiles);
            added += SowWeedGroups(free, smallBudget, 2, 5);
            added += SowScatteredWeeds(free, budget - added, true);

            // 좁거나 이미 풀이 많은 맵에서는 고립될 자리가 부족할 수 있다.
            // 남은 예산만 빈 칸에 뿌리고, 원래 생성량과 맵 상한은 지킨다.
            SowScatteredWeeds(free, budget - added, false);
        }

        private int SowWeedGroups(List<Vector2Int> free, int budget, int minSize, int maxSize)
        {
            var candidates = new List<Vector2Int>(free);
            int added = 0;
            while (candidates.Count > 0 && added < budget)
            {
                int pick = Random.Range(0, candidates.Count);
                var seed = candidates[pick];
                candidates[pick] = candidates[candidates.Count - 1];
                candidates.RemoveAt(candidates.Count - 1);
                if (!CanHoldDebris(seed.x, seed.y)) continue;
                int size = Mathf.Min(budget - added, Random.Range(minSize, maxSize + 1));
                added += GrowWeedPatch(seed, size);
            }
            return added;
        }

        private int SowScatteredWeeds(List<Vector2Int> free, int budget, bool isolated)
        {
            var candidates = new List<Vector2Int>(free);
            int added = 0;
            while (candidates.Count > 0 && added < budget)
            {
                int pick = Random.Range(0, candidates.Count);
                var pos = candidates[pick];
                candidates[pick] = candidates[candidates.Count - 1];
                candidates.RemoveAt(candidates.Count - 1);
                if (!CanHoldDebris(pos.x, pos.y)) continue;
                if (isolated && HasNeighboringWeed(pos)) continue;
                AddWeed(pos);
                added++;
            }
            return added;
        }

        private bool HasNeighboringWeed(Vector2Int pos)
        {
            foreach (var offset in Around8)
                if (plants.TryGetValue(pos + offset, out var plant) && plant.plantId == PlantDatabase.WeedId)
                    return true;
            return false;
        }

        private void RemoveDebris(Vector2Int anchor)
        {
            if (!debris.TryGetValue(anchor, out var feature)) return;
            debris.Remove(anchor);
            if (_debrisRenderers.TryGetValue(anchor, out var renderer)) RemoveObjectCollision(renderer);
            DestroyRenderer(_debrisRenderers, anchor);

            var size = feature.Def.size;
            for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                {
                    var p = new Vector2Int(anchor.x + dx, anchor.y + dy);
                    _debrisAnchors.Remove(p);
                }
        }

        // ---------- 하루가 지나면 ----------

        /// <summary>
        /// 마지막으로 처리한 날부터 오늘까지를 하루씩 따라잡는다. 계절 첫날이면 새로 깔리고,
        /// 그 밖의 날이면 이미 있는 것이 번진다. 처음 보는 농장에는 새해 첫날만큼 깐다.
        /// </summary>
        private void AdvanceDebris(LocationData loc)
        {
            DebrisDamage = 0;
            if (!HasDebrisField) return;

            if (loc.debrisDay <= 0)
            {
                loc.debrisDay = _today;
                ScatterNewYear();
                return;
            }

            int from = Mathf.Max(loc.debrisDay, _today - MaxDebrisCatchUpDays);
            loc.debrisDay = Mathf.Max(loc.debrisDay, _today);
            for (int day = from + 1; day <= _today; day++)
            {
                if (Seasons.DayOfSeason(day) != 1) SpreadDebris();
                else if (Seasons.Of(day) == Season.Spring) ScatterNewYear();
                else Scatter(SeasonWoodRatio, SeasonStoneRatio, 0f, SeasonMinPerKind);
            }
        }

        private void ScatterNewYear()
            => Scatter(NewYearWoodRatio, NewYearStoneRatio, NewYearWeedRatio, NewYearMinPerKind);

        /// <summary>잡초는 여러 크기의 무리와 개별 칸을 섞고, 나뭇가지·돌멩이는 남은 빈 칸에 흩뿌린다.</summary>
        private void Scatter(float woodRatio, float stoneRatio, float weedRatio, int minPerKind)
        {
            var free = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (CanHoldDebris(x, y)) free.Add(new Vector2Int(x, y));

            int total = free.Count;
            int wood = Mathf.Max(minPerKind, Mathf.RoundToInt(total * woodRatio));
            int stone = Mathf.Max(minPerKind, Mathf.RoundToInt(total * stoneRatio));
            int weed = weedRatio > 0f && Seasons.AllowsNow(WeedDef.seasons)
                ? Mathf.Max(WeedPatchMinTiles, Mathf.Max(minPerKind, Mathf.RoundToInt(total * weedRatio))) : 0;

            // 잔해를 먼저 흩뿌리면 군락이 중간중간 끊기므로, 잡초 자리를 먼저 잡는다.
            if (weed > 0) SowWeedsNaturally(free, weed);

            while (free.Count > 0 && (wood > 0 || stone > 0))
            {
                int pick = Random.Range(0, free.Count);
                var pos = free[pick];
                free[pick] = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
                if (!CanHoldDebris(pos.x, pos.y)) continue;

                // 남은 개수에 비례해 종류를 고른다 — 한 종류가 먼저 다 깔리고 나머지가 몰리지 않는다.
                int roll = Random.Range(0, wood + stone);
                if (roll < wood) { wood--; if (debris.Count < MaxDebrisPerLocation) AddSmallDebris(pos, DebrisKind.Wood); }
                else { stone--; if (debris.Count < MaxDebrisPerLocation) AddSmallDebris(pos, DebrisKind.Stone); }
            }
        }

        /// <summary>작은 잔해는 둘레 여덟 칸, 잡초는 변을 맞댄 네 칸으로 번진다.</summary>
        private void SpreadDebris()
        {
            // 번지는 도중에 목록이 커지므로 지금 있는 것만 훑는다.
            var sources = new List<DebrisFeature>();
            foreach (var kv in debris)
                if (kv.Value.Def.spreads) sources.Add(kv.Value);

            var weeds = new List<Vector2Int>();
            foreach (var kv in plants)
                if (kv.Value.plantId == PlantDatabase.WeedId) weeds.Add(kv.Key);

            foreach (var from in sources)
            {
                if (debris.Count >= MaxDebrisPerLocation) break;
                if (TrySpreadFrom(new Vector2Int(from.x, from.y), out var to)) AddSmallDebris(to, from.kind);
            }

            foreach (var from in weeds)
            {
                if (!CanAddWeed()) break;
                if (TrySpreadWeedFrom(from, out var to)) AddWeed(to);
            }
        }

        private bool TrySpreadWeedFrom(Vector2Int from, out Vector2Int to)
        {
            // 성공해도 다른 방향을 다시 찾지 않는다. 이 실패가 군락의 가장자리를 들쭉날쭉하게 만든다.
            to = from + Neighbor(Random.Range(0, 4));
            if (Random.value > WeedSpreadChance) return false;
            return CanHoldDebris(to.x, to.y) || TryCrush(to);
        }

        /// <summary>이번 밤에 번지는지 굴려 보고, 번진다면 들어설 수 있는 이웃 칸을 돌려준다.</summary>
        private bool TrySpreadFrom(Vector2Int from, out Vector2Int to)
        {
            to = from + Around8[Random.Range(0, Around8.Length)];
            if (Random.value > DebrisSpreadChance) return false;
            return CanHoldDebris(to.x, to.y) || TryCrush(to);
        }

        /// <summary>
        /// 번지려는 칸에 심어 둔 작물이나 설치물이 있으면 확률적으로 뭉갠다. 뭉갠 것은 그대로 사라진다
        /// (드랍 없음). 뭉개고 난 자리에 잔해가 들어설 수 있으면 true.
        /// 상점·작업대·침대 구실을 하는 가구는 건드리지 않는다.
        /// </summary>
        private bool TryCrush(Vector2Int pos)
        {
            if (!IsTillable(pos.x, pos.y)) return false;

            bool hasCrop = hoeDirts.TryGetValue(pos, out var dirt) && dirt.HasCrop;
            bool hasPlaced = placed.ContainsKey(pos) && RoleAt(pos.x, pos.y) == PlaceableRole.None;
            if (!hasCrop && !hasPlaced) return false;
            if (Random.value > DebrisCrushChance) return false;

            if (hasCrop)
            {
                hoeDirts.Remove(pos);
                DestroyRenderer(_hoeRenderers, pos);
                DestroyRenderer(_wetRenderers, pos);
                DestroyRenderer(_cropRenderers, pos);
                RefreshSoilAround(pos);
            }
            else
            {
                RemovePlaced(pos.x, pos.y, out _);
            }

            DebrisDamage++;
            return CanHoldDebris(pos.x, pos.y);
        }

        // ---------- 저장 / 복원 ----------

        /// <summary>
        /// 저장해 둔 잔해를 되살린다. 풀보다 <b>먼저</b> 불러야 한다 — 잔해가 놓인 칸을 막아 두어야
        /// 그 위에 잡초가 나지 않는다.
        /// </summary>
        private void RestoreDebris(LocationData loc)
        {
            foreach (var d in loc.debris)
            {
                var def = DebrisDatabase.Get((DebrisKind)d.kind, d.variant);
                // 맵 밖이거나 모르는 종류는 건드리지 않고 보관했다가 그대로 저장한다.
                if (def == null || !InBounds(d.x, d.y)) { _outOfBoundsDebris.Add(d); continue; }
                AddDebris(d.x, d.y, def, d.hp > 0 ? d.hp : def.maxHp);
            }
        }

        private void SaveDebrisInto(LocationData loc)
        {
            loc.debris.Clear();
            foreach (var kv in debris)
            {
                var d = kv.Value;
                loc.debris.Add(new DebrisData { x = d.x, y = d.y, kind = (int)d.kind, variant = d.variant, hp = d.hp });
            }
            loc.debris.AddRange(_outOfBoundsDebris);
        }
    }
}
