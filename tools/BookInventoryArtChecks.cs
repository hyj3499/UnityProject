#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using FarmMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BookInventoryArtChecks
{
    public static void Run()
    {
        try
        {
            if (!File.Exists("inventory-art-validation")) throw new Exception("Isolated project required");
            Directory.CreateDirectory("Output");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var menu = Object.FindFirstObjectByType<BookMenuView>(FindObjectsInactive.Include);
            var shop = Object.FindFirstObjectByType<ShopBookView>(FindObjectsInactive.Include);
            foreach (int capacity in new[] { 10, 20, 30, 10 })
            {
                BookInventoryLayout.ApplyMenu(menu, capacity);
                ShopBookLayout.ArrangeInventory(shop, capacity, true);
                Check(menu.slots.Count(s => s.button.gameObject.activeSelf) == capacity, "Menu capacity " + capacity);
                Check(shop.slots.Count(s => s.button.gameObject.activeSelf) == capacity, "Shop capacity " + capacity);
                string art = "Book Inventory_" + (capacity / 10 - 1);
                Check(menu.inventoryBackground.sprite.name == art && shop.inventory.GetComponent<Image>().sprite.name == art, "Capacity artwork " + capacity);
                Check(menu.lockedRows.All(r => !r.activeSelf), "No unlock notices");
                Check(menu.slots.Concat(shop.slots).All(s => s.button.GetComponent<Mask>() != null && s.button.image.sprite.name == "Book Inventory_3"), "Every slot has alpha mask");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            menu = Object.FindFirstObjectByType<BookMenuView>(FindObjectsInactive.Include);
            Check(menu.inventoryBackground != null && menu.slots[29].button.GetComponent<Mask>() != null, "Scene reload keeps artwork and masks");
            const string path = "Assets/Prefabs/UI/ShopBookUi.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                shop = root.GetComponent<ShopBookView>();
                ShopBookLayout.Upgrade(shop);
                ShopBookLayout.ArrangeInventory(shop, 10, true);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            CheckClippingAndPreview();
            File.WriteAllText("Output/inventory-art-passed.txt", "PASS: Scene/prefab migration, 10/20/30/10 capacities, serialization, rounded corner and oversized icon pixel clipping.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void CheckClippingAndPreview()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
        canvas.renderMode = RenderMode.WorldSpace;
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.orthographic = true;
        camera.orthographicSize = 540;
        camera.farClipPlane = 2000;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        canvas.worldCamera = camera;
        var menu = BookMenuLayout.Build(BookMenuLayout.Node(canvas.transform, "Menu", 0, 0, 1, 1));
        for (int n = 10; n <= 30; n += 10)
        {
            BookInventoryLayout.ApplyMenu(menu, n);
            ShopBookChecks.Capture(camera, "Output/menu-inventory-" + n + ".png");
        }
        menu.gameObject.SetActive(false);
        var shop = ShopBookLayout.Build(BookMenuLayout.Node(canvas.transform, "Shop", 0, 0, 1, 1));
        for (int n = 10; n <= 30; n += 10)
        {
            ShopBookLayout.ArrangeInventory(shop, n, true);
            ShopBookChecks.Capture(camera, "Output/shop-inventory-" + n + ".png");
        }
        // Oversize a fully opaque item to prove both rounded corners and outer edges clip.
        var slot = shop.slots[0];
        slot.button.transform.SetParent(canvas.transform, false);
        shop.gameObject.SetActive(false);
        var rect = (RectTransform)slot.button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(290, 290);
        slot.icon.enabled = true;
        slot.icon.sprite = null;
        slot.icon.color = Color.green;
        slot.icon.rectTransform.anchorMin = new Vector2(-.5f, -.5f);
        slot.icon.rectTransform.anchorMax = new Vector2(1.5f, 1.5f);
        ShopBookChecks.Capture(camera, "Output/rounded-mask.png");
        var pixels = new Texture2D(2, 2);
        pixels.LoadImage(File.ReadAllBytes("Output/rounded-mask.png"));
        Check(pixels.GetPixel(960, 540).g > .9f, "Icon center remains visible");
        Check(pixels.GetPixel(1100, 680).g < .1f, "Rounded corner clips icon");
        Check(pixels.GetPixel(1120, 540).g < .1f, "Oversized icon clips outside slot");
        Object.DestroyImmediate(pixels);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Debug.Log("INVENTORY ART PASS: " + message);
    }
}
#endif
