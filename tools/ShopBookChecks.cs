// 격리된 Unity 검증 프로젝트의 Assets/Editor에 복사하여 실행한다.
// -executeMethod FarmMVP.ShopBookChecks.Run (플레이 모드 검증이 끝나면 직접 종료한다.)
#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FarmMVP
{
    public static class ShopBookChecks
    {
        public static void VerifyInstalledScene()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
                var canvas = scene.GetRootGameObjects().First(g => g.name == "Canvas");
                Check(canvas.transform.Find("ShopBookUI") == null, "Legacy shop hierarchy removed");
                var root = canvas.transform.Find("ShopBookUi");
                Check(root != null && !root.gameObject.activeSelf, "New shop under Canvas, initially hidden");
                var view = root.GetComponent<ShopBookView>();
                Check(view != null && view.configured, "Scene resolves shop view component");
                Check(view.idle != null && view.detailPaper.GetComponent<Image>().sprite != null && view.slots.Length == 30, "Scene resolves prefab and sprite references");
                Check(root.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing shop scripts");
                File.WriteAllText("Output/scene-checks-passed.txt", "Installed Main scene references verified.");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [InitializeOnLoadMethod]
        private static void InstallWatchdog()
        {
            EditorApplication.update += () =>
            {
                if (!SessionState.GetBool("ShopBookChecks.Active", false)) return;
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat("ShopBookChecks.Start", 0) > 120)
                {
                    Debug.LogError("Shop checks timed out");
                    EditorApplication.Exit(1);
                }
            };
        }

        public static void Run()
        {
            try
            {
                PrepareAssets();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvas.layer = LayerMask.NameToLayer("UI");
                var rect = (RectTransform)canvas.transform;
                rect.sizeDelta = new Vector2(1920, 1080);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.orthographic = true;
                camera.orthographicSize = 540;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.11f, .09f, .08f);
                canvas.GetComponent<Canvas>().worldCamera = camera;
                var root = ShopBookLayout.Node(rect, "ShopBookUi", 0, 0, 1, 1);
                var view = ShopBookLayout.Build(root);
                Check(view.idle != null && view.opening.Length > 0 && view.closing.Length > 0, "Book art and frames");
                Check(view.detailPaper.GetComponent<Image>().sprite != null, "Detail paper art");
                Check(view.productsScroll.verticalScrollbar.image.sprite != null, "Holder scrollbar art");
                Check(view.slots.Length == 30, "Inventory slots");
                Check(view.flipLeft.Length > 0 && view.flipRight.Length > 0, "Both page turn directions loaded");
                Check(view.increase.image.sprite.name == "Arrow_btn_2"
                    && view.decrease.image.sprite.name == "Arrow_btn_1", "Right/left quantity arrows");
                var slot0Rect = (RectTransform)view.slots[0].button.transform;
                var slot10Rect = (RectTransform)view.slots[10].button.transform;
                var slot20Rect = (RectTransform)view.slots[20].button.transform;
                Check(Mathf.Approximately(slot10Rect.anchorMin.x, slot0Rect.anchorMin.x)
                    && slot20Rect.anchorMin.y < slot10Rect.anchorMin.y
                    && ((RectTransform)view.slots[1].button.transform).anchorMin.x > slot0Rect.anchorMax.x,
                    $"Inventory has ten columns and three rows (slot0={slot0Rect.anchorMin}, slot10={slot10Rect.anchorMin}, slot20={slot20Rect.anchorMin})");
                Check(view.slots.All(slot => !slot.button.GetComponent<Mask>().showMaskGraphic
                    && slot.button.GetComponentsInChildren<Text>(true).Where(t => t.name == "Count").All(t => t.font != null && t.font.name == "BoldPixels"
                        && t.color == Color.white && t.canvasRenderer.GetColor() == Color.white && t.GetComponent<Outline>() != null)),
                    "Every ShopBook count, including serialized duplicates, uses white outlined BoldPixels text");
                view.descriptionScroll.content.sizeDelta = new Vector2(0, 110);
                view.Fit();
                Canvas.ForceUpdateCanvases();
                Directory.CreateDirectory("Output");
                Capture(camera, "Output/shop-preview.png");
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/ShopBookUi.prefab");
                UnityEngine.Object.DestroyImmediate(root.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShopBookUi.prefab"), canvas.transform);
                view = instance.GetComponent<ShopBookView>();
                instance.SetActive(false);
                canvas.SetActive(false);
                view.pages.alpha = 0;
                ShopBookUILayoutTools.MakeShopBookEditableInScene();
                Check(view.gameObject.activeInHierarchy && view.pages.alpha == 1 && view.stage.localScale.x > 0, "Edit menu activates hidden UI and parent canvas");
                Check(!PrefabUtility.IsPartOfPrefabInstance(view), "Edit menu unpacks prefab");
                ShopBookUILayoutTools.MakeShopBookEditableInScene();
                Check(view.gameObject.activeInHierarchy && view.detailPaper.gameObject.activeSelf && view.slots.Count(s => s.button.gameObject.activeSelf) == 10, "Repeated edit menu restores preview with only unlocked row");
                EditorSceneManager.SaveScene(scene, "Assets/ShopBookValidation.unity");
                SessionState.SetBool("ShopBookChecks.Run", true);
                SessionState.SetBool("ShopBookChecks.Active", true);
                SessionState.SetFloat("ShopBookChecks.Start", (float)EditorApplication.timeSinceStartup);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void PrepareAssets()
        {
            const string path = "Assets/Resources/Sprites/UI/Button/PlusMinus.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 16;
            importer.npotScale = TextureImporterNPOTScale.None;
            string[] names = { "Plus", "PlusPressed", "Minus", "MinusPressed" };
#pragma warning disable CS0618
            importer.spritesheet = names.Select((name, i) => new SpriteMetaData
            { name = name, rect = new Rect(i * 16, 0, 16, 16), pivot = Vector2.one * .5f }).ToArray();
#pragma warning restore CS0618
            importer.SaveAndReimport();
            const string prefabPath = "Assets/Prefabs/UI/ShopBookUi.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var view = root.GetComponent<ShopBookView>();
                ShopBookLayout.Upgrade(view);
                var position = view.book.anchoredPosition;
                ShopBookLayout.Upgrade(view);
                Check(view.book.anchoredPosition == position && root.GetComponentsInChildren<ShopQuantityButton>(true).Length == 2, "Old prefab upgrade is idempotent");
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunPlayChecks()
        {
            if (!SessionState.GetBool("ShopBookChecks.Run", false)) return;
            SessionState.SetBool("ShopBookChecks.Run", false);
            new GameObject("ShopBookChecks").AddComponent<ShopBookCheckRunner>();
        }

        public static void Check(bool condition, string label)
        {
            if (!condition) throw new Exception("FAIL: " + label);
            Debug.Log("SHOP CHECK PASS: " + label);
        }

        public static void Capture(Camera camera, string path)
        {
            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    public class ShopBookCheckRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var routine = Checks();
            while (true)
            {
                object next;
                try { if (!routine.MoveNext()) break; next = routine.Current; }
                catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); yield break; }
                yield return next;
            }
            File.WriteAllText("Output/checks-passed.txt", "Shop book edit/play checks passed.");
            EditorApplication.Exit(0);
        }

        private IEnumerator Checks()
        {
            Application.runInBackground = true;
            var game = new GameObject("TestGame").AddComponent<GameManager>();
            game.enabled = false; // World/time update is outside these UI and transaction checks.
            var data = new GameData();
            typeof(GameManager).GetProperty("Data").SetValue(game, data);
            var inventory = new Inventory { unlockedSlots = 10 };
            typeof(GameManager).GetProperty("Inventory").SetValue(game, inventory);
            data.farmer.money = 1250;
            var general = ShopDatabase.Get("general");
            ShopBookChecks.Check(general.products.Count > 10, "Existing seeds and backpacks retained");
            var wood = new ShopProduct { id = "wood", itemId = "wood", price = 20 };
            ShopBookChecks.Check(ShopPurchaseService.TryBuy(game, wood, 10, out _) && game.Money == 1050 && inventory.CountOf("wood") == 10, "Bulk purchase charges delivered amount");
            data.farmer.money = 45;
            ShopBookChecks.Check(ShopPurchaseService.TryBuy(game, wood, 10, out _) && game.Money == 5 && inventory.CountOf("wood") == 12, "Bulk limited by money");
            ShopBookChecks.Check(!ShopPurchaseService.TryBuy(game, wood, 1, out _) && game.Money == 5, "Insufficient funds unchanged");
            data.farmer.money = 1000;
            for (int i = 0; i < 10; i++) inventory.slots[i] = new ItemStack("wood", 99);
            ShopBookChecks.Check(!ShopPurchaseService.TryBuy(game, wood, 1, out _) && game.Money == 1000, "Full inventory no debit");
            inventory.slots[0].count = 98;
            ShopBookChecks.Check(ShopPurchaseService.TryBuy(game, wood, 10, out _) && game.Money == 980 && inventory.slots[0].count == 99, "Partial inventory only charges one");
            var bags = general.products.Where(p => p.backpackLevel > 0).ToArray();
            ShopBookChecks.Check(!ShopPurchaseService.TryBuy(game, bags[1], 1, out _), "Backpack order enforced");
            ShopBookChecks.Check(ShopPurchaseService.TryBuy(game, bags[0], 1, out _) && inventory.unlockedSlots == 20 && game.Money == 680, "Backpack expands capacity");
            ShopBookChecks.Check(!ShopPurchaseService.TryBuy(game, bags[0], 1, out _), "Backpack cannot be bought twice");
            wood.price = -1;
            ShopBookChecks.Check(!ShopPurchaseService.TryBuy(game, wood, 1, out _), "Negative prices rejected");
            wood.price = 0;
            ShopBookChecks.Check(ShopPurchaseService.TryBuy(game, wood, 1, out _) && game.Money == 680, "Free product supported");
            ShopBookChecks.Check(!ShopPurchaseService.TryBuy(game, wood, 0, out _), "Invalid quantity rejected");
            ShopSellingChecks.Run(game);

            var controller = new GameObject("ShopController").AddComponent<ShopBookController>();
            var canvas = GameObject.Find("Canvas").GetComponent<RectTransform>();
            controller.Boot(null, game, canvas);
            var view = controller.View;
            Time.timeScale = 0;
            controller.Open(general);
            yield return new WaitUntil(() => view.pages.blocksRaycasts);
            ShopBookChecks.Check(controller.IsOpen && view.pages.blocksRaycasts && view.pages.alpha == 1, "Opening completes while paused");
            ShopBookChecks.Check(view.slots.All(s => s.selected.GetComponent<BookInventorySelection>().corners.All(c => !c.enabled && c.sprite != null)),
                "First shop opening has no visible sprite-less selector rectangles");
            ShopBookChecks.Capture(Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>(), "Output/shop-first-open.png");
            ShopBookChecks.Check(view.detailPaper.gameObject.activeSelf && Mathf.Abs(view.detailPaper.anchoredPosition.y) < 30, "Detail paper lands on page");
            ShopBookChecks.Check(view.inventory.activeSelf && view.categoriesScroll.gameObject.activeSelf, "Per shop inventory and categories enabled");
            ShopBookChecks.Check(!view.productPreview.activeSelf && !view.categoryPreview.activeSelf, "Editor preview hidden in play");
            var rows = view.products.GetComponentsInChildren<ShopProductRow>();
            ShopBookChecks.Check(rows.Length == general.products.Count && view.productsScroll.verticalScrollbar.gameObject.activeSelf, "Long list and scrollbar");
            rows[2].button.onClick.Invoke();
            ShopBookChecks.Check(view.productName.text == general.products[2].Name && view.description.text.Contains(general.products[2].Description), "Selection updates detail");
            var rowId = rows[2].GetInstanceID();
            game.AddMoney(20);
            ShopBookChecks.Check(view.money.text == "700 G" && rows[2].GetInstanceID() == rowId, "External balance refresh retains rows");
            var tabs = view.categories.GetComponentsInChildren<Button>();
            tabs[1].onClick.Invoke();
            ShopBookChecks.Check(!view.pages.blocksRaycasts && !view.detailPaper.gameObject.activeSelf && view.pages.alpha == 0, "Bookmark starts page turn before paper");
            yield return new WaitUntil(() => view.detailPaper.gameObject.activeSelf);
            ShopBookChecks.Check(!view.pages.blocksRaycasts && view.detailPaper.anchoredPosition.y > 100, "Paper drops from above after page turn");
            yield return new WaitUntil(() => view.pages.blocksRaycasts);
            ShopBookChecks.Check(view.products.GetComponentsInChildren<ShopProductRow>().Length == 2, "Category bookmark filters products");
            tabs[0].onClick.Invoke();
            yield return new WaitUntil(() => view.pages.blocksRaycasts);
            ShopBookChecks.Check(view.detailPaper.anchoredPosition.y == 0, "Reverse bookmark turn lands paper");
            view.products.GetComponentsInChildren<ShopProductRow>().First(r => r.title.text == bags[0].Name).button.onClick.Invoke();
            ShopBookChecks.Check(!view.quantityControls.activeSelf && !view.cancelQuantity.gameObject.activeSelf, "One-time backpack has only buy button");
            ShopBookChecks.Check(view.slots.Count(s => s.button.gameObject.activeSelf) == 20 && !view.slots[20].button.gameObject.activeSelf, "Locked inventory row hidden");
            view.products.GetComponentsInChildren<ShopProductRow>()[2].button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f); // Graphic 재활성화와 버튼 색 전환 후 캡처한다.
            ShopBookChecks.Check(view.quantityControls.activeInHierarchy && view.cancelQuantity.gameObject.activeInHierarchy, "Bulk selection restores controls after single purchase product");
            ShopBookChecks.Check(view.increase.image.canvasRenderer.GetMesh().vertexCount > 0
                && view.cancelQuantity.image.canvasRenderer.GetMesh().vertexCount > 0, "Quantity controls have render geometry after reactivation");
            ShopBookChecks.Capture(GameObject.Find("PreviewCamera").GetComponent<Camera>(), "Output/shop-runtime.png");
            controller.Close();
            yield return new WaitUntil(() => !controller.IsOpen);
            ShopBookChecks.Check(!controller.IsOpen && !view.gameObject.activeSelf, "Close completes while paused");

            var material = ShopDatabase.Get("materials");
            material.products[0].description = string.Join("\n", Enumerable.Repeat("긴 상품 설명도 스크롤해서 읽을 수 있습니다.", 40));
            controller.Open(material);
            yield return new WaitUntil(() => view.pages.blocksRaycasts);
            ShopBookChecks.Check(!view.inventory.activeSelf && !view.categoriesScroll.gameObject.activeSelf, "Second shop hides inventory and categories");
            ShopBookChecks.Check(view.descriptionScroll.content.rect.height > view.descriptionScroll.viewport.rect.height, "Long description scrolls");
            ShopBookChecks.Check(!view.productsScroll.verticalScrollbar.gameObject.activeSelf, "Short list hides scrollbar");
            int balance = game.Money, count = inventory.CountOf("wood");
            view.buy.onClick.Invoke();
            ShopBookChecks.Check(game.Money == balance - 20 && inventory.CountOf("wood") == count + 1, "Buy button connects to transaction service");
            ShopBookChecks.Check(view.quantity.text == "1" && !view.cancelQuantity.interactable, "New selection starts at one with dim cancel");
            view.increase.onClick.Invoke();
            ShopBookChecks.Check(view.quantity.text == "2" && view.cancelQuantity.interactable, "Plus enables cancel");
            view.cancelQuantity.onClick.Invoke();
            view.decrease.onClick.Invoke();
            ShopBookChecks.Check(view.quantity.text == "1" && !view.cancelQuantity.interactable, "Cancel resets and minus never goes below one");
            var events = EventSystem.current ?? new GameObject("Events").AddComponent<EventSystem>();
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
            var plus = (ShopQuantityButton)view.increase;
            plus.OnPointerDown(pointer);
            ShopBookChecks.Check(view.quantity.text == "2", "Pointer down increments immediately");
            yield return new WaitForSecondsRealtime(.65f);
            int held = int.Parse(view.quantity.text);
            ShopBookChecks.Check(held > 2, "Held plus repeats while paused");
            plus.OnPointerUp(pointer);
            plus.OnPointerClick(pointer);
            yield return new WaitForSecondsRealtime(.15f);
            ShopBookChecks.Check(int.Parse(view.quantity.text) == held, "Release stops repeat without extra click");
            var minus = (ShopQuantityButton)view.decrease;
            minus.OnPointerDown(pointer);
            yield return new WaitForSecondsRealtime(1.2f);
            minus.OnPointerUp(pointer);
            ShopBookChecks.Check(view.quantity.text == "1" && !view.cancelQuantity.interactable, "Held minus clamps to one");
            view.increase.onClick.Invoke();
            view.increase.onClick.Invoke();
            balance = game.Money; count = inventory.CountOf("wood");
            view.buy.onClick.Invoke();
            ShopBookChecks.Check(game.Money == balance - 60 && inventory.CountOf("wood") == count + 3 && view.quantity.text == "1", "Selected quantity purchased and reset");
            view.increase.onClick.Invoke();
            view.products.GetComponentsInChildren<ShopProductRow>()[1].button.onClick.Invoke();
            ShopBookChecks.Check(view.quantity.text == "1" && !view.cancelQuantity.interactable, "Changing product resets quantity");
            var empty = new ShopDefinition("empty", "Empty's shop");
            controller.Open(empty);
            yield return new WaitUntil(() => view.pages.blocksRaycasts);
            ShopBookChecks.Check(view.emptyList.gameObject.activeSelf && !view.buy.interactable, "Empty shop safe state");
            controller.Open(general);
            yield return new WaitForSecondsRealtime(.05f);
            controller.Close();
            yield return new WaitUntil(() => !controller.IsOpen);
            ShopBookChecks.Check(!controller.IsOpen, "Close during opening cancels animation");
            controller.Open(material);
            yield return new WaitForSecondsRealtime(.05f);
            controller.Open(general);
            yield return new WaitUntil(() => view.pages.blocksRaycasts);
            ShopBookChecks.Check(controller.IsOpen && view.pages.blocksRaycasts && view.inventory.activeSelf, "Reopen switches shop safely");

            Array.Clear(inventory.slots, 0, inventory.slots.Length);
            inventory.slots[0] = new ItemStack("wood", 3);
            inventory.unlockedSlots = 10;
            data.farmer.money = 1000;
            data.currentDay = 1;
            inventory.RaiseChanged();
            ShopBookChecks.Check(view.stage.Find("Controls") == null && view.inventory.transform.Find("Capacity") == null
                && view.book.anchoredPosition.y == 0 && ((RectTransform)view.inventory.transform).anchoredPosition.y == 8,
                "Labels removed and book centered above bottom inventory");
            var slotInput = view.slots[0].button.GetComponent<ShopInventorySlot>();
            var right = new PointerEventData(events) { button = PointerEventData.InputButton.Right };
            slotInput.OnPointerEnter(right);
            ShopBookChecks.Check(view.inventoryTooltip.gameObject.activeSelf
                && view.inventoryTooltipText.text.Contains(ItemDatabase.Get("wood").Description)
                && view.inventoryTooltipText.text.Contains("12 G"), "Inventory hover shows sale price and full item description");
            var shopSelector = view.slots[0].selected.GetComponent<BookInventorySelection>();
            int shopSelectorFrame = shopSelector.CurrentFrame;
            Vector2 firstShopCorner = shopSelector.corners[0].rectTransform.anchorMin;
            float selectorDeadline = Time.realtimeSinceStartup + 1f;
            while (shopSelector.CurrentFrame == shopSelectorFrame && Time.realtimeSinceStartup < selectorDeadline)
                yield return null;
            ShopBookChecks.Check(shopSelector.Selected && shopSelector.CurrentFrame != shopSelectorFrame
                && shopSelector.corners.All(c => c.enabled)
                && shopSelector.corners[0].rectTransform.anchorMin != firstShopCorner,
                $"Shop inventory selector animates four corners while hovering an item (selected={shopSelector.Selected}, before={shopSelectorFrame}, after={shopSelector.CurrentFrame}, frames={shopSelector.frames?.Length ?? 0}, enabledCorners={shopSelector.corners.Count(c => c != null && c.enabled)})");
            slotInput.OnPointerClick(pointer);
            ShopBookChecks.Check(inventory.slots[0].count == 3, "Left click does not sell");
            slotInput.OnPointerClick(right);
            slotInput.OnPointerClick(right);
            ShopBookChecks.Check(inventory.slots[0].count == 1 && game.Money == 1024
                && view.products.GetComponentsInChildren<ShopProductRow>().Length == general.products.Count + 1,
                "Right clicks sell one each and add buyback to all products");
            slotInput.OnPointerExit(right);
            ShopBookChecks.Check(!view.inventoryTooltip.gameObject.activeSelf && !shopSelector.Selected, "Leaving inventory hides tooltip and selector");
            ShopBookChecks.Check(shopSelector.corners.All(c => !c.enabled), "Pointer exit disables all selector corner graphics");
            var soldTab = view.categories.GetComponentsInChildren<Button>().Single(t => t.GetComponentInChildren<Text>().text == "판매");
            soldTab.onClick.Invoke();
            yield return new WaitUntil(() => view.pages.blocksRaycasts);
            ShopBookChecks.Check(view.products.GetComponentsInChildren<ShopProductRow>().Length == 1
                && view.products.GetComponentsInChildren<ShopProductRow>()[0].price.text.Contains("남은 2개"), "Sales bookmark contains only sold stock");
            view.increase.onClick.Invoke();
            view.increase.onClick.Invoke();
            ShopBookChecks.Check(view.quantity.text == "2" && !view.increase.interactable, "Buyback selector caps at remaining stock");
            view.buy.onClick.Invoke();
            ShopBookChecks.Check(inventory.slots[0].count == 3 && game.Money == 1000 && view.emptyList.gameObject.activeSelf,
                "Buying back exact quantity exhausts and removes row");
            slotInput.OnPointerClick(right);
            controller.Close();
            yield return new WaitUntil(() => !controller.IsOpen);
            controller.Open(general);
            yield return new WaitUntil(() => view.pages.blocksRaycasts);
            ShopBookChecks.Check(view.products.GetComponentsInChildren<ShopProductRow>().Length == general.products.Count + 1, "Reopening same shop retains sold stock");
            data.currentDay = 8;
            var dayChanged = (Action)typeof(GameManager).GetField("OnDayChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game);
            dayChanged.Invoke();
            ShopBookChecks.Check(view.products.GetComponentsInChildren<ShopProductRow>().Length == general.products.Count
                && data.shopBuybacks.Count == 0, "Day change removes expired stock from open UI");
            slotInput.OnPointerClick(right);
            yield return new WaitForSecondsRealtime(.2f);
            ShopBookChecks.Capture(GameObject.Find("PreviewCamera").GetComponent<Camera>(), "Output/shop-selling.png");
            slotInput.OnPointerEnter(right);
            yield return new WaitForSecondsRealtime(.1f);
            ShopBookChecks.Capture(GameObject.Find("PreviewCamera").GetComponent<Camera>(), "Output/shop-tooltip.png");
            slotInput.OnPointerExit(right);
            inventory.unlockedSlots = 30;
            inventory.RaiseChanged();
            var bookCorners = new Vector3[4]; var inventoryCorners = new Vector3[4];
            view.book.GetWorldCorners(bookCorners);
            ((RectTransform)view.inventory.transform).GetWorldCorners(inventoryCorners);
            ShopBookChecks.Check(bookCorners[0].y > inventoryCorners[1].y && view.book.anchoredPosition.y < 60,
                "Three row inventory stays below book with minimal upward offset");
            controller.Open(material);
            yield return new WaitUntil(() => view.pages.blocksRaycasts);
            balance = game.Money; count = inventory.CountOf("wood");
            controller.HoverInventory(0);
            controller.SellInventory(0);
            ShopBookChecks.Check(!view.inventory.activeSelf && !view.inventoryTooltip.gameObject.activeSelf
                && !view.categoriesScroll.gameObject.activeSelf && game.Money == balance && inventory.CountOf("wood") == count,
                "Selling off hides inventory sales tab and tooltip and blocks sale input");
            var uncategorized = new ShopDefinition("uncategorized", "Uncategorized") { allowSelling = true, useCategories = false };
            controller.Open(uncategorized);
            yield return new WaitUntil(() => view.pages.blocksRaycasts);
            ShopBookChecks.Check(view.categories.GetComponentsInChildren<Button>().Length == 2
                && view.inventory.activeSelf && view.emptyList.gameObject.activeSelf, "Selling on always shows all and sales tabs even without categories");
            Time.timeScale = 1;
        }
    }
}
#endif
