using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    public static class StartScreenTools
    {
        [MenuItem("Tools/Start Screen/Create UI In Canvas")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var existing = Object.FindFirstObjectByType<StartScreenView>(FindObjectsInactive.Include);
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.isRootCanvas && c.name == "Canvas");
            if (canvas == null)
            {
                var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(go, "Create Start Screen Canvas");
                canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            }
            var view = StartScreenLayout.Build(canvas.transform);
            Undo.RegisterCreatedObjectUndo(view.gameObject, "Create Start Screen UI");
            Selection.activeGameObject = view.gameObject;
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }
        [MenuItem("Tools/Start Screen/Preview Main Menu")]
        public static void Main() => Preview(0);
        [MenuItem("Tools/Start Screen/Preview Basic Settings")]
        public static void Basic() => Preview(1);
        [MenuItem("Tools/Start Screen/Preview Custom Settings")]
        public static void Custom() => Preview(2);
        [MenuItem("Tools/Start Screen/Preview Part Picker")]
        public static void Picker() => Preview(3);
        private static void Preview(int page)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var view = Object.FindFirstObjectByType<StartScreenView>(FindObjectsInactive.Include);
            if (view == null) { Create(); view = Object.FindFirstObjectByType<StartScreenView>(FindObjectsInactive.Include); }
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Preview Start Screen");
            view.gameObject.SetActive(true); view.ShowMain();
            if (page > 0)
            {
                view.mainMenu.SetActive(false); view.creatorPanel.SetActive(true);
                view.basicPanel.SetActive(page == 1); view.customPanel.SetActive(page > 1);
                view.next.gameObject.SetActive(page == 1); view.back.gameObject.SetActive(page > 1); view.finish.gameObject.SetActive(page > 1);
                view.creatorTitle.text = page == 1 ? "캐릭터 기본 설정" : "캐릭터 커스텀 설정";
                view.pickerPanel.SetActive(page == 3);
                view.customScroll.verticalNormalizedPosition = 1;
            }
            Selection.activeGameObject = page == 0 ? view.mainMenu : page == 1 ? view.basicPanel : page == 2 ? view.customPanel : view.pickerPanel;
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }
    }

    public class StartScreenTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Sprites/StartScreen/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.textureShape = TextureImporterShape.Texture2D;
            if (importer.spriteImportMode == SpriteImportMode.None) importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = assetPath.EndsWith("/StartScreen.png") ? FilterMode.Bilinear : FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (assetPath.EndsWith("/StartScreen.png")) importer.spriteImportMode = SpriteImportMode.Single;
            if (assetPath.Contains("/CharacterPreview/"))
            {
                importer.isReadable = true;
                importer.spriteImportMode = SpriteImportMode.Single;
            }
        }
    }
}
