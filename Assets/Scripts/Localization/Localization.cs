using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FarmMVP
{
    [Serializable] public class LanguageInfo { public string code, nativeName; }
    [Serializable] public class LanguageManifest { public LanguageInfo[] languages; }
    [Serializable] public class TranslationEntry { public string key, value; }
    [Serializable] public class TranslationTable { public TranslationEntry[] entries; }

    /// <summary>Shared keys, independent language tables. Gameplay IDs never depend on a locale.</summary>
    public static class Localization
    {
        public const string PreferenceKey = "farm_language";
        public const string DefaultLanguage = "en";
        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();
        private static string _language;
        private static LanguageInfo[] _languages;
        public static event Action Changed;
        public static LanguageInfo[] Languages { get { EnsureLoaded(); return _languages; } }
        public static string Language { get { EnsureLoaded(); return _language; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Tables.Clear(); _languages = null; _language = null; Changed = null;
        }

        public static void EnsureLoaded()
        {
            if (_language != null) return;
            var manifest = Resources.Load<TextAsset>("Localization/languages");
            _languages = manifest != null ? JsonUtility.FromJson<LanguageManifest>(manifest.text).languages : null;
            if (_languages == null || _languages.Length == 0)
                _languages = new[] { new LanguageInfo { code = "en", nativeName = "English" } };
            _language = DefaultLanguage;
            var saved = PlayerPrefs.GetString(PreferenceKey, DefaultLanguage);
            if (Array.Exists(_languages, l => l.code == saved)) _language = saved;
        }

        public static bool SetLanguage(string code)
        {
            EnsureLoaded();
            if (!Array.Exists(_languages, l => l.code == code)) return false;
            if (_language == code) return true;
            _language = code;
            PlayerPrefs.SetString(PreferenceKey, code); PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static Dictionary<string, string> Table(string code)
        {
            if (Tables.TryGetValue(code, out var table)) return table;
            table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var asset in Resources.LoadAll<TextAsset>("Localization/" + code + "/Strings"))
            {
                var entries = JsonUtility.FromJson<TranslationTable>(asset.text)?.entries;
                if (entries == null) continue;
                foreach (var entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.key)) continue;
                    if (table.ContainsKey(entry.key)) Debug.LogError("Duplicate localization key: " + code + "/" + entry.key);
                    table[entry.key] = entry.value;
                }
            }
            Tables[code] = table;
            return table;
        }

        public static string Get(string key)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key)) return "";
            if (Table(_language).TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)) return value;
            if (Table(DefaultLanguage).TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return value;
            return "[" + key + "]";
        }

        public static string Format(string key, params object[] args)
            => string.Format(CultureInfo.GetCultureInfo(Language), Get(key), args);

        /// <summary>JSON dialogue/name fields use @key. Literal user-authored names remain untouched.</summary>
        public static string Resolve(string value) => value != null && value.StartsWith("@", StringComparison.Ordinal) ? Get(value.Substring(1)) : value;

        // Used only to migrate existing authored labels to explicit keys, never to translate user input.
        public static string FindKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            foreach (var locale in Languages)
                foreach (var entry in Table(locale.code))
                    if (entry.Value == value && !entry.Key.StartsWith("npc.") && !entry.Key.StartsWith("events.")) return entry.Key;
            return null;
        }
    }
}
