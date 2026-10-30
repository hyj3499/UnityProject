#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FarmMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

public static class QuickInventoryChecks
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Debug.Log("PASS: " + message);
    }
    private static void Call(object obj, string name, params object[] args)
        => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    private static BookInventorySelection Selector(QuickInventorySlot slot)
        => slot.GetComponentInChildren<BookInventorySelection>(true);
    private static void At(BookInventorySelection selector, float seconds, int expected)
    {
        typeof(BookInventorySelection).GetField("_startedAt", Private).SetValue(selector, Time.unscaledTime - seconds);
        Call(selector, "Update");
        Check(selector.CurrentFrame == expected, "Selector at " + seconds + " seconds = " + expected);
    }

    public static void Run()
    {
        try
        {
            if (!File.Exists("quick-inventory-validation")) throw new Exception("Isolated project required");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            var root = new GameObject("QuickInventory", typeof(RectTransform));
            root.layer = 5;
            root.transform.SetParent(canvas.transform, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            var panel = new GameObject("QuickInventoryPanel", typeof(RectTransform), typeof(Image));
            panel.layer = 5;
            panel.transform.SetParent(root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 58);
            panelRect.localScale = Vector3.one * 2.8171806f;
            var hud = root.AddComponent<QuickInventoryHUD>();
            hud.BuildSlots();
            hud.BuildSlots();
            var slots = root.GetComponentsInChildren<QuickInventorySlot>();
            Check(slots.Length == 10 && panelRect.childCount == 10, "Repeated layout builds keep exactly ten slots");
            Check(!panel.GetComponent<Image>().enabled, "Old stretched panel is hidden");
            for (int i = 0; i < 10; i++)
            {
                var rect = (RectTransform)slots[i].transform;
                Check(rect.sizeDelta == Vector2.one * 27 && rect.anchoredPosition == new Vector2(-135 + i * 30, 0), "Equal slot spacing " + i);
                Check(Selector(slots[i]).frames.Length == 8 && Selector(slots[i]).frames.All(f => f != null), "Selector frames loaded " + i);
            }
            Directory.CreateDirectory("Output");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/QuickInventoryLayout.prefab");
            var serialized = PrefabUtility.LoadPrefabContents("Assets/QuickInventoryLayout.prefab");
            Check(serialized.GetComponentsInChildren<QuickInventorySlot>().Length == 10, "Ten slots survive serialization");
            PrefabUtility.UnloadPrefabContents(serialized);

            var game = new GameObject("Game").AddComponent<GameManager>();
            game.enabled = false;
            var data = new GameData();
            data.farmer.backpackLevel = 2;
            typeof(GameManager).GetProperty("Data").SetValue(game, data);
            typeof(GameManager).GetProperty("Inventory").SetValue(game, new Inventory());
            Call(hud, "Awake");
            Call(hud, "Bind", game);
            // Opening more bags must not recolor the current page. Tab cycles only unlocked bags.
            for (int unlocked = 1; unlocked <= 3; unlocked++)
            {
                data.farmer.backpackLevel = unlocked - 1;
                data.farmer.hotbarPage = 0;
                data.farmer.equippedHotbarIndex = 4;
                Call(hud, "Refresh");
                for (int step = 0; step <= unlocked; step++)
                {
                    int expectedPage = step % unlocked;
                    Check(game.HotbarPage == expectedPage && slots.All(s => s.GetComponent<Image>().sprite.name == "QuickInventory_" + expectedPage),
                        unlocked + " bags unlocked, Tab step " + step + " uses bag " + (expectedPage + 1));
                    game.CycleHotbarPage();
                }
            }
            for (int page = 0; page < 3; page++)
            {
                data.farmer.hotbarPage = page;
                data.farmer.equippedHotbarIndex = page * 10 + 4;
                Call(hud, "Refresh");
                Check(slots.All(s => s.GetComponent<Image>().sprite.name == "QuickInventory_" + page), "All slots use bag " + page);
                Check(hud.IndexOf(9) == page * 10 + 9, "Inventory index maps to bag " + page);
                Check(slots.Count(s => Selector(s).Selected) == 1 && Selector(slots[4]).Selected, "Correct slot selected on bag " + page);
                Check(((RectTransform)slots[4].transform).anchoredPosition.y == 3 && slots[4].GetComponent<Image>().color.r > slots[0].GetComponent<Image>().color.r, "Selection lifts and brightens entire slot");
                var selector = Selector(slots[4]);
                At(selector, .05f, 0);
                At(selector, .15f, 1);
                At(selector, .25f, 0);
                At(selector, .35f, 0);
                At(selector, .45f, 0);
                At(selector, 10.15f, 0);
                Call(hud, "Refresh");
                Call(selector, "Update");
                Check(selector.CurrentFrame == 0, "Content refresh does not replay completed selection");
            }
            hud.OnSlotClicked(4);
            Check(Selector(slots[4]).CurrentFrame == 0, "Explicit click restarts selection");
            At(Selector(slots[4]), .15f, 1);
            data.farmer.equippedHotbarIndex = 29;
            Call(hud, "Refresh");
            Check(!Selector(slots[4]).Selected && Selector(slots[4]).corners.All(c => !c.enabled)
                && ((RectTransform)slots[4].transform).anchoredPosition.y == 0, "Previous selection resets height and corners");
            var bookSelector = Selector(slots[9]);
            bookSelector.repeatCount = 0;
            At(bookSelector, 10.15f, 1);
            Check(bookSelector.corners[0].rectTransform.anchorMin.x > 0, "Book still loops with original inward corner motion");
            bookSelector.repeatCount = 1;
            slots[9].SetHighlighted(false);
            slots[4].SetHighlighted(true, true);
            var counts = slots.Select(s => s.transform.Find("Count").GetComponent<TMP_Text>()).ToArray();
            string[] quantities = { "2", "5", "8", "12", "28", "64", "99", "128", "256", "999" };
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i].text = quantities[i];
                LocalizedText.InventoryCount(counts[i]);
            }
            foreach (var count in counts)
            {
                Check(new SerializedObject(count.font).FindProperty("m_SourceFontFileGUID").stringValue
                    == AssetDatabase.AssetPathToGUID("Assets/Resources/Localization/systemfont1.ttf"), "Count uses systemfont1.ttf");
                Check(count.fontSize == 10f && count.color == Color.white, "Count is a 10px white font");
                Check(count.fontSharedMaterial.IsKeywordEnabled("OUTLINE_ON")
                    && count.fontSharedMaterial.GetColor(ShaderUtilities.ID_OutlineColor) == Color.black
                    && count.fontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth) > 0f,
                    "Count has a black outline");
            }
            Capture(canvas, panelRect, "quick-inventory.png");
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(game.gameObject);
            File.WriteAllText("Output/result.txt", "PASS: Unity compilation, ten slots, 1/2/3 unlocked bags with Tab cycling, selection lift/tint, one loop then idle, refresh/reselection, Book loop regression, smaller white systemfont1 counts with black outlines, prefab serialization.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void Capture(Canvas canvas, RectTransform panel, string filename)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(960, 180);
        panel.anchorMin = panel.anchorMax = Vector2.one * .5f;
        panel.anchoredPosition = Vector2.zero;
        var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -100);
        camera.orthographic = true;
        camera.orthographicSize = 90;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.13f, .17f, .2f);
        canvas.worldCamera = camera;
        Canvas.ForceUpdateCanvases();
        var target = new RenderTexture(960, 180, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var texture = new Texture2D(960, 180, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 960, 180), 0, 0);
        texture.Apply();
        File.WriteAllBytes("Output/" + filename, texture.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(texture);
    }
}
#endif
