// Runs only in the disposable project created by Check-OutdoorLighting.ps1.
using System;
using System.IO;
using System.Reflection;
using FarmMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class OutdoorLightingChecks : MonoBehaviour
{
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        if (!File.Exists("lighting-validation-project")) throw new Exception("Isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Prevent automatic game startup; these checks never read or write a player save.
        new GameObject("Disabled Bootstrap").AddComponent<GameBootstrap>().enabled = false;
        new GameObject("Lighting Checks").AddComponent<OutdoorLightingChecks>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Checks.unity");
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }

    private void Start()
    {
        try
        {
            Check(File.Exists("lighting-validation-project"), "Isolated project required");
            CheckCycle();
            CheckRendering();
            File.WriteAllText("result.txt", $"PASS: {checks} lighting checks; preview-*.png rendered.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("result.txt", "FAIL: " + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckCycle()
    {
        Equal(OutdoorLightingCycle.Evaluate(360), OutdoorLightingCycle.MorningTint, "06:00 morning");
        foreach (float m in new[] { 540f, 720f, 1020f, 1080f })
            Equal(OutdoorLightingCycle.Evaluate(m), Color.white, "Clear daylight at " + m);
        Equal(OutdoorLightingCycle.Evaluate(1140), OutdoorLightingCycle.SunsetTint, "19:00 orange sunset");
        Equal(OutdoorLightingCycle.Evaluate(1170), OutdoorLightingCycle.DuskTint, "19:30 dark red dusk");
        foreach (float m in new[] { 1200f, 1439f, 1440f, 1560f, 120f, 359f })
            Equal(OutdoorLightingCycle.Evaluate(m), OutdoorLightingCycle.NightTint, "Night at " + m);
        foreach (float m in new[] { 540f, 1020f, 1080f, 1140f, 1170f, 1200f, 1440f })
            Equal(OutdoorLightingCycle.Evaluate(m - 0.001f), OutdoorLightingCycle.Evaluate(m + 0.001f), "Continuous boundary " + m);
        float previous = 1f;
        for (float m = 1080; m <= 1200; m += 0.5f)
        {
            Color c = OutdoorLightingCycle.Evaluate(m);
            float brightness = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            Check(brightness <= previous + 0.0001f, "Sunset never brightens");
            previous = brightness;
        }
        Check(OutdoorLightingCycle.Sunlight(360) > 0 && OutdoorLightingCycle.Sunlight(540) == 1f,
            "Bright morning cloud shadows");
        Check(OutdoorLightingCycle.Sunlight(1080) == 1f && OutdoorLightingCycle.Sunlight(1200) == 0f,
            "Cloud shadows disappear with sunset");
    }

    private static void CheckRendering()
    {
        var game = new GameObject("GameManager").AddComponent<GameManager>();
        game.enabled = false;
        var data = new GameData { currentMinutes = 720 };
        typeof(GameManager).GetProperty("Data").SetValue(game, data);
        var location = new GameObject("Location").AddComponent<GameLocation>();
        location.id = LocationId.Farm;
        typeof(GameManager).GetProperty("CurrentLocation").SetValue(game, location);
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.6f, 0.8f, 0.5f);
        var target = new RenderTexture(320, 180, 24);
        camera.targetTexture = target;

        // A world sprite proves that lighting affects sprite art as well as camera background.
        var swatch = new GameObject("World swatch").AddComponent<SpriteRenderer>();
        swatch.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
        swatch.transform.localScale = new Vector3(5, 5, 1);
        swatch.color = new Color(0.8f, 0.6f, 0.4f);
        swatch.sortingOrder = Depth.YSort(0);

        // Camera-space HUD stands in for the actual overlay HUD so Camera.Render can capture it.
        var canvas = new GameObject("HUD", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        canvas.sortingOrder = 31000;
        var badge = new GameObject("HUD badge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        badge.transform.SetParent(canvas.transform, false);
        badge.color = new Color(1, 0.8f, 0.2f);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = Vector2.one;
        badge.rectTransform.pivot = Vector2.one;
        badge.rectTransform.anchoredPosition = new Vector2(-4, -4);
        badge.rectTransform.sizeDelta = new Vector2(32, 24);
        Canvas.ForceUpdateCanvases();

        var lighting = game.gameObject.AddComponent<OutdoorLighting>();
        lighting.Init(game, camera);
        var baseline = Capture(camera, "day");
        Color world = baseline.GetPixel(160, 90), background = baseline.GetPixel(3, 3), hud = baseline.GetPixel(300, 164);
        Equal(hud, badge.color, "HUD is present in render");
        foreach (int minute in new[] { 360, 450, 1080, 1110, 1140, 1170, 1200, 1500 })
        {
            data.currentMinutes = minute;
            lighting.SendMessage("LateUpdate");
            var picture = Capture(camera, minute.ToString());
            Color tint = OutdoorLightingCycle.Evaluate(minute);
            Equal(picture.GetPixel(160, 90), world * tint, "World shaded at " + minute);
            Equal(picture.GetPixel(3, 3), background * tint, "Full viewport shaded at " + minute);
            Equal(picture.GetPixel(300, 164), hud, "HUD unchanged at " + minute);
        }
        location.id = LocationId.FarmHouse;
        lighting.SendMessage("LateUpdate");
        Equal(Capture(camera, "indoor").GetPixel(160, 90), world, "Entering indoors removes night tint immediately");
        location.id = LocationId.Farm;
        camera.transform.position = new Vector3(100, 100, -10);
        camera.orthographicSize = 12;
        lighting.SendMessage("LateUpdate");
        Equal(Capture(camera, "zoom").GetPixel(3, 3), background * OutdoorLightingCycle.NightTint,
            "Outdoor return, camera movement and zoom retain full coverage");
        lighting.enabled = false;
        Equal(Capture(camera, "disabled").GetPixel(3, 3), background, "Disabling restores normal brightness");

        data.currentMinutes = 1130;
        typeof(GameManager).GetField("_timeAccum", Private).SetValue(game, 3.5f);
        Check(Mathf.Abs(game.LightingMinutes - 1133.5f) < 0.001f, "Lighting uses fractional clock time");
        typeof(GameManager).GetMethod("AdvanceTime", Private).Invoke(game, new object[] { 6.5f });
        Check(data.currentMinutes == 1140 && game.LightingMinutes == 1140, "Ten-minute tick preserves continuous time");
        data.currentMinutes = 360;
        typeof(GameManager).GetField("_timeAccum", Private).SetValue(game, 0f);
        Check(game.LightingMinutes == 360, "New-day clock resets lighting to morning");
    }

    private static Texture2D Capture(Camera camera, string label)
    {
        camera.Render();
        RenderTexture.active = camera.targetTexture;
        var image = new Texture2D(320, 180, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("preview-" + label + ".png", image.EncodeToPNG());
        return image;
    }

    private static void Equal(Color actual, Color expected, string message)
        => Check(Mathf.Abs(actual.r - expected.r) < 0.016f && Mathf.Abs(actual.g - expected.g) < 0.016f
            && Mathf.Abs(actual.b - expected.b) < 0.016f, message + $": {actual} vs {expected}");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
}
