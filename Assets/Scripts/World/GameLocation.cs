using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// Runtime representation of a map. Owns the tile grid, collision, terrain
    /// features (HoeDirt / Trees) and their rendering GameObjects. Rebuilt from
    /// LocationData whenever the player enters (design doc §15/§17).
    /// </summary>
    public partial class GameLocation : MonoBehaviour
    {
        public LocationId id;
        public int width = 20;
        public int height = 15;

        /// <summary>
        /// 경작 마스크 레이어("Tillable_{id}")가 없을 때 이 위치를 경작할 수 있다고 볼지.
        /// 실내(FarmHouse)처럼 밭을 갈 수 없는 곳은 빌더가 false로 둔다.
        /// 마스크 레이어를 칠하면 이 값과 상관없이 칠한 칸만 경작할 수 있다.
        /// </summary>
        public bool tillableByDefault = true;

        /// <summary>코드가 까는 기본 바닥의 정렬 순서 (빌더들이 쓴다). 자세한 규칙은 Depth 참고.</summary>
        internal const int GroundOrder = Depth.Ground;

        public Dictionary<Vector2Int, HoeDirt> hoeDirts = new Dictionary<Vector2Int, HoeDirt>();
        public Dictionary<Vector2Int, TreeFeature> trees = new Dictionary<Vector2Int, TreeFeature>();
        public Dictionary<Vector2Int, RockFeature> rocks = new Dictionary<Vector2Int, RockFeature>();

        /// <summary>드랍된 월드 아이템(WorldItem)을 매달아 둘 부모. 위치가 다시 로드되면 함께 정리된다.</summary>
        public Transform FeatureRoot => _featureRoot;

        private bool[,] _blocked;
        /// <summary>
        /// 오브젝트 발판처럼 <b>치울 수 없는</b> 이유로 막힌 칸. 나무를 베거나 바위를 부술 때 그 칸을
        /// 무조건 열어 버리면, 집 발판 위에 서 있던 나무를 벤 순간 집에 구멍이 뚫린다.
        /// </summary>
        private bool[,] _objectBlocked;
        /// <summary>경작 마스크. null이면 마스크 레이어가 없다는 뜻이라 어디든 경작할 수 있다.</summary>
        private bool[,] _tillable;
        /// <summary>맵 크기를 씬에 칠한 바닥에서 가져왔는지. true면 빌더의 기본 크기를 무시한다.</summary>
        private bool _sizeFromTilemap;

        private Tilemap _groundMap, _blockedMask, _tillableMask, _objectMarkers, _waterMap;
        private Tilemap _cliffMap;
        /// <summary>물 칸. null이면 물 레이어가 없다.</summary>
        private bool[,] _water;
        /// <summary>절벽 칸. null이면 절벽 레이어가 없다.</summary>
        private bool[,] _cliff;

        /// <summary>
        /// 눈에 보이는(렌더러를 켜 두는) 레이어들. 계절 타일 교체는 레이어 종류를 따지지 않고
        /// 여기 담긴 것을 전부 훑는다 — 바닥이든 장식이든 절벽이든, 계절 짝이 있는 타일을 칠했으면
        /// 계절마다 그림이 바뀐다. 보이지 않는 마스크는 바꿔도 보이는 게 없어서 넣지 않고,
        /// 마커 레이어는 <b>타일 이름</b>이 곧 무엇을 놓을지 정하는 약속이라 건드리면 안 된다.
        /// </summary>
        private readonly List<Tilemap> _visibleMaps = new List<Tilemap>();

        // 맵 밖으로 밀려난 저장 데이터. 지우지 않고 들고 있다가 저장할 때 그대로 되돌려 준다 —
        // 나중에 바닥을 더 칠해서 맵이 커지면 그 시설/작물이 다시 살아난다.
        private readonly List<HoeDirtData> _outOfBoundsHoeDirts = new List<HoeDirtData>();
        private readonly List<TreeData> _outOfBoundsTrees = new List<TreeData>();
        private readonly List<RockData> _outOfBoundsRocks = new List<RockData>();
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

        /// <summary>
        /// 밟으면 다른 맵으로 넘어가는 칸들. "Obj_Exit{위치}" 마커를 칠해서 만들거나,
        /// 마커가 없으면 빌더가 가장자리 영역을 여기에 등록한다.
        /// </summary>
        private readonly Dictionary<Vector2Int, LocationId> _exits = new Dictionary<Vector2Int, LocationId>();

        /// <summary>이 칸을 밟으면 다른 맵으로 가도록 등록한다. 출구 칸은 당연히 걸어갈 수 있어야 한다.</summary>
        public void AddExit(int x, int y, LocationId target)
        {
            if (!InBounds(x, y)) return;
            _exits[new Vector2Int(x, y)] = target;
            SetBlocked(x, y, false);
        }

        /// <summary>이 칸이 다른 맵으로 가는 칸인지.</summary>
        public bool TryGetExit(int x, int y, out LocationId target)
            => _exits.TryGetValue(new Vector2Int(x, y), out target);

        /// <summary>
        /// origin 맵에서 넘어왔을 때 내려설 자리. 되돌아가는 출구(= origin을 가리키는 칸) 중
        /// 떠나온 높이와 가장 가까운 것을 고르고, 그 <b>옆</b>의 걸을 수 있는 칸에 내려놓는다.
        /// 출구 칸 위에 그대로 세우면 다음 프레임에 곧바로 되돌아가 버리기 때문이다.
        /// </summary>
        public Vector2 FindEntryFrom(LocationId origin, Vector2Int fromTile)
        {
            Vector2Int? best = null;
            int bestScore = int.MaxValue;
            foreach (var kv in _exits)
            {
                if (kv.Value != origin) continue;
                int score = Mathf.Abs(kv.Key.y - fromTile.y) * 100 + Mathf.Abs(kv.Key.x - fromTile.x);
                if (score < bestScore) { bestScore = score; best = kv.Key; }
            }

            if (best.HasValue)
            {
                // 안쪽(오른쪽/왼쪽/위/아래) 순으로 출구가 아닌 빈 칸을 찾는다.
                var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
                foreach (var d in dirs)
                {
                    var n = best.Value + d;
                    if (IsBlocked(n.x, n.y) || _exits.ContainsKey(n)) continue;
                    return new Vector2(n.x, n.y);
                }
            }

            Debug.LogWarning($"[GameLocation] {id}: {origin}에서 되돌아올 출구를 찾지 못해 맵 가운데에 내려놓습니다. " +
                             $"\"Obj_Exit{origin}\" 마커를 칠해 주세요.");
            return FindWalkableNear(new Vector2(width / 2f, height / 2f));
        }

        public void Build(GameData data)
        {
            AssetLibrary.EnsureLoaded();

            _tileRoot = new GameObject("Tiles").transform;
            _tileRoot.SetParent(transform, false);
            _featureRoot = new GameObject("Features").transform;
            _featureRoot.SetParent(transform, false);

            // 씬에 칠해 둔 레이어를 먼저 찾는다. 칠한 바닥의 크기가 곧 이 맵의 크기라서
            // 빌더가 시설을 놓기 전에 width/height가 확정돼 있어야 한다.
            FindSceneTilemaps();
            if (_groundMap != null) AlignGridToMap(_groundMap);

            // 칠해 둔 평범한 타일을 계절 타일로 바꾸고(이미 바뀐 칸은 건너뛴다) 다시 그리게 한다.
            // 씬의 Tilemap은 위치를 다시 로드해도 살아 있으므로 직접 갱신해야 한다.
            foreach (var map in _visibleMaps)
            {
                SeasonalTileset.Apply(map);
                map.RefreshAllTiles();
            }

            _blocked = new bool[width, height];

            switch (id)
            {
                case LocationId.Farm1: Farm1Builder.Build(this, data); break;
                case LocationId.Farm2: Farm2Builder.Build(this, data); break;
                case LocationId.FarmHouse: FarmHouseBuilder.Build(this, data); break;
            }

            ApplyMaskTilemaps();

            // 문 칸은 무슨 일이 있어도 걸어 들어갈 수 있어야 한다. 집이 막는 칸을 Blocked 레이어로
            // 칠하다 보면 문까지 함께 칠하기 쉬운데, 그러면 집에 들어갈 방법이 없어진다.
            if (doorExitTile.HasValue)
                SetBlocked(doorExitTile.Value.x, doorExitTile.Value.y, false);
        }

        /// <summary>
        /// 빌더가 쓰는 기본 맵 크기. 씬에 칠해 둔 바닥이 있으면 그쪽 크기가 우선이라 무시된다.
        /// </summary>
        internal void SetDefaultSize(int w, int h)
        {
            if (_sizeFromTilemap) return;
            width = w;
            height = h;
        }

        /// <summary>
        /// 씬에 타일 팔레트로 칠해 둔 Tilemap들을 이 위치에 연결한다. 이름으로 역할을 구분한다:
        ///
        ///   바닥   "Location_{id}"  또는 "{id}Ground"    — 눈에 보이는 바닥
        ///   충돌   "Blocked_{id}"   또는 "{id}Blocked"   — 칠한 칸은 지나갈 수 없다
        ///   경작   "Tillable_{id}"  또는 "{id}Tillable"  — 칠한 칸에서만 대지마법을 쓸 수 있다
        ///   물건   "Objects_{id}"   또는 "{id}Objects"    — 집/배송함/나무... 를 놓을 자리
        ///   물     "Water_{id}"     또는 "{id}Water"      — 칠한 칸은 물. 못 지나가고, 낚시할 수 있다
        ///   장식   "Decor_{id}"     또는 "{id}Decor"      — 꽃·잔디 장식. 막지 않고, 경작지에 덮인다
        ///   절벽   "Cliff_{id}"     또는 "{id}Cliff"      — 칠한 칸은 절벽. 못 지나가고, 덮을 수 없다
        ///
        /// 마스크 레이어는 "어떤 타일을 칠했는지"는 보지 않고 "칠했는지 아닌지"만 본다. 그래서
        /// 아무 타일이나 하나 골라 영역만 쓱 칠하면 되고, 타일셋의 타일을 하나씩 분류할 필요가 없다.
        /// 화면에 나오면 안 되므로 마스크 레이어의 렌더러는 꺼 둔다.
        /// 경작 레이어가 아예 없으면 _tillable이 null로 남아 지금까지처럼 어디든 경작할 수 있다.
        ///
        /// 이름으로 GameObject.Find를 하지 않고 Tilemap 컴포넌트로 찾는 이유: GameManager가
        /// 런타임에 만드는 위치 루트도 이름이 "Location_{id}"라서 이름만으로는 구분되지 않는다.
        /// </summary>
        private void FindSceneTilemaps()
        {
            _groundMap = _blockedMask = _tillableMask = _objectMarkers = _waterMap = null;
            _cliffMap = null;
            _visibleMaps.Clear();

            // 계절 전용 레이어가 있으면 기본 레이어보다 우선한다. 그래서 두 번 훑는다 —
            // 첫 판에서 이번 계절 전용 레이어를 잡고, 둘째 판에서 빈 자리만 기본 레이어로 채운다.
            var seasonal = new HashSet<TilemapLayer>();

            for (int pass = 0; pass < 2; pass++)
            foreach (var tm in FindObjectsOfType<Tilemap>())
            {
                if (!TryParseLayerName(tm.name, out var owner, out var layer, out var layerSeason)) continue;
                var tr = tm.GetComponent<TilemapRenderer>();

                bool isSeasonal = layerSeason.HasValue;
                if (pass == 0 != isSeasonal) continue;              // 0번 판은 계절 전용만, 1번 판은 기본만
                if (isSeasonal && layerSeason.Value != Seasons.Current)
                {
                    if (tr != null) tr.enabled = false;             // 지금 계절이 아닌 전용 레이어는 숨긴다
                    continue;
                }
                if (owner != id)
                {
                    if (tr != null) tr.enabled = false;   // 다른 위치의 레이어는 안 보이게
                    continue;
                }
                if (pass == 1 && seasonal.Contains(layer))
                {
                    if (tr != null) tr.enabled = false;   // 이번 계절 전용 레이어가 대신하고 있다
                    continue;
                }
                if (isSeasonal) seasonal.Add(layer);

                switch (layer)
                {
                    case TilemapLayer.Ground:
                        _groundMap = tm;
                        Show(tm, tr, Depth.PaintedGround);
                        break;
                    case TilemapLayer.Blocked:
                        _blockedMask = tm;
                        if (tr != null) tr.enabled = false;
                        break;
                    case TilemapLayer.Tillable:
                        _tillableMask = tm;
                        if (tr != null) tr.enabled = false;
                        break;
                    case TilemapLayer.Water:
                        // 물은 눈에 보여야 하는 레이어다 — 마스크들과 달리 렌더러를 켜 둔다.
                        _waterMap = tm;
                        Show(tm, tr, Depth.Water);
                        break;
                    case TilemapLayer.Decor:
                        // 장식은 경작지(Depth.Soil)보다 뒤에 그린다 — 밭을 갈면 흙에 덮여 사라지고,
                        // 흙을 없애면 그대로 다시 드러난다. 숨겼다 되살리는 상태가 아예 없다.
                        Show(tm, tr, Depth.Decor);
                        break;
                    case TilemapLayer.Cliff:
                        _cliffMap = tm;
                        Show(tm, tr, Depth.Cliff);
                        break;
                    case TilemapLayer.Objects:
                        _objectMarkers = tm;
                        tm.CompressBounds();
                        HasObjectMarkers = tm.cellBounds.size.x > 0 && tm.cellBounds.size.y > 0;
                        if (tr != null) tr.enabled = false;   // 마커는 에디터에서만 보이면 된다
                        break;
                }
            }
        }

        /// <summary>보이는 레이어로 등록한다. 계절 타일 교체는 여기 등록된 것만 훑는다.</summary>
        private void Show(Tilemap tm, TilemapRenderer tr, int sortingOrder)
        {
            if (tr != null) { tr.enabled = true; tr.sortingOrder = sortingOrder; }
            _visibleMaps.Add(tm);
        }

        /// <summary>
        /// 칠해 둔 바닥의 왼쪽 아래 칸이 맵의 (0,0) 칸 위에 오도록 Grid를 통째로 옮긴다.
        ///
        /// 코드가 놓는 타일 스프라이트는 "중심"이 정수 좌표에 오지만 Tilemap 칸은 "왼쪽 아래
        /// 모서리"가 정수 좌표라 좌표만 맞추면 반 칸이 어긋난다 — 칸 중심을 기준으로 맞춰야
        /// 두 격자가 정확히 겹친다. Tilemap 하나가 아니라 Grid를 옮기는 이유는 마스크 레이어들이
        /// 바닥과 같은 격자에 붙어 있어야 하기 때문이다.
        /// 이미 맞춰져 있으면 이동량이 0이라 여러 번 불려도 안전하다.
        /// </summary>
        private void AlignGridToMap(Tilemap ground)
        {
            ground.CompressBounds();
            var b = ground.cellBounds;
            if (b.size.x <= 0 || b.size.y <= 0) return;   // 아직 아무것도 안 칠했다

            var grid = ground.GetComponentInParent<Grid>();
            var root = grid != null ? grid.transform : ground.transform;

            var center = ground.GetCellCenterWorld(b.min);
            var shift = new Vector3(center.x, center.y, 0f);
            if (shift.sqrMagnitude > 0.0001f)
            {
                root.position -= shift;
                Debug.Log($"[GameLocation] {ground.name}: 칠한 영역의 왼쪽 아래 칸이 맵의 (0,0)에 오도록 " +
                          $"({-shift.x}, {-shift.y}) 만큼 옮겼습니다.");
            }

            // 칠한 영역이 곧 맵의 크기다. 빌더의 기본값(SetDefaultSize)보다 우선한다.
            width = b.size.x;
            height = b.size.y;
            _sizeFromTilemap = true;
            Debug.Log($"[GameLocation] {id} 맵 크기를 {ground.name}에 칠한 영역에 맞춰 {width}x{height}로 정했습니다.");
        }

        /// <summary>
        /// 마스크 레이어를 읽어 충돌/경작 가능 여부에 반영한다. 격자를 맞춘 뒤라 맵의 (x,y) 칸
        /// 중심이 곧 월드 좌표 (x,y)이므로, WorldToCell로 각 레이어의 칸을 바로 찾을 수 있다
        /// (부모가 달라도 안전하고, 칸 경계에서 0.5칸 떨어져 있어 반올림 문제도 없다).
        /// 충돌은 코드가 이미 막아 둔 칸(집/나무/바위/맵 테두리)에 더해서 적용된다.
        /// </summary>
        private void ApplyMaskTilemaps()
        {
            Tilemap blockedMask = _blockedMask, tillableMask = _tillableMask, waterMap = _waterMap;
            Tilemap cliffMap = _cliffMap;
            _tillable = tillableMask != null ? new bool[width, height] : null;
            _water = waterMap != null ? new bool[width, height] : null;
            _cliff = cliffMap != null ? new bool[width, height] : null;
            if (blockedMask == null && tillableMask == null && waterMap == null && cliffMap == null) return;

            int blockedCount = 0, tillableCount = 0, waterCount = 0, cliffCount = 0;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var world = new Vector3(x, y, 0f);
                    if (blockedMask != null && !_exits.ContainsKey(new Vector2Int(x, y))
                        && blockedMask.HasTile(blockedMask.WorldToCell(world)))
                    {
                        SetBlocked(x, y, true);
                        blockedCount++;
                    }
                    if (tillableMask != null && tillableMask.HasTile(tillableMask.WorldToCell(world)))
                    {
                        _tillable[x, y] = true;
                        tillableCount++;
                    }
                    // 물은 칠하기만 하면 자동으로 못 지나가는 칸이 된다 (따로 Blocked를 칠 필요 없음)
                    if (waterMap != null && waterMap.HasTile(waterMap.WorldToCell(world)))
                    {
                        _water[x, y] = true;
                        SetBlocked(x, y, true);
                        waterCount++;
                    }
                    // 절벽도 칠하기만 하면 못 지나가는 칸이 된다. 경작·씨앗·오브젝트가 덮지 못하는 것은
                    // CanTill이 IsBlocked를 먼저 보기 때문에 따로 막을 필요가 없다.
                    if (cliffMap != null && cliffMap.HasTile(cliffMap.WorldToCell(world)))
                    {
                        _cliff[x, y] = true;
                        SetBlocked(x, y, true);
                        cliffCount++;
                    }
                }

            if (blockedMask != null)
                Debug.Log($"[GameLocation] {blockedMask.name}: {blockedCount}칸을 통행 불가로 표시했습니다.");
            if (tillableMask != null)
                Debug.Log($"[GameLocation] {tillableMask.name}: {tillableCount}칸만 경작할 수 있습니다.");
            if (waterMap != null)
                Debug.Log($"[GameLocation] {waterMap.name}: 물 {waterCount}칸 (통행 불가, 물마법으로 낚시).");
            if (cliffMap != null)
                Debug.Log($"[GameLocation] {cliffMap.name}: 절벽 {cliffCount}칸 (통행 불가, 덮을 수 없음).");
        }

        // ---------- 오브젝트 마커 레이어 ----------

        /// <summary>"Objects_{id}" 레이어에 뭔가 칠해져 있으면 true. 빌더는 이때 하드코딩 배치를 건너뛴다.</summary>
        public bool HasObjectMarkers { get; private set; }

        /// <summary>
        /// "Objects_{id}" 레이어를 읽어 집·배송함·나무 같은 것들을 놓는다.
        /// 무엇을 놓을지는 ObjectMarkerDatabase의 표가, 놓는 일은 ObjectMarkerPlacer가 한다 —
        /// 오브젝트를 아무리 추가해도 이 파일은 길어지지 않는다.
        /// </summary>
        public void ApplyObjectMarkers(LocationData locData)
            => ObjectMarkerPlacer.Apply(this, _objectMarkers, locData);

        private enum TilemapLayer { Ground, Blocked, Tillable, Objects, Water, Decor, Cliff }

        /// <summary>
        /// 레이어 이름을 해석한다. 앞에 계절이 붙어 있으면("Winter_Location_Farm1") 그 계절 전용이다.
        /// season이 null이면 계절과 무관한 기본 레이어.
        /// </summary>
        private static bool TryParseLayerName(string name, out LocationId locId, out TilemapLayer layer,
                                              out Season? season)
        {
            season = null;
            foreach (Season s in System.Enum.GetValues(typeof(Season)))
            {
                string prefix = Seasons.Key(s) + "_";
                if (!name.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                if (!TryParseLayerName(name.Substring(prefix.Length), out locId, out layer)) break;
                season = s;
                return true;
            }
            return TryParseLayerName(name, out locId, out layer);
        }

        private static bool TryParseLayerName(string name, out LocationId locId, out TilemapLayer layer)
        {
            foreach (LocationId candidate in System.Enum.GetValues(typeof(LocationId)))
            {
                if (name == $"Location_{candidate}" || name == $"{candidate}Ground")
                { locId = candidate; layer = TilemapLayer.Ground; return true; }
                if (name == $"Blocked_{candidate}" || name == $"{candidate}Blocked")
                { locId = candidate; layer = TilemapLayer.Blocked; return true; }
                if (name == $"Tillable_{candidate}" || name == $"{candidate}Tillable")
                { locId = candidate; layer = TilemapLayer.Tillable; return true; }
                if (name == $"Objects_{candidate}" || name == $"{candidate}Objects")
                { locId = candidate; layer = TilemapLayer.Objects; return true; }
                if (name == $"Water_{candidate}" || name == $"{candidate}Water")
                { locId = candidate; layer = TilemapLayer.Water; return true; }
                if (name == $"Decor_{candidate}" || name == $"{candidate}Decor")
                { locId = candidate; layer = TilemapLayer.Decor; return true; }
                if (name == $"Cliff_{candidate}" || name == $"{candidate}Cliff")
                { locId = candidate; layer = TilemapLayer.Cliff; return true; }
            }
            locId = default;
            layer = TilemapLayer.Ground;
            return false;
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

        /// <summary>
        /// 오브젝트를 놓는다. 그리는 위치(x, y)와 <b>정렬 기준이 되는 발밑(footY)</b>을 따로 받는다 —
        /// 나무처럼 그림이 발밑보다 훨씬 위로 올라가는 것을 그림 위치로 정렬하면 앞뒤가 어긋난다.
        /// footY를 주지 않으면 그리는 위치를 그대로 발밑으로 본다.
        /// </summary>
        internal SpriteRenderer PlaceObject(Sprite sprite, float x, float y, float footY, int bias = 0)
        {
            var go = new GameObject("obj");
            go.transform.SetParent(_featureRoot, false);
            go.transform.position = new Vector3(x, y, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = Depth.YSort(footY, bias);
            return sr;
        }

        internal SpriteRenderer PlaceObject(Sprite sprite, float x, float y)
            => PlaceObject(sprite, x, y, y);

        /// <summary>
        /// Assets/Resources/Prefabs/{locId}Ground.prefab 가 있으면 그걸 인스턴스화해서 바닥으로 쓴다
        /// (Unity 에디터의 Tile Palette로 손으로 칠한 Tilemap). 없으면 false.
        /// 기본 바닥은 호출부가 항상 먼저 깔아 두므로, 칠하지 않은 칸이 비어 보이지는 않는다.
        /// </summary>
        internal bool TryPlaceGroundTilemap(LocationId locId)
        {
            var prefab = Resources.Load<GameObject>($"Prefabs/{locId}Ground");
            if (prefab == null) return false;
            var go = Instantiate(prefab, _tileRoot);
            go.transform.localPosition = Vector3.zero;
            // 프리팹에 저장된 정렬 순서가 0이면 경작지를 덮어 버리므로 바닥 순서로 낮춘다.
            foreach (var tr in go.GetComponentsInChildren<TilemapRenderer>(true))
                tr.sortingOrder = Depth.PaintedGround;
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
            if (!InBounds(x, y)) return;
            _blocked[x, y] = v;
            // 일부러 연 칸(문·출구)은 오브젝트 발판에서도 빼야 다시 막히지 않는다.
            if (!v && _objectBlocked != null) _objectBlocked[x, y] = false;
        }

        /// <summary>
        /// 오브젝트 발판으로 막는다. 그냥 SetBlocked와 달리, 이 칸에 있던 나무/바위를 치워도
        /// 계속 막힌 채로 남는다.
        /// </summary>
        internal void SetObjectBlocked(int x, int y)
        {
            if (!InBounds(x, y)) return;
            if (_objectBlocked == null) _objectBlocked = new bool[width, height];
            _objectBlocked[x, y] = true;
            _blocked[x, y] = true;
        }

        /// <summary>나무/바위가 사라진 칸을 연다. 그 칸이 오브젝트 발판이면 막힌 채로 둔다.</summary>
        private void ClearFeatureBlock(Vector2Int pos)
            => SetBlocked(pos.x, pos.y, _objectBlocked != null && _objectBlocked[pos.x, pos.y]);

        /// <summary>
        /// 이 칸에서 대지마법(경작)을 쓸 수 있는지. "Tillable_{id}" 마스크 레이어를 칠해 두면
        /// 거기 칠한 칸만 경작할 수 있고, 레이어가 없으면 (예전처럼) 어디든 경작할 수 있다.
        /// </summary>
        public bool IsTillable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            if (IsWater(x, y) || IsCliff(x, y)) return false;
            return _tillable != null ? _tillable[x, y] : tillableByDefault;
        }

        /// <summary>
        /// 물 칸인지. "Water_{id}" 타일맵에 칠한 칸이다. 물은 지나갈 수 없고,
        /// 물마법을 쓰면 물을 주는 대신 낚시가 시작된다.
        /// </summary>
        public bool IsWater(int x, int y)
        {
            if (!InBounds(x, y) || _water == null) return false;
            return _water[x, y];
        }

        /// <summary>
        /// 절벽 칸인지. "Cliff_{id}" 타일맵에 칠한 칸이다. 지나갈 수도, 무언가로 덮을 수도 없다.
        /// </summary>
        public bool IsCliff(int x, int y)
        {
            if (!InBounds(x, y) || _cliff == null) return false;
            return _cliff[x, y];
        }

        /// <summary>맵 크기(width/height)가 바뀐 뒤 충돌 배열을 다시 만든다 (FarmHouseBuilder에서 사용).</summary>
        internal void ResetBlocked()
        {
            _blocked = new bool[width, height];
            _objectBlocked = null;
        }

        /// <summary>배송함 스프라이트 렌더러를 등록한다 (Farm1Builder에서 사용).</summary>
        internal void SetShippingBoxRenderer(SpriteRenderer sr) => _shippingBoxSr = sr;

        // ---------- feature restore / render ----------
        public void RestoreFeatures(LocationData loc)
        {
            foreach (var hd in loc.hoeDirts)
            {
                if (!InBounds(hd.x, hd.y)) { _outOfBoundsHoeDirts.Add(hd); continue; }
                var pos = new Vector2Int(hd.x, hd.y);
                var dirt = new HoeDirt(hd.x, hd.y) { watered = hd.watered };
                if (hd.hasCrop)
                {
                    dirt.crop = new Crop(hd.cropId, hd.variant) { growthStage = hd.growthStage, dayCounter = hd.dayCounter };
                }
                hoeDirts[pos] = dirt;
            }
            RefreshAllSoil(); // 이웃 모양(오토타일)을 보려면 전부 채운 뒤에 그려야 한다
            foreach (var t in loc.trees)
            {
                // 맵 밖의 자리만 건너뛴다. 집 발판처럼 이미 막힌 칸이어도 그린다 — 집 뒤에 칠해 둔
                // 나무가 통째로 사라지지 않게. 베고 나서도 발판은 ClearFeatureBlock이 지켜 준다.
                if (!InBounds(t.x, t.y)) { _outOfBoundsTrees.Add(t); continue; }
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
                if (!InBounds(r.x, r.y)) { _outOfBoundsRocks.Add(r); continue; }
                var pos = new Vector2Int(r.x, r.y);
                rocks[pos] = new RockFeature(r.x, r.y, r.variant) { hp = r.hp };
                SetBlocked(r.x, r.y, true);
                RenderRock(pos);
            }

            RestorePlaced(loc);

            int outside = _outOfBoundsHoeDirts.Count + _outOfBoundsTrees.Count + _outOfBoundsRocks.Count;
            if (outside > 0)
            {
                Debug.Log($"[GameLocation] {id}: 시설 {outside}개가 지금 맵({width}x{height}) 밖이라 " +
                          "표시하지 않습니다. 저장 데이터에는 그대로 남아 있어서, 바닥을 더 칠해 맵을 넓히면 다시 나옵니다.");
            }
        }

        public void RenderHoeDirt(Vector2Int pos)
        {
            if (!hoeDirts.TryGetValue(pos, out var dirt)) return;

            bool autoTile = SoilTileset.Available;

            // soil renderer — 대지마법으로 경작된 땅(Tilled Soil)
            if (!_hoeRenderers.TryGetValue(pos, out var soilSr))
            {
                soilSr = PlaceTile(AssetLibrary.Tilled, pos.x, pos.y, Depth.Soil, _featureRoot);
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
                    wetSr = PlaceTile(null, pos.x, pos.y, Depth.WetSoil, _featureRoot);
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
                    cropSr = go.AddComponent<SpriteRenderer>();
                    cropSr.sortingOrder = Depth.YSort(pos.y, Depth.CropBias);
                    _cropRenderers[pos] = cropSr;
                }
                var cropSprite = dirt.crop.GetSprite();
                cropSr.sprite = cropSprite;
                // 단계마다 그림 높이가 달라질 수 있으므로(16px / 32px) 그릴 때마다 다시 맞춘다.
                cropSr.transform.position = new Vector3(pos.x, pos.y + 0.25f + CropSpriteLift(cropSprite), 0);
                cropSr.enabled = true;
            }
            else if (_cropRenderers.TryGetValue(pos, out var cropSr2))
            {
                cropSr2.enabled = false;
            }
        }

        /// <summary>
        /// 한 칸(16px)보다 높은 작물 그림을 얼마나 위로 올릴지. 스프라이트는 가운데를 기준으로
        /// 그려지므로, 그냥 두면 키 큰 단계일수록 밑동이 땅 밑으로 내려간다.
        /// </summary>
        private static float CropSpriteLift(Sprite sprite)
            => sprite == null ? 0f : (sprite.rect.height - 16f) / 32f;

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
                    ClearFeatureBlock(pos);
                }
                else
                {
                    sr.sprite = tree.GetSprite(); // 자라면서 그림이 바뀐다
                }
                return;
            }

            if (!alive) return;
            var created = PlaceObject(tree.GetSprite(), pos.x, pos.y + 0.6f, pos.y);
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
                    ClearFeatureBlock(pos);
                }
                return;
            }

            if (!alive) return;
            var created = PlaceObject(rock.GetSprite(), pos.x, pos.y, pos.y);
            _rockRenderers[pos] = created;
        }

        // ---------- gameplay actions ----------
        /// <summary>지금 이 칸을 경작할 수 있는지 (실제로 갈지는 않는다 — 조준 표시가 쓴다).</summary>
        public bool CanTill(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            if (IsBlocked(x, y)) return false;
            if (!IsTillable(x, y)) return false;    // 경작 마스크 밖이거나 원래 못 가는 위치
            if (hoeDirts.ContainsKey(pos)) return false;
            if (trees.ContainsKey(pos)) return false;
            return true;
        }

        public bool Till(int x, int y)
        {
            if (!CanTill(x, y)) return false;
            var pos = new Vector2Int(x, y);
            hoeDirts[pos] = new HoeDirt(x, y);
            RefreshSoilAround(pos);
            return true;
        }

        public bool Plant(int x, int y, string cropId)
        {
            var pos = new Vector2Int(x, y);
            if (!hoeDirts.TryGetValue(pos, out var d)) return false;
            var def = CropDatabase.Get(cropId);
            if (def != null && !Seasons.AllowsNow(def.seasons)) return false;   // 계절이 안 맞는다
            if (!d.Plant(cropId)) return false;
            RenderHoeDirt(pos);
            return true;
        }

        /// <summary>지금 이 칸에 물을 줄 수 있는지 (경작된 땅이고 아직 안 젖었을 때).</summary>
        public bool CanWater(int x, int y)
            => hoeDirts.TryGetValue(new Vector2Int(x, y), out var d) && !d.watered;

        public bool Water(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            if (!CanWater(x, y)) return false;
            hoeDirts[pos].Water();
            RefreshSoilAround(pos);
            return true;
        }

        /// <summary>지금 이 칸에 씨앗을 심을 수 있는지 (경작된 땅이고 아직 아무것도 안 심었을 때).</summary>
        public bool CanPlant(int x, int y)
            => hoeDirts.TryGetValue(new Vector2Int(x, y), out var d) && !d.HasCrop;

        /// <summary>그 작물을 지금 계절에 이 칸에 심을 수 있는지.</summary>
        public bool CanPlant(int x, int y, string cropId)
        {
            if (!CanPlant(x, y)) return false;
            var def = CropDatabase.Get(cropId);
            return def != null && Seasons.AllowsNow(def.seasons);
        }

        /// <summary>지금 이 칸에 벨 수 있는 나무가 있는지.</summary>
        public bool HasChoppableTree(int x, int y)
            => trees.TryGetValue(new Vector2Int(x, y), out var t) && t.IsAlive;

        /// <summary>지금 이 칸에 부술 수 있는 바위가 있는지.</summary>
        public bool HasBreakableRock(int x, int y)
            => rocks.TryGetValue(new Vector2Int(x, y), out var r) && r.IsAlive;

        /// <summary>이 칸에 자란 작물을 스치고 지나간다 — 잎이 잠깐 흔들린다.</summary>
        public void BrushCropAt(Vector2Int pos)
        {
            if (_cropRenderers.TryGetValue(pos, out var sr) && sr.enabled)
                Wobble.Play(sr, 6f, 0.3f, 2.5f);
        }

        /// <summary>NPC가 서 있는 칸은 지나갈 수 없게 막는다.</summary>
        public void SetNpcBlocked(Vector2Int tile) => SetBlocked(tile.x, tile.y, true);

        /// <summary>
        /// 문으로 드나들었을 때 내려설 칸 — 문 바로 위/아래 중 걸을 수 있는 쪽.
        /// 집을 어디에 놓든(마커로 옮기든, 맵이 줄어들든) 출입구 좌표를 따로 적어 둘 필요가 없다.
        /// </summary>
        public Vector2 DoorEntryTile
        {
            get
            {
                if (!doorExitTile.HasValue) return new Vector2(width / 2f, height / 2f);
                var d = doorExitTile.Value;
                if (!IsBlocked(d.x, d.y + 1)) return new Vector2(d.x, d.y + 1);   // 실내: 문 위쪽
                if (!IsBlocked(d.x, d.y - 1)) return new Vector2(d.x, d.y - 1);   // 실외: 집 아래쪽
                return new Vector2(d.x, d.y);
            }
        }

        /// <summary>
        /// 스폰하려는 자리가 막혀 있으면(맵이 줄어들어 벽/테두리 속이 된 경우 등) 가장 가까운
        /// 걸을 수 있는 칸을 찾아 준다. 안 그러면 플레이어가 벽 안에서 영영 못 움직인다.
        /// 원래 자리가 멀쩡하면 소수점 위치까지 그대로 돌려준다.
        /// </summary>
        public Vector2 FindWalkableNear(Vector2 wanted)
        {
            int wx = Mathf.RoundToInt(wanted.x), wy = Mathf.RoundToInt(wanted.y);
            if (InBounds(wx, wy) && !IsBlocked(wx, wy)) return wanted;

            int sx = Mathf.Clamp(wx, 0, Mathf.Max(0, width - 1));
            int sy = Mathf.Clamp(wy, 0, Mathf.Max(0, height - 1));

            int maxR = Mathf.Max(width, height);
            for (int r = 0; r <= maxR; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue; // 정사각 테두리만
                        int x = sx + dx, y = sy + dy;
                        if (IsBlocked(x, y)) continue;
                        Debug.Log($"[GameLocation] {id}: 스폰 지점 ({wanted.x}, {wanted.y})이 막혀 있어 " +
                                  $"({x}, {y})로 옮겼습니다.");
                        return new Vector2(x, y);
                    }

            Debug.LogWarning($"[GameLocation] {id}: 걸을 수 있는 칸을 찾지 못했습니다. 맵이 너무 작거나 전부 막혀 있습니다.");
            return wanted;
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
        /// 아직 다 자라지 않은 나무는 한 번에 뽑히고 아무것도 남기지 않는다.
        /// </summary>
        public bool ChopTree(int x, int y, out bool destroyed, out string dropTableId)
        {
            destroyed = false;
            dropTableId = null;
            if (!HasChoppableTree(x, y)) return false;
            var pos = new Vector2Int(x, y);
            var t = trees[pos];

            bool wasMature = t.IsMature;
            destroyed = t.Chop();

            // 아직 안 쓰러졌으면 휘청인다 (쓰러지면 그림 자체가 사라지므로 흔들 것이 없다)
            if (!destroyed && _treeRenderers.TryGetValue(pos, out var treeSr))
                Wobble.Play(treeSr, 8f, 0.35f);

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
            if (!HasBreakableRock(x, y)) return false;
            var pos = new Vector2Int(x, y);

            broken = rocks[pos].Hit();

            if (!broken && _rockRenderers.TryGetValue(pos, out var rockSr))
                Wobble.Play(rockSr, 5f, 0.22f, 4f);   // 바위는 짧고 뻣뻣하게

            if (broken)
            {
                dropTableId = RockFeature.DropTableId;
                rocks.Remove(pos);
                RenderRock(pos);
            }
            return true;
        }

        /// <summary>빈 땅에 나무 씨앗을 심는다.</summary>
        /// <summary>빈 땅에 나무 씨앗을 심을 수 있는지 (조준 표시가 쓴다).</summary>
        public bool CanPlantTree(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            if (!InBounds(x, y) || IsBlocked(x, y)) return false;
            return !trees.ContainsKey(pos) && !rocks.ContainsKey(pos) && !hoeDirts.ContainsKey(pos);
        }

        public bool PlantTree(int x, int y, string treeId)
        {
            var pos = new Vector2Int(x, y);
            if (!CanPlantTree(x, y)) return false;

            trees[pos] = new TreeFeature(x, y, treeId, 0);
            SetBlocked(x, y, true);
            RenderTree(pos);
            return true;
        }

        internal static void AddDefaultTree(LocationData loc, int x, int y)
            => AddTree(loc, x, y, TreeDatabase.DefaultTreeId);

        internal static void AddTree(LocationData loc, int x, int y, string treeId)
        {
            var def = TreeDatabase.Get(treeId);
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
                    dayCounter = d.HasCrop ? d.crop.dayCounter : 0,
                    variant = d.HasCrop ? d.crop.variant : 0
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

            // 맵 밖이라 못 그렸던 것들을 되돌려 놓는다 (저장에서 사라지지 않게).
            loc.hoeDirts.AddRange(_outOfBoundsHoeDirts);
            loc.trees.AddRange(_outOfBoundsTrees);
            loc.rocks.AddRange(_outOfBoundsRocks);

            SavePlacedInto(loc);

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
