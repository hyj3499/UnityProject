using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 땅을 덮는 풀(잔디·잡초)을 다루는 GameLocation의 반쪽. 나무·바위와 규칙이 꽤 달라
    /// (칸을 막지 않고, 칸마다 여러 포기가 놓이고, 스스로 번지고 난다) 파일을 나눠 두었다.
    ///
    /// 자라고·번지고·새로 나는 것은 <b>그 맵에 들어올 때</b> 밀린 날수만큼 한꺼번에 처리한다
    /// (<see cref="AdvancePlants"/>). 잠잘 때 데이터만 보고 처리할 수 없는 이유는, 어디에 날 수
    /// 있는지가 경작 마스크·충돌·이미 놓인 것에 달려 있어서 <b>지도가 있어야</b> 알 수 있기 때문이다.
    /// </summary>
    public partial class GameLocation
    {
        public Dictionary<Vector2Int, PlantFeature> plants = new Dictionary<Vector2Int, PlantFeature>();

        /// <summary>칸마다 포기 여러 개를 매달아 두는 부모. 흔들 때도 이 하나만 흔들면 된다.</summary>
        private readonly Dictionary<Vector2Int, Transform> _plantRoots = new Dictionary<Vector2Int, Transform>();

        /// <summary>한 번에 따라잡을 수 있는 최대 날수. 오래 안 가 본 맵이 잡초로 뒤덮이지 않게.</summary>
        private const int MaxPlantCatchUpDays = 7;

        /// <summary>밀린 날수를 한꺼번에 처리하는 동안에는 그리지 않는다 — 끝나고 한 번만 그린다.</summary>
        private bool _suspendPlantRender;

        // ---------- 그리기 ----------

        /// <summary>
        /// 한 칸의 풀을 그린다. 포기 수·어긋난 자리·좌우 뒤집기는 칸 좌표에서 나오므로
        /// (PlantFeature.Tufts) 다시 그려도 모양이 그대로다.
        /// </summary>
        public void RenderPlant(Vector2Int pos)
        {
            if (_suspendPlantRender) return;

            if (_plantRoots.TryGetValue(pos, out var old))
            {
                if (old != null) Destroy(old.gameObject);
                _plantRoots.Remove(pos);
            }

            if (!plants.TryGetValue(pos, out var plant)) return;
            var sprite = plant.GetSprite();
            if (sprite == null) return;   // 겨울처럼 그림이 없는 계절

            var root = new GameObject("plant").transform;
            root.SetParent(_featureRoot, false);
            root.position = new Vector3(pos.x, pos.y - 0.5f, 0f);

            foreach (var tuft in plant.Tufts())
            {
                var go = new GameObject("tuft");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(tuft.x, tuft.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.flipX = tuft.flip;
                // 같은 칸 안에서도 아래쪽 포기가 앞에 오도록 포기의 실제 높이로 정렬한다.
                sr.sortingOrder = Depth.YSort(pos.y + tuft.y, Depth.PlantBias);
            }

            _plantRoots[pos] = root;
        }

        private void RenderAllPlants()
        {
            foreach (var key in plants.Keys) RenderPlant(key);
        }

        /// <summary>이 칸의 풀을 스치고 지나간다 — 한 무더기가 통째로 잠깐 흔들린다.</summary>
        public void BrushPlantAt(Vector2Int pos)
        {
            if (_plantRoots.TryGetValue(pos, out var root) && root != null)
                Wobble.Play(root, 7f, 0.32f, 2.5f);
        }

        // ---------- 심기 / 베기 ----------

        /// <summary>이 칸에 풀이 날 수 있는지. 막힌 칸·물·절벽·밭·이미 무언가 있는 칸에는 안 난다.</summary>
        public bool CanSowPlant(int x, int y)
        {
            if (!InBounds(x, y) || IsBlocked(x, y)) return false;
            if (IsWater(x, y) || IsCliff(x, y)) return false;
            var pos = new Vector2Int(x, y);
            // 나무·바위·설치물은 IsBlocked에 이미 걸리지만, 갈아 둔 밭은 걸어 다닐 수 있어 따로 본다.
            return !hoeDirts.ContainsKey(pos) && !plants.ContainsKey(pos);
        }

        /// <summary>씨앗으로 잔디를 심는다 (GameManager가 우클릭에서 부른다).</summary>
        public bool SowPlant(int x, int y, string plantId)
        {
            var def = PlantDatabase.Get(plantId);
            if (def == null || !CanSowPlant(x, y)) return false;
            if (!Seasons.AllowsNow(def.seasons)) return false;

            AddPlant(x, y, plantId, 0);
            return true;
        }

        /// <summary>지금 이 칸에 벨 수 있는 풀이 있는지 (조준 표시와 바람마법이 쓴다).</summary>
        public bool HasCuttablePlant(int x, int y) => plants.ContainsKey(new Vector2Int(x, y));

        /// <summary>
        /// 풀을 벤다. 한 번에 없어지고, 덜 자란 것이 아니면 드랍 테이블을 돌려준다
        /// (잔디는 목초, 잡초는 섬유 — 실제 스폰은 호출자가 한다).
        /// </summary>
        public bool CutPlant(int x, int y, out string dropTableId)
        {
            dropTableId = null;
            var pos = new Vector2Int(x, y);
            if (!plants.TryGetValue(pos, out var plant)) return false;

            dropTableId = plant.DropTableId;
            plants.Remove(pos);
            RenderPlant(pos);
            return true;
        }

        private void AddPlant(int x, int y, string plantId, int stage)
        {
            var pos = new Vector2Int(x, y);
            plants[pos] = new PlantFeature(x, y, plantId, stage);
            RenderPlant(pos);
        }

        // ---------- 하루가 지나면 ----------

        /// <summary>
        /// 이 맵에 들어올 때, 마지막으로 처리한 날부터 오늘까지를 하루씩 따라잡는다.
        /// 하루마다: 자라고 → 다 자란 잔디가 옆으로 번지고 → 빈 경작 구역에 잡초가 난다.
        /// 겨울에는 아무것도 하지 않는다 (그 전에 이미 전부 걷혔다 — WitherOutOfSeasonPlants).
        /// </summary>
        private void AdvancePlants(LocationData loc)
        {
            int days = Mathf.Clamp(_today - loc.plantDay, 0, MaxPlantCatchUpDays);
            loc.plantDay = _today;
            if (days <= 0) return;

            _suspendPlantRender = true;
            try
            {
                for (int d = 0; d < days; d++)
                {
                    foreach (var kv in plants) kv.Value.Grow();
                    foreach (var def in PlantDatabase.All)
                    {
                        if (!Seasons.AllowsNow(def.seasons)) continue;
                        SpreadPlant(def);
                        SowPlantsRandomly(def);
                    }
                }
            }
            finally { _suspendPlantRender = false; }
        }

        /// <summary>다 자란 포기가 옆 칸으로 번진다 (잔디).</summary>
        private void SpreadPlant(PlantDef def)
        {
            if (def.spreadChance <= 0f) return;

            int living = CountPlants(def.plantId);
            if (living >= def.maxPerLocation) return;

            // 번지는 도중에 plants가 커지므로 지금 있는 것만 훑는다.
            var sources = new List<Vector2Int>();
            foreach (var kv in plants)
                if (kv.Value.plantId == def.plantId && kv.Value.IsMature) sources.Add(kv.Key);

            foreach (var from in sources)
            {
                if (living >= def.maxPerLocation) return;
                if (Random.value > def.spreadChance) continue;

                var to = from + Neighbor(Random.Range(0, 4));
                if (!CanSowPlant(to.x, to.y)) continue;

                AddPlant(to.x, to.y, def.plantId, 0);
                living++;
            }
        }

        /// <summary>경작할 수 있는 구역의 빈 칸에 저절로 난다 (잡초).</summary>
        private void SowPlantsRandomly(PlantDef def)
        {
            if (def.sowAttemptsPerDay <= 0) return;
            // "Tillable_{맵}"을 칠해 둔 맵에만 난다. 마스크가 없으면 IsTillable이 맵 전체를 밭으로 보기
            // 때문에, 그냥 두면 들판을 정해 주지 않은 맵(산·마을·바다)까지 잡초로 뒤덮인다.
            if (!HasTillableMask) return;

            int living = CountPlants(def.plantId);
            for (int i = 0; i < def.sowAttemptsPerDay && living < def.maxPerLocation; i++)
            {
                int x = Random.Range(0, width), y = Random.Range(0, height);
                // 잡초는 "경작할 수 있는 땅"에만 난다 — Tillable 마스크를 칠해 둔 구역이 곧 들판이다.
                if (!IsTillable(x, y) || !CanSowPlant(x, y)) continue;

                AddPlant(x, y, def.plantId, Random.Range(0, PlantArt.Variants));
                living++;
            }
        }

        private int CountPlants(string plantId)
        {
            int n = 0;
            foreach (var kv in plants) if (kv.Value.plantId == plantId) n++;
            return n;
        }

        private static Vector2Int Neighbor(int i)
        {
            switch (i)
            {
                case 0: return Vector2Int.up;
                case 1: return Vector2Int.down;
                case 2: return Vector2Int.left;
                default: return Vector2Int.right;
            }
        }

        // ---------- 저장 / 복원 ----------

        /// <summary>
        /// "Grass_{맵}" 마스크에 칠해 둔 칸에 잔디를 깔아 둔다 — <b>맵마다 딱 한 번만</b>.
        /// 들어올 때마다 다시 깔면 베어 낸 잔디가 계속 되살아나 벨 이유가 없어진다.
        ///
        /// 마스크라서 <b>무슨 타일을 칠했는지는 보지 않는다</b> (Blocked_/Tillable_과 같은 규칙).
        /// 아무 타일이나 골라 잔디밭으로 쓸 영역만 쓱 칠하면, 15x12짜리 포기를 칸 안에 흩어 심는 것은
        /// 코드가 한다 — 그래서 그림 크기가 칸 격자와 어긋나도 손으로 맞출 것이 없다.
        /// </summary>
        private void SeedPaintedGrass(LocationData loc)
        {
            if (loc.grassSeeded) return;
            loc.grassSeeded = true;
            if (_grassMask == null) return;

            int seeded = 0;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    if (!_grassMask.HasTile(_grassMask.WorldToCell(new Vector3(x, y, 0f)))) continue;
                    if (!CanSowPlant(x, y)) continue;
                    // 칠해 둔 잔디밭은 이미 자리를 잡은 것으로 본다 — 다 자란 상태로 시작한다.
                    AddPlant(x, y, PlantDatabase.GrassId, PlantDatabase.Get(PlantDatabase.GrassId).maxStage);
                    seeded++;
                }

            if (seeded > 0)
                Debug.Log($"[GameLocation] {id}: 칠해 둔 자리에 잔디 {seeded}칸을 깔았습니다.");
        }

        /// <summary>
        /// 저장해 둔 풀을 되살리고, 칠해 둔 잔디를 깔고, 밀린 날수만큼 자라게 한다.
        /// <b>GameLocation.Build가 맨 마지막에</b> 부른다 — 경작 마스크·물·절벽·충돌이 모두 정해진 뒤라야
        /// 어디에 풀이 날 수 있는지 제대로 알 수 있기 때문이다.
        /// </summary>
        private void RestorePlants(LocationData loc)
        {
            foreach (var p in loc.plants)
            {
                if (!InBounds(p.x, p.y)) continue;   // 맵이 줄었으면 그냥 버린다 (풀은 아까울 것이 없다)
                if (PlantDatabase.Get(p.plantId) == null) continue;
                plants[new Vector2Int(p.x, p.y)] =
                    new PlantFeature(p.x, p.y, p.plantId, p.stage) { dayCounter = p.dayCounter };
            }

            _suspendPlantRender = true;
            try { SeedPaintedGrass(loc); }
            finally { _suspendPlantRender = false; }

            AdvancePlants(loc);   // 밀린 날수를 따라잡은 뒤에 한 번만 그린다
            RenderAllPlants();
        }

        private void SavePlantsInto(LocationData loc)
        {
            loc.plants.Clear();
            foreach (var kv in plants)
            {
                var p = kv.Value;
                loc.plants.Add(new PlantData { x = p.x, y = p.y, plantId = p.plantId, stage = p.stage, dayCounter = p.dayCounter });
            }
        }
    }
}
