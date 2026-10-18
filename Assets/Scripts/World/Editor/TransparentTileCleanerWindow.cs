using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    internal sealed class TransparentTileCleanerWindow : EditorWindow
    {
        private static readonly string[] Scopes = { "모든 타일맵", "지역 전체", "지역 + 타일맵 종류", "특정 타일맵", "선택한 부모 아래" };
        [SerializeField] private int scope;
        [SerializeField] private LocationId location;
        [SerializeField] private int layer;
        [SerializeField] private Tilemap singleMap;
        [SerializeField] private GameObject parent;
        [SerializeField] private TileBase onlyTile;
        [SerializeField] private bool limitArea;
        [SerializeField] private Vector3Int areaMin;
        [SerializeField] private Vector3Int areaSize = new Vector3Int(10, 10, 1);
        [SerializeField] private bool includeMissing;
        private bool picking, dragging;
        private int dragControl;
        private Vector3Int dragStart;
        private Tilemap referenceMap;
        private Vector2 scroll;
        private string status;
        private TransparentTileCleaner.ScanResult preview;

        [MenuItem("Tools/FarmMVP/완전 투명 타일 지우기", false, 22)]
        internal static void Open()
        {
            var window = GetWindow<TransparentTileCleanerWindow>("투명 타일 정리");
            window.minSize = new Vector2(440, 480);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= Invalidate;
            EditorApplication.hierarchyChanged -= Invalidate;
            EditorApplication.projectChanged -= Invalidate;
            StopPicking();
        }

        private void Invalidate() { preview = null; Repaint(); }

        private Tilemap[] Targets()
        {
            var maps = TransparentTileCleaner.LoadedMaps();
            switch (scope)
            {
                case 1: return maps.Where(m => TransparentTileCleaner.MatchesName(m.name, location.ToString(), null)).ToArray();
                case 2: return maps.Where(m => TransparentTileCleaner.MatchesName(m.name, location.ToString(), TransparentTileCleaner.Layers[layer])).ToArray();
                case 3: return maps.Where(m => m == singleMap).ToArray();
                case 4: return maps.Where(m => parent != null && m.transform.IsChildOf(parent.transform)).ToArray();
                default: return maps;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("현재 열린 씬의 타일 배치를 정리합니다. 비활성 타일맵도 포함합니다.\n" +
                "스프라이트의 모든 픽셀이 알파 0인 타일만 지웁니다. 원본 타일 에셋·팔레트는 유지됩니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUI.BeginChangeCheck();
                scope = EditorGUILayout.Popup("대상 범위", scope, Scopes);
                if (scope == 1 || scope == 2) location = (LocationId)EditorGUILayout.EnumPopup("지역", location);
                if (scope == 2) layer = EditorGUILayout.Popup("타일맵 종류 (레이어)", layer, TransparentTileCleaner.Layers);
                if (scope == 3) singleMap = (Tilemap)EditorGUILayout.ObjectField("타일맵", singleMap, typeof(Tilemap), true);
                if (scope == 4) parent = (GameObject)EditorGUILayout.ObjectField("부모 오브젝트", parent, typeof(GameObject), true);
                onlyTile = (TileBase)EditorGUILayout.ObjectField(new GUIContent("특정 타일 에셋만", "비워 두면 모든 타일 에셋을 검사합니다."), onlyTile, typeof(TileBase), false);
                limitArea = EditorGUILayout.Toggle("사각형 영역만 검사", limitArea);
                if (limitArea)
                {
                    areaMin = EditorGUILayout.Vector3IntField("시작 셀 (포함)", areaMin);
                    areaSize = EditorGUILayout.Vector3IntField("크기 (X / Y / Z)", areaSize);
                    EditorGUILayout.HelpBox("각 타일맵의 로컬 셀 좌표에 같은 범위를 적용합니다.\nScene에서 드래그하면 X/Y 범위를 선택합니다. Z는 위의 값을 사용합니다.", MessageType.None);
                }
                includeMissing = EditorGUILayout.Toggle(new GUIContent("스프라이트 없는 타일도", "그림 참조가 없는 타일도 지웁니다. 기본값은 보존입니다."), includeMissing);
                if (includeMissing)
                    EditorGUILayout.HelpBox("그림 없는 마커·충돌용 타일도 대상이 될 수 있습니다. 아래 검사 결과에서 대상 타일맵을 확인하세요.", MessageType.Warning);
                if (EditorGUI.EndChangeCheck())
                {
                    preview = null;
                    status = null;
                    StopPicking();
                    SceneView.RepaintAll();
                }

                var targets = Targets();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"대상 타일맵 {targets.Length}개", EditorStyles.boldLabel);
                if (targets.Length == 0) EditorGUILayout.HelpBox("해당하는 타일맵이 없습니다. 지역 또는 대상을 바꿔 주세요.", MessageType.Info);
                bool validArea = !limitArea || (areaSize.x > 0 && areaSize.y > 0 && areaSize.z > 0 &&
                    (long)areaMin.x + areaSize.x <= int.MaxValue && (long)areaMin.y + areaSize.y <= int.MaxValue &&
                    (long)areaMin.z + areaSize.z <= int.MaxValue);
                if (!validArea) EditorGUILayout.HelpBox("영역 크기는 각 축에서 1 이상이며 좌표 범위를 넘지 않아야 합니다.", MessageType.Error);
                using (new EditorGUI.DisabledScope(targets.Length == 0 || !validArea))
                {
                    if (limitArea && GUILayout.Button(picking ? "영역 선택 취소 (Esc)" : "Scene에서 영역 드래그 선택"))
                    {
                        if (picking) StopPicking();
                        else
                        {
                            referenceMap = targets.Contains(singleMap) ? singleMap : targets[0];
                            picking = true;
                            SceneView.RepaintAll();
                        }
                    }
                    if (picking) EditorGUILayout.HelpBox($"Scene에서 마우스 왼쪽 버튼으로 드래그하세요. 기준: {referenceMap.name}", MessageType.Info);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("투명 타일 검사", GUILayout.Height(30))) Run(false);
                    if (GUILayout.Button("범위 내 투명 타일 모두 지우기", GUILayout.Height(30))) Run(true);
                    EditorGUILayout.EndHorizontal();
                }

                if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
                if (preview != null)
                {
                    EditorGUILayout.LabelField($"검사 {preview.Inspected:N0}칸 / 완전 투명 {preview.Count:N0}칸 / 판정 불가·그림 없음 보존 {preview.PreservedUnknown:N0}칸");
                    if (preview.Warnings.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", preview.Warnings.Take(4)), MessageType.Warning);
                }
                scroll = EditorGUILayout.BeginScrollView(scroll);
                foreach (var map in targets)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(map, typeof(Tilemap), true);
                    var result = preview?.Maps.FirstOrDefault(m => m.Map == map);
                    if (result != null) GUILayout.Label($"{result.Cells.Count:N0}칸", GUILayout.Width(65));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.HelpBox("삭제는 Ctrl+Z로 한 번에 되돌릴 수 있습니다. 삭제 후 씬을 저장하세요.\n" +
                "색상·Renderer로 숨긴 타일은 보존하며, 애니메이션·계절 타일은 모든 그림을 검사합니다.", MessageType.None);
        }

        private void Run(bool erase)
        {
            StopPicking();
            preview = null;
            try
            {
                // Re-read pixels and cells on every action, including deletion after a preview.
                preview = TransparentTileCleaner.Scan(Targets(), limitArea ? new BoundsInt(areaMin, areaSize) : (BoundsInt?)null,
                    onlyTile, includeMissing, (name, progress) => EditorUtility.DisplayCancelableProgressBar("투명 타일 검사", name, progress));
                if (erase)
                {
                    int count = TransparentTileCleaner.Apply(preview);
                    status = $"완전 투명 타일 {count:N0}칸을 지웠습니다. Ctrl+Z로 되돌릴 수 있습니다.";
                    preview = null;
                }
                else status = "검사 완료. 오른쪽 숫자는 타일맵별 삭제 대상 수입니다.";
            }
            catch (OperationCanceledException) { status = "검사를 취소했습니다. 삭제하지 않았습니다."; }
            catch (Exception e) { status = "정리하지 못했습니다: " + e.Message; Debug.LogException(e); }
            finally { EditorUtility.ClearProgressBar(); Repaint(); }
        }

        private void StopPicking()
        {
            if (dragging && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
            picking = dragging = false;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView view)
        {
            if (!limitArea || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var targets = Targets();
            if (areaSize.x > 0 && areaSize.y > 0)
                foreach (var map in targets.Take(50)) DrawArea(map);
            if (!picking || referenceMap == null) return;
            Event e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { StopPicking(); e.Use(); Repaint(); return; }
            if (e.alt || e.button != 0) return;
            if (e.type == EventType.MouseDown || (e.type == EventType.MouseDrag && dragging))
            {
                // Plane follows the actual grid, including translated/rotated/scaled parents.
                Vector3 origin = referenceMap.CellToWorld(new Vector3Int(0, 0, areaMin.z));
                Vector3 right = referenceMap.CellToWorld(new Vector3Int(1, 0, areaMin.z)) - origin;
                Vector3 up = referenceMap.CellToWorld(new Vector3Int(0, 1, areaMin.z)) - origin;
                var plane = new Plane(Vector3.Cross(right, up), origin);
                var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (!plane.Raycast(ray, out float distance)) return;
                var cell = referenceMap.WorldToCell(ray.GetPoint(distance));
                cell.z = areaMin.z;
                if (!dragging)
                {
                    dragging = true;
                    dragStart = cell;
                    dragControl = control;
                    GUIUtility.hotControl = control;
                }
                areaMin = Vector3Int.Min(dragStart, cell);
                var extent = Vector3Int.Max(dragStart, cell) - areaMin + Vector3Int.one;
                areaSize = new Vector3Int(extent.x, extent.y, areaSize.z);
                preview = null;
                e.Use();
                Repaint();
                SceneView.RepaintAll();
            }
            else if (e.type == EventType.MouseUp && dragging) { StopPicking(); e.Use(); Repaint(); }
        }

        private void DrawArea(Tilemap map)
        {
            var max = areaMin + areaSize;
            var points = new[]
            {
                map.CellToWorld(areaMin), map.CellToWorld(new Vector3Int(max.x, areaMin.y, areaMin.z)),
                map.CellToWorld(new Vector3Int(max.x, max.y, areaMin.z)),
                map.CellToWorld(new Vector3Int(areaMin.x, max.y, areaMin.z))
            };
            Handles.DrawSolidRectangleWithOutline(points, new Color(0f, 1f, 1f, .06f), Color.cyan);
        }
    }
}
