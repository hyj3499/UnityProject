using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>Shared shop/gameplay confirmation, authored beneath the scene Canvas.</summary>
    public sealed class ConfirmationDialogView : MonoBehaviour
    {
        public Image panel;
        public Text message;
        public Button yes, no;

        public static ConfirmationDialogView Build(Transform parent)
        {
            var root = StartScreenLayout.Node(parent, "ConfirmDialog", 0, 0, 960, 540);
            StartScreenLayout.Stretch(root);
            var view = root.gameObject.AddComponent<ConfirmationDialogView>();
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = UILayers.Book + 1;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var shield = root.gameObject.AddComponent<Image>();
            shield.color = new Color(0, 0, 0, .45f);
            // Reference-sized stage keeps the same proportions on the authored
            // 1920x1080 Canvas and the 960x540 runtime fallback Canvas.
            var stage = StartScreenLayout.Node(root, "Design", 0, 0, 1920, 1080);
            stage.gameObject.AddComponent<StartScreenFit>();
            var card = StartScreenLayout.Node(stage, "Panel", 0, 0, 800, 400);
            view.panel = card.gameObject.AddComponent<Image>();
            UITheme.ApplyPanel(view.panel, 4);
            view.message = Label(card, "Message", Localization.Get("ui.confirmationdialogview.make_this_purchase"), 0, 60, 680, 150, 32);
            view.message.color = UITheme.LightInk;
            view.yes = MakeButton(card, "Yes", Localization.Get("ui.confirmationdialogview.yes"), -160);
            view.no = MakeButton(card, "No", Localization.Get("ui.confirmationdialogview.no"), 160);
            root.gameObject.SetActive(false);
            return view;
        }

        private static Button MakeButton(Transform parent, string name, string text, float x)
        {
            var rect = StartScreenLayout.Node(parent, name, x, -105, 240, 64);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label(rect, "Label", text, 0, 0, 224, 52, 26);
            UITheme.ApplyButton(button, false, 4);
            return button;
        }

        private static Text Label(Transform parent, string name, string text, float x, float y, float w, float h, int size)
        {
            var label = StartScreenLayout.Node(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = LocalizedFonts.Get();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HidePreview()
        {
            foreach (var view in FindObjectsByType<ConfirmationDialogView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                view.gameObject.SetActive(false);
        }
    }
}
