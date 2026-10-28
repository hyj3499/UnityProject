using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FarmMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ContinueChecks
{
    private static int checks;
    private static string root;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++; Debug.Log("PASS: " + message);
    }
    private static void Setup()
    {
        if (!File.Exists("continue-validation")) throw new Exception("Isolated project required");
        Directory.CreateDirectory("Output");
        root = Path.GetFullPath("Output/SaveTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        typeof(SaveSystem).GetField("TestSaveRoot", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, root);
    }
    public static void Run()
    {
        try
        {
            Setup();
            Check(!SaveSystem.HasSave && SaveSystem.CanCreate, "No saves means no Continue");
            var legacy = new GameData(); legacy.farmer.playerName = "기존 농부";
            File.WriteAllText(Path.Combine(root, "farm_save.json"), JsonUtility.ToJson(legacy));
            var migrated = SaveSystem.Load();
            Check(migrated.saveId == "legacy" && migrated.farmer.playerName == "기존 농부", "Legacy snapshot retains profile and gets stable identity");
            migrated.farmer.money = 1234;
            Check(SaveSystem.TrySave(migrated) && SaveSystem.GetSaves().Count == 1 && SaveSystem.Load("legacy").farmer.money == 1234, "Legacy resave replaces the same snapshot");
            var first = new GameData(); first.farmer.playerName = "테스트 농부";
            first.farmer.birthdaySeason = 2; first.farmer.birthdayDay = 15;
            first.farmer.appearance.eyes = FarmerEyes.SecondStyle;
            first.farmer.appearance.skin.custom = true; first.farmer.appearance.skin.rgb = new Color(.7f,.5f,.3f);
            first.farmer.money = 45678; first.currentDay = 35; first.playTimeSeconds = 7260;
            Check(SaveSystem.TrySave(first), "Create independent character");
            string identity = first.saveId;
            first.farmer.money = 56789;
            Check(SaveSystem.TrySave(first) && first.saveId == identity && SaveSystem.GetSaves().Count == 2, "Repeated save never creates another slot");
            var loaded = SaveSystem.Load(identity);
            Check(loaded.farmer.money == 56789 && loaded.playTimeSeconds == 7260 && loaded.farmer.appearance.eyes == FarmerEyes.SecondStyle, "Selected ID reload preserves progress, time and appearance");
            for (int i = 0; i < 3; i++) { var d = new GameData(); d.farmer.playerName = "다른 농부 " + i; Check(SaveSystem.TrySave(d), "Create slot " + (i + 3)); }
            Check(!SaveSystem.CanCreate && SaveSystem.GetSaves().Count == 5, "Five save capacity");
            var sixth = new GameData();
            Check(!SaveSystem.TrySave(sixth) && string.IsNullOrEmpty(sixth.saveId) && SaveSystem.GetSaves().Count == 5, "Sixth character is rejected without altering five saves");
            Check(SaveSystem.TrySave(first) && SaveSystem.GetSaves()[0].saveId == identity, "Existing character can save when full and sorts first");
            Check(SaveSystem.Load("../farm_save") == null && !SaveSystem.DeleteSave("../farm_save"), "Invalid IDs cannot target other files");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var view = StartScreenLayout.Build(canvas.transform);
            Directory.CreateDirectory("Assets/Resources/UI");
            PrefabUtility.SaveAsPrefabAsset(view.gameObject, "Assets/Resources/UI/GeneratedStartScreenUI.prefab");
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(view.gameObject);
            view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<StartScreenView>("Assets/Resources/UI/GeneratedStartScreenUI.prefab"), canvas.transform);
            CheckReferences(view);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var host = new GameObject("Menu"); var menu = host.AddComponent<MainMenuController>(); menu.Boot(null);
            Check(view.continueGame.interactable && view.continueGame.GetComponent<CanvasGroup>().alpha == 1, "Continue is enabled for existing saves");
            view.newGame.onClick.Invoke();
            Check(view.modalPanel.activeSelf && !view.creatorPanel.activeSelf && view.modalMessage.text.Contains("5개"), "Full capacity blocks New Game with explanation");
            view.modalYes.onClick.Invoke();
            view.continueGame.onClick.Invoke();
            Check(view.continuePanel.activeSelf && !view.mainMenu.activeSelf && view.saveName.text == "테스트 농부", "Continue opens most recently saved character");
            Check(view.saveBirthday.text == "가을 15일" && view.saveDate.text == "1년 여름 7일" && view.saveMoney.text == "56,789 G" && view.savePlayTime.text == "2시간 1분", "All saved metadata uses game calendar and actual play duration");
            var expected = StartScreenPortrait.Render(view, first.farmer.appearance, 0, false);
            Check(expected.GetPixels32().SequenceEqual(((Texture2D)view.savedCharacterPreview.texture).GetPixels32()), "Portrait matches saved customized front appearance");
            Object.DestroyImmediate(expected);
            Render(canvas, "Continue-1920x1080", 1920,1080);
            Render(canvas, "Continue-1280x720", 1280,720);
            Render(canvas, "Continue-1024x768", 1024,768);
            var sorted = SaveSystem.GetSaves(); view.nextSave.onClick.Invoke();
            Check(view.saveName.text == sorted[1].farmer.playerName, "Right arrow selects the next most recent save");
            view.previousSave.onClick.Invoke(); view.previousSave.onClick.Invoke();
            Check(view.saveName.text == sorted[4].farmer.playerName, "Left arrow wraps to oldest save");
            view.nextSave.onClick.Invoke();
            view.deleteSave.onClick.Invoke();
            Check(view.modalPanel.activeSelf && SaveSystem.GetSaves().Count == 5, "Delete asks before touching selected save");
            view.modalNo.onClick.Invoke();
            Check(SaveSystem.Load(identity) != null && view.continuePanel.activeSelf, "Cancel deletion preserves save and selection");
            view.deleteSave.onClick.Invoke(); view.modalYes.onClick.Invoke();
            Check(SaveSystem.Load(identity) == null && SaveSystem.GetSaves().Count == 4 && SaveSystem.CanCreate, "Confirm deletes only selected ID and frees capacity");
            view.closeContinue.onClick.Invoke();
            Check(view.mainMenu.activeSelf && !view.continuePanel.activeSelf, "Close returns to title");
            view.continueGame.onClick.Invoke();
            while (SaveSystem.GetSaves().Count > 1) { view.deleteSave.onClick.Invoke(); view.modalYes.onClick.Invoke(); }
            Check(!view.nextSave.interactable && !view.previousSave.interactable, "One save disables arrows");
            view.deleteSave.onClick.Invoke(); view.modalYes.onClick.Invoke();
            Check(view.mainMenu.activeSelf && !view.continueGame.interactable && view.continueGame.GetComponent<CanvasGroup>().alpha < .5f, "Deleting last save closes panel and dims Continue");
            Render(canvas, "Continue-EmptyTitle", 1920,1080);
            view.newGame.onClick.Invoke(); Check(view.creatorPanel.activeSelf, "New Game works after freeing capacity");
            view.closeCreator.onClick.Invoke(); view.modalYes.onClick.Invoke();
            var badPath = Path.Combine(root, "Saves", Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(badPath,"{}");
            Check(!SaveSystem.HasSave, "Invalid file cannot enable Continue"); File.Delete(badPath);
            Object.DestroyImmediate(host);
            host = new GameObject("Menu reboot"); host.AddComponent<MainMenuController>().Boot(null);
            var again = new GameData(); again.farmer.playerName = "재시작"; SaveSystem.TrySave(again);
            view.continueGame.onClick.Invoke(); Check(view.saveName.text == "재시작", "Rebinding scene UI refreshes data and has no stale listeners");
            Object.DestroyImmediate(host);
            File.WriteAllText("Output/result.txt", "PASS: " + checks + " Continue checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("Output/result.txt", "FAIL: " + e); EditorApplication.Exit(1); }
    }
    private static void CheckReferences(StartScreenView view)
    {
        Check(view.continuePanel != null && view.closeContinue != null && view.previousSave != null && view.nextSave != null && view.startSave != null && view.deleteSave != null, "Serialized navigation references survive prefab reload");
        Check(view.savedCharacterPreview != null && view.savePosition != null && view.saveName != null && view.saveBirthday != null && view.saveDate != null && view.saveMoney != null && view.savePlayTime != null, "Serialized info and portrait references survive prefab reload");
        Check(AssetDatabase.GetAssetPath(view.startSave.image.sprite).EndsWith("Btn_White.png") && AssetDatabase.GetAssetPath(view.deleteSave.image.sprite).EndsWith("Btn_White.png"), "Actions use requested white button art");
        Check(AssetDatabase.GetAssetPath(view.closeContinue.image.sprite).EndsWith("Close_btn.png") && AssetDatabase.GetAssetPath(view.nextSave.image.sprite).EndsWith("Arrow_btn.png"), "Close and arrows use requested art");
        Check(AssetDatabase.GetAssetPath(view.continuePanel.transform.Find("GameDataInfoPanel").GetComponent<Image>().sprite).EndsWith("game data info pannel.png"), "Info panel uses requested art");
    }
    public static void VerifyInstalled()
    {
        try
        {
            Setup();
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var view = Object.FindFirstObjectByType<StartScreenView>(FindObjectsInactive.Include);
            Check(view != null && view.transform.parent.name == "Canvas", "Scene contains editable Canvas/StartScreenUI");
            CheckReferences(view);
            Check(view.continuePanel.transform.parent == view.mainMenu.transform.parent, "Continue is a scene-authored sibling of character creation");
            Check(view.continuePanel.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts in installed Continue hierarchy");
            CheckReferences(AssetDatabase.LoadAssetAtPath<StartScreenView>("Assets/Resources/UI/StartScreenUI.prefab"));
            var host = new GameObject("Installed menu test"); host.AddComponent<MainMenuController>().Boot(null);
            var sample = new GameData(); sample.farmer.playerName = "설치 확인"; SaveSystem.TrySave(sample);
            view.continueGame.onClick.Invoke(); Check(view.saveName.text == "설치 확인", "Installed scene runs Continue controller");
            Render(view.GetComponentInParent<Canvas>().rootCanvas, "Continue-Installed",1920,1080);
            Object.DestroyImmediate(host);
            File.WriteAllText("Output/installed-result.txt", "PASS: " + checks + " installed checks"); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("Output/installed-result.txt", "FAIL: " + e); EditorApplication.Exit(1); }
    }
    private static void Render(Canvas canvas, string name, int width, int height)
    {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        var camera = new GameObject("Render camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 540;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
        canvas.worldCamera = camera; canvas.planeDistance = 1;
        var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        foreach (var fit in canvas.GetComponentsInChildren<StartScreenFit>(true)) fit.SendMessage("LateUpdate");
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes("Output/" + name + ".png", texture.EncodeToPNG()); RenderTexture.active = previous;
        camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture); Object.DestroyImmediate(camera.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }
}
