#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using FarmMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class UIThemeChecks
{
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Output");
            foreach (var path in new[] {"Assets/Resources/UI/StartScreenUI.prefab", "Assets/Prefabs/UI/ShopBookUi.prefab"})
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                UIThemeTools.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            UIThemeTools.ApplyScene();
            EditorSceneManager.SaveScene(scene);
            Validate();
            File.WriteAllText("Output/result.txt", "PASS: scene and prefab art, pressed bottom alignment, edit persistence, and confirmation sorting.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void VerifyInstalled()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            Validate();
            File.WriteAllText("Output/installed.txt", "PASS: final installed scene reloads with correct buttons, panels and references.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void Validate()
    {
        var start = Object.FindFirstObjectByType<StartScreenView>(FindObjectsInactive.Include);
        var book = Object.FindFirstObjectByType<BookMenuView>(FindObjectsInactive.Include);
        var shop = Object.FindFirstObjectByType<ShopBookView>(FindObjectsInactive.Include);
        var dialog = Object.FindFirstObjectByType<ConfirmationDialogView>(FindObjectsInactive.Include);
        foreach (var button in new[] {start.newGame, start.continueGame, start.settings, start.next,
            start.back, start.finish, start.modalYes, start.modalNo, start.editName,
            book.applyDisplay, book.confirmYes, book.confirmNo,
            book.saveAndQuit, book.quit, shop.buy, shop.cancelQuantity,
            dialog.yes, dialog.no}.Concat(book.categories)) CheckButton(button, false);
        CheckButton(start.pickerApply, true);
        foreach (var button in new[] {start.exit, start.settingsBack, start.closeCreator, start.pickerClose, book.close, shop.close})
            Check(button.image.sprite.texture.name == "Close_btn" && button.spriteState.pressedSprite != null
                && button.image.sprite.rect.size == button.spriteState.pressedSprite.rect.size, "Close normal/pressed art");
        foreach (var button in new[] {start.rotateLeft, start.rotateRight, start.seasonPrevious, start.seasonNext,
            start.dayPrevious, start.dayNext, shop.decrease, shop.increase})
            Check(button.image.sprite.texture.name == "Arrow_btn" && button.image.preserveAspect, "Directional arrow art");
        foreach (var panel in new[] {start.modalMessage.transform.parent.GetComponent<Image>(),
            book.confirmationText.transform.parent.GetComponent<Image>(), dialog.panel})
            Check(panel.sprite.texture.name == "Pannel_White" && panel.type == Image.Type.Sliced, "Serialized panel art");
        Check(dialog.GetComponent<Canvas>().sortingOrder > shop.GetComponent<Canvas>().sortingOrder, "Shop confirmation renders above book");
        Check(dialog.transform.parent.name == "Canvas", "Editable confirmation is in scene Canvas");
        Check(!dialog.gameObject.activeSelf, "Confirmation is initially hidden");

        // Exercise actual uGUI state transitions on a standalone live canvas.
        var previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(previewScene);
        var canvas = new GameObject("Preview", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(960, 540);
        var events = new GameObject("EventSystem").AddComponent<EventSystem>();
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.orthographic = true; camera.orthographicSize = 270; camera.farClipPlane = 2000;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f, .15f, .17f);
        camera.cullingMask = 1 << 31;
        canvas.worldCamera = camera;
        var clone = Object.Instantiate(dialog, canvas.transform, false);
        PreviewLayer(canvas.gameObject);
        clone.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)clone.yes.transform;
        var position = rect.anchoredPosition;
        var size = rect.sizeDelta;
        Capture(camera, "Output/confirmation-normal.png");
        var pointer = new PointerEventData(events) {button = PointerEventData.InputButton.Left};
        clone.yes.OnPointerEnter(pointer); clone.yes.OnPointerDown(pointer);
        Check(clone.yes.image.overrideSprite == clone.yes.spriteState.pressedSprite, "Pointer down switches to pressed art");
        Check(rect.anchoredPosition == position && rect.sizeDelta == size, "Pressed leaves bottom and hit rectangle fixed");
        Capture(camera, "Output/confirmation-pressed.png");
        clone.yes.OnPointerUp(pointer); clone.yes.OnPointerExit(pointer);
        Check(clone.yes.image.overrideSprite == clone.yes.image.sprite, "Release restores normal art");
        clone.yes.OnPointerDown(pointer);
        clone.gameObject.SetActive(false); clone.gameObject.SetActive(true);
        Check(clone.yes.image.overrideSprite == clone.yes.image.sprite, "Reopening clears pressed state");
        // Scene edits are consumed unchanged: no theme component rewrites them.
        clone.panel.color = Color.cyan; rect.sizeDelta = new Vector2(250, 70);
        clone.gameObject.SetActive(false); clone.gameObject.SetActive(true);
        Check(clone.panel.color == Color.cyan && rect.sizeDelta == new Vector2(250, 70), "Inspector edits persist");
        Object.DestroyImmediate(clone.gameObject);

        var picker = Object.Instantiate(start.pickerPanel, canvas.transform, false);
        PreviewLayer(picker);
        picker.SetActive(true);
        var pickerRect = (RectTransform)picker.transform;
        pickerRect.anchoredPosition = Vector2.zero; pickerRect.localScale = Vector3.one * .7f;
        Capture(camera, "Output/part-picker.png");
        Object.DestroyImmediate(picker);
        var startModal = Object.Instantiate(start.modalPanel, canvas.transform, false);
        startModal.SetActive(true);
        PreviewLayer(startModal);
        Capture(camera, "Output/character-confirmation.png");
        Object.DestroyImmediate(startModal);
        var bookPreview = Object.Instantiate(book, canvas.transform, false);
        bookPreview.gameObject.SetActive(true);
        bookPreview.book.gameObject.SetActive(false);
        bookPreview.confirmation.SetActive(true);
        PreviewLayer(bookPreview.gameObject);
        Capture(camera, "Output/book-confirmation.png");
        EditorSceneManager.CloseScene(previewScene, true);
    }

    private static void PreviewLayer(GameObject root)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
    }

    private static void CheckButton(Button button, bool dark)
    {
        Check(button != null && button.image != null, "Button reference");
        var normal = button.image.sprite;
        var pressed = button.spriteState.pressedSprite;
        Check(normal != null && pressed != null && normal.texture.name == (dark ? "Btn_Dark" : "Btn_White"), "Button sprite: " + button.name);
        Check(button.transition == Selectable.Transition.SpriteSwap, "SpriteSwap: " + button.name);
        Check(normal.rect.size == pressed.rect.size && normal.rect.y == pressed.rect.y && normal.border == pressed.border, "Common sprite bounds and borders");
        Check(button.image.type == Image.Type.Sliced && !button.image.preserveAspect, "Stable sliced rectangle");
    }

    private static void Capture(Camera camera, string path)
    {
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(960, 540, 24);
        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(960, 540, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = null;
        Object.DestroyImmediate(texture); Object.DestroyImmediate(rt);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Debug.Log("UI THEME PASS: " + message);
    }
}
#endif
