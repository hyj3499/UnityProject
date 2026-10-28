using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    public static class LocalizationBindings
    {
        /// <summary>One-time authoring migration. Controller-owned values receive only a font binding.</summary>
        public static void Apply(GameObject root)
        {
            var dynamicTexts = new HashSet<Component>();
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component is LocalizedText || component.GetType().Namespace != "FarmMVP") continue;
                foreach (var field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (!field.Name.EndsWith("Header") && (typeof(Text).IsAssignableFrom(field.FieldType) || typeof(TMP_Text).IsAssignableFrom(field.FieldType)) && field.GetValue(component) is Component text)
                        dynamicTexts.Add(text);
            }
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                Bind(text, text.text, text.fontStyle == FontStyle.Bold, dynamicTexts);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                Bind(text, text.text, (text.fontStyle & FontStyles.Bold) != 0, dynamicTexts);
            foreach (var view in root.GetComponentsInChildren<StartScreenView>(true))
            {
                UseNameFont(view.nameInput.textComponent);
                UseNameFont(view.saveName);
                foreach (var part in view.parts ?? new StartScreenPart[0])
                {
                    if (part.label != null && part.label.StartsWith("@")) continue;
                    var key = Localization.FindKey(part.label); if (key != null) part.label = "@" + key;
                }
                // The confirmation controller switches this between Yes and OK.
                var yes = view.modalYes.GetComponentInChildren<LocalizedText>(true);
                if (yes != null) yes.key = "";
            }
        }

        private static void UseNameFont(Text text)
        {
            if (text == null) return;
            var binding = text.GetComponent<LocalizedText>();
            if (binding == null) binding = LocalizedText.Attach(text);
            binding.key = ""; binding.fontLanguage = "ko"; binding.Refresh();
        }

        private static void Bind(Component text, string value, bool bold, HashSet<Component> dynamicTexts)
        {
            var existing = text.GetComponent<LocalizedText>();
            if (text.name == "Count") { LocalizedText.InventoryCount(text); return; }
            if (text.GetComponentInParent<DateTimeHUD>(true) != null)
            { LocalizedText.Attach(text, null, FontRole.System1); return; }
            if (existing != null && existing.fontRole == FontRole.System1) return;
            if (existing != null && !string.IsNullOrEmpty(existing.key)) return;
            bool input = text.GetComponentInParent<InputField>(true) != null || text.GetComponentInParent<TMP_InputField>(true) != null;
            string key = dynamicTexts.Contains(text) || input ? null : Localization.FindKey(value);
            if (value == "NEW GAME") key = "ui.menu.new_game";
            if (value == "CONTINUE") key = "ui.menu.continue";
            if (value == "SETTING" || value == "SETTINGS") key = "ui.menu.settings";
            if (value == "EXIT") key = "ui.menu.exit";
            if (value == "name" && text.name == "NameLabel") key = "ui.character.name";
            var role = bold || text.name.Contains("Title") || text.name.Contains("Heading") || text.name.Contains("Header") ? FontRole.Bold : FontRole.Default;
            if (existing != null && !string.IsNullOrEmpty(existing.fontLanguage)) return;
            LocalizedText.Attach(text, key, role);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeScene()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) Apply(root);
        }
    }
}
