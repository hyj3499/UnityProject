#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FarmMVP;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class LocalizationChecks
{
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Debug.Log("PASS: " + message); }
    public static void VerifyInstalled()
    {
        Directory.CreateDirectory("Output");
        try
        {
            Check(File.Exists("localization-validation"), "Isolated project");
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var view = Object.FindFirstObjectByType<StartScreenView>(FindObjectsInactive.Include);
            Check(view != null && view.language != null && view.resolution != null, "Serialized main scene has both dropdown references");
            foreach (var dropdown in new[] { view.language, view.resolution })
            {
                Check(dropdown.template != null && dropdown.itemText != null && dropdown.captionText != null, "Serialized dropdown text and template references resolve");
                Check(dropdown.template.GetComponent<ScrollRect>().verticalScrollbar != null, "Serialized scrollbar resolves");
                Check(dropdown.itemText.GetComponentInParent<DropdownRowStyle>(true) != null, "Serialized row style resolves");
            }
            Check(view.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Start screen has no missing scripts");
            var dates = Object.FindObjectsByType<DateTimeHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(dates.Length > 0, "DateTimePanel exists");
            foreach (var date in dates) foreach (var text in date.GetComponentsInChildren<TMP_Text>(true))
            {
                var binding = text.GetComponent<LocalizedText>();
                Check(binding != null && binding.fontRole == FontRole.System1, "Serialized date/time uses System1");
                binding.Refresh(); Check(text.font == LocalizedFonts.GetTMP(FontRole.System1), "Date/time system font resolves");
            }
            var bindings = Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(b => b.name == "Count").ToArray();
            Check(bindings.Length >= 40, "Inventory count bindings present");
            foreach (var binding in bindings)
            {
                Check(binding.fontRole == FontRole.System1 && binding.inventoryCount, "Serialized inventory count uses fixed style");
                binding.Refresh();
                var tmp = binding.GetComponent<TMP_Text>();
                if (tmp != null) Check(tmp.color == Color.white && tmp.fontSharedMaterial.mainTexture != null, "TMP count material and atlas resolve");
                var legacy = binding.GetComponent<Text>();
                if (legacy != null) Check(legacy.color == Color.white && legacy.GetComponent<Outline>().effectColor == Color.black, "Legacy count white/black styling resolves");
            }
            foreach (string path in new[] { "Assets/Resources/UI/StartScreenUI.prefab", "Assets/Prefabs/UI/ShopBookUi.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                Check(root.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Prefab has no missing scripts: " + path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            File.WriteAllText("Output/installed-result.txt", "PASS: " + checks + " installed scene checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("Output/installed-result.txt", "FAIL: " + e); EditorApplication.Exit(1); }
    }
    public static void Run()
    {
        Directory.CreateDirectory("Output");
        try
        {
            Check(File.Exists("localization-validation"), "Isolated project");
            PlayerPrefs.DeleteAll();
            Check(Localization.Language == "en", "Fresh startup defaults to English");
            Check(Localization.Languages.Select(l => l.code).SequenceEqual(new[] { "en", "ko" }), "Exactly English and Korean offered");
            var english = Localization.Table("en"); var korean = Localization.Table("ko");
            Check(english.Keys.OrderBy(k => k).SequenceEqual(korean.Keys.OrderBy(k => k)), "Translation key parity");
            foreach (var key in english.Keys)
            {
                var enArgs = Regex.Matches(english[key], @"\{\d+(?:[^}]*)\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v);
                var koArgs = Regex.Matches(korean[key], @"\{\d+(?:[^}]*)\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v);
                Check(enArgs.SequenceEqual(koArgs), "Format placeholders: " + key);
            }
            Check(ItemDatabase.Get("wood").displayName == "Wood", "English item name");
            var seed = ItemDatabase.Get("strawberry_seed");
            Check(seed.displayName == "Strawberry Seeds", "English seed name");
            Check(Localization.Resolve("@npc.Leona.displayName") == "Leona", "English NPC name");
            korean.Remove("ui.menu.new_game");
            Localization.SetLanguage("ko");
            Check(Localization.Get("ui.menu.new_game") == "NEW GAME", "Missing Korean entry falls back to English");
            korean["ui.menu.new_game"] = "새 게임";
            Check(ItemDatabase.Get("wood").displayName == "나무" && seed.displayName == "딸기 씨앗", "Cached item definitions change language");
            Check(Localization.Resolve("@npc.Leona.displayName") == "레오나", "Korean NPC name");
            Check(!Localization.SetLanguage("invalid") && Localization.Language == "ko", "Unknown locale rejected");
            Check(PlayerPrefs.GetString(Localization.PreferenceKey) == "ko", "Language preference persisted");
            foreach (string locale in new[] { "en", "ko" }) foreach (FontRole role in Enum.GetValues(typeof(FontRole)))
            {
                Check(LocalizedFonts.Get(role, locale) != null, "Font " + locale + "/" + role);
                var font = LocalizedFonts.GetTMP(role, locale);
                Check(font != null && font.atlasPopulationMode == AtlasPopulationMode.Dynamic && font.isMultiAtlasTexturesEnabled, "Dynamic multi atlas " + locale + "/" + role);
                string chars = role != FontRole.System1 && locale == "ko" ? "설정언어해상도한국어볼드체일반" : "SettingsLanguageResolution0123456789";
                Check(font.HasCharacters(chars, out uint[] missing, false, true), "Required glyphs " + locale + "/" + role);
            }
            Check(LocalizedFonts.Get(FontRole.System1, "en") == LocalizedFonts.Get(FontRole.System1, "ko"), "System font independent of language");
            LocalizedFonts.RebuildPreviews();
            Check(LocalizedFonts.GetTMP(FontRole.System1).HasCharacters("0123456789", out uint[] missingNumbers, false, true), "Rebuilt system font preview supports numbers");
            var count = new GameObject("Count", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            LocalizedText.InventoryCount(count);
            Check(count.color == Color.white && count.fontSharedMaterial.GetColor(ShaderUtilities.ID_OutlineColor) == Color.black, "TMP count uses white with black outline");
            Object.DestroyImmediate(count.gameObject);
            Localization.SetLanguage("en");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var view = StartScreenLayout.Build(canvas.transform);
            LocalizationBindings.Apply(view.gameObject);
            var menu = new GameObject("Menu").AddComponent<MainMenuController>(); menu.Boot(null);
            view.settings.onClick.Invoke();
            Check(view.settingsPanel.activeSelf && view.language.options.Count == 2 && view.resolution.options.Count > 0, "Settings dropdowns wired");
            Check(view.language.options[1].text == "한국어", "Native language labels");
            view.language.value = 1;
            Check(Localization.Language == "ko", "Dropdown changes locale");
            Check(view.settingsPanel.transform.Find("LanguageLabel").GetComponent<TMP_Text>().text == "언어", "Settings changes immediately");
            Render(canvas, "settings-ko");
            view.language.value = 0;
            Check(Localization.Language == "en", "Dropdown switches back to English");
            Render(canvas, "settings-en");
            Check(view.language.template.GetComponent<ScrollRect>() != null && view.language.itemText != null, "Dropdown template has scroll content and item text");
            Check(view.language.template.GetComponent<ScrollRect>().verticalScrollbar != null, "Dropdown has visible scroll track and handle");
            // Export only new authored dropdowns; installation preserves unrelated scene layout.
            Directory.CreateDirectory("Assets/Generated");
            PrefabUtility.SaveAsPrefabAsset(view.language.gameObject, "Assets/Generated/Language.prefab");
            PrefabUtility.SaveAsPrefabAsset(view.resolution.gameObject, "Assets/Generated/Resolution.prefab");
            PrefabUtility.SaveAsPrefabAsset(view.settingsPanel.transform.Find("LanguageLabel").gameObject, "Assets/Generated/LanguageLabel.prefab");
            PrefabUtility.SaveAsPrefabAsset(view.settingsPanel.transform.Find("ResolutionLabel").gameObject, "Assets/Generated/ResolutionLabel.prefab");
            AssetDatabase.SaveAssets();
            File.WriteAllText("Output/result.txt", "PASS: " + checks + " localization checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("Output/result.txt", "FAIL: " + e); EditorApplication.Exit(1); }
    }

    private static void Render(Canvas canvas, string name)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
        canvas.transform.position = Vector3.zero;
        canvas.transform.localScale = Vector3.one;
        var camera = new GameObject("RenderCamera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 540;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
        canvas.worldCamera = camera; canvas.planeDistance = 1;
        var target = new RenderTexture(1920, 1080, 24); camera.targetTexture = target;
        Canvas.ForceUpdateCanvases(); Canvas.ForceUpdateCanvases();
        foreach (var fit in canvas.GetComponentsInChildren<StartScreenFit>(true)) fit.SendMessage("LateUpdate");
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Output/" + name + ".png", image.EncodeToPNG()); RenderTexture.active = previous;
        camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image); Object.DestroyImmediate(camera.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }
}
#endif
