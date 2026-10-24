using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmMVP
{
    public static class ShopBookUILayoutTools
    {
        [MenuItem("Tools/Book UI/Build Interactive Shop Book", false, 2)]
        public static void BuildInteractiveShop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.isRootCanvas && c.gameObject.scene == scene && c.name == "Canvas");
            if (canvas == null) { Debug.LogError("씬에 Canvas가 필요합니다."); return; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Shop Book");
            var legacy = canvas.transform.Find("ShopBookUI");
            if (legacy != null) Undo.DestroyObjectImmediate(legacy.gameObject);
            var root = canvas.transform.Find("ShopBookUi") as RectTransform;
            if (root == null) root = ShopBookLayout.Node(canvas.transform, "ShopBookUi", 0, 0, 1, 1);
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Build Shop Book");
            var view = ShopBookLayout.Build(root);
            ShowPreview(view);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root.gameObject;
            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("Tools/Book UI/Make Shop Book Editable In Scene", false, 3)]
        public static void MakeShopBookEditableInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var scene = SceneManager.GetActiveScene();
            var root = Object.FindObjectsByType<ShopBookView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(view => view.gameObject.scene == scene)?.transform;
            if (root == null)
            {
                Debug.LogWarning("Book UI: ShopBookUi를 찾을 수 없습니다.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Make Shop Book Editable In Scene");
            if (PrefabUtility.IsPartOfPrefabInstance(root.gameObject))
                PrefabUtility.UnpackPrefabInstance(root.gameObject, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            var view = root.GetComponent<ShopBookView>();
            ShopBookLayout.Upgrade(view);
            ShowPreview(view);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root.gameObject);
            Debug.Log("Book UI: 상점 미리보기를 켰습니다. Canvas/ShopBookUi 아래에서 편집한 뒤 씬을 저장하세요. 플레이 시 자동으로 숨겨집니다.", root);
        }

        private static void ShowPreview(ShopBookView view)
        {
            // 프리팹을 풀어도 inactive/alpha=0 상태는 그대로이므로 미리보기 상태를 명시적으로 복원한다.
            for (var node = view.transform; node != null; node = node.parent)
            {
                Undo.RecordObject(node.gameObject, "Show Shop Book Preview");
                node.gameObject.SetActive(true);
                var canvas = node.GetComponent<Canvas>();
                if (canvas != null) { Undo.RecordObject(canvas, "Show Shop Book Preview"); canvas.enabled = true; }
                var group = node.GetComponent<CanvasGroup>();
                if (group != null) { Undo.RecordObject(group, "Show Shop Book Preview"); group.alpha = 1; }
                SceneVisibilityManager.instance.Show(node.gameObject, false);
            }
            view.stage.gameObject.SetActive(true);
            view.book.gameObject.SetActive(true);
            view.pages.gameObject.SetActive(true);
            view.pages.alpha = 1;
            view.pages.blocksRaycasts = true;
            view.pages.interactable = true;
            view.detailPaper.gameObject.SetActive(true);
            view.background.gameObject.SetActive(true);
            view.background.enabled = true;
            view.background.sprite = view.idle;
            var background = view.background.rectTransform;
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            if (view.productPreview != null) view.productPreview.SetActive(true);
            if (view.categoryPreview != null) view.categoryPreview.SetActive(true);
            view.categoriesScroll.gameObject.SetActive(true);
            view.quantityControls.SetActive(true);
            view.quantity.text = "1";
            view.cancelQuantity.gameObject.SetActive(true);
            view.cancelQuantity.interactable = false;
            ShopBookLayout.ArrangeInventory(view, 10, true);
            view.inventoryTooltip.gameObject.SetActive(false);
            SceneVisibilityManager.instance.Show(view.gameObject, true);
            SceneVisibilityManager.instance.EnablePicking(view.gameObject, true);
            Tools.visibleLayers |= 1 << view.gameObject.layer;
            Canvas.ForceUpdateCanvases();
            view.Fit();
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            view.stage.GetWorldCorners(corners);
            var bounds = new Bounds(corners[0], Vector3.zero);
            foreach (var corner in corners) bounds.Encapsulate(corner);
            var sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            sceneView.in2DMode = true;
            sceneView.Frame(bounds, true);
            SceneView.RepaintAll();
        }
    }
}
