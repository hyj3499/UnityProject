using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// 굵기가 계속 변하는 구불구불한 흙길과, 둥그스름한 공터를 타일맵에 자동으로 깔아 주는 도구.
    ///
    /// Tileset_spring 시트의 [행 0~2 / 열 8~16] 블록(3x9칸)이 코너 기반(wang/blob) 오토타일이다.
    /// 타일 한 장의 정체성은 그 타일의 네 꼭짓점(TL,TR,BL,BR)이 각각 흙인지 잔디인지로 정해진다.
    /// 그래서 맵을 "타일 격자"가 아니라 그보다 한 칸 큰 "꼭짓점 격자"에 그린 뒤,
    /// 각 타일이 자기 네 꼭짓점 조합에 맞는 그림을 골라 쓰면 경계가 저절로 이어진다.
    ///
    /// 반전·회전·합성은 쓰지 않는다. 시트에 실제로 그려진 조각만 그대로 놓는다.
    /// 시트에 없는 조합은 아래 EmptyCodes 설명 참고.
    /// </summary>
    internal class RoadPainter : EditorWindow
    {
        private const string MenuRoot = "Tools/FarmMVP/";
        private const string TileFolder = "Assets/Resources/Sprites/Tiles/Tileset";
        private const string SheetName = "Tileset_spring";
        private const int SheetCols = 28;          // Tileset_spring 한 줄의 칸 수 (448/16)

        /// <summary>기본 대상 — 없으면 만들어 준다. 여기에 그려 두고 복사해서 쓰면 된다.</summary>
        private const string TestGridName = "test";
        private const string TestMapName = "test_location";

        /// <summary>
        /// 코너 코드 -> 블록 안에서의 (행, 열). 코드 비트는 TL,TR,BL,BR 순서이고 1이 흙이다.
        ///
        ///   0001/0010/0100/1000  볼록 코너 — 귀퉁이 하나만 흙 (길이 뾰족하게 시작/끝나는 곳)
        ///   0011/1100/0101/1010  변      — 귀퉁이 둘이 흙 (길의 곧은 가장자리)
        ///   0111/1011/1101/1110  오목 코너 — 귀퉁이 하나만 잔디 (길이 꺾이는 안쪽)
        ///   1111                 흙 채움   — 길 한가운데
        ///
        /// 여러 개 적힌 코드는 같은 역할의 그림이 여러 장이라는 뜻이고, 매번 무작위로 골라 쓴다.
        /// </summary>
        private static readonly Dictionary<int, (int row, int col)[]> BlockCells =
            new Dictionary<int, (int, int)[]>
            {
                { 0x1, new[] { (0, 0) } },                                    // 0001
                { 0x2, new[] { (0, 3), (1, 5) } },                            // 0010
                { 0x4, new[] { (2, 0) } },                                    // 0100
                { 0x8, new[] { (2, 3) } },                                    // 1000
                { 0x3, new[] { (0, 1), (1, 6), (1, 7) } },                    // 0011
                { 0xC, new[] { (0, 5), (0, 6), (0, 7), (2, 1) } },            // 1100
                { 0x5, new[] { (1, 0) } },                                    // 0101
                { 0xA, new[] { (1, 3) } },                                    // 1010
                { 0x7, new[] { (0, 2), (1, 8) } },                            // 0111
                { 0xB, new[] { (1, 4) } },                                    // 1011
                { 0xD, new[] { (0, 8), (2, 2) } },                            // 1101
                { 0xE, new[] { (0, 4) } },                                    // 1110
                { 0xF, new[] { (1, 1), (1, 2), (2, 4) } },                    // 1111
            };

        // 시트에 그림이 없는 조합:
        //   0000  길에서 먼 순수 잔디 — 그릴 것이 없으므로 그 칸은 타일을 놓지 않고 비워 둔다.
        //         (밑에 깔린 잔디 레이어가 그대로 보이면 된다.)
        //   0110/1001  마주보는 귀퉁이 둘만 흙인 대각 — 폭이 있는 하나의 덩어리에서는 나올 수 없는
        //         모양이라 애초에 그리지 않는다. FixDiagonals()가 꼭짓점 하나를 채워서 없앤다.

        private enum Shape { Road, Clearing }

        /// <summary>길이 어느 쪽으로 이어질지. 사행은 항상 이 방향과 직각으로 흔들린다.</summary>
        private enum Direction { Horizontal, Vertical }

        private Tilemap _target;
        private Shape _shape = Shape.Road;
        private int _seed = 7;
        private int _w = 48;
        private int _h = 32;
        private Vector3Int _origin = Vector3Int.zero;
        private bool _clearFirst = true;

        // 길
        private Direction _direction = Direction.Horizontal;
        private bool _useEndPoints;
        private Vector2 _startPoint = new Vector2(0f, 0f);
        private Vector2 _endPoint = new Vector2(48f, 32f);
        private float _roadWidthMin = 2f;
        private float _roadWidthMax = 5f;
        private float _meander = 1f;      // 0이면 거의 직선, 1이면 위아래로 크게 사행

        // 공터
        private float _clearingRadius = 9f;
        private float _clearingWobble = 0.28f;

        // 시트 안에서 오토타일 블록이 차지하는 칸 범위. 시트를 고치면 여기만 바꾸면 된다.
        private const int BlockRows = 3, BlockCols = 9;   // BlockCells 표가 전제하는 블록 크기
        private int _blockRow0 = 0, _blockRow1 = 2;
        private int _blockCol0 = 8, _blockCol1 = 16;

        private string _status = "";

        [MenuItem(MenuRoot + "길·공터 그리기", false, 20)]
        private static void Open()
        {
            GetWindow<RoadPainter>("길·공터 그리기").minSize = new Vector2(360f, 460f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);
            _target = (Tilemap)EditorGUILayout.ObjectField("타일맵", _target, typeof(Tilemap), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"{TestMapName} 쓰기 (없으면 만듦)"))
                    _target = FindOrCreateTestTilemap();
                using (new EditorGUI.DisabledScope(Selection.activeGameObject == null ||
                                                   Selection.activeGameObject.GetComponent<Tilemap>() == null))
                {
                    if (GUILayout.Button("선택한 것 쓰기"))
                        _target = Selection.activeGameObject.GetComponent<Tilemap>();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("모양", EditorStyles.boldLabel);
            _shape = (Shape)EditorGUILayout.EnumPopup("종류", _shape);
            using (new EditorGUILayout.HorizontalScope())
            {
                _seed = EditorGUILayout.IntField("시드", _seed);
                if (GUILayout.Button("새로", GUILayout.Width(50f)))
                    _seed = UnityEngine.Random.Range(0, int.MaxValue);
            }
            _w = Mathf.Max(4, EditorGUILayout.IntField("가로 칸 수", _w));
            _h = Mathf.Max(4, EditorGUILayout.IntField("세로 칸 수", _h));
            _origin = EditorGUILayout.Vector3IntField("시작 칸(왼쪽 아래)", _origin);

            EditorGUILayout.Space();
            if (_shape == Shape.Road)
            {
                EditorGUILayout.LabelField("길 설정", EditorStyles.boldLabel);
                _useEndPoints = EditorGUILayout.Toggle("시작/끝점 직접 지정", _useEndPoints);
                if (_useEndPoints)
                {
                    _startPoint = EditorGUILayout.Vector2Field("시작점(칸)", _startPoint);
                    _endPoint = EditorGUILayout.Vector2Field("끝점(칸)", _endPoint);
                    if (GUILayout.Button("지금 영역의 모서리끼리 잇기"))
                    {
                        _startPoint = Vector2.zero;
                        _endPoint = new Vector2(_w, _h);
                    }
                    EditorGUILayout.HelpBox(
                        $"영역 왼쪽 아래가 (0,0), 오른쪽 위가 ({_w},{_h}) 입니다. " +
                        "두 점을 잇는 선을 따라 길이 나고, 양 끝에서는 흔들림이 0이 되어 지정한 점에 정확히 닿습니다.",
                        MessageType.None);
                }
                else
                {
                    _direction = (Direction)EditorGUILayout.EnumPopup("방향", _direction);
                }
                _roadWidthMin = EditorGUILayout.Slider("가장 가는 굵기(칸)", _roadWidthMin, 1f, 10f);
                _roadWidthMax = EditorGUILayout.Slider("가장 굵은 굵기(칸)", _roadWidthMax, 1f, 14f);
                if (_roadWidthMax < _roadWidthMin) _roadWidthMax = _roadWidthMin;
                _meander = EditorGUILayout.Slider("구불거림", _meander, 0f, 1.5f);
            }
            else
            {
                EditorGUILayout.LabelField("공터 설정", EditorStyles.boldLabel);
                _clearingRadius = EditorGUILayout.Slider("반지름(칸)", _clearingRadius, 2f, 30f);
                _clearingWobble = EditorGUILayout.Slider("울퉁불퉁함", _clearingWobble, 0f, 0.6f);
            }

            EditorGUILayout.Space();
            _clearFirst = EditorGUILayout.Toggle("그리기 전에 그 영역 비우기", _clearFirst);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"타일 영역 ({SheetName})", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("행", GUILayout.Width(20f));
                _blockRow0 = EditorGUILayout.IntField(_blockRow0);
                EditorGUILayout.LabelField("~", GUILayout.Width(12f));
                _blockRow1 = EditorGUILayout.IntField(_blockRow1);
                EditorGUILayout.LabelField("열", GUILayout.Width(20f));
                _blockCol0 = EditorGUILayout.IntField(_blockCol0);
                EditorGUILayout.LabelField("~", GUILayout.Width(12f));
                _blockCol1 = EditorGUILayout.IntField(_blockCol1);
            }

            int rows = _blockRow1 - _blockRow0 + 1, cols = _blockCol1 - _blockCol0 + 1;
            bool blockOk = rows == BlockRows && cols == BlockCols &&
                           _blockRow0 >= 0 && _blockCol0 >= 0 && _blockCol1 < SheetCols;
            if (blockOk)
            {
                EditorGUILayout.HelpBox(
                    $"[행 {_blockRow0}~{_blockRow1} / 열 {_blockCol0}~{_blockCol1}] " +
                    $"{rows}x{cols} 블록을 오토타일로 씁니다.\n" +
                    "길에서 먼 칸(순수 잔디)은 시트에 조각이 없어 비워 둡니다 — 잔디 레이어 위에 겹쳐 쓰세요.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"블록은 정확히 {BlockRows}행 x {BlockCols}열이어야 합니다 (지금 {rows}x{cols}). " +
                    $"열은 0~{SheetCols - 1} 안이어야 합니다.\n" +
                    "칸 배치는 그대로 두고 위치만 옮겨졌을 때 범위를 바꿔 주세요.",
                    MessageType.Error);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_target == null || !blockOk))
            {
                if (GUILayout.Button("그리기", GUILayout.Height(30f))) Paint();
            }
            using (new EditorGUI.DisabledScope(_target == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("그 영역만 지우기")) ClearArea();
                    if (GUILayout.Button("타일맵 전체 지우기")) ClearAll();
                }
            }
            if (_target == null)
                EditorGUILayout.HelpBox("타일맵을 지정하세요.", MessageType.Warning);

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        // ------------------------------------------------------------------ 그리기

        private void Paint()
        {
            var tiles = LoadTiles(out string error);
            if (tiles == null)
            {
                _status = error;
                Debug.LogError("[RoadPainter] " + error);
                return;
            }

            var rng = new System.Random(_seed);
            bool[,] mask = _shape == Shape.Road ? RoadMask(rng) : ClearingMask(rng);
            FixDiagonals(mask);

            Undo.RegisterCompleteObjectUndo(_target, "길·공터 그리기");

            int painted = 0, empty = 0;
            for (int j = 0; j < _h; j++)
            {
                for (int i = 0; i < _w; i++)
                {
                    // 마스크의 j는 위에서 아래로 세지만 타일맵의 y는 위로 올라간다.
                    var pos = new Vector3Int(_origin.x + i, _origin.y + (_h - 1 - j), _origin.z);
                    int code = CodeAt(mask, i, j);

                    if (code == 0)
                    {
                        if (_clearFirst) _target.SetTile(pos, null);
                        empty++;
                        continue;
                    }

                    if (!tiles.TryGetValue(code, out var variants))
                        variants = tiles[0xF];     // 0110/1001 안전장치 (정상 경로에선 안 옴)

                    _target.SetTile(pos, variants[rng.Next(variants.Length)]);
                    painted++;
                }
            }

            EditorSceneManager.MarkSceneDirty(_target.gameObject.scene);
            _status = $"{painted}칸 깔았습니다 (잔디라 비워 둔 칸 {empty}개). 시드 {_seed}.";
            Debug.Log($"[RoadPainter] {_target.name}: " + _status);
        }

        private void ClearArea()
        {
            Undo.RegisterCompleteObjectUndo(_target, "영역 지우기");
            for (int j = 0; j < _h; j++)
                for (int i = 0; i < _w; i++)
                    _target.SetTile(new Vector3Int(_origin.x + i, _origin.y + (_h - 1 - j), _origin.z), null);

            EditorSceneManager.MarkSceneDirty(_target.gameObject.scene);
            _status = $"{_w}x{_h} 영역을 비웠습니다.";
        }

        /// <summary>설정한 영역 밖까지 포함해, 그 타일맵에 남아 있는 것을 전부 지운다.</summary>
        private void ClearAll()
        {
            var bounds = _target.cellBounds;
            int count = 0;
            foreach (var pos in bounds.allPositionsWithin)
                if (_target.GetTile(pos) != null) count++;

            if (count == 0)
            {
                _status = $"{_target.name}에는 이미 아무것도 없습니다.";
                return;
            }
            if (!EditorUtility.DisplayDialog("타일맵 전체 지우기",
                    $"\"{_target.name}\"에 깔린 {count}칸을 전부 지웁니다.\n(Ctrl+Z로 되돌릴 수 있습니다.)",
                    "지우기", "취소"))
                return;

            Undo.RegisterCompleteObjectUndo(_target, "타일맵 전체 지우기");
            _target.ClearAllTiles();
            _target.CompressBounds();

            EditorSceneManager.MarkSceneDirty(_target.gameObject.scene);
            _status = $"{_target.name}의 {count}칸을 전부 지웠습니다.";
            Debug.Log("[RoadPainter] " + _status);
        }

        // ------------------------------------------------------------------ 타일 읽기

        /// <summary>코너 코드 -> 쓸 수 있는 타일들. 하나라도 없으면 null을 주고 error에 이유를 담는다.</summary>
        private Dictionary<int, TileBase[]> LoadTiles(out string error)
        {
            var result = new Dictionary<int, TileBase[]>();
            var missing = new List<string>();

            foreach (var pair in BlockCells)
            {
                var list = new List<TileBase>();
                foreach (var cell in pair.Value)
                {
                    int index = (_blockRow0 + cell.row) * SheetCols + (_blockCol0 + cell.col);
                    string path = $"{TileFolder}/{SheetName}_{index}.asset";
                    var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile == null) missing.Add(path);
                    else list.Add(tile);
                }
                if (list.Count > 0) result[pair.Key] = list.ToArray();
            }

            if (missing.Count > 0)
            {
                error = "타일 에셋을 찾지 못했습니다:\n  " + string.Join("\n  ", missing);
                return null;
            }
            if (!result.ContainsKey(0xF))
            {
                error = "흙 채움(1111) 타일이 없습니다. 시트 블록 위치를 확인하세요.";
                return null;
            }
            error = null;
            return result;
        }

        // ------------------------------------------------------------------ 모양 만들기

        /// <summary>사인파 몇 개를 겹쳐 만드는 부드러운 흔들림.</summary>
        private struct Wave
        {
            public double Amp, Freq, Phase;
        }

        private static Wave[] MakeWaves(System.Random rng, double[] amps, double[] loF, double[] hiF)
        {
            var w = new Wave[amps.Length];
            for (int i = 0; i < amps.Length; i++)
            {
                w[i] = new Wave
                {
                    Amp = amps[i],
                    Freq = loF[i] + rng.NextDouble() * (hiF[i] - loF[i]),
                    Phase = rng.NextDouble() * Math.PI * 2.0,
                };
            }
            return w;
        }

        private static double Sum(Wave[] waves, double t)
        {
            double s = 0.0, norm = 0.0;
            foreach (var w in waves)
            {
                s += w.Amp * Math.Sin(Math.PI * 2.0 * w.Freq * t + w.Phase);
                norm += w.Amp;
            }
            return norm > 0.0 ? s / norm : 0.0;
        }

        /// <summary>
        /// 시작점에서 끝점까지 이어지면서, 그 직선과 직각으로 사행하고 굵기가 계속 변하는 길.
        /// 시작/끝점을 직접 주면 대각선이든 어떤 각도든 만들 수 있다.
        /// 마스크만 이 방향을 따를 뿐 타일 그림 자체는 늘 똑바로 선 채로 쓰인다.
        /// </summary>
        private bool[,] RoadMask(System.Random rng)
        {
            EndPoints(out double ax, out double ay, out double bx, out double by);

            var yWaves = MakeWaves(rng, new[] { 1.0, 0.42, 0.17 },
                                        new[] { 1.2, 2.6, 5.0 }, new[] { 1.9, 3.6, 6.6 });
            var wWaves = MakeWaves(rng, new[] { 1.0, 0.45, 0.25 },
                                        new[] { 2.0, 4.2, 8.0 }, new[] { 2.8, 5.4, 10.0 });
            var edge = MakeWaves(rng, new[] { 0.13, 0.13, 0.13 },
                                      new[] { 0.9, 0.9, 0.9 }, new[] { 1.6, 1.6, 1.6 });

            double dirX = bx - ax, dirY = by - ay;
            double len = Math.Sqrt(dirX * dirX + dirY * dirY);
            if (len < 1e-6) { dirX = 1.0; dirY = 0.0; len = 1.0; }
            double ux = dirX / len, uy = dirY / len;     // 길이 방향
            double px = -uy, py = ux;                    // 사행하는 직각 방향

            double amp = 0.2 * len * _meander;
            double hwMid = (_roadWidthMin + _roadWidthMax) * 0.25;   // 반지름(=굵기/2)의 가운데값
            double hwAmp = (_roadWidthMax - _roadWidthMin) * 0.25;
            double hwLo = _roadWidthMin * 0.5, hwHi = _roadWidthMax * 0.5;
            // 끝점을 직접 준 경우엔 영역 가장자리까지 쓸 수 있어야 하니 여백을 두지 않는다.
            double margin = _useEndPoints ? 0.0 : 2.2;

            // 중심선을 촘촘히 샘플링해 둔다. (x, y, 반지름)
            var pts = new List<(double x, double y, double hw)>();
            int steps = Math.Max(64, (int)(len / 0.05));
            for (int s = 0; s <= steps; s++)
            {
                double t = (double)s / steps;
                double baseX = ax + ux * (t * len);
                double baseY = ay + uy * (t * len);

                // 끝점을 직접 줬으면 양 끝에서 사행을 0으로 줄여, 정확히 그 점에서 시작하고 끝나게 한다.
                double taper = _useEndPoints ? Math.Pow(Math.Sin(Math.PI * t), 0.6) : 1.0;
                double off = amp * Sum(yWaves, t) * taper;
                off = ClampOffset(off, baseX, baseY, px, py, margin);

                double hw = hwMid + hwAmp * Sum(wWaves, t);
                hw = Math.Min(Math.Max(hw, hwLo), hwHi);
                pts.Add((baseX + px * off, baseY + py * off, hw));
            }

            var mask = new bool[_w + 1, _h + 1];
            for (int i = 0; i <= _w; i++)
            {
                for (int j = 0; j <= _h; j++)
                {
                    foreach (var p in pts)
                    {
                        double dx = i - p.x;
                        if (dx * dx > 64.0) continue;          // 넉넉한 조기 컷
                        double dy = j - p.y;
                        double d = Math.Sqrt(dx * dx + dy * dy);
                        // 가장자리를 살짝 울퉁불퉁하게 흔든다.
                        double wob = SumAngle(edge, Math.Atan2(dy, dx));
                        if (d <= p.hw + wob) { mask[i, j] = true; break; }
                    }
                }
            }
            return mask;
        }

        /// <summary>
        /// 길의 시작점과 끝점. 마스크 좌표계(왼쪽 위가 0,0이고 y가 아래로 증가) 기준이다.
        /// 직접 지정하지 않았으면 방향에 맞춰 영역을 처음부터 끝까지 가로지른다.
        /// </summary>
        private void EndPoints(out double ax, out double ay, out double bx, out double by)
        {
            if (_useEndPoints)
            {
                // 창에서는 영역 왼쪽 아래를 (0,0)으로 보고 y가 위로 가므로 여기서 뒤집는다.
                ax = _startPoint.x; ay = _h - _startPoint.y;
                bx = _endPoint.x;   by = _h - _endPoint.y;
            }
            else if (_direction == Direction.Horizontal)
            {
                ax = -2.5; ay = _h * 0.5;
                bx = _w + 2.5; by = _h * 0.5;
            }
            else
            {
                ax = _w * 0.5; ay = -2.5;
                bx = _w * 0.5; by = _h + 2.5;
            }
        }

        /// <summary>
        /// 사행이 영역 밖으로 길을 밀어내지 않도록 직각 방향 이동량을 제한한다.
        /// 기준점이 이미 영역 밖이면(가장자리 너머로 뻗은 구간) 제한하지 않는다.
        /// </summary>
        private double ClampOffset(double off, double baseX, double baseY,
                                   double px, double py, double margin)
        {
            double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
            NarrowRange(baseX, px, margin, _w - margin, ref lo, ref hi);
            NarrowRange(baseY, py, margin, _h - margin, ref lo, ref hi);
            if (lo > hi) return off;
            return Math.Min(Math.Max(off, lo), hi);
        }

        /// <summary>min &lt;= b + p*off &lt;= max 를 만족하는 off의 범위로 [lo, hi]를 좁힌다.</summary>
        private static void NarrowRange(double b, double p, double min, double max,
                                        ref double lo, ref double hi)
        {
            if (Math.Abs(p) < 1e-9) return;              // 이 축으로는 안 움직이므로 제한 없음
            double t1 = (min - b) / p, t2 = (max - b) / p;
            lo = Math.Max(lo, Math.Min(t1, t2));
            hi = Math.Min(hi, Math.Max(t1, t2));
        }

        /// <summary>가운데가 둥그스름하게 뚫린 공터. 반지름이 각도에 따라 조금씩 달라 원이 티나지 않는다.</summary>
        private bool[,] ClearingMask(System.Random rng)
        {
            // 각도로 한 바퀴 돌았을 때 값이 딱 맞아떨어지도록 정수 배수 주파수만 쓴다.
            // 2배수(길쭉하게 찌그러지는 성분)를 약하게 둬야 땅콩 모양으로 갈라지지 않는다.
            var lobes = new Wave[3];
            int[] harmonics = { 2, 3, 5 };
            double[] amps = { 0.45, 0.8, 0.5 };
            for (int i = 0; i < lobes.Length; i++)
            {
                lobes[i] = new Wave
                {
                    Amp = amps[i],
                    Freq = harmonics[i],
                    Phase = rng.NextDouble() * Math.PI * 2.0,
                };
            }

            double cx = _w * 0.5, cy = _h * 0.5;
            // 울퉁불퉁함까지 더해져도 영역 밖으로 삐져나가지 않도록 반지름 상한을 잡는다.
            double maxR = (Math.Min(_w, _h) * 0.5 - 1.5) / (1.0 + _clearingWobble);
            double baseR = Math.Min(_clearingRadius, Math.Max(2.0, maxR));

            var mask = new bool[_w + 1, _h + 1];
            for (int i = 0; i <= _w; i++)
            {
                for (int j = 0; j <= _h; j++)
                {
                    double dx = i - cx, dy = j - cy;
                    double ang = Math.Atan2(dy, dx);
                    double r = baseR * (1.0 + _clearingWobble * SumAngle(lobes, ang));
                    mask[i, j] = dx * dx + dy * dy <= r * r;
                }
            }
            return mask;
        }

        /// <summary>각도를 그대로 넣어 쓰는 흔들림 (한 바퀴 기준).</summary>
        private static double SumAngle(Wave[] waves, double angle)
        {
            double s = 0.0;
            foreach (var w in waves) s += w.Amp * Math.Sin(w.Freq * angle + w.Phase);
            return s;
        }

        // ------------------------------------------------------------------ 코너 격자

        /// <summary>타일 (i,j)를 둘러싼 네 꼭짓점을 TL,TR,BL,BR 순서의 4비트로 만든다.</summary>
        private static int CodeAt(bool[,] mask, int i, int j)
        {
            int tl = mask[i, j] ? 1 : 0;
            int tr = mask[i + 1, j] ? 1 : 0;
            int bl = mask[i, j + 1] ? 1 : 0;
            int br = mask[i + 1, j + 1] ? 1 : 0;
            return (tl << 3) | (tr << 2) | (bl << 1) | br;
        }

        /// <summary>
        /// 마주보는 귀퉁이 둘만 흙인 칸(0110/1001)을 없앤다. 그런 그림은 시트에 없고,
        /// 실제로도 덩어리 두 개가 점 하나로만 닿는 이상한 모양이라 꼭짓점 하나를 채워 붙여 버린다.
        /// </summary>
        private void FixDiagonals(bool[,] mask)
        {
            for (int pass = 0; pass < 12; pass++)
            {
                bool changed = false;
                for (int i = 0; i < _w; i++)
                {
                    for (int j = 0; j < _h; j++)
                    {
                        int c = CodeAt(mask, i, j);
                        if (c == 0x9) { mask[i + 1, j] = true; changed = true; }        // 1001 -> 1101
                        else if (c == 0x6) { mask[i, j] = true; changed = true; }       // 0110 -> 1110
                    }
                }
                if (!changed) break;
            }
        }

        // ------------------------------------------------------------------ 기본 타일맵

        private static Tilemap FindOrCreateTestTilemap()
        {
            var gridGo = GameObject.Find(TestGridName);
            if (gridGo == null)
            {
                gridGo = new GameObject(TestGridName);
                gridGo.transform.position = new Vector3(0f, -60f, 0f);   // 다른 맵과 겹치지 않게
                Undo.RegisterCreatedObjectUndo(gridGo, "test 만들기");
            }
            if (gridGo.GetComponent<Grid>() == null)
                gridGo.AddComponent<Grid>().cellSize = new Vector3(1f, 1f, 0f);

            var found = gridGo.transform.Find(TestMapName);
            if (found != null)
            {
                var existing = found.GetComponent<Tilemap>();
                if (existing != null) return existing;
            }

            var go = new GameObject(TestMapName);
            go.transform.SetParent(gridGo.transform, false);
            var map = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = Depth.PaintedGround;
            Undo.RegisterCreatedObjectUndo(go, "test_location 만들기");
            EditorSceneManager.MarkSceneDirty(go.scene);
            return map;
        }
    }
}
