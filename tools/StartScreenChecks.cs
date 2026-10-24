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

public static class StartScreenChecks
{
    private static int _checks;
    public static void Run()
    {
        try
        {
            if (!File.Exists("start-screen-validation")) throw new Exception("Isolated project required");
            Directory.CreateDirectory("Output");
            foreach (var file in Directory.GetFiles("Assets/Resources/Sprites/StartScreen/CharacterPreview", "*.png"))
            {
                var path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Debug.Log("PREVIEW " + path + " -> " + string.Join(",", AssetDatabase.LoadAllAssetsAtPath(path).Select(a => a.GetType().Name + ":" + a.name)));
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
            var view = StartScreenLayout.Build(canvas.transform);
            Check(view.parts.Length == 8 && view.parts.Skip(2).All(p => p.options.Length == 1 && p.options[0] == ""), "Unmade parts each have exactly one empty slot");
            Check(view.parts[1].options.Length == 2, "Both authored eye styles available");
            Check(view.GetComponentsInChildren<Image>(true).Where(i => i.sprite != null).All(i => AssetDatabase.GetAssetPath(i.sprite).StartsWith("Assets/Resources/Sprites/StartScreen/")), "All UI sprites belong to StartScreen");
            Check(view.GetComponentsInChildren<Text>(true).All(t => AssetDatabase.GetAssetPath(t.font).StartsWith("Assets/Resources/Sprites/StartScreen/")), "All fonts also belong to StartScreen");
            Check(view.previewBody.isReadable && view.previewEyes.isReadable && view.previewHeadGuide.isReadable, "Preview pixels readable");
            Check(!view.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("농장 이야기")), "No title text");
            Check(view.newGame.GetComponentInChildren<Text>().text == "NEW GAME", "English main menu");
            Check(view.mainMenu.transform.Find("StartScreen").GetComponent<Image>().sprite.rect.width > 1000, "Main illustration uses full artwork rather than an automatic edge slice");
            view.characterPreview.texture = SaveDefaultPortrait(view, false);
            view.parts[1].portrait.texture = SaveDefaultPortrait(view, true);
            Directory.CreateDirectory("Assets/Resources/UI");
            PrefabUtility.SaveAsPrefabAsset(view.gameObject, "Assets/Resources/UI/StartScreenUI.prefab");
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(view.gameObject);
            view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<StartScreenView>("Assets/Resources/UI/StartScreenUI.prefab"), canvas.transform);
            view.name = "StartScreenUI";
            Check(view.next != null && view.pickerContent != null && view.parts[7].value != null, "Prefab reload preserves authoring references");
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var menuObject = new GameObject("Menu test");
            var menu = menuObject.AddComponent<MainMenuController>(); menu.Boot(null);
            var creator = menuObject.GetComponent<CharacterCreatorUI>();
            Render(canvas, "MainMenu");
            view.newGame.onClick.Invoke();
            Check(view.basicPanel.activeSelf && view.nameInput.text == "Ros" && view.nameInput.readOnly, "New game opens defaults and locked name");
            Check(!view.male.isOn && !view.female.isOn, "No default gender");
            view.next.onClick.Invoke();
            Check(view.modalPanel.activeSelf && view.modalMessage.text.Contains("성별") && !view.customPanel.activeSelf, "Missing gender blocks next step with alert");
            view.modalYes.onClick.Invoke();
            view.editName.onClick.Invoke(); Check(!view.nameInput.readOnly, "Edit activates name input");
            view.nameInput.text = "Ros 테스트"; view.nameInput.onEndEdit.Invoke(view.nameInput.text);
            view.male.isOn = true; view.female.isOn = true;
            Check(view.female.isOn && !view.male.isOn && creator.Draft.Profile.gender == "female", "Radio selection exclusive");
            view.female.isOn = false; Check(view.female.isOn, "Selected gender cannot be toggled off");
            view.seasonPrevious.onClick.Invoke(); view.dayPrevious.onClick.Invoke();
            Check(creator.Draft.Profile.birthdaySeason == 3 && creator.Draft.Profile.birthdayDay == 30, "Birthday backwards wraps winter/day30");
            view.seasonNext.onClick.Invoke(); view.dayNext.onClick.Invoke();
            Check(creator.Draft.Profile.birthdaySeason == 0 && creator.Draft.Profile.birthdayDay == 1, "Birthday forwards wraps spring/day1");
            var initialPixels = ((Texture2D)view.characterPreview.texture).GetPixels32();
            view.rotateRight.onClick.Invoke(); Check(creator.Draft.Facing == 1, "Rotate right");
            view.rotateLeft.onClick.Invoke(); Check(creator.Draft.Facing == 0, "Rotate left");
            Render(canvas, "BasicSettings");
            view.next.onClick.Invoke();
            Check(view.customPanel.activeSelf && view.creatorTitle.text == "캐릭터 커스텀 설정", "Next opens custom settings");
            view.parts[1].select.onClick.Invoke();
            var slots = view.pickerContent.GetComponentsInChildren<Button>().ToArray();
            Check(slots.Length == 2 && ((Texture2D)slots[0].GetComponentInChildren<RawImage>().texture).width == 16, "Two true 16px eye portraits");
            slots[1].onClick.Invoke();
            Check(creator.Draft.Preview().eyes == FarmerEyes.SecondStyle && creator.Draft.Appearance.eyes == FarmerEyes.FirstStyle, "Slot click is tentative preview");
            view.parts[2].select.onClick.Invoke();
            Check(creator.Draft.Preview().eyes == FarmerEyes.FirstStyle && view.pickerContent.GetComponentsInChildren<Button>().Length == 1, "Switching part rolls back pending eyes; hair has one empty slot");
            view.parts[1].select.onClick.Invoke(); view.pickerContent.GetComponentsInChildren<Button>()[1].onClick.Invoke(); view.pickerApply.onClick.Invoke();
            Check(creator.Draft.Appearance.eyes == FarmerEyes.SecondStyle && !view.pickerPanel.activeSelf, "Apply commits chosen eyes");
            view.parts[0].recommendations[3].onClick.Invoke(); view.parts[1].recommendations[3].onClick.Invoke();
            Check(creator.Draft.Appearance.skin.custom && creator.Draft.Appearance.eyeColor.custom, "Recommended colors update both body and eyes");
            view.parts[0].hue.value = .2f; view.parts[0].saturation.value = .4f; view.parts[0].value.value = .8f;
            Check(Vector4.Distance(creator.Draft.Appearance.skin.rgb, Color.HSVToRGB(.2f, .4f, .8f)) < .001f, "HSV drives stored color");
            Check(!initialPixels.SequenceEqual(((Texture2D)view.characterPreview.texture).GetPixels32()), "Preview changes with selected appearance");
            var data = new GameData(); creator.Draft.Profile.Apply(data.farmer); data.farmer.appearance = creator.Draft.Appearance.Clone();
            var loaded = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(data));
            Check(loaded.farmer.playerName == "Ros 테스트" && loaded.farmer.gender == "female" && loaded.farmer.birthdayDay == 1 && loaded.farmer.appearance.eyeColor.custom, "Profile and colors survive save roundtrip");
            Check(loaded.farmer.appearance.eyes == FarmerEyes.SecondStyle, "Eye selection survives save roundtrip");
            var character = new GameObject("Renderer parity").AddComponent<FarmerAnimator>(); character.Init(loaded.farmer.appearance);
            var body = character.transform.Find("Body").GetComponent<SpriteRenderer>().sprite;
            Check(body.texture.GetPixels32().Any(p => ((Color32)loaded.farmer.appearance.skin.rgb).Equals(p)), "Game renderer uses selected skin pixels");
            var eye = character.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Eyes").sprite;
            Check(eye.texture.GetPixels32().Any(p => ((Color32)loaded.farmer.appearance.eyeColor.rgb).Equals(p)), "Game renderer uses selected eye pixels");
            Object.DestroyImmediate(character.gameObject);
            Render(canvas, "CustomSettings");
            view.parts[1].select.onClick.Invoke(); Render(canvas, "EyePicker");
            // Overflow exercise uses an authored options list, just like adding more eye styles later.
            view.parts[1].options = Enumerable.Repeat(FarmerEyes.FirstStyle, 40).ToArray();
            view.parts[1].select.onClick.Invoke(); Canvas.ForceUpdateCanvases(); Canvas.ForceUpdateCanvases();
            Check(view.pickerContent.rect.height > view.pickerScroll.viewport.rect.height && view.pickerScroll.verticalScrollbar.gameObject.activeSelf, "Many slots produce a working scroll area");
            view.parts[1].options = new[] { FarmerEyes.FirstStyle, FarmerEyes.SecondStyle };
            creator.ClosePicker(); view.closeCreator.onClick.Invoke();
            Check(view.modalMessage.text == "캐릭터 생성을 취소하시겠습니까?", "Cancel asks confirmation");
            Render(canvas, "CancelConfirmation");
            view.modalNo.onClick.Invoke(); Check(view.creatorPanel.activeSelf, "No keeps creation");
            view.closeCreator.onClick.Invoke(); view.modalYes.onClick.Invoke(); Check(view.mainMenu.activeSelf && !view.creatorPanel.activeSelf, "Yes returns to main");
            view.newGame.onClick.Invoke(); Check(!view.male.isOn && !view.female.isOn && view.nameInput.text == "Ros", "Reopening resets draft");
            view.closeCreator.onClick.Invoke(); view.modalYes.onClick.Invoke();
            Object.DestroyImmediate(menuObject);
            menuObject = new GameObject("Reboot test"); menu = menuObject.AddComponent<MainMenuController>(); menu.Boot(null);
            view.newGame.onClick.Invoke(); Check(menuObject.GetComponent<CharacterCreatorUI>().Draft != null, "Reboot uses same scene view without stale listeners");
            Object.DestroyImmediate(menuObject);
            view.ShowMain();
            File.WriteAllText("Output/result.txt", "PASS: " + _checks + " StartScreen checks\n");
            Debug.Log("PASS: " + _checks + " StartScreen checks");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); File.WriteAllText("Output/result.txt", "FAIL: " + error); EditorApplication.Exit(1); }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message); _checks++; Debug.Log("PASS: " + message);
    }
    private static Texture2D SaveDefaultPortrait(StartScreenView view, bool bust)
    {
        var image = StartScreenPortrait.Render(view, new PlayerAppearance(), 0, bust);
        string path = "Assets/Resources/Sprites/StartScreen/CharacterPreview/" + (bust ? "DefaultPortrait" : "DefaultStanding") + ".png";
        File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    public static void VerifyInstalledScene()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var view = Object.FindFirstObjectByType<StartScreenView>(FindObjectsInactive.Include);
            Check(view != null && view.transform.parent.name == "Canvas", "Installed scene contains Canvas/StartScreenUI");
            Check(view.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Installed UI has no missing scripts");
            Check(view.previewBody != null && view.characterPreview.texture != null && view.parts[1].portrait.texture != null, "Editable scene contains default character and face previews");
            Check(view.mainMenu.transform.Find("StartScreen").GetComponent<Image>().sprite.rect.width > 1000, "Installed scene resolves full main image");
            Check(view.parts.Length == 8 && view.parts.All(p => p.swatch != null && p.value != null), "Installed scene resolves all custom rows");
            Check(view.newGame != null && view.closeCreator != null && view.pickerApply != null && view.modalYes != null, "Installed scene resolves navigation and confirmation");
            File.WriteAllText("Output/scene-result.txt", "PASS: 6 installed scene checks"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); File.WriteAllText("Output/scene-result.txt", "FAIL: " + error); EditorApplication.Exit(1); }
    }
    private static void Render(Canvas canvas, string name)
    {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        var camera = new GameObject("Render camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 540;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
        canvas.worldCamera = camera; canvas.planeDistance = 1;
        var target = new RenderTexture(1920, 1080, 24); camera.targetTexture = target;
        Canvas.ForceUpdateCanvases(); Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
        File.WriteAllBytes("Output/" + name + ".png", texture.EncodeToPNG()); RenderTexture.active = previous;
        camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture); Object.DestroyImmediate(camera.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }
}
#endif
