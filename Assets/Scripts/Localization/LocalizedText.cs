using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>Leave Key empty for text managed by a controller (numbers, player names, formatted text).</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        public string key;
        public FontRole fontRole;
        public bool inventoryCount;
        [Tooltip("Optional fixed font language, e.g. ko for the 한국어 option.")]
        public string fontLanguage;
        private Text _legacy;
        private TMP_Text _tmp;
        private void OnEnable() { Localization.Changed += Refresh; Refresh(); }
        private void OnDisable() { Localization.Changed -= Refresh; }
        public void Refresh()
        {
            if (_legacy == null) _legacy = GetComponent<Text>();
            if (_tmp == null) _tmp = GetComponent<TMP_Text>();
            string locale = string.IsNullOrEmpty(fontLanguage) ? null : fontLanguage;
            if (_legacy != null)
            {
                _legacy.font = LocalizedFonts.Get(fontRole, locale);
                _legacy.fontStyle = FontStyle.Normal;
                if (!string.IsNullOrEmpty(key)) _legacy.text = Localization.Get(key);
                if (inventoryCount)
                {
                    _legacy.color = Color.white;
                    var outline = GetComponent<Outline>();
                    if (outline == null) outline = gameObject.AddComponent<Outline>();
                    foreach (var effect in GetComponents<Shadow>()) effect.enabled = effect == outline;
                    outline.effectColor = Color.black; outline.effectDistance = new Vector2(.5f, -.5f);
                    outline.useGraphicAlpha = true; outline.enabled = true;
                }
            }
            if (_tmp != null)
            {
                _tmp.font = LocalizedFonts.GetTMP(fontRole, locale);
                _tmp.fontStyle &= ~FontStyles.Bold;
                if (!string.IsNullOrEmpty(key)) _tmp.text = Localization.Get(key);
                if (inventoryCount)
                {
                    _tmp.color = Color.white;
                    _tmp.fontSharedMaterial = LocalizedFonts.CountMaterial;
                    _tmp.fontSize = 10f;
                    _tmp.extraPadding = true;
                    _tmp.enableAutoSizing = false;
                    _tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    _tmp.overflowMode = TextOverflowModes.Overflow;
                    foreach (var effect in GetComponents<Shadow>()) effect.enabled = false;
                }
            }
        }

        public static LocalizedText Attach(Component text, string translationKey = null, FontRole role = FontRole.Default)
        {
            var binding = text.GetComponent<LocalizedText>();
            if (binding == null) binding = text.gameObject.AddComponent<LocalizedText>();
            binding.key = translationKey ?? ""; binding.fontRole = role; binding.Refresh();
            return binding;
        }

        public static void InventoryCount(Component text)
        {
            if (text == null) return;
            var binding = text.GetComponent<LocalizedText>();
            if (binding == null) binding = text.gameObject.AddComponent<LocalizedText>();
            binding.key = ""; binding.fontLanguage = "";
            binding.fontRole = FontRole.System1; binding.inventoryCount = true; binding.Refresh();
        }
    }
}
