using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace FarmMVP
{
    public enum FontRole { Default, Bold, System1 }

    public static class LocalizedFonts
    {
        private static readonly Dictionary<string, TMP_FontAsset> TmpFonts = new Dictionary<string, TMP_FontAsset>();
        private static Material _countMaterial;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { TmpFonts.Clear(); _countMaterial = null; }
#if UNITY_EDITOR
        public static void RebuildPreviews()
        {
            TmpFonts.Clear();
            if (_countMaterial == null)
                _countMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Localization/systemfont1 Count.mat");
            foreach (var locale in Localization.Languages)
            foreach (FontRole role in System.Enum.GetValues(typeof(FontRole)))
            {
                if (role == FontRole.System1 && locale.code != Localization.DefaultLanguage) continue;
                var target = GetTMP(role, locale.code);
                var replacement = TMP_FontAsset.CreateFontAsset(Get(role, locale.code), 32, 4, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                var material = target.material;
                var oldTextures = (Texture2D[])target.atlasTextures.Clone();
                var newTextures = (Texture2D[])replacement.atlasTextures.Clone();
                var replacementMaterial = replacement.material;
                UnityEditor.EditorUtility.CopySerialized(replacementMaterial, material);
                UnityEditor.EditorUtility.CopySerialized(replacement, target);
                target.name = "Localized " + locale.code + "/" + role;
                target.material = material;
                target.atlasTextures = newTextures;
                target.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (locale.code != "ko") target.fallbackFontAssetTable.Add(GetTMP(FontRole.Default, "ko"));
                foreach (var texture in oldTextures) if (texture != null) Object.DestroyImmediate(texture, true);
                foreach (var texture in target.atlasTextures) UnityEditor.AssetDatabase.AddObjectToAsset(texture, target);
                material.mainTexture = target.atlasTextures[0];
                target.ReadFontAssetDefinition();
                UnityEditor.EditorUtility.SetDirty(target);
                UnityEditor.EditorUtility.SetDirty(material);
                Object.DestroyImmediate(replacementMaterial);
                Object.DestroyImmediate(replacement);
            }
            if (_countMaterial != null)
            {
                _countMaterial.mainTexture = GetTMP(FontRole.System1).atlasTextures[0];
                UnityEditor.EditorUtility.SetDirty(_countMaterial);
            }
            UnityEditor.AssetDatabase.SaveAssets();
            foreach (var text in Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None)) text.Refresh();
        }
#endif
        public static Font Get(FontRole role = FontRole.Default, string language = null)
        {
            string path = role == FontRole.System1 ? "Localization/systemfont1"
                : "Localization/" + (language ?? Localization.Language) + "/Fonts/" + (role == FontRole.Bold ? "bold" : "default");
            return Resources.Load<Font>(path) ?? Resources.Load<Font>("Localization/en/Fonts/default")
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static TMP_FontAsset GetTMP(FontRole role = FontRole.Default, string language = null)
        {
            language = role == FontRole.System1 ? "system" : language ?? Localization.Language;
            string key = language + "/" + role;
            if (TmpFonts.TryGetValue(key, out var cached) && cached != null) return cached;
#if UNITY_EDITOR
            string editorPath = role == FontRole.System1 ? "Assets/Resources/Localization/systemfont1 SDF.asset"
                : "Assets/Resources/Localization/" + language + "/Fonts/" + (role == FontRole.Bold ? "bold" : "default") + " SDF.asset";
            if (!Application.isPlaying)
            {
                cached = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(editorPath);
                if (cached != null) { TmpFonts[key] = cached; return cached; }
            }
#endif
            var asset = TMP_FontAsset.CreateFontAsset(Get(role, language), 32, 4, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = "Localized " + key;
            TmpFonts[key] = asset;
            // Korean player names and the native language selector remain readable in English UI.
            asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (language != "ko") asset.fallbackFontAssetTable.Add(GetTMP(FontRole.Default, "ko"));
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.AssetDatabase.CreateAsset(asset, editorPath);
                UnityEditor.AssetDatabase.AddObjectToAsset(asset.material, asset);
                foreach (var texture in asset.atlasTextures) UnityEditor.AssetDatabase.AddObjectToAsset(texture, asset);
                UnityEditor.EditorUtility.SetDirty(asset);
            }
#endif
            return asset;
        }

        public static Material CountMaterial
        {
            get
            {
                if (_countMaterial != null) return _countMaterial;
#if UNITY_EDITOR
                const string path = "Assets/Resources/Localization/systemfont1 Count.mat";
                if (!Application.isPlaying)
                {
                    _countMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (_countMaterial != null)
                    {
                        _countMaterial.mainTexture = GetTMP(FontRole.System1).atlasTextures[0];
                        UnityEditor.EditorUtility.SetDirty(_countMaterial);
                        return _countMaterial;
                    }
                }
#endif
                _countMaterial = new Material(GetTMP(FontRole.System1).material) { name = "System inventory count" };
                _countMaterial.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
                _countMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                _countMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, .18f);
                _countMaterial.EnableKeyword("OUTLINE_ON");
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.AssetDatabase.CreateAsset(_countMaterial, path);
#endif
                return _countMaterial;
            }
        }
    }
}
