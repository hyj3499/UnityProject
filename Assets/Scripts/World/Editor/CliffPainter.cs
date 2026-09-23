using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// Tileset_cliff_spring 시트로 절벽(고지대)을 타일맵에 깔아 주는 도구.
    ///
    /// 쓰는 법은 두 가지다.
    ///   1) 자동 생성 — 구불구불한 절벽 띠(ridge)나 흩어진 고원 덩어리(blobs)를 만들어 준다.
    ///   2) 칠한 모양 따라가기 — "test_cliff" 타일맵에 아무 타일이나 칠해서 고지대 모양을 잡아 두면
    ///      그 모양대로 가장자리·모서리·벽을 골라 "test_cliff_out" 에 완성해 준다.
    ///
    /// 원칙은 RoadPainter 와 같다 — 시트에 그려진 조각을 그대로 놓는다. 반전도 회전도 없다.
    /// 딱 하나 예외가 볼록 모서리 두 장인데, 맞는 조각이 시트에 없어서 합성한다(Synth 표 참고).
    /// 원본 그림은 건드리지 않고 Tileset_cliff_spring_auto.png 사본에만 넣는다.
    ///
    /// ---------------------------------------------------------------------------------
    /// 시트 분석 — 이 시트는 오토타일 표가 아니라 "예시 지형을 통째로 그려 놓은 그림"이다.
    /// ---------------------------------------------------------------------------------
    /// (A) 0~10행 x 0~5열 : 위쪽 큰 고지대에서 남쪽으로 튀어나온 폭 4칸짜리 "혀" 모양.
    ///       1열 0~5행  : 서쪽 가장자리(잔디 + 왼쪽 금빛 테두리) 6종. 0~2행은 왼쪽에 0열
    ///                    바위벽이 붙는 자리라 끝까지 불투명한 그늘이 칠해져 있고, 3~5행은
    ///                    옆이 빈 자리라 반투명 그림자로 끝난다(끝 열 불투명 픽셀 16,16,16 / 8,6,4).
    ///                    섞어 쓰면 벽도 없는데 굵은 갈색 띠가 튀어나와 보인다.
    ///       4열 0~5행  : 동쪽 가장자리 6종 (같은 식으로 두 벌)
    ///       (6,1)/(6,4): 남쪽 끝에서 테두리가 안으로 말리는 좌/우 아래 모서리
    ///       1~4열 7~10행: 남쪽 절벽면(벽) 4칸 x 4장.
    ///                    7행=벽 윗단(잔디가 살짝 넘어옴) 8,9행=중간 10행=바닥(자갈+그림자)
    ///                    1열=왼쪽 끝 2,3열=중간(좌우로 이어짐) 4열=오른쪽 끝
    ///       0열/5열 0~3행: 시트 밖에서 내려온 또 다른 벽 한 줄. 좌우가 잘리지 않아 중간 벽으로
    ///                    그대로 재사용할 수 있고, 맨 위 칸엔 잔디가 조금 얹혀 있다.
    /// (B) 3~6행 x 2,3열 : 고지대 안쪽 빈 자리에 모아 둔 북쪽 가장자리 조각들.
    ///       (4,2),(6,3): 북쪽 가장자리. 위 몇 픽셀이 투명하고 그 아래로 금빛 테두리 + 잔디.
    ///                    (북쪽 절벽은 벽이 시야 반대쪽이라 테두리만 그린다)
    ///       (6,2),(4,3): 테두리가 위로 빠져나가는 칸 = 오목(안쪽) 모서리
    ///       (3,3),(5,2): 둥근 볼록 모서리. 옆면 그늘이 없어 그대로는 서/동 가장자리와 안 이어진다.
    ///
    /// 금빛 테두리가 칸 경계에서 실제로 이어지는지 픽셀로 확인했다. 서쪽 가장자리 6종은 모두
    /// 위쪽 x=2~3 / 아래쪽 x=2 에서 끝나 어떤 순서로 쌓아도 이어지고, 동쪽도 마찬가지다(위 12~13
    /// 아래 13). 계단식 경계는 E가장자리 -> (6,2) -> N가장자리 순으로 물린다.
    ///
    /// ---------------------------------------------------------------------------------
    /// 이어붙이는 규칙 (hi = 그 칸이 고지대인가)
    /// ---------------------------------------------------------------------------------
    ///   윗면 — n,w 낮음=NW / n,e 낮음=NE / n 낮음=북 / s,w 낮음=SW / s,e 낮음=SE /
    ///          w 낮음=서 / e 낮음=동 / nw만 낮음=오목NW / ne만 낮음=오목NE / 그 외=안쪽(비움).
    ///          "s 만 낮음"도 비운다 — 바로 아래 벽 윗단에 잔디가 넘어와 있어 그걸로 충분하다.
    ///   벽   — 같은 열에서 위로 d칸(1~4)이 고지대면 깊이 d의 벽. 좌우 이웃이 "벽도 고지대도
    ///          아닌" 경우에만 끝막이 조각을 쓴다. 벽 한 줄은 윗단/중간/중간/바닥 4장이 세트로
    ///          그려져 있어 세로로는 섞지 않는다.
    ///
    /// 시트에 조각이 없는 모양(폭·높이 1칸, 폭 1칸짜리 틈, nw·ne 동시에 낮은 오목 모서리,
    /// 남쪽 벽 자리 부족, 너무 작은 자투리)은 Repair() 가 "깎는 방향으로만" 정리한다.
    /// 그래서 반드시 끝난다.
    ///
    /// ---------------------------------------------------------------------------------
    /// 모양 쪽에서 신경 쓴 것
    /// ---------------------------------------------------------------------------------
    ///  - 경계선은 저주파 사인 노이즈로 흔들고, 그 위에 BumpEdges() 로 한 칸씩 더 흔든다
    ///    (울퉁불퉁함 0~1). 한 칸짜리 돌기는 쓸 조각이 없어 Repair 가 다시 깎으므로 실제로는
    ///    2칸 이상 폭의 돌기와 홈만 남고, 그게 딱 손으로 찍은 듯한 결이 된다.
    ///  - 반면 남쪽 경계는 FlattenBottom() 으로 몇 칸씩 평평하게 맞춘다. 벽은 열마다 4장이
    ///    통째로 내려오기 때문에, 남쪽이 한 칸씩 계단이 되면 폭 1칸짜리 벽이 서로 다른 높이로
    ///    촘촘히 늘어서 누더기처럼 보인다. ("남쪽 벽 한 단 폭" 을 1,1 로 두면 남쪽도 흔들린다)
    ///  - 벽 중간 조각은 4벌(2·3열 / 0열 / 5열)을 열마다 무작위로 고른다. 네 벌의 평균 밝기가
    ///    거의 같아서(깊이별 145/100/69/67) 섞어도 얼룩지지 않는다.
    /// </summary>
    internal class CliffPainter : EditorWindow
    {
        private const string MenuRoot = "Tools/FarmMVP/";
        private const string TilesFolder = "Assets/Resources/Sprites/Tiles";
        private const string TileAssetFolder = TilesFolder + "/Tileset";
        private const string SrcSheetPath = TilesFolder + "/Tileset_cliff_spring.png";
        private const string AutoSheetPath = TilesFolder + "/Tileset_cliff_spring_auto.png";
        private const string AutoSheetName = "Tileset_cliff_spring_auto";
        private const int SheetCols = 28;          // 448 / 16
        private const int Cell = 16;
        private const int WallH = 4;               // 절벽 벽면 높이(칸)

        private const string GridName = "test_cliff";
        private const string ShapeMapName = "test_cliff";        // 모양을 칠하는 곳
        private const string OutMapName = "test_cliff_out";      // 완성된 절벽이 나오는 곳

        // ------------------------------------------------------------------ 조각표

        // 윗면. 서/동 가장자리는 두 벌인데, 시트 0~2행은 옆에 바위벽이 붙는 자리라 끝까지
        // 불투명한 그늘이 칠해져 있고 3~5행은 반투명 그림자로 끝난다. 옆 칸이 벽일 때만 _WALL 쪽.
        private static readonly Dictionary<string, (int r, int c)[]> Top =
            new Dictionary<string, (int, int)[]>
            {
                { "W",      new[] { (3, 1), (4, 1), (5, 1) } },
                { "E",      new[] { (3, 4), (4, 4), (5, 4) } },
                { "W_WALL", new[] { (0, 1), (1, 1), (2, 1) } },
                { "E_WALL", new[] { (0, 4), (1, 4), (2, 4) } },
                { "N",      new[] { (4, 2), (6, 3) } },
                { "NW",     new[] { (12, 0) } },     // 합성 조각 — BuildAutoSheet() 참고
                { "NE",     new[] { (12, 1) } },
                { "SW",     new[] { (6, 1) } },
                { "SE",     new[] { (6, 4) } },
                { "IN_NW",  new[] { (4, 3) } },
                { "IN_NE",  new[] { (6, 2) } },
            };

        // 벽. 한 줄은 윗단/중간/중간/바닥 4장이 세트로 그려져 있어 세로로는 섞지 않는다.
        private static readonly Dictionary<string, (int r, int c)[][]> Wall =
            new Dictionary<string, (int, int)[][]>
            {
                { "L", new[] { new[] { (7, 1), (8, 1), (9, 1), (10, 1) } } },
                { "R", new[] { new[] { (7, 4), (8, 4), (9, 4), (10, 4) } } },
                { "M", new[]
                    {
                        new[] { (7, 2), (8, 2), (9, 2), (10, 2) },
                        new[] { (7, 3), (8, 3), (9, 3), (10, 3) },
                        new[] { (0, 0), (1, 0), (2, 0), (3, 0) },
                        new[] { (0, 5), (1, 5), (2, 5), (3, 5) },
                    } },
                // 폭 1칸 벽. 좌우를 다 막은 조각이 시트에 없어 양쪽이 잘리지 않은 줄로 대신한다.
                { "SOLO", new[]
                    {
                        new[] { (0, 0), (1, 0), (2, 0), (3, 0) },
                        new[] { (0, 5), (1, 5), (2, 5), (3, 5) },
                    } },
            };

        /// <summary>
        /// 합성해서 넣는 칸: 넣을 자리 -> (옆면 조각, 북쪽 조각, 둥근 모서리 조각).
        ///
        /// 시트에 있는 볼록 모서리는 (3,3)/(5,2) — 둥근 꼭대기 조각뿐인데
        /// 여기엔 절벽 옆면 그늘이 아예 없다(왼쪽 끝 열 불투명 0픽셀). 반면 바로 아래에 오는
        /// 서/동 가장자리에는 2~3픽셀 그늘이 있어서, 모서리 바로 밑에서 그늘이 뚝 생긴다.
        /// 튀어나온 부분이 안 맞아 보이는 게 이것 때문이다.
        /// 그래서 서(동) 가장자리를 북쪽 조각의 알파 모양대로 윗단만 잘라 내고 그 위에 둥근
        /// 모서리를 얹어, 그늘이 모서리부터 이어지는 조각을 한 장 만들어 쓴다.
        /// </summary>
        private static readonly Dictionary<(int r, int c), ((int r, int c) side, (int r, int c) north, (int r, int c) corner)> Synth =
            new Dictionary<(int, int), ((int, int), (int, int), (int, int))>
            {
                { (12, 0), ((4, 1), (6, 3), (3, 3)) },
                { (12, 1), ((4, 4), (4, 2), (5, 2)) },
            };

        private enum Mode { Auto, FollowPainted }
        private enum Shape { Ridge, Blobs }

        private Tilemap _outMap;
        private Tilemap _shapeMap;
        private Mode _mode = Mode.Auto;
        private Shape _shape = Shape.Ridge;

        private int _seed = 3;
        private int _w = 48;
        private int _h = 32;
        private Vector3Int _origin = Vector3Int.zero;
        private float _thickness = 7f;
        private int _blobs = 5;

        private float _bump = 0.4f;
        private int _stepMin = 3;
        private int _stepMax = 6;
        private bool _bumpPainted;
        private bool _flattenPainted = true;
        private bool _clearFirst = true;
        private bool _hideShapeAfter = true;

        private string _status = "";
        private int _missingTiles = -1;         // -1 = 아직 안 세어 봄

        private void OnEnable() { _missingTiles = -1; }
        private void OnFocus() { _missingTiles = -1; }

        [MenuItem(MenuRoot + "절벽 그리기", false, 21)]
        private static void Open()
        {
            GetWindow<CliffPainter>("절벽 그리기").minSize = new Vector2(380f, 520f);
        }

        // ------------------------------------------------------------------ 창

        private void OnGUI()
        {
            EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);
            _outMap = (Tilemap)EditorGUILayout.ObjectField("결과 타일맵", _outMap, typeof(Tilemap), true);
            _shapeMap = (Tilemap)EditorGUILayout.ObjectField("모양 타일맵", _shapeMap, typeof(Tilemap), true);
            if (GUILayout.Button($"{GridName} 만들기 / 쓰기"))
            {
                FindOrCreateTestCliff(out _shapeMap, out _outMap);
                _status = $"\"{GridName}\" 아래의 \"{ShapeMapName}\"(모양) 과 \"{OutMapName}\"(결과) 를 씁니다.";
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("무엇을 그릴까", EditorStyles.boldLabel);
            _mode = (Mode)EditorGUILayout.EnumPopup(
                new GUIContent("방식", "자동 생성 = 알아서 만든다 / 칠한 모양 따라가기 = 모양 타일맵을 읽는다"),
                _mode);

            using (new EditorGUILayout.HorizontalScope())
            {
                _seed = EditorGUILayout.IntField("시드", _seed);
                if (GUILayout.Button("새로", GUILayout.Width(50f)))
                    _seed = UnityEngine.Random.Range(0, int.MaxValue);
            }

            if (_mode == Mode.Auto)
            {
                _shape = (Shape)EditorGUILayout.EnumPopup("모양", _shape);
                _w = Mathf.Max(6, EditorGUILayout.IntField("가로 칸 수", _w));
                _h = Mathf.Max(6 + WallH, EditorGUILayout.IntField("세로 칸 수", _h));
                _origin = EditorGUILayout.Vector3IntField("시작 칸(왼쪽 아래)", _origin);
                if (_shape == Shape.Ridge)
                    _thickness = EditorGUILayout.Slider("띠 두께(칸)", _thickness, 3f, 20f);
                else
                    _blobs = EditorGUILayout.IntSlider("덩어리 개수", _blobs, 1, 12);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"\"{ShapeMapName}\" 타일맵에 아무 타일이나 칠해 고지대 모양을 잡아 두세요.\n" +
                    "칠한 칸 = 절벽 윗면입니다. 가장자리·모서리·벽은 알아서 골라 깝니다.\n" +
                    $"벽은 칠한 모양 아래로 {WallH}칸 더 내려가니 아래쪽에 자리를 비워 두세요.",
                    MessageType.Info);
                _bumpPainted = EditorGUILayout.Toggle("칠한 모양도 흔들기", _bumpPainted);
                _flattenPainted = EditorGUILayout.Toggle("남쪽 경계 평탄화", _flattenPainted);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("가장자리 결", EditorStyles.boldLabel);
            _bump = EditorGUILayout.Slider(
                new GUIContent("울퉁불퉁함", "0이면 매끈한 직선, 1이면 한 칸씩 최대로 흔든다"),
                _bump, 0f, 1f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("남쪽 벽 한 단 폭(칸)",
                    "남쪽 경계를 몇 칸씩 평평하게 맞출지. 1,1로 두면 남쪽도 한 칸씩 울퉁불퉁해진다."));
                _stepMin = Mathf.Clamp(EditorGUILayout.IntField(_stepMin), 1, 24);
                EditorGUILayout.LabelField("~", GUILayout.Width(12f));
                _stepMax = Mathf.Clamp(EditorGUILayout.IntField(_stepMax), _stepMin, 24);
            }

            EditorGUILayout.Space();
            _clearFirst = EditorGUILayout.Toggle("그리기 전에 결과 타일맵 비우기", _clearFirst);
            if (_mode == Mode.FollowPainted)
                _hideShapeAfter = EditorGUILayout.Toggle("그린 뒤 모양 레이어 숨기기", _hideShapeAfter);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("타일 에셋", EditorStyles.boldLabel);
            if (_missingTiles < 0) TilesReady(out _missingTiles);
            int missing = _missingTiles;
            bool ready = missing == 0;
            EditorGUILayout.HelpBox(ready
                    ? $"{AutoSheetName} 타일 {UsedCells().Count}장 준비됨."
                    : $"타일 에셋이 {missing}장 없습니다. 아래 버튼을 누르면 원본 시트로 " +
                      $"{AutoSheetName}.png 를 만들고 슬라이스해서 타일 에셋까지 만들어 줍니다.",
                ready ? MessageType.None : MessageType.Warning);
            if (GUILayout.Button("타일 준비 (시트 만들고 타일 에셋 생성)"))
            {
                PrepareTiles();
                _missingTiles = -1;
            }

            EditorGUILayout.Space();
            bool canPaint = _outMap != null && ready &&
                            (_mode == Mode.Auto || _shapeMap != null);
            using (new EditorGUI.DisabledScope(!canPaint))
            {
                if (GUILayout.Button("그리기", GUILayout.Height(30f))) Paint();
            }
            using (new EditorGUI.DisabledScope(_outMap == null))
            {
                if (GUILayout.Button("결과 타일맵 전체 지우기")) ClearAll(_outMap);
            }
            if (_shapeMap != null && _shapeMap.TryGetComponent(out TilemapRenderer sr) && !sr.enabled)
            {
                if (GUILayout.Button("모양 레이어 다시 보이기")) { sr.enabled = true; MarkDirty(_shapeMap); }
            }

            if (_outMap == null) EditorGUILayout.HelpBox("결과 타일맵을 지정하세요.", MessageType.Warning);
            else if (_mode == Mode.FollowPainted && _shapeMap == null)
                EditorGUILayout.HelpBox("모양 타일맵을 지정하세요.", MessageType.Warning);

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        // ------------------------------------------------------------------ 그리기

        private void Paint()
        {
            var tiles = LoadTiles(out string error);
            if (tiles == null) { Fail(error); return; }

            var rng = new System.Random(_seed);
            bool[,] mask;
            int originX, topY;          // topY = 마스크 y=0 이 놓이는 타일맵 y
            int z = _mode == Mode.Auto ? _origin.z : 0;
            string note = "";

            if (_mode == Mode.Auto)
            {
                mask = _shape == Shape.Ridge
                    ? RidgeMask(_w, _h, rng, _thickness + 1.5f * _bump)
                    : BlobMask(_w, _h, rng, _blobs);
                BumpEdges(mask, rng, _bump);
                Repair(mask);
                FlattenBottom(mask, rng, _stepMin, _stepMax);
                Repair(mask);
                originX = _origin.x;
                topY = _origin.y + _h - 1;
            }
            else
            {
                if (!MaskFromTilemap(_shapeMap, out mask, out originX, out topY))
                {
                    Fail($"\"{_shapeMap.name}\" 에 칠해진 타일이 없습니다. 고지대로 쓸 칸을 먼저 칠하세요.");
                    return;
                }
                int painted = Count(mask);
                if (_bumpPainted) BumpEdges(mask, rng, _bump);
                Repair(mask);
                if (_flattenPainted) { FlattenBottom(mask, rng, _stepMin, _stepMax); Repair(mask); }
                note = $"칠한 {painted}칸 중 {Count(mask)}칸을 고지대로 씁니다. ";
            }

            int w = mask.GetLength(0), h = mask.GetLength(1);
            Undo.RegisterCompleteObjectUndo(_outMap, "절벽 그리기");

            if (_clearFirst)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        _outMap.SetTile(new Vector3Int(originX + x, topY - y, z), null);

            // 1) 벽 먼저. 벽 한 줄은 세로 4장이 한 세트라 윗단에서 세트를 정해 내려간다.
            var depth = new int[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    depth[x, y] = WallDepth(mask, x, y);

            Func<int, int, bool> filled = (x, y) =>
                Hi(mask, x, y) || (x >= 0 && x < w && y >= 0 && y < h && depth[x, y] > 0);

            var colSet = new Dictionary<(int, int, string), (int r, int c)[]>();
            int wallCount = 0, topCount = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int d = depth[x, y];
                    if (d == 0) continue;
                    bool leftOpen = !filled(x - 1, y);
                    bool rightOpen = !filled(x + 1, y);
                    string role = leftOpen && rightOpen ? "SOLO" : leftOpen ? "L" : rightOpen ? "R" : "M";

                    var key = (x, y - d, role);
                    if (!colSet.TryGetValue(key, out var set))
                    {
                        var sets = Wall[role];
                        set = sets[rng.Next(sets.Length)];
                        colSet[key] = set;
                    }
                    _outMap.SetTile(new Vector3Int(originX + x, topY - y, z), tiles[set[d - 1]]);
                    wallCount++;
                }
            }

            // 2) 윗면
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!mask[x, y]) continue;
                    string role = TopRole(mask, x, y);
                    if (role == null) continue;                  // 안쪽 — 밑에 깔린 잔디가 그대로 보이면 된다
                    if (role == "W" && filled(x - 1, y) && !Hi(mask, x - 1, y)) role = "W_WALL";
                    else if (role == "E" && filled(x + 1, y) && !Hi(mask, x + 1, y)) role = "E_WALL";

                    var variants = Top[role];
                    var cell = variants[rng.Next(variants.Length)];
                    _outMap.SetTile(new Vector3Int(originX + x, topY - y, z), tiles[cell]);
                    topCount++;
                }
            }

            if (_mode == Mode.FollowPainted && _hideShapeAfter &&
                _shapeMap.TryGetComponent(out TilemapRenderer sr))
                sr.enabled = false;

            MarkDirty(_outMap);
            _status = note + $"윗면 {topCount}칸 + 벽 {wallCount}칸을 깔았습니다. 시드 {_seed}. " +
                       "(고지대 안쪽은 원본에 조각이 없어 비워 둡니다 — 잔디 레이어 위에 얹어 쓰세요.)";
            Debug.Log($"[CliffPainter] {_outMap.name}: {_status}");
        }

        private void Fail(string message)
        {
            _status = message;
            Debug.LogError("[CliffPainter] " + message);
        }

        private static void MarkDirty(Component c)
        {
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        }

        private void ClearAll(Tilemap map)
        {
            int count = 0;
            foreach (var p in map.cellBounds.allPositionsWithin)
                if (map.GetTile(p) != null) count++;
            if (count == 0) { _status = $"{map.name} 에는 이미 아무것도 없습니다."; return; }
            if (!EditorUtility.DisplayDialog("타일맵 전체 지우기",
                    $"\"{map.name}\" 의 {count}칸을 전부 지웁니다.\n(Ctrl+Z 로 되돌릴 수 있습니다.)",
                    "지우기", "취소"))
                return;

            Undo.RegisterCompleteObjectUndo(map, "타일맵 전체 지우기");
            map.ClearAllTiles();
            map.CompressBounds();
            MarkDirty(map);
            _status = $"{map.name} 의 {count}칸을 지웠습니다.";
        }

        // ------------------------------------------------------------------ 모양 만들기

        private static double Sum(IEnumerable<(double amp, double freq, double phase)> waves, double t)
        {
            double s = 0.0, norm = 0.0;
            foreach (var w in waves)
            {
                s += w.amp * Math.Sin(Math.PI * 2.0 * w.freq * t + w.phase);
                norm += w.amp;
            }
            return norm > 0.0 ? s / norm : 0.0;
        }

        private static (double, double, double) Octave(System.Random rng, double amp, double lo, double hi)
            => (amp, lo + rng.NextDouble() * (hi - lo), rng.NextDouble() * Math.PI * 2.0);

        /// <summary>맵을 가로지르며 구불구불 흐르는 고지대 띠. 두께도 계속 변한다.</summary>
        private static bool[,] RidgeMask(int w, int h, System.Random rng, float thickness)
        {
            var fy = new[]
            {
                Octave(rng, 1.00, 1.1, 1.8), Octave(rng, 0.45, 2.4, 3.4), Octave(rng, 0.18, 4.8, 6.4),
            };
            var ft = new[] { Octave(rng, 1.00, 1.6, 2.4), Octave(rng, 0.40, 3.6, 5.0) };
            double amp = Math.Max(0.0, h - thickness - WallH - 3) * 0.35;

            var mask = new bool[w, h];
            for (int x = 0; x < w; x++)
            {
                double t = (x + 0.5) / w;
                double mid = (h - WallH) * 0.45 + amp * Sum(fy, t);
                double half = Math.Max(1.2, thickness * 0.5 * (1.0 + 0.40 * Sum(ft, t)));
                double top = Math.Max(1.0, mid - half);
                double bot = Math.Min(h - WallH - 1.0, mid + half);
                for (int y = 0; y < h; y++)
                    if (top <= y + 0.5 && y + 0.5 <= bot) mask[x, y] = true;
            }
            return mask;
        }

        /// <summary>반지름을 각도마다 흔든 타원 덩어리들. 겹치면 자연스럽게 합쳐진다.</summary>
        private static bool[,] BlobMask(int w, int h, System.Random rng, int count)
        {
            var blobs = new List<(double cx, double cy, double rx, double ry, double rot, (double, double, double)[] harm)>();
            for (int i = 0; i < count; i++)
            {
                double rx = w * (0.07 + rng.NextDouble() * 0.10);
                double ry = h * (0.08 + rng.NextDouble() * 0.12);
                double cx = rx + 2.0 + rng.NextDouble() * Math.Max(0.0, w - 2 * rx - 4.0);
                double loY = ry + 1.5, hiY = Math.Max(loY, h - ry - (WallH + 2.0));
                double cy = loY + rng.NextDouble() * (hiY - loY);
                double rot = rng.NextDouble() * Math.PI * 2.0;
                var harm = new[] { 2.0, 3.0, 5.0 }
                    .Select(k => (0.07 + rng.NextDouble() * 0.12, k, rng.NextDouble() * Math.PI * 2.0))
                    .ToArray();
                blobs.Add((cx, cy, rx, ry, rot, harm));
            }

            var mask = new bool[w, h];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    double px = x + 0.5, py = y + 0.5;
                    foreach (var b in blobs)
                    {
                        double dx = px - b.cx, dy = py - b.cy;
                        double ca = Math.Cos(-b.rot), sa = Math.Sin(-b.rot);
                        double ux = dx * ca - dy * sa, uy = dx * sa + dy * ca;
                        double ang = Math.Atan2(uy, ux);
                        double wob = 1.0;
                        foreach (var (a, k, p) in b.harm) wob += a * Math.Sin(k * ang + p);
                        wob = Math.Max(0.45, wob);
                        double nx = ux / (b.rx * wob), ny = uy / (b.ry * wob);
                        if (nx * nx + ny * ny <= 1.0) { mask[x, y] = true; break; }
                    }
                }
            }
            return mask;
        }

        /// <summary>
        /// 경계 칸을 한 칸씩 무작위로 깎거나 붙여 직선 구간을 없앤다.
        /// 한 칸만 튀어나온 돌기는 쓸 조각이 없어 Repair() 가 다시 깎으므로, 실제로 남는 것은
        /// 2칸 이상 폭의 돌기와 홈이다. 그게 딱 손으로 찍은 듯한 결이 된다.
        /// </summary>
        private static void BumpEdges(bool[,] mask, System.Random rng, float strength)
        {
            if (strength <= 0f) return;
            int w = mask.GetLength(0), h = mask.GetLength(1);
            double p = 0.5 * Math.Min(1f, strength);
            var src = (bool[,])mask.Clone();

            Func<int, int, bool> s = (x, y) => x >= 0 && x < w && y >= 0 && y < h && src[x, y];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    bool n = s(x, y - 1), so = s(x, y + 1), we = s(x - 1, y), ea = s(x + 1, y);
                    if (src[x, y])
                    {
                        // 깎는 쪽을 약하게 둔다. 붙인 돌기는 상당수가 Repair 에서 다시 깎이기 때문에
                        // 같은 확률로 흔들면 덩어리가 계속 줄어든다.
                        if (!(n && so && we && ea) && rng.NextDouble() < p * 0.6) mask[x, y] = false;
                    }
                    else if ((n || so || we || ea) && rng.NextDouble() < p) mask[x, y] = true;
                }
            }
        }

        /// <summary>
        /// 남쪽 경계를 몇 칸씩 묶어 가장 높은 칸에 맞춘다(깎기만 한다).
        /// 벽은 열마다 4장이 통째로 내려오기 때문에, 남쪽이 한 칸씩 계단이 되면 폭 1칸짜리 벽이
        /// 서로 다른 높이로 촘촘히 늘어서 누더기처럼 보인다.
        /// </summary>
        private static void FlattenBottom(bool[,] mask, System.Random rng, int lo, int hi)
        {
            if (hi <= 1) return;
            int h = mask.GetLength(1);
            foreach (var comp in Components(mask))
            {
                var bottom = new Dictionary<int, int>();
                foreach (var (x, y) in comp)
                    bottom[x] = bottom.TryGetValue(x, out int b) ? Math.Max(b, y) : y;

                var xs = bottom.Keys.OrderBy(v => v).ToList();
                int i = 0;
                while (i < xs.Count)
                {
                    int k = Math.Min(lo + rng.Next(Math.Max(1, hi - lo + 1)), xs.Count - i);
                    var group = xs.GetRange(i, k);
                    int target = group.Min(x => bottom[x]);
                    foreach (int x in group)
                        for (int y = target + 1; y < h; y++)
                            if (comp.Contains((x, y))) mask[x, y] = false;
                    i += k;
                }
            }
        }

        private static List<HashSet<(int, int)>> Components(bool[,] mask)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            var seen = new bool[w, h];
            var all = new List<HashSet<(int, int)>>();
            for (int sx = 0; sx < w; sx++)
            {
                for (int sy = 0; sy < h; sy++)
                {
                    if (!mask[sx, sy] || seen[sx, sy]) continue;
                    var comp = new HashSet<(int, int)>();
                    var stack = new Stack<(int, int)>();
                    stack.Push((sx, sy));
                    seen[sx, sy] = true;
                    while (stack.Count > 0)
                    {
                        var (x, y) = stack.Pop();
                        comp.Add((x, y));
                        foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                            if (Hi(mask, nx, ny) && !seen[nx, ny]) { seen[nx, ny] = true; stack.Push((nx, ny)); }
                    }
                    all.Add(comp);
                }
            }
            return all;
        }

        // ------------------------------------------------------------------ 모양 다듬기

        private static bool Hi(bool[,] mask, int x, int y)
            => x >= 0 && x < mask.GetLength(0) && y >= 0 && y < mask.GetLength(1) && mask[x, y];

        private static int Count(bool[,] mask)
        {
            int n = 0;
            foreach (bool b in mask) if (b) n++;
            return n;
        }

        /// <summary>
        /// 시트에 조각이 없는 모양을 없앤다. 모든 단계가 "지우기"뿐이라 반드시 수렴한다.
        ///   폭·높이 1칸 / 고지대 사이 폭 1칸짜리 틈 / nw·ne 동시에 낮은 오목 모서리 /
        ///   남쪽 끝 아래 4칸 안의 다른 고지대 / 2x2 도 안 되는 자투리
        /// </summary>
        private static void Repair(bool[,] mask, int minCells = 6)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            for (int pass = 0; pass < 60; pass++)
            {
                var kill = new List<(int, int)>();
                RunsTooThin(mask, kill);
                WallSpace(mask, kill);
                ThinGaps(mask, kill);
                BadCorners(mask, kill);
                TinyBlobs(mask, minCells, kill);
                if (kill.Count == 0) return;
                foreach (var (x, y) in kill)
                    if (x >= 0 && x < w && y >= 0 && y < h) mask[x, y] = false;
            }
        }

        private static void RunsTooThin(bool[,] mask, List<(int, int)> kill)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    if (!mask[x, y]) { x++; continue; }
                    int s = x;
                    while (x < w && mask[x, y]) x++;
                    if (x - s < 2) for (int i = s; i < x; i++) kill.Add((i, y));
                }
            }
            for (int x = 0; x < w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    if (!mask[x, y]) { y++; continue; }
                    int s = y;
                    while (y < h && mask[x, y]) y++;
                    if (y - s < 2) for (int j = s; j < y; j++) kill.Add((x, j));
                }
            }
        }

        private static void WallSpace(bool[,] mask, List<(int, int)> kill)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (mask[x, y] && !Hi(mask, x, y + 1))
                        for (int d = 2; d <= WallH; d++)
                            if (Hi(mask, x, y + d)) kill.Add((x, y + d));
        }

        /// <summary>고지대 사이에 낀 폭 1칸짜리 틈을 넓힌다 — 그대로 두면 양쪽 그늘이 맞붙어 금처럼 보인다.</summary>
        private static void ThinGaps(bool[,] mask, List<(int, int)> kill)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (mask[x, y]) continue;
                    if (Hi(mask, x - 1, y) && Hi(mask, x + 1, y)) kill.Add((x + 1, y));
                    if (Hi(mask, x, y - 1) && Hi(mask, x, y + 1)) kill.Add((x, y + 1));
                }
            }
        }

        /// <summary>nw, ne 가 동시에 낮은 오목 모서리는 조각이 없다.</summary>
        private static void BadCorners(bool[,] mask, List<(int, int)> kill)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (mask[x, y] && Hi(mask, x, y - 1) && Hi(mask, x - 1, y) && Hi(mask, x + 1, y) &&
                        !Hi(mask, x - 1, y - 1) && !Hi(mask, x + 1, y - 1))
                        kill.Add((x, y));
        }

        private static void TinyBlobs(bool[,] mask, int minCells, List<(int, int)> kill)
        {
            foreach (var comp in Components(mask))
                if (comp.Count < minCells) kill.AddRange(comp);
        }

        // ------------------------------------------------------------------ 칸 고르기

        private static string TopRole(bool[,] mask, int x, int y)
        {
            bool n = Hi(mask, x, y - 1), s = Hi(mask, x, y + 1);
            bool w = Hi(mask, x - 1, y), e = Hi(mask, x + 1, y);
            if (!n && !w) return "NW";
            if (!n && !e) return "NE";
            if (!s && !w) return "SW";
            if (!s && !e) return "SE";
            if (!n) return "N";
            if (!w) return "W";
            if (!e) return "E";
            if (!s) return null;                                  // 아래 벽 윗단이 처리한다
            if (!Hi(mask, x - 1, y - 1)) return "IN_NW";
            if (!Hi(mask, x + 1, y - 1)) return "IN_NE";
            return null;                                          // 안쪽
        }

        /// <summary>이 칸이 벽이면 깊이(1=윗단 .. WallH=바닥), 아니면 0.</summary>
        private static int WallDepth(bool[,] mask, int x, int y)
        {
            if (Hi(mask, x, y)) return 0;
            for (int d = 1; d <= WallH; d++)
                if (Hi(mask, x, y - d)) return d;
            return 0;
        }

        // ------------------------------------------------------------------ 칠한 모양 읽기

        /// <summary>
        /// 모양 타일맵에 칠해진 칸을 고지대 마스크로 읽는다.
        /// 마스크 y는 위에서 아래로 세므로 타일맵 y와 뒤집어 맞춘다. 벽이 들어갈 자리로
        /// 아래쪽에 WallH 칸, 옆에는 한 칸씩 여유를 둔다.
        /// </summary>
        private static bool MaskFromTilemap(Tilemap map, out bool[,] mask, out int originX, out int topY)
        {
            mask = null; originX = 0; topY = 0;
            map.CompressBounds();
            var cells = new List<Vector3Int>();
            foreach (var p in map.cellBounds.allPositionsWithin)
                if (map.GetTile(p) != null) cells.Add(p);
            if (cells.Count == 0) return false;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in cells)
            {
                minX = Math.Min(minX, p.x); maxX = Math.Max(maxX, p.x);
                minY = Math.Min(minY, p.y); maxY = Math.Max(maxY, p.y);
            }
            minX -= 1; maxX += 1;                 // 옆 가장자리 조각이 들어갈 여유

            originX = minX;
            topY = maxY;
            int w = maxX - minX + 1;
            int h = (maxY - minY + 1) + WallH;
            mask = new bool[w, h];
            foreach (var p in cells) mask[p.x - minX, maxY - p.y] = true;
            return true;
        }

        // ------------------------------------------------------------------ 타일 에셋

        private static List<(int r, int c)> UsedCells()
        {
            var set = new SortedSet<(int, int)>();
            foreach (var v in Top.Values) foreach (var cell in v) set.Add(cell);
            foreach (var sets in Wall.Values) foreach (var s in sets) foreach (var cell in s) set.Add(cell);
            return set.ToList();
        }

        private static string TileAssetPath((int r, int c) cell)
            => $"{TileAssetFolder}/{AutoSheetName}_{cell.r * SheetCols + cell.c}.asset";

        private static bool TilesReady(out int missing)
        {
            missing = UsedCells().Count(cell => AssetDatabase.LoadAssetAtPath<TileBase>(TileAssetPath(cell)) == null);
            return missing == 0;
        }

        private static Dictionary<(int r, int c), TileBase> LoadTiles(out string error)
        {
            var result = new Dictionary<(int, int), TileBase>();
            var missing = new List<string>();
            foreach (var cell in UsedCells())
            {
                string path = TileAssetPath(cell);
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile == null) missing.Add(path);
                else result[cell] = tile;
            }
            if (missing.Count > 0)
            {
                error = $"타일 에셋 {missing.Count}장이 없습니다. \"타일 준비\" 를 먼저 누르세요.\n  " +
                        string.Join("\n  ", missing.Take(5));
                return null;
            }
            error = null;
            return result;
        }

        /// <summary>원본 시트 -> 합성 모서리를 얹은 auto 시트 -> 슬라이스 -> 타일 에셋까지 한 번에.</summary>
        private void PrepareTiles()
        {
            if (!File.Exists(SrcSheetPath)) { Fail($"원본 시트가 없습니다: {SrcSheetPath}"); return; }
            try
            {
                BuildAutoSheet();
                SliceAutoSheet(out string sliceError);
                if (sliceError != null) { Fail(sliceError); return; }
                int made = CreateTileAssets(out string tileError);
                if (tileError != null) { Fail(tileError); return; }
                _status = $"{AutoSheetName}.png 를 만들고 {UsedCells().Count}칸을 잘랐습니다. " +
                          $"타일 에셋 {made}장을 새로 만들었습니다.";
                Debug.Log("[CliffPainter] " + _status);
            }
            catch (Exception e)
            {
                Fail("타일 준비 중 오류: " + e.Message);
            }
        }

        /// <summary>
        /// 원본 시트를 그대로 복사하고, Synth 표대로 합성한 모서리 조각을 빈 칸에 얹어 저장한다.
        /// 원본 그림 파일은 건드리지 않는다. 원본이 바뀌면 다시 누르기만 하면 된다.
        /// </summary>
        private static void BuildAutoSheet()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(SrcSheetPath));     // 임포터 설정과 무관하게 읽는다
            int texH = tex.height;
            var px = tex.GetPixels32();

            // 시트 좌표 (행은 위에서부터) -> 텍스처 픽셀 인덱스 (유니티는 아래가 y=0)
            Func<(int r, int c), int, int, Color32> at = (cell, i, j) =>
            {
                int x = cell.c * Cell + i;
                int y = texH - 1 - (cell.r * Cell + j);
                return px[y * tex.width + x];
            };

            foreach (var kv in Synth)
            {
                var (dst, src) = (kv.Key, kv.Value);
                for (int j = 0; j < Cell; j++)
                {
                    for (int i = 0; i < Cell; i++)
                    {
                        Color32 side = at(src.side, i, j);
                        Color32 north = at(src.north, i, j);
                        Color32 corner = at(src.corner, i, j);

                        Color32 outPx = side;
                        outPx.a = Math.Min(side.a, north.a);      // 북쪽 조각이 투명한 만큼 윗단을 자른다
                        if (corner.a > 0) outPx = corner;         // 둥근 모서리를 얹는다

                        int x = dst.c * Cell + i;
                        int y = texH - 1 - (dst.r * Cell + j);
                        px[y * tex.width + x] = outPx;
                    }
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(AutoSheetPath, tex.EncodeToPNG());
            DestroyImmediate(tex);
            AssetDatabase.ImportAsset(AutoSheetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>auto 시트를 16x16으로 자른다. 실제로 쓰는 칸만 잘라서 이름을 index로 붙인다.</summary>
        private static void SliceAutoSheet(out string error)
        {
            error = null;
            var importer = AssetImporter.GetAtPath(AutoSheetPath) as TextureImporter;
            if (importer == null) { error = $"임포터를 찾지 못했습니다: {AutoSheetPath}"; return; }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AutoSheetPath);
            int texH = tex != null ? tex.height : 256;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Cell;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var metas = UsedCells().Select(cell => new SpriteMetaData
            {
                name = $"{AutoSheetName}_{cell.r * SheetCols + cell.c}",
                rect = new Rect(cell.c * Cell, texH - (cell.r + 1) * Cell, Cell, Cell),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            }).ToArray();

            importer.spritesheet = metas;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static int CreateTileAssets(out string error)
        {
            error = null;
            if (!AssetDatabase.IsValidFolder(TileAssetFolder))
            {
                error = $"타일 폴더가 없습니다: {TileAssetFolder}";
                return 0;
            }

            var sprites = AssetDatabase.LoadAllAssetsAtPath(AutoSheetPath)
                .OfType<Sprite>().ToDictionary(s => s.name, s => s);

            int made = 0;
            var missing = new List<string>();
            foreach (var cell in UsedCells())
            {
                string name = $"{AutoSheetName}_{cell.r * SheetCols + cell.c}";
                if (!sprites.TryGetValue(name, out var sprite)) { missing.Add(name); continue; }

                string path = TileAssetPath(cell);
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.Sprite;
                    AssetDatabase.CreateAsset(tile, path);
                    made++;
                }
                else if (tile.sprite != sprite)
                {
                    tile.sprite = sprite;
                    EditorUtility.SetDirty(tile);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 시트에서 조각을 옮기거나 지웠으면 예전 index 로 만든 타일 에셋이 남는다.
            // 타일맵이 아직 참조하고 있을 수 있어 지우지는 않고 알려만 준다.
            var wanted = new HashSet<string>(UsedCells().Select(c => TileAssetPath(c)));
            var stale = AssetDatabase.FindAssets($"{AutoSheetName}_ t:Tile", new[] { TileAssetFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => System.IO.Path.GetFileName(path).StartsWith(AutoSheetName + "_") &&
                               !wanted.Contains(path))
                .ToList();
            if (stale.Count > 0)
                Debug.LogWarning($"[CliffPainter] 이제 안 쓰는 타일 에셋 {stale.Count}장이 남아 있습니다. " +
                                 "타일맵에서 쓰고 있지 않다면 지워도 됩니다:\n  " +
                                 string.Join("\n  ", stale));

            if (missing.Count > 0)
                error = $"잘린 스프라이트를 찾지 못했습니다 ({missing.Count}장). " +
                        $"{AutoSheetName}.png 임포트를 확인하세요.\n  " + string.Join("\n  ", missing.Take(5));
            return made;
        }

        // ------------------------------------------------------------------ 기본 타일맵

        /// <summary>"test_cliff" 그리드 아래에 모양/결과 타일맵 두 장을 만든다(있으면 그대로 쓴다).</summary>
        private static void FindOrCreateTestCliff(out Tilemap shape, out Tilemap outMap)
        {
            var gridGo = GameObject.Find(GridName);
            if (gridGo == null)
            {
                gridGo = new GameObject(GridName);
                gridGo.transform.position = new Vector3(0f, -120f, 0f);   // 다른 맵과 겹치지 않게
                Undo.RegisterCreatedObjectUndo(gridGo, $"{GridName} 만들기");
            }
            if (gridGo.GetComponent<Grid>() == null)
                gridGo.AddComponent<Grid>().cellSize = new Vector3(1f, 1f, 0f);

            shape = FindOrCreateLayer(gridGo, ShapeMapName, Depth.Cliff);
            outMap = FindOrCreateLayer(gridGo, OutMapName, Depth.Cliff + 1);
            EditorSceneManager.MarkSceneDirty(gridGo.scene);
        }

        private static Tilemap FindOrCreateLayer(GameObject parent, string name, int sortingOrder)
        {
            var found = parent.transform.Find(name);
            if (found != null)
            {
                var existing = found.GetComponent<Tilemap>();
                if (existing != null) return existing;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var map = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            Undo.RegisterCreatedObjectUndo(go, $"{name} 만들기");
            return map;
        }
    }
}
