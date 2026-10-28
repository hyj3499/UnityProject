using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    public static class SettingsDropdown
    {
        private static Sprite Art(string name) => Resources.LoadAll<Sprite>("Sprites/UI/Button/" + name).First();
        private static Image Picture(Transform parent, string name, string art, float x, float y, float w, float h)
        {
            var image = StartScreenLayout.Node(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = Art(art); image.color = Color.white;
            image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 1f / 3;
            return image;
        }
        private static TextMeshProUGUI Label(Transform parent, string name, float x, float w)
        {
            var label = StartScreenLayout.Node(parent, name, x, 0, w, 48).gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 25; label.color = UITheme.Ink; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 25;
            label.raycastTarget = false;
            LocalizedText.Attach(label);
            return label;
        }

        public static TMP_Dropdown Build(Transform parent, string name, float y)
        {
            var image = Picture(parent, name, "combobox_white", 85, y, 360, 60);
            var dropdown = image.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = image;
            dropdown.captionText = Label(image.transform, "Caption", -10, 290);
            var arrow = Picture(image.transform, "Arrow", "DropdownList_icon_white", 152, 0, 24, 24);
            arrow.raycastTarget = false; arrow.preserveAspect = true;
            var templateImage = Picture(image.transform, "Template", "combobox_white", 0, -60, 360, 192);
            var template = templateImage.rectTransform;
            template.anchorMin = template.anchorMax = new Vector2(.5f, 0);
            template.pivot = new Vector2(.5f, 1); template.anchoredPosition = new Vector2(0, -2);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36;
            var viewport = StartScreenLayout.Node(template, "Viewport", 0, 0, 0, 0);
            StartScreenLayout.Stretch(viewport, 4); viewport.gameObject.AddComponent<RectMask2D>();
            var content = StartScreenLayout.Node(viewport, "Content", 0, 0, 0, 48);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            var item = StartScreenLayout.Node(content, "Item", 0, 0, 0, 48);
            item.anchorMin = new Vector2(0, .5f); item.anchorMax = new Vector2(1, .5f);
            var hover = item.gameObject.AddComponent<Image>(); hover.color = new Color(1, 1, 1, .15f);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = hover;
            var radio = Picture(item, "Unchecked", "Radiobutton_unchecked_white", -146, 0, 28, 28);
            radio.raycastTarget = false; radio.preserveAspect = true;
            var check = Picture(radio.transform, "Checked", "Radiobutton_checked_white", 0, 0, 28, 28);
            check.raycastTarget = false; check.preserveAspect = true; toggle.graphic = check;
            dropdown.itemText = Label(item, "ItemLabel", 14, 258);
            scroll.viewport = viewport; scroll.content = content;
            dropdown.template = template;
            dropdown.options.Clear(); dropdown.options.Add(new TMP_Dropdown.OptionData("-"));
            StyleTemplate(dropdown);
            template.gameObject.SetActive(false);
            return dropdown;
        }

        public static void Ensure(StartScreenView view)
        {
            if (view.language != null && view.resolution != null)
            {
                StyleTemplate(view.language); StyleTemplate(view.resolution); return;
            }
            var panel = (RectTransform)view.settingsPanel.transform;
            panel.sizeDelta = new Vector2(720, 640);
            SetPosition(panel.Find("Title"), 0, 250);
            SetPosition(panel.Find("VolumeLabel"), -230, 155);
            SetPosition(view.volume.transform, 70, 155);
            SetPosition(view.settingsBack.transform, 298, 250);
            ((RectTransform)view.settingsBack.transform).sizeDelta = new Vector2(48, 48);
            CreateLabel(panel, "ResolutionLabel", "ui.settings.resolution", 50);
            CreateLabel(panel, "LanguageLabel", "ui.settings.language", -55);
            if (view.resolution == null) view.resolution = Build(panel, "Resolution", 50);
            if (view.language == null) view.language = Build(panel, "Language", -55);
            // The English TMP font uses the Korean font as a fallback for native language names.
        }

        public static void StyleTemplate(TMP_Dropdown dropdown)
        {
            var template = dropdown.template;
            var scroll = template.GetComponent<ScrollRect>();
            var background = template.GetComponent<Image>();
            background.sprite = null; background.type = Image.Type.Simple;
            background.color = new Color32(60, 34, 43, 255);
            var row = dropdown.itemText.GetComponentInParent<Toggle>(true);
            row.transition = Selectable.Transition.None;
            if (row.GetComponent<DropdownRowStyle>() == null) row.gameObject.AddComponent<DropdownRowStyle>();
            dropdown.itemText.color = UITheme.LightInk;
            var separator = row.transform.Find("Separator") as RectTransform;
            if (separator == null)
            {
                separator = StartScreenLayout.Node(row.transform, "Separator", 0, 0, 0, 1);
                separator.anchorMin = Vector2.zero; separator.anchorMax = new Vector2(1, 0); separator.pivot = new Vector2(.5f, 0);
                var line = separator.gameObject.AddComponent<Image>(); line.color = new Color(1, 1, 1, .13f); line.raycastTarget = false;
            }
            if (scroll.verticalScrollbar == null)
            {
                var track = StartScreenLayout.Node(template, "Scrollbar", 0, 0, 8, 0);
                track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
                track.offsetMin = new Vector2(-12, 4); track.offsetMax = new Vector2(-4, -4);
                var image = track.gameObject.AddComponent<Image>(); image.color = new Color(1, 1, 1, .10f);
                var handle = StartScreenLayout.Node(track, "Handle", 0, 0, 0, 0);
                StartScreenLayout.Stretch(handle);
                var thumb = handle.gameObject.AddComponent<Image>(); thumb.color = new Color(1, .91f, .76f, .4f);
                var bar = track.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop;
                bar.handleRect = handle; bar.targetGraphic = thumb; bar.transition = Selectable.Transition.None;
                scroll.verticalScrollbar = bar;
            }
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.viewport.offsetMin = new Vector2(3, 3); scroll.viewport.offsetMax = new Vector2(-18, -3);
        }
        private static void SetPosition(Transform target, float x, float y)
        { if (target != null) ((RectTransform)target).anchoredPosition = new Vector2(x, y); }
        private static void CreateLabel(Transform parent, string name, string key, float y)
        {
            if (parent.Find(name) != null) return;
            var label = Label(parent, name, -230, 180);
            label.rectTransform.anchoredPosition = new Vector2(-230, y);
            label.color = UITheme.LightInk;
            LocalizedText.Attach(label, key);
        }
    }
}
