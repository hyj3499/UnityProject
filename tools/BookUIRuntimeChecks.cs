// Isolated Unity project only. Does not load or write the user's save.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using FarmMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class BookUIRuntimeChecks : MonoBehaviour
{
    private int checks;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
        checks++;
        File.AppendAllText("checks.txt", "PASS: " + message + "\n");
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private IEnumerator Start()
    {
        var test = Tests();
        while (true)
        {
            object current;
            try { if (!test.MoveNext()) break; current = test.Current; }
            catch (Exception e) { File.WriteAllText("result.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); yield break; }
            yield return current;
        }
        File.WriteAllText("result.txt", $"PASS: {checks} book UI assertions; rendered inventory/display/exit previews.");
        EditorApplication.Exit(0);
    }
    private IEnumerator Tests()
    {
        Check(File.Exists("book-validation-project"), "Isolated project marker");
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var root = BookMenuLayout.Node(canvas.transform, "MenuBookUI", 0, 0, 1, 1);
        var view = BookMenuLayout.Build(root);
        Check(view.pages.Length == 8 && view.tabs.All(b => b != null), "Eight editable pages and bookmark buttons");
        Check(view.slots.Length == 30 && view.slots.All(s => s.button != null), "Three rows of ten slots");
        Check(view.opening.Length == 4 && view.closing.Length == 4 && view.flipRight.Length == 8 && view.flipLeft.Length == 8, "All animation frames loaded in numeric order");
        Check(view.categories.Length == 5 && view.settingPanels.Length == 5, "Five settings categories");
        var custom = view.playerName.rectTransform.anchoredPosition + Vector2.right*3;
        view.playerName.rectTransform.anchoredPosition = custom;
        Check(BookMenuLayout.Build(root) == view && view.playerName.rectTransform.anchoredPosition == custom, "Repeated generation preserves user layout");
        var game = new GameObject("GameManager").AddComponent<GameManager>();
        game.enabled = false;
        Property(game, "Data", new GameData());
        Property(game, "Inventory", new Inventory());
        Property(game, "ShippingBox", new Inventory(20));
        game.Data.farmer.playerName = "하루";
        game.Data.farmer.birthdaySeason = 0;
        game.Data.farmer.birthdayDay = 12;
        game.Inventory.Add("wood", 23);
        game.Inventory.Add("stone", 8);
        var player = new GameObject("Player").AddComponent<PlayerController>();
        player.enabled = false;
        var animator = player.gameObject.AddComponent<FarmerAnimator>();
        animator.Init(new PlayerAppearance());
        animator.enabled = false;
        Property(game, "Player", player);
        var ui = new GameObject("UIManager").AddComponent<UIManager>();
        Set(ui, "_game", game);
        var book = ui.gameObject.AddComponent<BookUI>();
        Set(ui, "_book", book);
        book.Boot(ui, game, canvas.transform as RectTransform);
        var hud = new GameObject("HUD", typeof(RectTransform));
        hud.transform.SetParent(canvas.transform);
        var extra = new GameObject("OtherHUD");
        var alreadyHidden = new GameObject("HiddenHUD"); alreadyHidden.SetActive(false);
        view.hudRoots = new[] { extra, alreadyHidden };
        Time.timeScale = 0;
        book.OpenInventory();
        Check(book.IsOpen && game.Paused && !hud.activeSelf && !extra.activeSelf, "Open immediately pauses and hides all registered HUD roots");
        Check(view.content.alpha == 0, "Page contents hidden during opening");
        Check(view.background.rectTransform.sizeDelta.x == view.opening[0].rect.width, "Cropped narrow opening frame is not stretched across book");
        yield return new WaitForSecondsRealtime(.55f);
        Check(book.IsOpen && view.content.alpha == 1 && view.pages[0].activeSelf, "Opening finishes with Time.timeScale zero");
        Check(view.playerName.text.Contains("하루") && view.birthday.text.Contains("12일"), "Player identity bound");
        Check(view.portrait.Find("Appearance_0") != null, "Player appearance copied into portrait UI");
        int activeSlots = view.slots.Count(s => s.button.gameObject.activeSelf);
        bool backgroundHidden = view.inventoryBackground == null || !view.inventoryBackground.enabled;
        string slotSprite = view.slots[0].button.image.sprite != null ? view.slots[0].button.image.sprite.name : "null";
        Canvas.ForceUpdateCanvases();
        var firstSlotRect = (RectTransform)view.slots[0].button.transform;
        var secondSlotRect = (RectTransform)view.slots[1].button.transform;
        Check(view.lockedRows.All(r => !r.activeSelf) && activeSlots == 10 && backgroundHidden && slotSprite == "Book Inventory_4",
            $"Base bag shows ten individual masked slots (active={activeSlots}, backgroundHidden={backgroundHidden}, sprite={slotSprite})");
        Check(Mathf.Abs(firstSlotRect.rect.width - firstSlotRect.rect.height) < .25f
            && secondSlotRect.anchorMin.x > firstSlotRect.anchorMax.x,
            $"Menu slots are square with a visible gap (size={firstSlotRect.rect.size}, gap={secondSlotRect.anchorMin.x - firstSlotRect.anchorMax.x})");
        Check(view.profileHeader.text == "프로필" && view.inventoryHeader.text == "인벤토리"
            && !view.itemHeader.transform.parent.gameObject.activeSelf, "Inventory page headers and empty detail page");
        // Fitters update in a later layout pass. Check after refreshes, not just creation.
        for (int pass = 0; pass < 3; pass++)
        {
            book.RefreshStatus();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var slotCorners = new Vector3[4];
            var headerCorners = new Vector3[4];
            firstSlotRect.GetWorldCorners(slotCorners);
            ((RectTransform)view.inventoryHeader.transform.parent).GetWorldCorners(headerCorners);
            Check(slotCorners[1].y < headerCorners[0].y, "Inventory remains below its header after layout pass " + pass);
        }
        Check(view.slots.All(s => s.selected.GetComponent<BookInventorySelection>().corners.All(c => !c.enabled && c.sprite != null)),
            "Every unselected slot starts with initialized, hidden corners");
        Check(view.slots.All(s => s.button.GetComponentsInChildren<Text>(true).Where(t => t.name == "Count").All(t => t.font != null
            && t.font.name == "BoldPixels" && t.color == Color.white && t.canvasRenderer.GetColor() == Color.white && t.GetComponent<Outline>() != null)),
            "Every MenuBookUI count, including serialized duplicates, uses BoldPixels with white CanvasRenderer tint and black outline");
        Render(canvas, "inventory-first-open.png");
        // Let the first FitInParent calculation settle before measuring click behavior.
        yield return null;
        Canvas.ForceUpdateCanvases();
        // Clicking an item must only change selection. In particular, it must not
        // recreate/reset the fitted grid and move every slot down a row.
        Vector3 slotPositionBeforeClick = firstSlotRect.position;
        view.slots[0].button.onClick.Invoke();
        yield return null;
        Canvas.ForceUpdateCanvases();
        Vector3 slotPositionAfterFirstClick = firstSlotRect.position;
        view.slots[1].button.onClick.Invoke();
        yield return null;
        Canvas.ForceUpdateCanvases();
        Vector3 slotPositionAfterSecondClick = firstSlotRect.position;
        Check(Vector3.Distance(slotPositionBeforeClick, slotPositionAfterFirstClick) < .01f
            && Vector3.Distance(slotPositionBeforeClick, slotPositionAfterSecondClick) < .01f,
            $"Selecting slots keeps the inventory grid fixed (before={slotPositionBeforeClick}, first={slotPositionAfterFirstClick}, second={slotPositionAfterSecondClick})");
        view.slots[0].button.onClick.Invoke();
        Check(view.itemName.text == "나무" && view.itemIcon.enabled && view.itemDescription.text.Contains("목재"), "Selecting real inventory item updates enlarged icon and description");
        Check(view.slots[0].selected.GetComponent<BookInventorySelection>().Selected && view.itemFacts.text.Contains("23"), "Animated selection and count match inventory");
        Check(view.itemHeader.transform.parent.gameObject.activeSelf && view.iconHolder.anchoredPosition.y > 0 && view.detailReveal.alpha == 0, "Item holder starts above its destination");
        var menuSelector = view.slots[0].selected.GetComponent<BookInventorySelection>();
        int firstSelectorFrame = menuSelector.CurrentFrame;
        Vector2 firstSelectorCorner = menuSelector.corners[0].rectTransform.anchorMin;
        float selectorDeadline = Time.realtimeSinceStartup + 1f;
        while (menuSelector.CurrentFrame == firstSelectorFrame && Time.realtimeSinceStartup < selectorDeadline)
            yield return null;
        Check(menuSelector.CurrentFrame != firstSelectorFrame && menuSelector.corners.All(c => c.enabled)
            && menuSelector.corners[0].rectTransform.anchorMin != firstSelectorCorner,
            $"Menu inventory selector advances four-corner animation frames (selected={menuSelector.Selected}, before={firstSelectorFrame}, after={menuSelector.CurrentFrame}, frames={menuSelector.frames?.Length ?? 0}, enabledCorners={menuSelector.corners.Count(c => c != null && c.enabled)})");
        yield return new WaitForSecondsRealtime(.38f);
        Check(view.detailReveal.alpha == 1 && view.iconHolder.anchoredPosition.y < 1, "Item holder lands and reveals item information");
        Render(canvas, "inventory.png");
        var countText = view.slots[0].count;
        File.WriteAllText("count-layout.txt", $"text={countText.text}; rect={countText.rectTransform.rect}; preferred={countText.preferredWidth} x {countText.preferredHeight}; visible={countText.cachedTextGenerator.characterCountVisible}; enabled={countText.isActiveAndEnabled}; color={countText.color}");
        Check(countText.cachedTextGenerator.characterCountVisible >= 2 && countText.color == Color.white
            && countText.GetComponent<Outline>() != null
            && (countText.GetComponent<Image>() == null || !countText.GetComponent<Image>().enabled),
            "Inventory stack count fits with white text, black outline, and no background box");
        game.Data.farmer.backpackLevel = 1; book.RefreshStatus();
        Check(view.slots.Count(s => s.button.gameObject.activeSelf) == 20
            && view.slots[0].button.image.sprite.name == "Book Inventory_4"
            && view.slots[10].button.image.sprite.name == "Book Inventory_5", "First expansion shows row-specific slot art");
        game.Data.farmer.backpackLevel = 2; book.RefreshStatus();
        Check(view.slots.Count(s => s.button.gameObject.activeSelf) == 30
            && view.slots[20].button.image.sprite.name == "Book Inventory_5", "Second expansion shows three rows with third-row art");
        yield return null;
        Render(canvas, "inventory-three-rows.png");
        game.Inventory.Remove("wood", 23);
        Check(!view.itemHeader.transform.parent.gameObject.activeSelf && !view.detailReveal.gameObject.activeSelf, "Removed selected stack leaves right page empty");
        view.tabs[7].onClick.Invoke();
        Check(view.background.sprite == view.flipRight[0], "Forward bookmark plays right animation");
        yield return new WaitForSecondsRealtime(.8f);
        view.categories[1].onClick.Invoke();
        Check(view.pages[7].activeSelf && view.settingPanels[1].activeSelf, "Display page selected by bookmarks and category");
        var oldMode = view.modeLabel.text; view.nextMode.onClick.Invoke();
        Check(view.modeLabel.text != oldMode, "Display mode cycles");
        var oldResolution = view.resolutionLabel.text; view.nextResolution.onClick.Invoke();
        Check(view.resolutionLabel.text != oldResolution, "Resolution cycles");
        view.scale.value = 1.4f;
        Check(Mathf.Approximately(game.ZoomMultiplier, 1), "Scale is staged before apply");
        view.applyDisplay.onClick.Invoke();
        Check(Mathf.Approximately(game.ZoomMultiplier, 1.4f), "Apply updates camera scale");
        Render(canvas, "display.png");
        view.categories[4].onClick.Invoke();
        Render(canvas, "exit.png");
        view.quit.onClick.Invoke();
        Check(view.confirmation.activeSelf && view.confirmationText.text.Contains("저장하지"), "Quit without save has explicit confirmation");
        view.confirmNo.onClick.Invoke();
        Check(!view.confirmation.activeSelf, "Cancel returns to book");
        view.tabs[0].onClick.Invoke();
        Check(view.background.sprite == view.flipLeft[0], "Backward bookmark plays left animation");
        // Interrupt a flip with close, then reopen during closing: last request must win.
        book.Close(); book.OpenInventory();
        yield return new WaitForSecondsRealtime(.55f);
        Check(book.IsOpen && game.Paused && view.content.alpha == 1, "Rapid close/reopen leaves usable open book");
        book.Close();
        Check(game.Paused && !hud.activeSelf, "Closing keeps pause and HUD suppression until animation ends");
        yield return new WaitForSecondsRealtime(.55f);
        Check(!book.IsOpen && !game.Paused && !view.gameObject.activeSelf, "Close completes and unpauses");
        Check(hud.activeSelf && extra.activeSelf && !alreadyHidden.activeSelf, "HUD active states restored exactly");
        book.OpenSettings();
        yield return new WaitForSecondsRealtime(.55f);
        view.saveAndQuit.onClick.Invoke();
        Property(game, "Data", null);
        view.confirmYes.onClick.Invoke();
        Check(view.confirmation.activeSelf && !view.confirmYes.interactable, "Save failure keeps application open");
        Property(game, "Data", new GameData());
        // The intentional error above can trigger the editor's Error Pause preference.
        // Finish remaining cleanup synchronously, without waiting for another player frame.
        Object.DestroyImmediate(book);
        Check(hud.activeSelf && extra.activeSelf && !view.gameObject.activeSelf, "Destroying controller hides scene book and restores HUD");
        Time.timeScale = 1;
    }
    private static void Render(Canvas canvas, string name)
    {
        var go = new GameObject("PreviewCamera");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.23f, .29f, .26f);
        cam.orthographic = true;
        cam.transform.position = new Vector3(0,0,-10);
        var texture = new RenderTexture(1440, 810, 24);
        cam.targetTexture = texture;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var image = new Texture2D(1440,810,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1440,810),0,0); image.Apply();
        File.WriteAllBytes(name, image.EncodeToPNG());
        RenderTexture.active = previous;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        Object.Destroy(image); cam.targetTexture = null; texture.Release(); Object.Destroy(texture); Object.Destroy(go);
    }
}
#endif
