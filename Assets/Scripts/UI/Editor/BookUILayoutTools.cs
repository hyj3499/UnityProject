using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>씬에서 직접 꾸미기 위한 책 UI 뼈대. 게임 로직이나 런타임 UI 생성과는 독립적이다.</summary>
    public static class BookUILayoutTools
    {
        private const string MenuPath = "Tools/Book UI/";
        private const string SpritePath =
            "Assets/Resources/Sprites/UI/Book/Inventory Book/Book Idle/1.png";
        private const string UndoName = "Create Book UI Layout";
        private const float BookScale = BookMenuLayout.BookScale;

        private static readonly string[] Pages =
        {
            "Inventory", "Skills", "Residents", "Livestock",
            "Quests", "MapEncyclopedia", "Tutorial", "Settings"
        };

        private static readonly string[] Settings = { "Gameplay", "Display", "Controls", "Exit" };

        [InitializeOnLoadMethod]
        private static void UpgradeLoadedInventoryAfterCompile()
        {
            EditorApplication.delayCall += UpgradeLoadedInventory;
            EditorSceneManager.sceneOpened -= OnInventorySceneOpened;
            EditorSceneManager.sceneOpened += OnInventorySceneOpened;
            EditorApplication.playModeStateChanged -= OnInventoryPlayModeChanged;
            EditorApplication.playModeStateChanged += OnInventoryPlayModeChanged;
        }

        private static void OnInventorySceneOpened(Scene scene, OpenSceneMode mode)
            => EditorApplication.delayCall += UpgradeLoadedInventory;

        private static void OnInventoryPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += UpgradeLoadedInventory;
        }

        private static void UpgradeLoadedInventory()
        {
                if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
                var scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || !scene.isLoaded) return;
                var view = UnityEngine.Object.FindObjectsByType<BookMenuView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(v => v.gameObject.scene == scene);
                var shop = UnityEngine.Object.FindObjectsByType<ShopBookView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(v => v.gameObject.scene == scene);
                bool upgradeMenu = view != null && view.configured && view.inventoryLayoutVersion < 10;
                bool upgradeShop = shop != null && shop.configured && shop.layoutVersion < 10;
                if (!upgradeMenu && !upgradeShop) return;
                if (upgradeMenu)
                {
                    Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Upgrade Menu Book Inventory Presentation");
                    BookMenuLayout.UpgradeInventory(view);
                    view.gameObject.SetActive(true);
                    view.PreviewPage(0);
                    EditorUtility.SetDirty(view);
                }
                if (upgradeShop)
                {
                    Undo.RegisterFullObjectHierarchyUndo(shop.gameObject, "Upgrade Shop Book Inventory Presentation");
                    ShopBookLayout.Upgrade(shop);
                    ShopBookLayout.ArrangeInventory(shop, 10, true);
                    EditorUtility.SetDirty(shop);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("Book UI: 열려 있는 Canvas의 인벤토리 슬롯과 선택 애니메이션을 갱신했습니다.", view != null ? view : (UnityEngine.Object)shop);
        }

        [MenuItem(MenuPath + "Create Menu Book", false, 10)]
        public static void CreateMenuBook() => Create();


        [MenuItem(MenuPath + "Build Interactive Menu Book", false, 1)]
        public static void BuildInteractiveMenu()
        {
            if (!CanCreate()) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Interactive Menu Book");
            try
            {
                var canvas = FindOrCreateCanvas();
                var root = canvas.transform.Find("MenuBookUI") as RectTransform;
                if (root == null) root = Area(canvas.transform, "MenuBookUI", Vector2.zero, Vector2.one);
                Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Build Interactive Menu Book");
                var view = BookMenuLayout.Build(root);
                root.gameObject.SetActive(true);
                view.PreviewPage(0);
                view.PreviewSettings(1);
                EditorUtility.SetDirty(view);
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                Selection.activeGameObject = root.gameObject;
                EditorGUIUtility.PingObject(root.gameObject);
                Debug.Log("Book UI: 인벤토리·설정·책갈피 생성 완료. 씬을 저장하세요. 재실행 시 꾸민 내용을 유지합니다.", root);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            finally { Undo.CollapseUndoOperations(group); }
        }

        [MenuItem(MenuPath + "Apply Inventory Presentation To Scene", false, 4)]
        public static void ApplyInventoryPresentationToScene()
        {
            if (!CanCreate()) return;
            var scene = SceneManager.GetActiveScene();
            var menu = UnityEngine.Object.FindObjectsByType<BookMenuView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(v => v.gameObject.scene == scene);
            var shop = UnityEngine.Object.FindObjectsByType<ShopBookView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(v => v.gameObject.scene == scene);
            if (menu == null) { Debug.LogWarning("Book UI: MenuBookUI를 찾을 수 없습니다."); return; }
            Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Apply Inventory Presentation");
            BookMenuLayout.UpgradeInventory(menu);
            menu.gameObject.SetActive(true);
            menu.PreviewPage(0);
            if (shop != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(shop.gameObject, "Apply Shop Inventory Presentation");
                ShopBookLayout.Upgrade(shop);
                ShopBookLayout.ArrangeInventory(shop, 10, true);
            }
            EditorUtility.SetDirty(menu);
            if (shop != null) EditorUtility.SetDirty(shop);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = menu.pages[0];
            EditorGUIUtility.PingObject(menu.pages[0]);
            Debug.Log("Book UI: Main Canvas에 프로필/인벤토리 헤더, 개별 슬롯, 빈 상세 페이지, 선택 애니메이션을 반영했습니다. 씬을 저장하세요.", menu);
        }

        [MenuItem(MenuPath + "Preview Inventory", false, 30)]
        public static void PreviewInventory() => Preview(0, 1);
        [MenuItem(MenuPath + "Preview Display Settings", false, 31)]
        public static void PreviewDisplay() => Preview(7, 1);
        [MenuItem(MenuPath + "Preview Exit Settings", false, 32)]
        public static void PreviewExit() => Preview(7, 4);

        private static void Preview(int page, int settings)
        {
            if (!CanCreate()) return;
            var selected = Selection.activeGameObject;
            var view = selected != null ? selected.GetComponentInParent<BookMenuView>(true) : null;
            if (view == null && selected != null) view = selected.GetComponentInChildren<BookMenuView>(true);
            if (view == null) view = UnityEngine.Object.FindObjectsByType<BookMenuView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(v => v.gameObject.scene == SceneManager.GetActiveScene());
            if (view == null || !view.configured) { Debug.LogWarning("Build Interactive Menu Book을 먼저 실행하세요."); return; }
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Preview Book Page");
            view.gameObject.SetActive(true);
            view.PreviewPage(page);
            view.PreviewSettings(settings);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            Selection.activeGameObject = view.pages[page];
        }

        [MenuItem(MenuPath + "Apply Book Scale 1.875x", false, 20)]
        public static void ApplyBookScale()
        {
            if (!CanCreate()) return;
            var selected = Selection.activeGameObject;
            var canvas = selected != null ? selected.GetComponentInParent<Canvas>(true) : null;
            var scene = SceneManager.GetActiveScene();
            var books = UnityEngine.Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.name == "Book" && r.parent != null
                    && (r.parent.name == "MenuBookUI")
                    && (canvas != null ? r.IsChildOf(canvas.transform) : r.gameObject.scene == scene))
                .ToArray();
            if (books.Length == 0)
            {
                Debug.LogWarning("Book UI: 크기를 적용할 MenuBookUI가 없습니다.");
                return;
            }

            Undo.RecordObjects(books, "Apply Book Scale 1.875x");
            foreach (var book in books)
            {
                // 절대 배율을 지정하므로 반복 실행해도 계속 커지지 않는다.
                book.localScale = new Vector3(BookScale, BookScale, book.localScale.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(book);
                var card = book.parent.Find("Confirmation/Card");
                if (card != null)
                {
                    Undo.RecordObject(card, "Apply Book Confirmation Scale");
                    card.localScale = new Vector3(BookMenuLayout.ScaleIncrease, BookMenuLayout.ScaleIncrease, card.localScale.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(card);
                }
                EditorSceneManager.MarkSceneDirty(book.gameObject.scene);
            }
            Debug.Log($"Book UI: {books.Length}개 책의 배경과 모든 하위 UI에 {BookScale}배 크기를 적용했습니다.");
        }

        [MenuItem(MenuPath + "Create Menu Book", true)]
        [MenuItem(MenuPath + "Build Interactive Menu Book", true)]
        [MenuItem(MenuPath + "Apply Inventory Presentation To Scene", true)]
        [MenuItem(MenuPath + "Preview Inventory", true)]
        [MenuItem(MenuPath + "Preview Display Settings", true)]
        [MenuItem(MenuPath + "Preview Exit Settings", true)]
        [MenuItem(MenuPath + "Apply Book Scale 1.25x", true)]
        private static bool CanCreate() => !EditorApplication.isPlayingOrWillChangePlaymode
            && PrefabStageUtility.GetCurrentPrefabStage() == null;

        private static void Create()
        {
            if (!CanCreate()) return;

            // Multiple 모드로 임포트된 1.png의 실제 Sprite 서브 에셋을 사용한다.
            var sprite = AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                Debug.LogError("Book UI: 펼쳐진 책 Sprite를 찾을 수 없습니다: " + SpritePath);
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                var canvas = FindOrCreateCanvas();
                string rootName = "MenuBookUI";
                var existing = canvas.transform.Find(rootName);
                if (existing != null)
                {
                    Selection.activeGameObject = existing.gameObject;
                    EditorGUIUtility.PingObject(existing.gameObject);
                    Debug.Log("Book UI: 기존 " + rootName + "을 선택했습니다. 편집한 내용은 유지됩니다.", existing);
                    return;
                }

                var root = Area(canvas.transform, rootName, Vector2.zero, Vector2.one);
                var book = Area(root, "Book", Vector2.one * 0.5f, Vector2.one * 0.5f);
                // SetNativeSize의 PPU 보정 대신 실제 잘린 이미지 픽셀 크기를 기준으로 한다.
                book.sizeDelta = sprite.rect.size;
                // 부모에서 확대해 고정 크기 요소, 글자, 여백, 책갈피도 함께 확대한다.
                book.localScale = new Vector3(BookScale, BookScale, 1f);
                var background = Area(book, "Background", Vector2.zero, Vector2.one);
                var image = Undo.AddComponent<Image>(background.gameObject);
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.raycastTarget = false;

                BuildMenu(book);

                // 다른 책과 겹치지 않게 두 번째로 생성한 책은 비활성 상태로 준비한다.
                var other = canvas.transform.Find("ShopBookUi");
                if (other != null && other.gameObject.activeSelf) root.gameObject.SetActive(false);

                EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
                Selection.activeGameObject = root.gameObject;
                EditorGUIUtility.PingObject(root.gameObject);
                Debug.Log("Book UI: " + rootName + " 생성 완료. Background 외에는 빈 RectTransform입니다. " +
                    "Hierarchy에서 편집할 책/페이지를 활성화하고 꾸민 뒤 씬을 저장하세요.", root);

                if (!canvas.gameObject.activeInHierarchy || canvas.transform.lossyScale.x == 0f
                    || canvas.transform.lossyScale.y == 0f)
                    Debug.LogWarning("Book UI: 부모 Canvas가 비활성이거나 Scale이 0이어서 책이 보이지 않습니다. " +
                        "Canvas 활성 상태와 Transform Scale을 확인하세요.", canvas);
            }
            catch (Exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static Canvas FindOrCreateCanvas()
        {
            var selected = Selection.activeGameObject;
            if (selected != null && selected.scene.IsValid() && selected.scene.isLoaded)
            {
                var selectedCanvas = selected.GetComponentInParent<Canvas>(true);
                if (selectedCanvas != null) return selectedCanvas;
            }

            var scene = SceneManager.GetActiveScene();
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.gameObject.scene == scene && c.isRootCanvas)
                .OrderBy(c => c.name == "Canvas" ? 0 : 1)
                .ThenBy(c => c.transform.GetSiblingIndex());
            var existing = canvases.FirstOrDefault();
            if (existing != null) return existing;

            var root = Area(null, "Canvas", Vector2.zero, Vector2.one);
            var canvas = Undo.AddComponent<Canvas>(root.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UILayers.Gameplay;
            var scaler = Undo.AddComponent<CanvasScaler>(root.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Undo.AddComponent<GraphicRaycaster>(root.gameObject);
            return canvas;
        }

        private static void BuildMenu(RectTransform book)
        {
            var pages = Area(book, "Pages", Vector2.zero, Vector2.one);
            for (int i = 0; i < Pages.Length; i++)
            {
                var page = Area(pages, "Page_" + Pages[i], Vector2.zero, Vector2.one);
                var left = PageArea(page, false);
                var right = PageArea(page, true);
                if (i == 0) BuildInventory(left, right);
                if (i == 7) BuildSettings(left, right);
                page.gameObject.SetActive(i == 0);
            }

            var bookmarks = Area(book, "Bookmarks", new Vector2(0.97f, 0.08f), new Vector2(1.07f, 0.94f));
            for (int i = 0; i < Pages.Length; i++)
                Area(bookmarks, "Tab_" + Pages[i], new Vector2(0, 1f - (i + 1) / 8f),
                    new Vector2(1, 1f - i / 8f));
        }

        private static void BuildInventory(RectTransform left, RectTransform right)
        {
            var status = Area(left, "PlayerStatus", new Vector2(0, 0.54f), Vector2.one);
            Area(status, "Portrait", Vector2.zero, new Vector2(0.38f, 1));
            string[] fields = { "PlayerName", "Birthday", "Money", "HP", "MP" };
            for (int i = 0; i < fields.Length; i++)
                Area(status, fields[i], new Vector2(0.42f, 1f - (i + 1) / 5f),
                    new Vector2(1, 1f - i / 5f));
            Area(left, "InventorySlots", Vector2.zero, new Vector2(1, 0.5f));
            Area(right, "ItemDetails", Vector2.zero, Vector2.one);
        }

        private static void BuildSettings(RectTransform left, RectTransform right)
        {
            var categories = Area(left, "Categories", Vector2.zero, Vector2.one);
            var panels = Area(right, "SettingPanels", Vector2.zero, Vector2.one);
            for (int i = 0; i < Settings.Length; i++)
            {
                Area(categories, "Category_" + Settings[i], new Vector2(0, 0.78f - i * 0.24f),
                    new Vector2(1, 0.98f - i * 0.24f));
                var panel = Area(panels, "Panel_" + Settings[i], Vector2.zero, Vector2.one);
                panel.gameObject.SetActive(i == 0);
            }
        }

        private static RectTransform PageArea(RectTransform parent, bool right)
        {
            // 663 x 418 스프라이트의 종이 안쪽 여백. 가운데 제본 영역을 피한다.
            return Area(parent, right ? "RightPage" : "LeftPage",
                new Vector2(right ? 0.54f : 0.055f, 0.12f),
                new Vector2(right ? 0.945f : 0.46f, 0.91f));
        }

        private static RectTransform Area(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = Vector2.one * 0.5f;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            return rect;
        }
    }
}
