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

        /// <summary>드랍된 월드 아이템(WorldItem)을 매달아 둘 부모. 위치가 다시 로드되면 함께 정리된다.</summary>
        public Transform FeatureRoot => _featureRoot;

        private bool[,] _blocked;
        private Transform _tileRoot, _featureRoot;
        private readonly Dictionary<Vector2Int, SpriteRenderer> _hoeRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _cropRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _treeRenderers = new Dictionary<Vector2Int, SpriteRenderer>();

        // Special interaction points
        public Vector2Int? shippingBoxTile; // Farm1 배송함
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
                case LocationId.Farm1: BuildFarm1(data); break;
                case LocationId.Farm2: BuildFarm2(data); break;
                case LocationId.FarmHouse: BuildFarmHouse(data); break;
            }
        }

        // ---------- tile helpers ----------
        private SpriteRenderer PlaceTile(Sprite sprite, int x, int y, int order, Transform parent = null)
        {
            var go = new GameObject($"tile_{x}_{y}");
            go.transform.SetParent(parent ?? _tileRoot, false);
            go.transform.position = new Vector3(x, y, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        private SpriteRenderer PlaceObject(Sprite sprite, float x, float y, int baseOrder)
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

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        public bool IsBlocked(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            return _blocked[x, y];
        }

        private void SetBlocked(int x, int y, bool v)
        {
            if (InBounds(x, y)) _blocked[x, y] = v;
        }

        // ---------- FARM 1 ----------
        private void BuildFarm1(GameData data)
        {
            // grass ground
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    PlaceTile(AssetLibrary.Grass, x, y, -100);

            // border blocking (except right exit)
            for (int x = 0; x < width; x++) { SetBlocked(x, 0, true); SetBlocked(x, height - 1, true); }
            for (int y = 0; y < height; y++) { SetBlocked(0, y, true); }

            // right exit region -> Farm2
            rightExit = new RectInt(width - 1, height / 2 - 1, 1, 3);
            for (int y = 0; y < height; y++)
            {
                bool open = y >= rightExit.Value.yMin && y < rightExit.Value.yMax;
                SetBlocked(width - 1, y, !open);
            }

            // House structure (top-left). Door tile at bottom of house leads to FarmHouse.
            var houseSr = PlaceObject(AssetLibrary.House, 3.5f, height - 3.0f, 1000);
            houseSr.sortingOrder = 500;
            // block house footprint
            for (int hx = 2; hx <= 6; hx++)
                for (int hy = height - 5; hy <= height - 1; hy++)
                    SetBlocked(hx, hy, true);
            // door tile (walk into it to enter house)
            doorExitTile = new Vector2Int(4, height - 5);
            SetBlocked(doorExitTile.Value.x, doorExitTile.Value.y, false);

            // 배송함 (집 옆) — 우클릭해서 열고, 넣어 둔 물건은 다음 날 아침에 팔린다
            shippingBoxTile = new Vector2Int(8, height - 5);
            _shippingBoxSr = PlaceObject(AssetLibrary.ShippingBox,
                shippingBoxTile.Value.x, shippingBoxTile.Value.y + 0.15f, 600);
            SetBlocked(shippingBoxTile.Value.x, shippingBoxTile.Value.y, true);

            // Trees (from data or defaults)
            var loc = data.GetLocation(LocationId.Farm1);
            if (!loc.initialized)
            {
                loc.trees.Add(new TreeData { x = 12, y = 10, hp = TreeFeature.MaxHp });
                loc.trees.Add(new TreeData { x = 15, y = 8, hp = TreeFeature.MaxHp });
                loc.trees.Add(new TreeData { x = 10, y = 4, hp = TreeFeature.MaxHp });
                loc.initialized = true;
            }
            RestoreFeatures(loc);
        }

        // ---------- FARM 2 ----------
        private void BuildFarm2(GameData data)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    PlaceTile(AssetLibrary.Grass, x, y, -100);

            for (int x = 0; x < width; x++) { SetBlocked(x, 0, true); SetBlocked(x, height - 1, true); }
            for (int y = 0; y < height; y++) { SetBlocked(width - 1, y, true); }

            // left exit -> Farm1
            leftExit = new RectInt(0, height / 2 - 1, 1, 3);
            for (int y = 0; y < height; y++)
            {
                bool open = y >= leftExit.Value.yMin && y < leftExit.Value.yMax;
                SetBlocked(0, y, !open);
            }

            // a couple of decorative trees
            var loc = data.GetLocation(LocationId.Farm2);
            if (!loc.initialized)
            {
                loc.trees.Add(new TreeData { x = 6, y = 9, hp = TreeFeature.MaxHp });
                loc.trees.Add(new TreeData { x = 14, y = 5, hp = TreeFeature.MaxHp });
                loc.initialized = true;
            }
            RestoreFeatures(loc);
        }

        // ---------- FARM HOUSE (interior) ----------
        private void BuildFarmHouse(GameData data)
        {
            width = 12; height = 9;
            _blocked = new bool[width, height];

            // wall row at top, wood floor elsewhere
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    if (y >= height - 2)
                        PlaceTile(AssetLibrary.Wall, x, y, -100);
                    else
                        PlaceTile(AssetLibrary.Floor, x, y, -100);
                }

            // border walls
            for (int x = 0; x < width; x++) { SetBlocked(x, 0, true); SetBlocked(x, height - 1, true); SetBlocked(x, height - 2, true); }
            for (int y = 0; y < height; y++) { SetBlocked(0, y, true); SetBlocked(width - 1, y, true); }

            // rug
            var rug = PlaceObject(AssetLibrary.Rug, width / 2f - 0.5f, 3f, 50);
            rug.sortingOrder = -50;

            // bed (top-left interior)
            bedTile = new Vector2Int(2, height - 3);
            var bed = PlaceObject(AssetLibrary.Bed, 2f, height - 3.0f, 500);
            SetBlocked(2, height - 3, true);
            SetBlocked(2, height - 4, true);

            // fireplace decor
            PlaceObject(AssetLibrary.Fireplace, 6f, height - 3.0f, 500);
            SetBlocked(6, height - 3, true);

            // door (bottom) -> back to Farm1
            doorExitTile = new Vector2Int(width / 2, 1);
            var door = PlaceObject(AssetLibrary.Door, width / 2f, 0.6f, 500);
            SetBlocked(doorExitTile.Value.x, 0, false);
        }

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
                RenderHoeDirt(pos);
            }
            foreach (var t in loc.trees)
            {
                var pos = new Vector2Int(t.x, t.y);
                trees[pos] = new TreeFeature(t.x, t.y, t.dropTableId) { hp = t.hp };
                SetBlocked(t.x, t.y, true);
                RenderTree(pos);
            }
        }

        public void RenderHoeDirt(Vector2Int pos)
        {
            var dirt = hoeDirts[pos];
            // soil renderer
            if (!_hoeRenderers.TryGetValue(pos, out var soilSr))
            {
                soilSr = PlaceTile(AssetLibrary.Tilled, pos.x, pos.y, -50, _featureRoot);
                _hoeRenderers[pos] = soilSr;
            }
            soilSr.sprite = dirt.watered ? AssetLibrary.TilledWatered : AssetLibrary.Tilled;

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

        public void RenderTree(Vector2Int pos)
        {
            if (_treeRenderers.TryGetValue(pos, out var sr))
            {
                if (!trees.ContainsKey(pos) || !trees[pos].IsAlive)
                {
                    Destroy(sr.gameObject);
                    _treeRenderers.Remove(pos);
                    SetBlocked(pos.x, pos.y, false);
                }
                return;
            }
            var s = PlaceObject(AssetLibrary.Tree, pos.x, pos.y + 0.6f, 600);
            _treeRenderers[pos] = s;
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
            RenderHoeDirt(pos);
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
            RenderHoeDirt(pos);
            return true;
        }

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
        /// </summary>
        public bool ChopTree(int x, int y, out bool destroyed, out string dropTableId)
        {
            destroyed = false;
            dropTableId = null;
            var pos = new Vector2Int(x, y);
            if (!trees.TryGetValue(pos, out var t) || !t.IsAlive) return false;
            destroyed = t.Chop();
            if (destroyed)
            {
                dropTableId = t.dropTableId;
                trees.Remove(pos);
                RenderTree(pos);
            }
            return true;
        }

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
                loc.trees.Add(new TreeData { x = kv.Value.x, y = kv.Value.y, hp = kv.Value.hp, dropTableId = kv.Value.dropTableId });

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
