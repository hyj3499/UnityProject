using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>Authoring factory; saved scene references are used unchanged during play.</summary>
    public static class StartScreenLayout
    {
        public const string ArtRoot = "Sprites/StartScreen/";
        private static Font Font => Resources.Load<Font>(ArtRoot + "Fonts/PF스타더스트 3.0") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static readonly Color Ink = new Color32(66, 39, 37, 255);
        private static readonly Color Cream = new Color32(249, 233, 196, 255);
        public static Sprite Art(string path, int index = 0)
        {
            var sprites = Resources.LoadAll<Sprite>(ArtRoot + path).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            if (sprites.Length == 0) throw new InvalidOperationException("Missing StartScreen sprite: " + path);
            return sprites[Mathf.Clamp(index, 0, sprites.Length - 1)];
        }
        public static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h); return rt;
        }
        public static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }
        private static Image Image(Transform parent, string name, string art, float x, float y, float w, float h)
        {
            var image = Node(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            if (art != null) image.sprite = Art(art);
            image.raycastTarget = false; return image;
        }
        private static Text Label(Transform parent, string name, string text, float x, float y, float w, float h, int size = 28, bool light = false)
        {
            var label = Node(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = Font; label.fontSize = size; label.text = text; label.color = light ? Cream : Ink;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private static Button Button(Transform parent, string name, string label, float x, float y, float w = 220, float h = 64, bool dark = false)
        {
            var image = Image(parent, name, (dark ? "Dark/" : "White/") + "Button_normal", x, y, w, h);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            var prefix = dark ? "Dark/" : "White/";
            button.spriteState = new SpriteState { highlightedSprite = Art(prefix + "Button_hover"), selectedSprite = Art(prefix + "Button_hover"), pressedSprite = Art(prefix + "Button_pressed"), disabledSprite = Art(prefix + "Button_disabled") };
            Label(image.transform, "Label", label, 0, 0, w - 16, h - 12, 26, dark);
            return button;
        }
        private static Button Icon(Transform parent, string name, string path, float x, float y, float size, int index = 0)
        {
            var image = Image(parent, name, path, x, y, size, size); image.sprite = Art(path, index); image.raycastTarget = true; image.preserveAspect = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; return button;
        }
        private static Button Close(Transform parent, float x, float y, bool dark = false)
        {
            string prefix = dark ? "Dark/" : "White/";
            var button = Icon(parent, "Close", prefix + "Close_btn", x, y, 48);
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = Art(prefix + "Close_btn_hovered"), pressedSprite = Art(prefix + "Close_btn_pressed") };
            return button;
        }
        private static Slider Slider(Transform parent, string name, float x, float y, float width, bool dark = false)
        {
            var root = Node(parent, name, x, y, width, 28);
            var track = Image(root, "Track", "White/ProgressBar_background", 0, 0, width, 16);
            track.raycastTarget = true;
            var fillArea = Node(root, "FillArea", 0, 0, width - 12, 12);
            var fill = Image(fillArea, "Fill", "White/ProgressBar_fill", 0, 0, width - 12, 12); Stretch(fill.rectTransform);
            var handleArea = Node(root, "HandleArea", 0, 0, width - 20, 28);
            var handle = Image(handleArea, "Handle", "White/Scrolbar_grab_horizontal_normal", 0, 0, 20, 28);
            handle.raycastTarget = true;
            var slider = root.gameObject.AddComponent<Slider>(); slider.targetGraphic = handle;
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform;
            slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            handle.rectTransform.sizeDelta = new Vector2(20, 0);
            return slider;
        }
        private static Toggle Radio(Transform parent, string name, string label, float x, float y, ToggleGroup group)
        {
            var root = Node(parent, name, x, y, 170, 56);
            var image = Image(root, "Unchecked", "White/Radiobutton_unchecked", -54, 0, 40, 40); image.raycastTarget = true;
            var check = Image(image.transform, "Checked", "White/Radiobutton_checked", 0, 0, 40, 40);
            var toggle = root.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = image; toggle.graphic = check; toggle.group = group;
            toggle.SetIsOnWithoutNotify(false);
            var text = Label(root, "Label", label, 24, 0, 100, 44, 30, true); text.raycastTarget = true;
            return toggle;
        }
        private static ScrollRect Scroll(Transform parent, string name, float x, float y, float width, float height, bool dark, out RectTransform content)
        {
            var root = Node(parent, name, x, y, width, height);
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45;
            var viewport = Node(root, "Viewport", -12, 0, width - 28, height);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, .001f);
            content = Node(viewport, "Content", 0, 0, width - 28, height);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, height); content.anchoredPosition = Vector2.zero;
            var bar = Image(root, "Scrollbar", dark ? "Dark/ScrollBar_scroll_vertical" : "White/Scrolbar_scroll_vertical", width / 2 - 10, 0, 20, height);
            bar.raycastTarget = true;
            var area = Node(bar.transform, "SlidingArea", 0, 0, 20, height - 8);
            var handle = Image(area, "Handle", dark ? "Dark/ScrollBar_grabber_vertical" : "White/Scrolbar_grab_vertical_normal", 0, 0, 20, 48);
            handle.raycastTarget = true;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
            handle.rectTransform.sizeDelta = Vector2.zero;
            scroll.viewport = viewport; scroll.content = content; scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
        public static StartScreenView Build(Transform parent)
        {
            var root = Node(parent, "StartScreenUI", 0, 0, 1920, 1080); Stretch(root);
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = UILayers.Title;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var view = root.gameObject.AddComponent<StartScreenView>();
            var background = Image(root, "Backdrop", null, 0, 0, 1920, 1080); Stretch(background.rectTransform);
            background.color = new Color32(234, 218, 181, 255); background.raycastTarget = true;
            var design = Node(root, "Design", 0, 0, 1920, 1080); design.gameObject.AddComponent<StartScreenFit>();
            var menu = Node(design, "MainMenu", 0, 0, 1920, 1080); view.mainMenu = menu.gameObject;
            var illustration = Image(menu, "StartScreen", "StartScreen", -240, 20, 1250, 860); illustration.preserveAspect = true;
            var buttons = Node(menu, "MenuButtons", 610, -35, 370, 460);
            view.newGame = Button(buttons, "NewGame", "NEW GAME", 0, 150, 350, 80);
            view.continueGame = Button(buttons, "Continue", "CONTINUE", 0, 50, 350, 80);
            view.settings = Button(buttons, "Settings", "SETTING", 0, -50, 350, 80);
            view.exit = Button(buttons, "Exit", "EXIT", 0, -150, 350, 80);

            var creator = Image(design, "CharacterCreator", "new game pannel_white", -180, 0, 1280, 768).rectTransform;
            creator.GetComponent<Image>().raycastTarget = true; view.creatorPanel = creator.gameObject;
            view.creatorTitle = Label(creator, "Title", "캐릭터 기본 설정", 0, 308, 1000, 60, 36, true);
            view.closeCreator = Close(creator, 578, 321);
            var standing = Image(creator, "StandingPanel", "new game payer standing pannel_white", -426, 0, 256, 384);
            view.characterPreview = Node(standing.transform, "Character", 0, 0, 256, 256).gameObject.AddComponent<RawImage>();
            view.characterPreview.raycastTarget = false;
            // The sprite sheet order is up, left, right, down.
            view.rotateLeft = Icon(creator, "RotateLeft", "arrow icon_white", -510, -245, 48, 1);
            view.rotateRight = Icon(creator, "RotateRight", "arrow icon_white", -342, -245, 48, 2);
            Label(creator, "RotateHint", "회전", -426, -245, 100, 44, 24, true);

            var basic = Node(creator, "BasicSettings", 170, 0, 680, 520); view.basicPanel = basic.gameObject;
            Label(basic, "NameLabel", "name", -245, 155, 130, 50, 30, true);
            var nameBox = Image(basic, "NameInput", "new game name pannel_white", 15, 155, 330, 70);
            nameBox.raycastTarget = true;
            var nameText = Label(nameBox.transform, "Text", "Ros", 0, 0, 292, 52, 30, true); nameText.alignment = TextAnchor.MiddleLeft;
            nameText.gameObject.AddComponent<RectMask2D>(); nameText.supportRichText = false;
            var input = nameBox.gameObject.AddComponent<InputField>(); input.textComponent = nameText; input.targetGraphic = nameBox;
            input.characterLimit = 20; input.lineType = InputField.LineType.SingleLine; input.text = "Ros"; input.readOnly = true;
            input.caretBlinkRate = .85f; input.caretWidth = 2; input.customCaretColor = true; input.caretColor = Cream;
            view.nameInput = input;
            view.editName = Icon(basic, "EditName", "Edit_btn_normal", 235, 155, 56);
            view.editName.transition = Selectable.Transition.SpriteSwap;
            view.editName.spriteState = new SpriteState { highlightedSprite = Art("Edit_btn_hover"), pressedSprite = Art("Edit_btn_pressed") };
            Label(basic, "GenderLabel", "성별", -245, 25, 130, 50, 30, true);
            var group = basic.gameObject.AddComponent<ToggleGroup>(); group.allowSwitchOff = true;
            view.male = Radio(basic, "Male", "남", -30, 25, group); view.female = Radio(basic, "Female", "여", 180, 25, group);
            Label(basic, "BirthdayLabel", "생년월일", -245, -110, 150, 50, 28, true);
            view.seasonPrevious = Icon(basic, "PreviousSeason", "arrow icon_white", -118, -110, 40, 1);
            view.seasonLabel = Label(basic, "Season", "봄", -25, -110, 120, 50, 30, true);
            view.seasonNext = Icon(basic, "NextSeason", "arrow icon_white", 66, -110, 40, 2);
            view.dayPrevious = Icon(basic, "PreviousDay", "arrow icon_white", 130, -110, 40, 1);
            view.dayLabel = Label(basic, "Day", "1일", 215, -110, 100, 50, 30, true);
            view.dayNext = Icon(basic, "NextDay", "arrow icon_white", 300, -110, 40, 2);
            view.validation = Label(basic, "Validation", "", 20, -205, 590, 50, 25, true);
            view.next = Button(creator, "NextStep", "다음 단계", 320, -302, 260, 68);
            view.back = Button(creator, "PreviousStep", "이전 단계", 20, -302, 240, 68);
            view.finish = Button(creator, "StartGame", "게임 시작", 320, -302, 260, 68);
            var custom = Node(creator, "CustomSettings", 170, 0, 680, 520); view.customPanel = custom.gameObject;
            view.customScroll = Scroll(custom, "PartsScroll", 0, 0, 680, 520, false, out var content);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlWidth = true; layout.childControlHeight = false;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.spacing = 12; layout.padding = new RectOffset(8, 8, 8, 8);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var labels = new[] { "피부색", "눈", "헤어", "상의", "하의", "한벌옷", "신발", "악세사리" };
            var ids = new[] { "Skin", "Eyes", "Hair", "Top", "Bottom", "Outfit", "Shoes", "Accessory" };
            view.parts = new StartScreenPart[labels.Length];
            for (int i = 0; i < labels.Length; i++) view.parts[i] = Part(content, ids[i], labels[i], i);
            BuildPicker(design, view); BuildSettings(design, view); BuildModal(root, view);
            view.previewBody = Art("CharacterPreview/base_animations").texture;
            view.previewEyes = Art("CharacterPreview/eyes").texture;
            view.previewHeadGuide = Art("CharacterPreview/base_animations_headguide").texture;
            view.characterPreview.texture = Resources.Load<Texture2D>(ArtRoot + "CharacterPreview/DefaultStanding");
            view.parts[1].portrait.texture = Resources.Load<Texture2D>(ArtRoot + "CharacterPreview/DefaultPortrait");
            view.ShowMain(); view.customPanel.SetActive(false); view.back.gameObject.SetActive(false); view.finish.gameObject.SetActive(false);
            return view;
        }
        private static StartScreenPart Part(Transform parent, string id, string title, int index)
        {
            var root = Node(parent, id, 0, 0, 620, 242);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 242;
            Label(root, "Label", title, -220, 95, 170, 42, 30, true);
            var part = new StartScreenPart(); part.label = title;
            if (index > 0)
            {
                var frame = Image(root, "PartFrame", "Panel_big_frame", -220, 6, 112, 112); frame.raycastTarget = true;
                part.select = frame.gameObject.AddComponent<Button>(); part.select.targetGraphic = frame;
                part.portrait = Node(frame.transform, "Portrait16x16", 0, 0, 80, 80).gameObject.AddComponent<RawImage>();
                part.portrait.raycastTarget = false; part.portrait.enabled = index == 1;
                if (index != 1) Label(frame.transform, "EmptyLabel", "없음", 0, 0, 88, 45, 23);
            }
            part.swatch = Image(root, "SelectedColor", null, index == 0 ? -220 : -105, index == 0 ? 6 : 34, index == 0 ? 96 : 48, index == 0 ? 96 : 48);
            part.swatch.color = index == 0 ? FarmerAppearancePalette.DefaultSkin : FarmerAppearancePalette.DefaultEyes;
            Label(root, "HueLabel", "H", -48, 56, 32, 30, 21, true);
            Label(root, "SaturationLabel", "S", -48, 12, 32, 30, 21, true);
            Label(root, "ValueLabel", "V", -48, -32, 32, 30, 21, true);
            part.hue = Slider(root, "Hue", 118, 56, 280);
            part.saturation = Slider(root, "Saturation", 118, 12, 280);
            part.value = Slider(root, "Value", 118, -32, 280);
            Color.RGBToHSV(part.swatch.color, out float initialH, out float initialS, out float initialV);
            part.hue.SetValueWithoutNotify(initialH); part.saturation.SetValueWithoutNotify(initialS); part.value.SetValueWithoutNotify(initialV);
            var bars = root.gameObject.AddComponent<StartScreenHsvBars>();
            bars.enabled = false;
            bars.hue = part.hue; bars.saturation = part.saturation; bars.value = part.value;
            bars.hueGradient = Gradient(part.hue); bars.saturationGradient = Gradient(part.saturation); bars.valueGradient = Gradient(part.value);
            bars.enabled = true;
            Label(root, "RecommendedLabel", "추천", -238, -86, 110, 36, 23, true);
            part.recommendedColors = index == 0
                ? new[] { FarmerAppearancePalette.DefaultSkin, Hex("F2D4B7"), Hex("DDB08B"), Hex("BF8861"), Hex("8F5C3F"), Hex("583B2C") }
                : new[] { FarmerAppearancePalette.DefaultEyes, Hex("825032"), Hex("578572"), Hex("5B8FAE"), Hex("9A78AB"), Hex("D6AD69") };
            part.recommendations = new Button[part.recommendedColors.Length];
            for (int i = 0; i < part.recommendations.Length; i++)
            {
                var frame = Image(root, "Recommended_" + i, "White/InventorySlot1", -145 + i * 72, -86, 52, 52); frame.raycastTarget = true;
                var color = Image(frame.transform, "Color", null, 0, 0, 36, 36); color.color = part.recommendedColors[i];
                var button = frame.gameObject.AddComponent<Button>(); button.targetGraphic = frame; part.recommendations[i] = button;
            }
            part.options = index == 1 ? new[] { FarmerEyes.FirstStyle, FarmerEyes.SecondStyle } : new[] { "" };
            return part;
        }
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var result); return result; }
        private static RawImage Gradient(Slider slider)
        {
            var gradient = Node(slider.transform.Find("Track"), "Gradient", 0, 0, 264, 10).gameObject.AddComponent<RawImage>();
            gradient.raycastTarget = false;
            slider.fillRect.gameObject.SetActive(false);
            return gradient;
        }
        private static void BuildPicker(Transform parent, StartScreenView view)
        {
            var root = Image(parent, "PartPicker", "Dark/Panel_bg1", 700, 0, 460, 680).rectTransform;
            root.GetComponent<Image>().raycastTarget = true; view.pickerPanel = root.gameObject;
            view.pickerTitle = Label(root, "Title", "눈", 0, 278, 300, 52, 34, true);
            view.pickerClose = Close(root, 180, 282, true);
            view.pickerScroll = Scroll(root, "SlotsScroll", 0, 5, 384, 452, true, out var content); view.pickerContent = content;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(96, 96); grid.spacing = new Vector2(16, 16);
            grid.padding = new RectOffset(10, 10, 10, 10); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var slot = Image(content, "SlotTemplate", "Dark/InventorySlot", 0, 0, 96, 96); slot.raycastTarget = true;
            view.slotTemplate = slot.gameObject.AddComponent<Button>(); view.slotTemplate.targetGraphic = slot;
            var portrait = Node(slot.transform, "Portrait16x16", 0, 0, 80, 80).gameObject.AddComponent<RawImage>(); portrait.raycastTarget = false;
            Label(slot.transform, "EmptyLabel", "", 0, 0, 80, 38, 23, true);
            slot.gameObject.SetActive(false);
            view.slotNormal = Art("Dark/InventorySlot"); view.slotSelected = Art("Dark/InventorySlot2");
            view.pickerApply = Button(root, "ApplySelection", "설정", -108, -274, 180, 60, true);
            Label(root, "Hint", "슬롯은 미리보기\n설정 버튼으로 확정", 95, -274, 190, 64, 20, true);
        }
        private static void BuildSettings(Transform parent, StartScreenView view)
        {
            var root = Image(parent, "Settings", "new game pannel_white", 0, 0, 720, 432).rectTransform;
            root.GetComponent<Image>().raycastTarget = true; view.settingsPanel = root.gameObject;
            Label(root, "Title", "설정", 0, 140, 500, 60, 36, true);
            Label(root, "VolumeLabel", "음량", -230, 15, 160, 50, 28, true);
            view.volume = Slider(root, "Volume", 70, 15, 360);
            view.settingsBack = Button(root, "Back", "돌아가기", 0, -135, 240, 64);
        }
        private static void BuildModal(Transform parent, StartScreenView view)
        {
            var overlay = Image(parent, "Confirmation", null, 0, 0, 1920, 1080); Stretch(overlay.rectTransform);
            overlay.color = new Color(0, 0, 0, .5f); overlay.raycastTarget = true; view.modalPanel = overlay.gameObject;
            var design = Node(overlay.transform, "Design", 0, 0, 1920, 1080); design.gameObject.AddComponent<StartScreenFit>();
            var panel = Image(design, "Dialog", "new game pannel_white", 0, 0, 800, 400).rectTransform;
            panel.GetComponent<Image>().raycastTarget = true;
            view.modalMessage = Label(panel, "Message", "캐릭터 생성을 취소하시겠습니까?", 0, 60, 680, 150, 32, true);
            view.modalYes = Button(panel, "Yes", "예", -160, -105, 240, 64);
            view.modalNo = Button(panel, "No", "아니오", 160, -105, 240, 64);
        }
    }
}
