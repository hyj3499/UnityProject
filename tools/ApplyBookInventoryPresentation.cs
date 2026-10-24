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

public static class ApplyBookInventoryPresentation
{
    public static void Run()
    {
        try
        {
            const string prefabPath = "Assets/Prefabs/UI/ShopBookUi.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var shopPrefab = prefab.GetComponent<ShopBookView>();
                ShopBookLayout.Upgrade(shopPrefab);
                ShopBookLayout.ArrangeInventory(shopPrefab, 10, true);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var menu = Object.FindObjectsByType<BookMenuView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(v => v.gameObject.scene == scene);
            var shop = Object.FindObjectsByType<ShopBookView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(v => v.gameObject.scene == scene);
            BookMenuLayout.UpgradeInventory(menu);
            ShopBookLayout.Upgrade(shop);
            ShopBookLayout.ArrangeInventory(shop, 10, true);
            menu.gameObject.SetActive(true);
            menu.PreviewPage(0);
            BookMenuLayout.SetItemDetailsVisible(menu, false);
            shop.gameObject.SetActive(false);
            EditorUtility.SetDirty(menu);
            EditorUtility.SetDirty(shop);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            menu = Object.FindObjectsByType<BookMenuView>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
            shop = Object.FindObjectsByType<ShopBookView>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
            Check(menu.gameObject.activeSelf && menu.pages[0].activeSelf, "Menu inventory page is visible in Canvas");
            Check(menu.profileHeader.text == "프로필" && menu.inventoryHeader.text == "인벤토리", "Editable headers are serialized");
            Check(menu.slots.Count(s => s.button.gameObject.activeSelf) == 10, "Editable MenuBookUI shows one row");
            Check(menu.slots.All(s => s.button.image.sprite != null && s.button.GetComponent<Mask>() != null
                && s.selected.GetComponent<BookInventorySelection>() != null), "Menu slots serialize art, masks and selectors");
            Check(menu.inventoryLayoutVersion == 10 && menu.slots[0].button.image.sprite.name == "Book Inventory_4"
                && menu.slots[10].button.image.sprite.name == "Book Inventory_5"
                && menu.slots[20].button.image.sprite.name == "Book Inventory_5", "Menu row art and layout version are serialized");
            Check(menu.slots.All(s => s.button.GetComponentsInChildren<Text>(true).Where(t => t.name == "Count").All(t => t.font != null
                && t.font.name == "BoldPixels" && t.color == Color.white && t.canvasRenderer.GetColor() == Color.white && t.GetComponent<Outline>() != null)
                && s.selected.GetComponent<BookInventorySelection>().corners.Length == 4), "Menu count outlines and four-corner selectors are serialized");
            Check(!menu.itemHeader.transform.parent.gameObject.activeSelf && !menu.detailReveal.gameObject.activeSelf, "Right page is empty before selection");
            Check(!shop.gameObject.activeSelf && shop.slots.All(s => s.selected != null
                && s.selected.GetComponent<BookInventorySelection>() != null), "Shop prefab instance has selectors and remains hidden");
            Check(shop.layoutVersion == 10 && shop.slots.All(s => !s.button.GetComponent<Mask>().showMaskGraphic
                && s.button.GetComponentsInChildren<Text>(true).Where(t => t.name == "Count").All(t => t.font != null && t.font.name == "BoldPixels"
                    && t.color == Color.white && t.canvasRenderer.GetColor() == Color.white && t.GetComponent<Outline>() != null)), "Shop tray masks and BoldPixels count outlines are serialized");
            Check(menu.slots.Concat(shop.slots).All(s => s.selected.GetComponent<BookInventorySelection>().corners.All(c => !c.enabled && c.sprite != null)),
                "Reloaded scene and prefab have initialized invisible selector corners");
            Canvas.ForceUpdateCanvases();
            Canvas.ForceUpdateCanvases();
            var slotCorners = new Vector3[4];
            var headerCorners = new Vector3[4];
            ((RectTransform)menu.slots[0].button.transform).GetWorldCorners(slotCorners);
            ((RectTransform)menu.inventoryHeader.transform.parent).GetWorldCorners(headerCorners);
            Check(slotCorners[1].y < headerCorners[0].y, "Reloaded scene inventory stays below its header after layout");
            Directory.CreateDirectory("Output");
            RenderSavedViews(menu, shop);
            File.WriteAllText("Output/inventory-scene-applied.txt", "PASS: Main Canvas and ShopBook prefab contain the editable inventory presentation.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void RenderSavedViews(BookMenuView savedMenu, ShopBookView savedShop)
    {
        // Render clones of the reloaded scene, so checks cover authored references
        // and stale serialized components as well as freshly generated UI.
        var sourceScene = savedMenu.gameObject.scene;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var canvas = new GameObject("Saved UI preview", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
        var menu = Object.Instantiate(savedMenu, canvas.transform, false);
        var shop = Object.Instantiate(savedShop, canvas.transform, false);
        EditorSceneManager.CloseScene(sourceScene, true);
        var camera = new GameObject("Saved UI camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.orthographic = true;
        camera.orthographicSize = 540;
        camera.farClipPlane = 2000;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .14f, .12f);
        canvas.worldCamera = camera;
        menu.gameObject.SetActive(true);
        menu.content.alpha = 1;
        FillPreview(menu.slots);
        ShopBookChecks.Capture(camera, "Output/saved-menu-10.png");
        BookInventoryLayout.ApplyMenu(menu, 30);
        ShopBookChecks.Capture(camera, "Output/saved-menu-30.png");
        menu.gameObject.SetActive(false);
        shop.gameObject.SetActive(true);
        shop.pages.alpha = 1;
        shop.Fit();
        FillPreview(shop.slots);
        ShopBookChecks.Capture(camera, "Output/saved-shop-10.png");
    }

    private static void FillPreview(BookMenuView.Slot[] slots)
    {
        for (int i = 0; i < 3; i++)
        {
            slots[i].icon.sprite = ItemDatabase.Get("wood").GetSprite();
            slots[i].icon.enabled = true;
            slots[i].count.text = new[] { "5", "23", "99" }[i];
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Debug.Log("SCENE APPLY PASS: " + message);
    }
}
#endif
