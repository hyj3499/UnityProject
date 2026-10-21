using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>런타임에 생성되는 카메라의 화면 범위를 에디터에서 쉽게 조절하는 창.</summary>
    internal class CameraViewTools : EditorWindow
    {
        private const string MenuPath = "Tools/FarmMVP/카메라 화면 범위";
        private const string AssetPath = "Assets/Resources/CameraViewSettings.asset";
        private CameraViewSettings _settings;
        private Tilemap _previewMap;
        private float _aspect = 16f / 9f;
        private string _status;

        [MenuItem(MenuPath, false, 10)]
        private static void Open()
        {
            GetWindow<CameraViewTools>("카메라 화면 범위").minSize = new Vector2(360f, 285f);
        }

        private void OnEnable()
        {
            _settings = LoadOrCreate();
            SceneView.duringSceneGui += DrawPreview;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawPreview;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("게임 화면(흰 사각형) 크기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "이 게임의 Main Camera는 Play를 누른 뒤 코드로 만들어집니다. " +
                "그래서 여기서 기본 화면 크기를 조절합니다.\n\n" +
                "작게 = 더 확대됨 = 흰 사각형이 작아짐\n" +
                "크게 = 더 멀리 보임 = 흰 사각형이 커짐", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            float size = EditorGUILayout.Slider("화면 크기", _settings.orthographicSize, 0.5f, 20f);
            float zoom = EditorGUILayout.Slider("기본 줌", _settings.defaultZoom, GameManager.MinZoom, GameManager.MaxZoom);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_settings, "Change Camera View Size");
                _settings.orthographicSize = size;
                _settings.defaultZoom = zoom;
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(6f);
            _aspect = Mathf.Max(0.5f, EditorGUILayout.FloatField("미리보기 가로/세로 비율", _aspect));
            _previewMap = (Tilemap)EditorGUILayout.ObjectField("중심에 둘 타일맵", _previewMap, typeof(Tilemap), true);
            if (_previewMap == null && Selection.activeGameObject != null)
                _previewMap = Selection.activeGameObject.GetComponent<Tilemap>();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택한 타일맵 사용"))
                {
                    _previewMap = Selection.activeGameObject == null
                        ? null : Selection.activeGameObject.GetComponent<Tilemap>();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("기본값 5.5로 복원"))
                {
                    Undo.RecordObject(_settings, "Reset Camera View Size");
                    _settings.orthographicSize = 5.5f;
                    _settings.defaultZoom = 1f;
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssets();
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space(5f);
            EditorGUILayout.HelpBox(
                _previewMap == null
                    ? "Location_Farm을 선택한 뒤 '선택한 타일맵 사용'을 누르면 Scene 창에 흰 화면 범위 미리보기가 보입니다."
                    : "Scene 창의 흰 테두리는 이 타일맵 중앙을 기준으로 한 카메라 한 화면 크기 미리보기입니다.",
                MessageType.None);
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
        }

        private void DrawPreview(SceneView sceneView)
        {
            if (_settings == null || _previewMap == null || Event.current.type != EventType.Repaint) return;

            _previewMap.CompressBounds();
            BoundsInt cells = _previewMap.cellBounds;
            if (cells.size.x <= 0 || cells.size.y <= 0) return;

            Vector3 min = _previewMap.GetCellCenterWorld(cells.min);
            Vector3 max = _previewMap.GetCellCenterWorld(cells.max - Vector3Int.one);
            Vector3 center = (min + max) * 0.5f;
            float halfHeight = _settings.orthographicSize * _settings.defaultZoom;
            float halfWidth = halfHeight * _aspect;
            Rect rect = Rect.MinMaxRect(center.x - halfWidth, center.y - halfHeight,
                                        center.x + halfWidth, center.y + halfHeight);

            Handles.DrawSolidRectangleWithOutline(rect, new Color(1f, 1f, 1f, 0.035f), Color.white);
            Handles.Label(new Vector3(rect.xMin, rect.yMax, 0f), "카메라 화면 범위");
        }

        private static CameraViewSettings LoadOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CameraViewSettings>(AssetPath);
            if (settings != null) return settings;

            settings = CreateInstance<CameraViewSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
