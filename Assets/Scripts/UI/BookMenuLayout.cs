using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>에디터 생성기와 런타임 대체 생성에서 같은 레이아웃을 사용한다.</summary>
    public static class BookMenuLayout
    {
        public const string ArtRoot = "Sprites/UI/Book/";
        public static readonly string[] PageNames = { "Inventory", "Skills", "Residents", "Livestock", "Quests", "MapEncyclopedia", "Tutorial", "Settings" };
        private static readonly string[] PageLabels = { "인벤토리", "스킬", "주민", "가축", "퀘스트", "지도도감", "튜토리얼", "설정" };
        private static readonly Color Ink = new Color(0.27f, 0.17f, 0.12f);
        private static Font Font => Resources.Load<Font>("Fonts/PF스타더스트 3.0") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static BookMenuView Build(RectTransform root)
        {
            var view = Component<BookMenuView>(root);
            if (view.configured) return view;
#if UNITY_EDITOR
            var viewFields = typeof(BookMenuView).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .Where(f => !Attribute.IsDefined(f, typeof(NonSerializedAttribute))).ToArray();
            var originalView = !Application.isPlaying ? viewFields.Select(f => f.GetValue(view)).ToArray() : null;
#endif
            var overlay = Component<Canvas>(root);
            overlay.overrideSorting = true;
            overlay.sortingOrder = UILayers.Book;
            Component<GraphicRaycaster>(root);
            view.dim = Picture(root, "Dim", 0, 0, 1, 1, null);
            view.dim.color = new Color(0, 0, 0, 0.35f);
            view.dim.raycastTarget = true;
            view.dim.transform.SetAsFirstSibling();
            view.book = Node(root, "Book", .5f, .5f, .5f, .5f);
            view.book.sizeDelta = new Vector2(663, 418);
            view.book.localScale = new Vector3(1.25f, 1.25f, 1);
            view.idle = Sprite("Inventory Book/Book Idle/1");
            view.background = Picture(view.book, "Background", 0, 0, 1, 1, view.idle);
            view.background.raycastTarget = true;
            view.opening = Frames("Book Open and Close/Open");
            view.closing = Frames("Book Open and Close/Close");
            view.flipLeft = Frames("Book Page Flip/Flip Left");
            view.flipRight = Frames("Book Page Flip/Flip Right");
            var pages = Node(view.book, "Pages", 0, 0, 1, 1);
            view.content = Component<CanvasGroup>(pages);
            view.pages = new GameObject[8];
            for (int i = 0; i < 8; i++)
            {
                var page = Node(pages, "Page_" + PageNames[i], 0, 0, 1, 1);
                view.pages[i] = page.gameObject;
                var left = Node(page, "LeftPage", .055f, .12f, .46f, .91f);
                var right = Node(page, "RightPage", .54f, .12f, .945f, .91f);
                if (i == 0) Inventory(view, left, right);
                if (i == 7) Settings(view, left, right);
            }
            var tabs = Node(view.book, "Bookmarks", .97f, .08f, 1.07f, .94f);
            view.bookmarks = tabs.gameObject;
            view.tabs = new Button[8];
            for (int i = 0; i < 8; i++)
                view.tabs[i] = Button(tabs, "Tab_" + PageNames[i], PageLabels[i], 0, 1-(i+1)/8f, 1, 1-i/8f-.008f, Sprite("Content/8 Side tabs/2"), 11);
            view.close = Button(view.book, "Close", "닫기", .84f, .015f, .94f, .085f, Sprite("Content/4 Buttons/0"), 11);
            BuildConfirmation(view, root);
            view.configured = true;
            view.PreviewPage(0);
            view.PreviewSettings(1);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // 하위 오브젝트의 생성 Undo는 진행 중인 RecordObject를 종료한다.
                // 모든 오브젝트를 만든 다음 참조 변경을 한 번에 기록해야 Redo에서도 살아남는다.
                var completedView = viewFields.Select(f => f.GetValue(view)).ToArray();
                for (int i = 0; i < viewFields.Length; i++) viewFields[i].SetValue(view, originalView[i]);
                UnityEditor.Undo.RecordObject(view, "Connect Menu Book References");
                for (int i = 0; i < viewFields.Length; i++) viewFields[i].SetValue(view, completedView[i]);
                UnityEditor.Undo.FlushUndoRecordObjects();
            }
#endif
            return view;
        }

        private static void Inventory(BookMenuView v, RectTransform left, RectTransform right)
        {
            Header(left, "인벤토리", .89f);
            var status = Node(left, "PlayerStatus", 0, .44f, 1, .87f);
            var portraitFrame = Picture(status, "PortraitFrame", 0, .02f, .34f, .98f, Sprite("Content/5 Holders/5"));
            portraitFrame.color = new Color(.92f, .87f, .74f);
            v.portrait = Node(status, "Portrait", .025f, .04f, .315f, .96f);
            Component<RectMask2D>(v.portrait);
            Label(v.portrait, "Placeholder", "플레이어\n모습", 0, 0, 1, 1, 12, TextAnchor.MiddleCenter);
            v.playerName = Label(status, "PlayerName", "이름  농부", .38f, .8f, 1, 1, 13);
            v.birthday = Label(status, "Birthday", "생일  미등록", .38f, .6f, 1, .8f, 12);
            v.money = Label(status, "Money", "소지금  0 G", .38f, .4f, 1, .6f, 12);
            v.hp = Label(status, "HP", "체력  100 / 100", .38f, .2f, 1, .4f, 12);
            v.mp = Label(status, "MP", "MP  100 / 100", .38f, 0, 1, .2f, 12);
            v.capacity = Label(left, "Capacity", "가방  10 / 30칸", 0, .35f, 1, .43f, 12);
            var inventory = Node(left, "InventorySlots", 0, .025f, 1, .34f);
            v.slots = new BookMenuView.Slot[30];
            v.lockedRows = new GameObject[3];
            for (int row = 0; row < 3; row++)
            {
                var rowRoot = Node(inventory, "Row_" + row, 0, 1-(row+1)/3f, 1, 1-row/3f);
                for (int col = 0; col < 10; col++)
                {
                    int index = row*10+col;
                    var b = Button(rowRoot, "Slot_" + index, "", col/10f+.003f, .12f, (col+1)/10f-.003f, .88f, Sprite("Content/5 Holders/7"), 10);
                    var icon = Picture(b.transform, "Icon", .12f, .12f, .88f, .88f, null);
                    icon.enabled = false;
                    var highlight = Picture(b.transform, "Selected", 0, 0, 1, 1, null);
                    highlight.color = new Color(.7f, .42f, .08f, .28f);
                    highlight.enabled = false;
                    var count = Label(b.transform, "Count", "", 0, 0, .95f, 1, 9, TextAnchor.LowerRight);
                    v.slots[index] = new BookMenuView.Slot { button = b, icon = icon, selected = highlight, count = count };
                }
                var locked = Picture(rowRoot, "Locked", 0, .08f, 1, .92f, null);
                locked.color = new Color(.75f, .66f, .49f, .94f);
                locked.raycastTarget = true;
                Label(locked.transform, "Label", "가방 확장 시 사용 가능", 0, 0, 1, 1, 11, TextAnchor.MiddleCenter);
                locked.gameObject.SetActive(row > 0);
                v.lockedRows[row] = locked.gameObject;
            }
            var detail = Node(right, "ItemDetails", 0, 0, 1, 1);
            Header(detail, "아이템 정보", .89f);
            Picture(detail, "IconHolder", .31f, .57f, .69f, .84f, Sprite("Content/5 Holders/2"));
            v.itemIcon = Picture(detail, "ItemIcon", .36f, .61f, .64f, .8f, null);
            v.itemIcon.enabled = false;
            v.itemName = Label(detail, "Name", "아이템을 선택하세요", 0, .46f, 1, .56f, 16, TextAnchor.MiddleCenter);
            v.itemDescription = Label(detail, "Description", "왼쪽 가방의 아이템을 누르면\n자세한 정보를 볼 수 있습니다.", .025f, .14f, .975f, .43f, 13, TextAnchor.UpperLeft);
            v.itemFacts = Label(detail, "Facts", "", .025f, 0, .975f, .13f, 11);
        }

        private static void Settings(BookMenuView v, RectTransform left, RectTransform right)
        {
            Header(left, "설정", .89f);
            var categories = Node(left, "Categories", 0, 0, 1, .86f);
            var panels = Node(right, "SettingPanels", 0, 0, 1, 1);
            string[] ids = { "Gameplay", "Display", "Audio", "Controls", "Exit" };
            string[] titles = { "게임 플레이", "디스플레이", "오디오", "조작", "종료" };
            v.categories = new Button[5];
            v.settingPanels = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                v.categories[i] = Button(categories, "Category_" + ids[i], titles[i], .04f, .8f-i*.19f, .96f, .95f-i*.19f, Sprite("Content/4 Buttons/0"), 15);
                var panel = Node(panels, "Panel_" + ids[i], 0, 0, 1, 1);
                v.settingPanels[i] = panel.gameObject;
                Header(panel, titles[i], .89f);
                if (i != 1 && i != 4)
                    Label(panel, "ComingSoon", "준비 중입니다", 0, .4f, 1, .65f, 15, TextAnchor.MiddleCenter);
                if (i == 1)
                {
                    Label(panel, "ModeTitle", "화면 모드", 0, .78f, 1, .86f, 13);
                    v.previousMode = Arrow(panel, "PreviousMode", "<", 0, .66f);
                    v.nextMode = Arrow(panel, "NextMode", ">", .86f, .66f);
                    v.modeLabel = Label(panel, "Mode", "창 모드", .14f, .66f, .86f, .77f, 12, TextAnchor.MiddleCenter);
                    Label(panel, "ResolutionTitle", "해상도", 0, .53f, 1, .62f, 13);
                    v.previousResolution = Arrow(panel, "PreviousResolution", "<", 0, .41f);
                    v.nextResolution = Arrow(panel, "NextResolution", ">", .86f, .41f);
                    v.resolutionLabel = Label(panel, "Resolution", "1920 × 1080", .14f, .41f, .86f, .52f, 13, TextAnchor.MiddleCenter);
                    v.scaleLabel = Label(panel, "ScaleLabel", "화면 스케일 (게임 줌)  100%", 0, .29f, 1, .38f, 12);
                    v.scale = MakeSlider(panel);
                    v.applyDisplay = Button(panel, "Apply", "적용", .45f, .07f, 1, .18f, Sprite("Content/4 Buttons/0"), 13);
                    v.displayMessage = Label(panel, "Message", "", 0, -.02f, 1, .065f, 10);
                }
                if (i == 4)
                {
                    Label(panel, "ExitNote", "게임을 종료합니다.\n저장하지 않은 진행 상황은\n바로 종료 시 사라집니다.", .03f, .5f, .97f, .79f, 14, TextAnchor.MiddleCenter);
                    v.saveAndQuit = Button(panel, "SaveAndQuit", "저장하고 종료", .06f, .3f, .94f, .44f, Sprite("Content/4 Buttons/0"), 14);
                    v.quit = Button(panel, "Quit", "바로 종료", .06f, .1f, .94f, .24f, Sprite("Content/4 Buttons/0"), 14);
                }
            }
        }

        private static void BuildConfirmation(BookMenuView v, RectTransform root)
        {
            var shield = Picture(root, "Confirmation", 0, 0, 1, 1, null);
            shield.color = new Color(0, 0, 0, .45f);
            shield.raycastTarget = true;
            v.confirmation = shield.gameObject;
            var card = Picture(shield.transform, "Card", .5f, .5f, .5f, .5f, Sprite("Content/5 Holders/5"));
            card.rectTransform.sizeDelta = new Vector2(340, 180);
            v.confirmationText = Label(card.transform, "Question", "게임을 종료할까요?", .08f, .44f, .92f, .88f, 16, TextAnchor.MiddleCenter);
            v.confirmYes = Button(card.transform, "Yes", "종료", .1f, .12f, .46f, .36f, Sprite("Content/4 Buttons/0"), 14);
            v.confirmNo = Button(card.transform, "No", "취소", .54f, .12f, .9f, .36f, Sprite("Content/4 Buttons/0"), 14);
            v.confirmation.SetActive(false);
        }

        private static Slider MakeSlider(Transform parent)
        {
            var root = Node(parent, "Scale", .03f, .21f, .97f, .28f);
            var track = Picture(root, "Track", 0, .35f, 1, .65f, null);
            track.color = new Color(.55f, .39f, .24f);
            var handleArea = Node(root, "HandleArea", 0, 0, 1, 1);
            handleArea.offsetMin = new Vector2(8, 0);
            handleArea.offsetMax = new Vector2(-8, 0);
            var handle = Picture(handleArea, "Handle", 0, 0, 0, 1, Sprite("Content/5 Holders/7"));
            handle.rectTransform.sizeDelta = new Vector2(16, 0);
            handle.raycastTarget = true;
            var slider = Component<Slider>(root);
            slider.targetGraphic = handle;
            slider.handleRect = handle.rectTransform;
            slider.minValue = GameManager.MinZoom;
            slider.maxValue = GameManager.MaxZoom;
            slider.value = 1;
            return slider;
        }

        private static Button Arrow(Transform parent, string name, string label, float x, float y)
            => Button(parent, name, label, x, y, x+.14f, y+.11f, Sprite("Content/5 Holders/7"), 15);

        private static void Header(Transform parent, string title, float y)
        {
            var banner = Picture(parent, "Header", 0, y, 1, 1, Sprite("Content/14 Banners & Headers/Plain/4"));
            Label(banner.transform, "Title", title, .04f, 0, .96f, 1, 17, TextAnchor.MiddleCenter);
        }

        public static Sprite Sprite(string path) => Resources.LoadAll<Sprite>(ArtRoot + path).FirstOrDefault();
        private static Sprite[] Frames(string path) => Resources.LoadAll<Sprite>(ArtRoot + "Inventory Book/" + path)
            .OrderBy(s => int.TryParse(s.name.Split('_')[0], out int number) ? number : 0).ToArray();

        private static Button Button(Transform parent, string name, string label, float x0, float y0, float x1, float y1, Sprite sprite, int size)
        {
            var image = Picture(parent, name, x0, y0, x1, y1, sprite);
            image.preserveAspect = false;
            image.raycastTarget = true;
            var button = Component<Button>(image.transform);
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1, .96f, .82f);
            colors.pressedColor = new Color(.76f, .64f, .43f);
            button.colors = colors;
            // 키보드 방향키로 숨겨진 페이지나 다른 메뉴로 포커스가 이동하지 않도록 한다.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(image.transform, "Label", label, .1f, 0, .9f, 1, size, TextAnchor.MiddleCenter);
            return button;
        }

        private static Image Picture(Transform parent, string name, float x0, float y0, float x1, float y1, Sprite sprite)
        {
            var image = Component<Image>(Node(parent, name, x0, y0, x1, y1));
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
            if (name == "Icon" || name == "ItemIcon") image.preserveAspect = true;
            return image;
        }

        private static Text Label(Transform parent, string name, string text, float x0, float y0, float x1, float y1, int size, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var label = Component<Text>(Node(parent, name, x0, y0, x1, y1));
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = Ink;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        public static RectTransform Node(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var existing = parent.Find(name) as RectTransform;
            var rect = existing;
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                go.layer = parent.gameObject.layer;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Build Menu Book");
#endif
            }
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RegisterCompleteObjectUndo(rect, "Build Menu Book");
#endif
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = Vector2.one*.5f;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static T Component<T>(Transform node) where T : Component
        {
            var existing = node.GetComponent<T>();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var component = existing != null ? existing : UnityEditor.Undo.AddComponent<T>(node.gameObject);
                // AddComponent 자체만 기록하면 Redo 시 참조/폰트/스프라이트가 기본값으로 돌아간다.
                UnityEditor.Undo.RegisterCompleteObjectUndo(component, "Build Menu Book");
                return component;
            }
#endif
            return existing != null ? existing : node.gameObject.AddComponent<T>();
        }
    }
}
