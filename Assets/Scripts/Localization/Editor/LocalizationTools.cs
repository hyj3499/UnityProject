using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmMVP
{
    public static class LocalizationTools
    {
        [MenuItem("Tools/Localization/Rebuild Font Previews")]
        public static void RebuildFonts()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) LocalizedFonts.RebuildPreviews();
        }
        [MenuItem("Tools/Localization/Bind Text In Active Scene")]
        public static void BindScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                Undo.RegisterFullObjectHierarchyUndo(root, "Bind localized text");
                foreach (var view in root.GetComponentsInChildren<StartScreenView>(true)) SettingsDropdown.Ensure(view);
                LocalizationBindings.Apply(root);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Localization/Validate Language Packs")]
        public static void Validate()
        {
            var english = Localization.Table("en");
            foreach (var locale in Localization.Languages)
            {
                var table = Localization.Table(locale.code);
                foreach (var key in english.Keys)
                    if (!table.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
                        Debug.LogWarning(locale.code + " missing translation: " + key);
                foreach (FontRole role in System.Enum.GetValues(typeof(FontRole)))
                {
                    if (role == FontRole.System1) continue;
                    if (Resources.Load<Font>("Localization/" + locale.code + "/Fonts/" + (role == FontRole.Bold ? "bold" : "default")) == null)
                        Debug.LogError(locale.code + " missing font: " + role);
                }
            }
            if (Resources.Load<Font>("Localization/systemfont1") == null) Debug.LogError("Missing systemfont1.ttf");
            Debug.Log("Checked " + english.Count + " English keys and " + Localization.Languages.Length + " language packs.");
        }
    }
}
