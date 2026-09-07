using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Runtime representation of a map. Owns the tile grid, collision, terrain
    /// features (HoeDirt / Trees) and their rendering GameObjects. Rebuilt from
    /// LocationData whenever the player enters (design doc §15/§17).
    /// </summary>
    public class GameLocation : MonoBehaviour
    {
        public LocationId id;
        public int width = 20;
        public int height = 15;

        public Dictionary<Vector2Int, HoeDirt> hoeDirts = new Dictionary<Vector2Int, HoeDirt>();
        public Dictionary<Vector2Int, TreeFeature> trees = new Dictionary<Vector2Int, TreeFeature>();
        public Dictionary<Vector2Int, RockFeature> rocks = new Dictionary<Vector2Int, RockFeature>();

        /// <summary>드랍된 월드 아이템(WorldItem)을 매달아 둘 부모. 위치가 다시 로드되면 함께 정리된다.</summary>
        public Transform FeatureRoot => _featureRoot;

        private bool[,] _blocked;
        private Transform _tileRoot, _featureRoot;
        private readonly Dictionary<Vector2Int, SpriteRenderer> _hoeRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _wetRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _cropRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _treeRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _rockRenderers = new Dictionary<Vector2Int, SpriteRenderer>();

        // Special interaction points
        public Vector2Int? shippingBoxTile; // Farm1 배송함
        public Vector2Int? shopTile;        // Farm1 상점 수레
        private SpriteRenderer _shippingBoxSr;

        public Vector2Int? bedTile;       // FarmHouse
        public Vector2Int? doorExitTile;  // FarmHouse door -> Farm1 ; Farm1 house -> FarmHouse
        public RectInt? rightExit;        // Farm1 -> Farm2 region
        public RectInt? leftExit;         // Farm2 -> Farm1 region

        public void Build(GameData data)
        {
            AssetLibrary.EnsureLoaded();
            _blocked = new bool[width, height];

            _tileRoot = new GameObject("Tiles").transform;
            _tileRoot.SetParent(transform, false);
            _featureRoot = new GameObject("Features").transform;
            _featureRoot.SetParent(transform, false);

            switch (id)
            {
                case LocationId.Farm1: Farm1Builder.Build(this, data); break;
                case LocationId.Farm2: Farm2Builder.Build(this, data); break;
                case LocationId.FarmHouse: FarmHouseBuilder.Build(this, data); break;
            }
        }

        // ---------- tile helpers (Locations/*Builder.cs 에서 사용) ----------
        internal SpriteRenderer PlaceTile(Sprite sprite, int x, int y, int order, Transform parent = null)
        {
            var go = new GameObject($"tile_{x}_{y}");
            go.transform.SetParent(parent ?? _tileRoot, false);
            go.transform.position = new Vector3(x, y, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        internal SpriteRenderer PlaceObject(Sprite sprite, float x, float y, int baseOrder)
        {
            var go = new GameObject("obj");
            go.transform.SetParent(_featureRoot, false);
            go.transform.position = new Vector3(x, y, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // y-sort so lower objects draw in front
            sr.sortingOrder = baseOrder - Mathf.RoundToInt(y * 10);
            return sr;
        }

        /// <summary>
        /// Assets/Resources/Prefabs/{locId}Ground.prefab 가 있으면 그걸 인스턴스화해서 바닥으로 쓴다
        /// (Unity 에디터의 Tile Palette로 손으로 칠한 Tilemap). 없으면 false를 반환해서
        /// 호출부가 기존 단색 잔디 루프로 대체(fallback)하게 한다.
        /// </summary>
        internal bool TryPlaceGroundTilemap(LocationId locId)
        {
            var prefab = Resources.Load<GameObject>($"Prefabs/{locId}Ground");
            if (prefab == null) return false;
            var go = Instantiate(prefab, _tileRoot);
            go.transform.localPosition = Vector3.zero;
            return true;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        public bool IsBlocked(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            return _blocked[x, y];
        }

        internal void SetBlocked(int x, int y, bool v)
        {
            if (InBounds(x, y)) _blocked[x, y] = v;
        }

        /// <summary>맵 크기(width/height)가 바뀐 뒤 충돌 배열을 다시 만든다 (FarmHouseBuilder에서 사용).</summary>
        internal void ResetBlocked() => _blocked = new bool[width, height];

        /// <summary>배송함 스프라이트 렌더러를 등록한다 (Farm1Builder에서 사용).</summary>
        internal void SetShippingBoxRenderer(SpriteRenderer sr) => _shippingBoxSr = sr;

        // ---------- feature restore / render ----------
        public void RestoreFeatures(LocationData loc)
        {
            foreach (var hd in loc.hoeDirts)
            {
                var pos = new Vector2Int(hd.x, hd.y);
                var dirt = new HoeDirt(hd.x, hd.y) { watered = hd.watered };
                if (hd.hasCrop)
                {
                    dirt.crop = new Crop(hd.cropId) { growthStage = hd.growthStage, dayCounter = hd.dayCounter };
                }
                hoeDirts[pos] = dirt;
            }
            RefreshAllSoil(); // 이웃 모양(오토타일)을 보려면 전부 채운 뒤에 그려야 한다
            foreach (var t in loc.trees)
            {
                var pos = new Vector2Int(t.x, t.y);
                trees[pos] = new TreeFeature(t.x, t.y, t.treeId, t.growthStage)
                {
                    hp = t.hp,
                    dayCounter = t.dayCounter
                };
                SetBlocked(t.x, t.y, true);
                RenderTree(pos);
            }

            foreach (var r in loc.rocks)
            {
                var pos = new Vector2Int(r.x, r.y);
                rocks[pos] = new RockFeature(r.x, r.y, r.variant) { hp = r.hp };
                SetBlocked(r.x, r.y, true);
                RenderRock(pos);
            }
        }

        public void RenderHoeDirt(Vector2Int pos)
        {
            if (!hoeDirts.TryGetValue(pos, out var dirt)) return;

            bool autoTile = SoilTileset.Available;

            // soil renderer — 대지마법으로 경작된 땅(Tilled Soil)
            if (!_hoeRenderers.TryGetValue(pos, out var soilSr))
            {
                soilSr = PlaceTile(AssetLibrary.Tilled, pos.x, pos.y, -50, _featureRoot);
                _hoeRenderers[pos] = soilSr;
            }
            soilSr.sprite = autoTile
                ? SoilTileset.GetDry(NeighborMask(pos, false))
                : (dirt.watered ? AssetLibrary.TilledWatered : AssetLibrary.Tilled);

            // wet overlay — 물마법으로 젖은 흙(Wet Soil)을 경작지 위에 덧그린다
            if (autoTile && dirt.watered)
            {
                if (!_wetRenderers.TryGetValue(pos, out var wetSr))
                {
                    wetSr = PlaceTile(null, pos.x, pos.y, -49, _featureRoot);
                    wetSr.gameObject.name = $"wet_{pos.x}_{pos.y}";
                    _wetRenderers[pos] = wetSr;
                }
                wetSr.sprite = SoilTileset.GetWet(NeighborMask(pos, true));
                wetSr.enabled = true;
            }
            else if (_wetRenderers.TryGetValue(pos, out var wetSr2))
            {
                wetSr2.enabled = false;
            }

            // crop renderer
            if (dirt.HasCrop)
            {
                if (!_cropRenderers.TryGetValue(pos, out var cropSr))
                {
                    var go = new GameObject($"crop_{pos.x}_{pos.y}");
                    go.transform.SetParent(_featureRoot, false);
                    go.transform.position = new Vector3(pos.x, pos.y + 0.25f, 0);
                    cropSr = go.AddComponent<SpriteRenderer>();
                    cropSr.sortingOrder = 400 - pos.y * 10;
                    _cropRenderers[pos] = cropSr;
                }
                cropSr.sprite = dirt.crop.GetSprite();
                cropSr.enabled = true;
            }
            else if (_cropRenderers.TryGetValue(pos, out var cropSr2))
            {
                cropSr2.enabled = false;
            }
        }

        /// <summary>
        /// 이웃 8칸이 같은 종류인지(경작지끼리 / 젖은 흙끼리) 검사해 오토타일 비트마스크를 만든다.
        /// 대각선·가로선·세로선·T자·3x3 어떤 배치든 이 마스크 하나로 맞는 그림이 결정된다.
        /// </summary>
        private int NeighborMask(Vector2Int p, bool wetOnly)
        {
            int m = 0;
            if (Matches(p.x, p.y + 1, wetOnly)) m |= SoilTileset.N;
            if (Matches(p.x + 1, p.y, wetOnly)) m |= SoilTileset.E;
            if (Matches(p.x, p.y - 1, wetOnly)) m |= SoilTileset.S;
            if (Matches(p.x - 1, p.y, wetOnly)) m |= SoilTileset.W;
            if (Matches(p.x + 1, p.y + 1, wetOnly)) m |= SoilTileset.NE;
            if (Matches(p.x + 1, p.y - 1, wetOnly)) m |= SoilTileset.SE;
            if (Matches(p.x - 1, p.y - 1, wetOnly)) m |= SoilTileset.SW;
            if (Matches(p.x - 1, p.y + 1, wetOnly)) m |= SoilTileset.NW;
            return SoilTileset.Normalize(m);
        }

        private bool Matches(int x, int y, bool wetOnly)
        {
            if (!hoeDirts.TryGetValue(new Vector2Int(x, y), out var d)) return false;
            return !wetOnly || d.watered;
        }

        /// <summary>한 칸이 바뀌면 맞닿은 8칸의 테두리도 달라지므로 같이 다시 그린다.</summary>
        private void RefreshSoilAround(Vector2Int pos)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    RenderHoeDirt(new Vector2Int(pos.x + dx, pos.y + dy));
        }

        private void RefreshAllSoil()
        {
            foreach (var key in hoeDirts.Keys)
                RenderHoeDirt(key);
        }

        public void RenderTree(Vector2Int pos)
        {
            bool alive = trees.TryGetValue(pos, out var tree) && tree.IsAlive;

            if (_treeRenderers.TryGetValue(pos, out var sr))
            {
                if (!alive)
                {
                    Destroy(sr.gameObject);
                    _treeRenderers.Remove(pos);
                    SetBlocked(pos.x, pos.y, false);
                }
                else
                {
                    sr.sprite = tree.GetSprite(); // 자라면서 그림이 바뀐다
                }
                return;
            }

            if (!alive) return;
            var created = PlaceObject(tree.GetSprite(), pos.x, pos.y + 0.6f, 600);
            _treeRenderers[pos] = created;
        }

        public void RenderRock(Vector2Int pos)
        {
            bool alive = rocks.TryGetValue(pos, out var rock) && rock.IsAlive;

            if (_rockRenderers.TryGetValue(pos, out var sr))
            {
                if (!alive)
                {
                    Destroy(sr.gameObject);
                    _rockRenderers.Remove(pos);
                    SetBlocked(pos.x, pos.y, false);
                }
                return;
            }

            if (!alive) return;
            var created = PlaceObject(rock.GetSprite(), pos.x, pos.y, 600);
            _rockRenderers[pos] = created;
        }

        // ---------- gameplay actions ----------
        public bool Till(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            if (id != LocationId.Farm1) return false;              // farming only on Farm1 in MVP
            if (IsBlocked(x, y)) return false;
            if (hoeDirts.ContainsKey(pos)) return false;
            if (trees.ContainsKey(pos)) return false;
            hoeDirts[pos] = new HoeDirt(x, y);
            RefreshSoilAround(pos);
            return true;
        }

        public bool Plant(int x, int y, string cropId)
        {
            var pos = new Vector2Int(x, y);
            if (!hoeDirts.TryGetValue(pos, out var d)) return false;
            if (!d.Plant(cropId)) return false;
            RenderHoeDirt(pos);
            return true;
        }

        public bool Water(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            if (!hoeDirts.TryGetValue(pos, out var d)) return false;
            if (d.watered) return false;
            d.Water();
            RefreshSoilAround(pos);
            return true;
        }

        /// <summary>NPC가 서 있는 칸은 지나갈 수 없게 막는다.</summary>
        public void SetNpcBlocked(Vector2Int tile) => SetBlocked(tile.x, tile.y, true);

        /// <summary>배송함 UI를 열고 닫을 때 뚜껑이 열린/닫힌 그림으로 바꾼다.</summary>
        public void SetShippingBoxOpen(bool open)
        {
            if (_shippingBoxSr == null) return;
            _shippingBoxSr.sprite = open ? AssetLibrary.ShippingBoxOpen : AssetLibrary.ShippingBox;
        }

        /// <summary>지금 이 타일에 수확 가능한(다 자란) 작물이 있는지 (실제로 캐지 않고 확인만).</summary>
        public bool IsHarvestableAt(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            return hoeDirts.TryGetValue(pos, out var d) && d.HasCrop && d.crop.IsHarvestable;
        }

        /// <summary>수확 가능하면 그 작물의 dropTableId를 반환한다 (없으면 null).</summary>
        public string HarvestAt(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            if (!hoeDirts.TryGetValue(pos, out var d)) return null;
            var dropTableId = d.Harvest();
            if (dropTableId != null) RenderHoeDirt(pos);
            return dropTableId;
        }

        /// <summary>
        /// 나무를 한 번 벤다. 이번 타격으로 나무가 쓰러졌으면 destroyed=true와 함께
        /// 그 나무의 dropTableId를 돌려준다 (호출자가 ItemDropSpawner로 실제 드랍을 스폰한다).
        /// 아직 다 자라지 않은 나무는 한 번에 뽑히고 아무것도 남기지 않는다.
        /// </summary>
        public bool ChopTree(int x, int y, out bool destroyed, out string dropTableId)
        {
            destroyed = false;
            dropTableId = null;
            var pos = new Vector2Int(x, y);
            if (!trees.TryGetValue(pos, out var t) || !t.IsAlive) return false;

            bool wasMature = t.IsMature;
            destroyed = t.Chop();
            if (destroyed)
            {
                if (wasMature) dropTableId = t.Def.dropTableId;
                trees.Remove(pos);
                RenderTree(pos);
            }
            return true;
        }

        /// <summary>바위를 한 번 친다. 부서졌으면 broken=true와 드랍 테이블을 돌려준다.</summary>
        public bool BreakRock(int x, int y, out bool broken, out string dropTableId)
        {
            broken = false;
            dropTableId = null;
            var pos = new Vector2Int(x, y);
            if (!rocks.TryGetValue(pos, out var r) || !r.IsAlive) return false;

            broken = r.Hit();
            if (broken)
            {
                dropTableId = RockFeature.DropTableId;
                rocks.Remove(pos);
                RenderRock(pos);
            }
            return true;
        }

        /// <summary>빈 땅에 나무 씨앗을 심는다.</summary>
        public bool PlantTree(int x, int y, string treeId)
        {
            var pos = new Vector2Int(x, y);
            if (!InBounds(x, y)) return false;
            if (IsBlocked(x, y)) return false;
            if (trees.ContainsKey(pos) || rocks.ContainsKey(pos) || hoeDirts.ContainsKey(pos)) return false;

            trees[pos] = new TreeFeature(x, y, treeId, 0);
            SetBlocked(x, y, true);
            RenderTree(pos);
            return true;
        }

        internal static void AddDefaultTree(LocationData loc, int x, int y)
        {
            var def = TreeDatabase.Get(TreeDatabase.DefaultTreeId);
            loc.trees.Add(new TreeData
            {
                x = x, y = y, hp = def.maxHp,
                treeId = def.treeId,
                growthStage = def.maxGrowthStage
            });
        }

        internal static void AddRock(LocationData loc, int x, int y, int variant)
            => loc.rocks.Add(new RockData { x = x, y = y, hp = RockFeature.MaxHp, variant = variant });

        /// <summary>Advance all crops one day (called on sleep).</summary>
        public void OnNewDay()
        {
            foreach (var kv in hoeDirts)
            {
                kv.Value.OnNewDay();
                RenderHoeDirt(kv.Key);
            }
        }

        /// <summary>Write live features back into serializable LocationData.</summary>
        public void SaveInto(LocationData loc)
        {
            loc.hoeDirts.Clear();
            foreach (var kv in hoeDirts)
            {
                var d = kv.Value;
                loc.hoeDirts.Add(new HoeDirtData
                {
                    x = d.x, y = d.y, watered = d.watered,
                    hasCrop = d.HasCrop,
                    cropId = d.HasCrop ? d.crop.cropId : null,
                    growthStage = d.HasCrop ? d.crop.growthStage : 0,
                    dayCounter = d.HasCrop ? d.crop.dayCounter : 0
                });
            }
            loc.trees.Clear();
            foreach (var kv in trees)
            {
                var t = kv.Value;
                loc.trees.Add(new TreeData
                {
                    x = t.x, y = t.y, hp = t.hp,
                    treeId = t.treeId,
                    growthStage = t.growthStage,
                    dayCounter = t.dayCounter
                });
            }

            loc.rocks.Clear();
            foreach (var kv in rocks)
                loc.rocks.Add(new RockData { x = kv.Value.x, y = kv.Value.y, hp = kv.Value.hp, variant = kv.Value.variant });

            loc.droppedItems.Clear();
            foreach (var wi in _featureRoot.GetComponentsInChildren<WorldItem>())
            {
                var pos = wi.transform.position;
                loc.droppedItems.Add(new WorldItemData { x = pos.x, y = pos.y, itemId = wi.ItemId, count = wi.Count });
            }
            loc.initialized = true;
        }
    }
}
