using System;
using System.Collections;
using System.IO;
using System.Linq;
using FarmMVP;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class LocalizationPlayChecks : MonoBehaviour
{
    private int checks;
#if UNITY_EDITOR
    public static void Run()
    {
        if (!File.Exists("localization-validation")) throw new Exception("Isolated project required");
        PlayerPrefs.DeleteAll();
        SessionState.SetBool("LocalizationPlayChecks", true);
        SessionState.SetFloat("LocalizationPlayChecks.Time", (float)EditorApplication.timeSinceStartup);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        new GameObject("Disabled bootstrap").AddComponent<GameBootstrap>().enabled = false;
        new GameObject("Localization play checks").AddComponent<LocalizationPlayChecks>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/LocalizationPlayChecks.unity");
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void Watchdog()
    {
        EditorApplication.update += () => {
            if (!SessionState.GetBool("LocalizationPlayChecks", false)) return;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat("LocalizationPlayChecks.Time", 0) > 120)
                Fail(new Exception("Play checks timed out"));
        };
    }
#endif
    private void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Debug.Log("PASS: " + message); }
    private IEnumerator Start()
    {
        var routine = Checks();
        while (true)
        {
            object current;
            try { if (!routine.MoveNext()) break; current = routine.Current; }
            catch (Exception error) { Fail(error); yield break; }
            yield return current;
        }
        File.WriteAllText("Output/play-result.txt", "PASS: " + checks + " localization play checks");
#if UNITY_EDITOR
        SessionState.SetBool("LocalizationPlayChecks", false);
        EditorApplication.Exit(0);
#endif
    }
    private IEnumerator Checks()
    {
        Check(Localization.Language == "en", "Play startup is English");
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        var view = StartScreenLayout.Build(canvas.transform);
        var menu = new GameObject("Menu").AddComponent<MainMenuController>(); menu.Boot(null);
        yield return null;
        view.settings.onClick.Invoke();
        yield return null;
        view.language.Show();
        yield return new WaitForSecondsRealtime(.25f);
        var list = view.language.transform.Find("Dropdown List");
        Check(list != null, "Real TMP dropdown opens in Play mode");
        var options = list.GetComponentsInChildren<Toggle>();
        Check(options.Length == 2, "Exactly two selectable language rows");
        Check(options[0].isOn && !options[1].isOn, "Radio selection matches English");
        Check(options.All(row => row.GetComponent<DropdownRowStyle>() != null && row.transform.Find("Separator") != null), "Every list row has a separate fill and separator");
        var languageScroll = list.GetComponent<ScrollRect>();
        Check(languageScroll.verticalScrollbar != null && languageScroll.verticalScrollbar.gameObject.activeInHierarchy, "Subtle scrollbar remains visible on short lists");
        Render(canvas, "language-list");
        options[1].isOn = true;
        yield return new WaitForSecondsRealtime(.25f);
        Check(Localization.Language == "ko", "Clicking Korean selects and closes");
        Check(view.settingsPanel.transform.Find("LanguageLabel").GetComponent<TMP_Text>().text == "언어", "Live label translation");
        Check(view.settingsPanel.transform.Find("Title").GetComponent<Text>().font == LocalizedFonts.Get(FontRole.Bold), "Heading receives selected bold font");
        view.resolution.Show();
        yield return new WaitForSecondsRealtime(.25f);
        var resolutionRows = view.resolution.transform.Find("Dropdown List").GetComponentsInChildren<Toggle>();
        Check(resolutionRows.Length == view.resolution.options.Count, "All resolution choices present");
        int pick = resolutionRows.Length - 1; resolutionRows[pick].isOn = true;
        yield return new WaitForSecondsRealtime(.25f);
        var selected = BookDisplaySettings.Resolutions()[pick];
        Check(PlayerPrefs.GetInt("farm_display_width") == selected.x && PlayerPrefs.GetInt("farm_display_height") == selected.y, "Resolution persisted by selecting a row");
        view.settingsBack.onClick.Invoke();
        Check(view.newGame.GetComponentInChildren<Text>().text == "새 게임", "Main menu Korean when opened");
        view.newGame.onClick.Invoke();
        view.next.onClick.Invoke();
        Check(view.modalPanel.activeSelf && view.modalYes.GetComponentInChildren<Text>().text == "확인", "Alert does not overwrite OK with Yes");
        view.modalYes.onClick.Invoke();
        view.male.isOn = true;
        view.next.onClick.Invoke();
        Check(view.creatorTitle.text == "캐릭터 커스텀 설정", "Character setup localized");
        view.closeCreator.onClick.Invoke(); view.modalYes.onClick.Invoke();
        view.settings.onClick.Invoke(); view.language.value = 0;
        Check(ItemDatabase.Get("strawberry_seed").displayName == "Strawberry Seeds", "Previously cached items switch back");
        // Native language names are still available while the UI uses English.
        Check(view.language.captionText.font.HasCharacters("한국어", out uint[] missing, true, true), "Hangul selector glyphs available in English");
        Check(LocalizedFonts.GetTMP().HasCharacters("농부", out missing, true, true), "TMP fallback supports Korean player names");
        var count = new GameObject("Count", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        LocalizedText.InventoryCount(count); count.text = "99";
        var fixedFont = count.font;
        Localization.SetLanguage("ko");
        Check(count.font == fixedFont && count.GetComponent<LocalizedText>().fontRole == FontRole.System1, "Inventory system font does not change with language");
        Check(count.color == Color.white && count.fontSharedMaterial.GetColor(ShaderUtilities.ID_OutlineColor) == Color.black, "Inventory count has white face and black outline");
        view.resolution.onValueChanged.RemoveAllListeners();
        view.resolution.ClearOptions(); view.resolution.AddOptions(Enumerable.Range(1, 12).Select(i => "Resolution " + i).ToList());
        view.resolution.Show();
        yield return new WaitForSecondsRealtime(.25f);
        var longScroll = view.resolution.transform.Find("Dropdown List").GetComponent<ScrollRect>();
        Check(longScroll.verticalScrollbar.size < 1, "Scrollbar thumb indicates long list length");
        longScroll.verticalScrollbar.value = 0;
        yield return null;
        Check(longScroll.verticalNormalizedPosition < .05f, "Dragging scrollbar reaches bottom");
        Render(canvas, "resolution-list");
        yield return new WaitForSecondsRealtime(.25f);
    }
    private static void Render(Canvas canvas, string name)
    {
        var rect = (RectTransform)canvas.transform;
        var mode = canvas.renderMode; var size = rect.sizeDelta; var position = rect.position; var scale = rect.localScale;
        canvas.renderMode = RenderMode.WorldSpace; rect.sizeDelta = new Vector2(1920, 1080); rect.position = Vector3.zero; rect.localScale = Vector3.one;
        var camera = new GameObject("Capture").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 540;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
        var target = new RenderTexture(1920, 1080, 24); camera.targetTexture = target; canvas.worldCamera = camera;
        Canvas.ForceUpdateCanvases();
        foreach (var fit in canvas.GetComponentsInChildren<StartScreenFit>(true)) fit.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Output/" + name + ".png", image.EncodeToPNG()); RenderTexture.active = previous;
        canvas.worldCamera = null; canvas.renderMode = mode; rect.sizeDelta = size; rect.position = position; rect.localScale = scale;
        Canvas.ForceUpdateCanvases(); foreach (var fit in canvas.GetComponentsInChildren<StartScreenFit>(true)) fit.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        camera.targetTexture = null; target.Release(); Destroy(target); Destroy(image); Destroy(camera.gameObject);
    }
    private static void Fail(Exception error)
    {
        Debug.LogException(error); Directory.CreateDirectory("Output"); File.WriteAllText("Output/play-result.txt", "FAIL: " + error);
#if UNITY_EDITOR
        SessionState.SetBool("LocalizationPlayChecks", false); EditorApplication.Exit(1);
#endif
    }
}
