using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    public static class UIThemeTools
    {
        [MenuItem("Tools/UI Theme/Apply Button And Confirmation Art")]
        public static void ApplyScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                Undo.RegisterFullObjectHierarchyUndo(root, "Apply UI art");
                Apply(root);
            }
            if (!Object.FindObjectsByType<ConfirmationDialogView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(v => v.gameObject.scene == scene))
            {
                var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .First(c => c.name == "Canvas" && c.isRootCanvas && c.gameObject.scene == scene);
                var dialog = ConfirmationDialogView.Build(canvas.transform);
                Undo.RegisterCreatedObjectUndo(dialog.gameObject, "Create editable confirmation");
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void Apply(GameObject root)
        {
            foreach (var view in root.GetComponentsInChildren<StartScreenView>(true))
            {
                foreach (var button in new[] {view.newGame, view.continueGame, view.settings, view.exit,
                    view.next, view.back, view.finish, view.settingsBack, view.modalYes, view.modalNo})
                    UITheme.ApplyButton(button, false, 4);
                UITheme.ApplyButton(view.pickerApply, true, 4);
                foreach (var button in new[] {view.pickerClose, view.closeCreator, view.exit, view.settingsBack}) UITheme.ApplyClose(button);
                UITheme.ApplyGlyphButton(view.editName, "수정", false, 2);
                foreach (var b in new[] {view.rotateLeft, view.seasonPrevious, view.dayPrevious}) UITheme.ApplyArrow(b, false);
                foreach (var b in new[] {view.rotateRight, view.seasonNext, view.dayNext}) UITheme.ApplyArrow(b, true);
                UITheme.ApplyPanel(view.modalMessage.transform.parent.GetComponent<Image>(), 4);
            }
            foreach (var view in root.GetComponentsInChildren<BookMenuView>(true))
            {
                foreach (var button in new[] {view.close, view.previousMode, view.nextMode,
                    view.previousResolution, view.nextResolution, view.applyDisplay, view.saveAndQuit,
                    view.quit, view.confirmYes, view.confirmNo}.Concat(view.categories))
                    UITheme.ApplyButton(button);
                UITheme.ApplyPanel(view.confirmationText.transform.parent.GetComponent<Image>(), 2);
                UITheme.ApplyClose(view.close);
            }
            foreach (var view in root.GetComponentsInChildren<ShopBookView>(true))
            {
                foreach (var button in new[] {view.buy, view.close, view.cancelQuantity}) UITheme.ApplyButton(button);
                UITheme.ApplyClose(view.close);
                UITheme.ApplyArrow(view.decrease, false);
                UITheme.ApplyArrow(view.increase, true);
                foreach (var bar in view.GetComponentsInChildren<Scrollbar>(true)) UITheme.ApplyShopScrollbar(bar);
            }
            // Preserve prefab overrides so the scene shows exactly the authored art.
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (!(component is Image) && !(component is Selectable) && !(component is Text) && !(component is RectTransform)) continue;
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        [MenuItem("Tools/UI Theme/Preview Gameplay Confirmation")]
        private static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var view = Object.FindFirstObjectByType<ConfirmationDialogView>(FindObjectsInactive.Include);
            if (view == null) { ApplyScene(); view = Object.FindFirstObjectByType<ConfirmationDialogView>(FindObjectsInactive.Include); }
            Undo.RecordObject(view.gameObject, "Preview confirmation");
            view.gameObject.SetActive(true);
            Selection.activeGameObject = view.panel.gameObject;
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }
    }
}
