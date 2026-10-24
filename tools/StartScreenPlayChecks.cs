using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FarmMVP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Copied only into the isolated validation project, never into the game Assets.
public class StartScreenPlayChecks : MonoBehaviour
{
    private int _checks;
#if UNITY_EDITOR
    public static void Run()
    {
        if (!File.Exists("start-screen-validation")) throw new Exception("Isolated project required");
        SessionState.SetBool("StartScreenPlayChecks", true);
        SessionState.SetFloat("StartScreenPlayChecks.Time", (float)EditorApplication.timeSinceStartup);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var bootstrap = new GameObject("Disabled bootstrap").AddComponent<GameBootstrap>(); bootstrap.enabled = false;
        new GameObject("Start screen play checks").AddComponent<StartScreenPlayChecks>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/PlayChecks.unity");
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void Watchdog()
    {
        EditorApplication.update += () => {
            if (!SessionState.GetBool("StartScreenPlayChecks", false)) return;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat("StartScreenPlayChecks.Time", 0) > 120)
                Fail(new Exception("Play checks timed out"));
        };
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartChecks()
    {
        if (!SessionState.GetBool("StartScreenPlayChecks", false)) return;
        foreach (var bootstrap in FindObjectsByType<GameBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None)) bootstrap.enabled = false;
    }
#endif
    private IEnumerator Start()
    {
        var routine = CheckRoutine();
        while (true)
        {
            bool more; object current;
            try { more = routine.MoveNext(); current = more ? routine.Current : null; }
            catch (Exception error) { Fail(error); yield break; }
            if (!more) break;
            yield return current;
        }
        File.WriteAllText("Output/play-result.txt", "PASS: " + _checks + " play mode checks");
#if UNITY_EDITOR
        EditorApplication.Exit(0);
#endif
    }
    private IEnumerator CheckRoutine()
    {
        var host = new GameObject("MainMenu");
        var menu = host.AddComponent<MainMenuController>(); menu.Boot(null);
        var view = FindFirstObjectByType<StartScreenView>();
        yield return null;
        Check(view.mainMenu.activeInHierarchy && !view.creatorPanel.activeSelf, "Starts at main menu");
        Capture(view, "Play-MainMenu");
        ClickAt(view.newGame);
        yield return null;
        Check(view.basicPanel.activeInHierarchy && !view.male.isOn && !view.female.isOn, "Actual raycast opens basic settings with no gender");
        ClickAt(view.editName);
        yield return null;
        Check(view.nameInput.isFocused && !view.nameInput.readOnly && view.nameInput.caretBlinkRate > 0, "Edit focuses live input and enables blinking caret");
        view.nameInput.text = "Ros";
        view.nameInput.DeactivateInputField();
        ClickAt(view.next);
        yield return null;
        Check(view.modalPanel.activeInHierarchy, "Gender validation modal blocks input");
        var behind = Raycast(view.newGame.transform as RectTransform);
        Check(behind != null && behind.transform.IsChildOf(view.modalPanel.transform), "Modal intercepts clicks behind dialog");
        ClickAt(view.modalYes);
        ClickToggle(view.female);
        ClickAt(view.next);
        yield return null;
        Check(view.customPanel.activeInHierarchy, "Gender selection advances to custom page");
        var bars = view.parts[0].hue.GetComponentInParent<StartScreenHsvBars>();
        Check(bars.hueGradient.texture != null && bars.saturationGradient.texture != null && bars.valueGradient.texture != null, "All HSV gradients generated in play mode");
        Capture(view, "Play-CustomSettings");
        ClickAt(view.parts[1].select);
        yield return null;
        Check(view.pickerPanel.activeInHierarchy && !view.pickerScroll.verticalScrollbar.gameObject.activeSelf, "Two eye slots need no scrollbar");
        var slots = view.pickerContent.GetComponentsInChildren<Button>();
        ClickAt(slots[1]);
        yield return null;
        Check(host.GetComponent<CharacterCreatorUI>().Draft.Preview().eyes == FarmerEyes.SecondStyle, "Live eye slot previews on character");
        ClickAt(view.pickerApply);
        yield return null;
        Check(host.GetComponent<CharacterCreatorUI>().Draft.Appearance.eyes == FarmerEyes.SecondStyle, "Live apply commits eye choice");
        view.customScroll.verticalNormalizedPosition = 0;
        yield return null;
        ClickAt(view.parts[7].select);
        yield return null;
        Check(view.pickerTitle.text == "악세사리", "Scrolling reaches and opens last part");
        Capture(view, "Play-Accessory");
        ClickAt(view.pickerClose);
        ClickAt(view.closeCreator);
        yield return null;
        ClickAt(view.modalNo);
        Check(view.creatorPanel.activeInHierarchy, "Cancel no preserves creation");
        ClickAt(view.closeCreator); yield return null; ClickAt(view.modalYes);
        yield return null;
        Check(view.mainMenu.activeInHierarchy, "Cancel yes returns to main in play mode");
        ClickAt(view.newGame);
        yield return null;
        Check(!view.male.isOn && !view.female.isOn && view.nameInput.readOnly, "A fresh draft resets gender and name editing");
        ClickAt(view.closeCreator); yield return null; ClickAt(view.modalYes);
        yield return null;
        ClickAt(view.settings);
        yield return null;
        view.volume.value = .35f;
        Check(Mathf.Approximately(AudioListener.volume, .35f), "Settings slider controls audio");
        ClickAt(view.settingsBack);
        yield return null;
        Check(view.mainMenu.activeInHierarchy, "Settings back returns to main");
    }
    private void Check(bool value, string name)
    {
        if (!value) throw new Exception(name); _checks++; Debug.Log("PLAY PASS: " + name);
    }
    private static GameObject Raycast(RectTransform target)
    {
        Canvas.ForceUpdateCanvases();
        var canvas = target.GetComponentInParent<Canvas>().rootCanvas;
        var position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, target.TransformPoint(target.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        return hits.Count > 0 ? hits[0].gameObject : null;
    }
    private static void ClickAt(Button button)
    {
        var hit = Raycast(button.transform as RectTransform);
        if (hit == null || hit.GetComponentInParent<Button>() != button) throw new Exception("Button not raycast reachable: " + button.name + "; hit=" + hit);
        ExecuteEvents.ExecuteHierarchy(hit, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }
    private static void ClickToggle(Toggle toggle)
    {
        var hit = Raycast(toggle.transform as RectTransform);
        if (hit == null || hit.GetComponentInParent<Toggle>() != toggle) throw new Exception("Toggle not reachable: " + toggle.name);
        ExecuteEvents.ExecuteHierarchy(hit, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }
    private static void Capture(StartScreenView view, string name)
    {
        var canvas = view.GetComponentInParent<Canvas>().rootCanvas;
        var camera = new GameObject("Capture").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 540;
        camera.clearFlags = CameraClearFlags.SolidColor;
        var target = new RenderTexture(1920, 1080, 24); camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Output/" + name + ".png", image.EncodeToPNG()); RenderTexture.active = previous;
        camera.targetTexture = null; target.Release(); Destroy(target); Destroy(image); Destroy(camera.gameObject);
        canvas.worldCamera = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.ForceUpdateCanvases();
    }
    private static void Fail(Exception error)
    {
        Debug.LogException(error); File.WriteAllText("Output/play-result.txt", "FAIL: " + error);
#if UNITY_EDITOR
        EditorApplication.Exit(1);
#endif
    }
}
