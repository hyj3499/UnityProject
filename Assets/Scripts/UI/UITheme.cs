using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>Authoring defaults only. Serialized scene images remain editable during play.</summary>
    public static class UITheme
    {
        public static readonly Color Ink = new Color32(66, 39, 37, 255);
        public static readonly Color LightInk = new Color32(249, 233, 196, 255);

        public static void ApplyButton(Button button, bool dark = false, float pixelScale = 1)
        {
            var sprites = Resources.LoadAll<Sprite>("Sprites/UI/Button/Btn_" + (dark ? "Dark" : "White"))
                .OrderBy(s => s.rect.x).ToArray();
            if (sprites.Length != 2) throw new InvalidOperationException("Button art must contain normal and pressed sprites.");
            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            button.targetGraphic = image;
            image.sprite = sprites[0];
            image.overrideSprite = null;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1 / pixelScale;
            image.color = Color.white;
            // Both slices retain the same 33x16 canvas, including the pressed
            // sprite's transparent top row. Never resize or move the RectTransform:
            // the bottom border and click area remain identical in both states.
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState {
                highlightedSprite = sprites[0], selectedSprite = sprites[0],
                pressedSprite = sprites[1], disabledSprite = sprites[0]
            };
            var label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label != null) { label.enabled = true; label.color = dark ? LightInk : Ink; }
        }

        public static void ApplyClose(Button button)
        {
            var sprites = Resources.LoadAll<Sprite>("Sprites/UI/Button/Close_btn").OrderBy(s => s.rect.x).ToArray();
            ApplyIcon(button, sprites[0]);
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState {
                highlightedSprite = sprites[0], selectedSprite = sprites[0],
                pressedSprite = sprites[1], disabledSprite = sprites[0]
            };
        }

        public static void ApplyArrow(Button button, bool right)
        {
            var sprites = Resources.LoadAll<Sprite>("Sprites/UI/Button/Arrow_btn").OrderBy(s => s.rect.x).ToArray();
            ApplyIcon(button, sprites[right ? 2 : 1]);
            // This sheet contains four directions, not pressed-state frames.
            button.spriteState = default;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ColorBlock.defaultColorBlock;
        }

        private static void ApplyIcon(Button button, Sprite sprite)
        {
            var image = button.targetGraphic as Image;
            image.sprite = sprite;
            image.overrideSprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            var label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label != null) label.enabled = false;
        }

        public static void ApplyShopScrollbar(Scrollbar bar)
        {
            var track = bar.GetComponent<Image>();
            var handle = (Image)bar.targetGraphic;
            track.sprite = Resources.LoadAll<Sprite>("Sprites/UI/Button/Scrolbar_scroll_vertical").First();
            handle.sprite = Resources.LoadAll<Sprite>("Sprites/UI/Button/Scrolbar_grab_vertical_normal").First();
            var hover = Resources.LoadAll<Sprite>("Sprites/UI/Button/Scrolbar_grab_vertical_hover").First();
            foreach (var image in new[] {track, handle})
            {
                image.color = Color.white;
                image.type = Image.Type.Sliced;
                image.preserveAspect = false;
                image.pixelsPerUnitMultiplier = 1;
            }
            bar.transition = Selectable.Transition.SpriteSwap;
            bar.spriteState = new SpriteState {
                highlightedSprite = hover, selectedSprite = hover, pressedSprite = hover,
                disabledSprite = handle.sprite
            };
        }

        public static void ApplyPanel(Image image, float pixelScale = 1)
        {
            image.sprite = Resources.LoadAll<Sprite>("Sprites/UI/Pannel_White").First();
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1 / pixelScale;
            image.color = Color.white;
        }

        public static void ApplyGlyphButton(Button button, string glyph, bool dark = false, float pixelScale = 1)
        {
            var label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label == null)
            {
                var rect = StartScreenLayout.Node(button.transform, "Label", 0, 0, 0, 0);
                StartScreenLayout.Stretch(rect, 2);
                label = rect.gameObject.AddComponent<Text>();
                label.font = Resources.Load<Font>("Fonts/PF스타더스트 3.0") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var bounds = ((RectTransform)button.transform).rect;
                label.fontSize = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(bounds.height * .5f, (bounds.width - 4) / glyph.Length)));
                label.alignment = TextAnchor.MiddleCenter;
                label.raycastTarget = false;
                label.text = glyph;
            }
            ApplyButton(button, dark, pixelScale);
        }
    }
}
