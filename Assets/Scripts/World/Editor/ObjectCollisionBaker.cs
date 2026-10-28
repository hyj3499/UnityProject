using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>Companion PNGs and fully opaque object silhouettes are baked before play; runtime only reads bits.</summary>
    [InitializeOnLoad]
    public sealed class ObjectCollisionBaker : AssetPostprocessor, IPreprocessBuildWithReport
    {
        public const string Root = "Assets/Resources/Sprites";
        public const string OutputPath = "Assets/Resources/ObjectCollisionMasks.asset";
        private const string Suffix = "_collision.png";
        private static bool queued;
        public int callbackOrder => 0;

        static ObjectCollisionBaker()
        {
            QueueRebuild();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Rebuild();
            };
        }

        private static void QueueRebuild()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += () => { queued = false; Rebuild(); };
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(path =>
                path.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))) QueueRebuild();
        }

        public void OnPreprocessBuild(BuildReport report) => Rebuild();

        [MenuItem("Tools/Farm MVP/Rebuild Object Collision Masks")]
        public static void Rebuild()
        {
            if (!Directory.Exists(Root)) return;
            var masks = new List<ObjectCollisionMasks.Mask>();
            foreach (var file in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).Where(IsObjectSource).OrderBy(p => p, StringComparer.Ordinal))
            {
                string collisionPath = file.Substring(0, file.Length - ".png".Length) + Suffix;
                var original = Read(file);
                Texture2D painted = null;
                try
                {
                    Color32[] paintedPixels = null;
                    if (File.Exists(collisionPath))
                    {
                        painted = Read(collisionPath);
                        if (original.width != painted.width || original.height != painted.height)
                            throw new InvalidDataException($"[ObjectCollision] {collisionPath}: original and collision PNG dimensions must match.");
                        paintedPixels = painted.GetPixels32();
                    }
                    var originalPixels = original.GetPixels32();
                    foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(file).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal))
                    {
                        var rect = sprite.rect;
                        var spriteRect = new RectInt(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y),
                            Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
                        // A companion's keyed paint takes precedence. Slices left unpainted in
                        // that companion take priority even outside the visible source art.
                        // Every source without a companion uses the fully opaque silhouette;
                        // semi-transparent source pixels are shadows.
                        var bits = paintedPixels != null ? Bake(paintedPixels, original.width, original.height, spriteRect) : null;
                        bool usesKeyedPaint = bits != null;
                        if (bits == null) bits = BakeOpaque(originalPixels, original.width, original.height, spriteRect);
                        if (bits == null) continue;
                        masks.Add(new ObjectCollisionMasks.Mask { sprite = sprite, usesKeyedPaint = usesKeyedPaint,
                            width = (int)rect.width,
                            height = (int)rect.height, pivot = sprite.pivot, pixelsPerUnit = sprite.pixelsPerUnit, bits = bits });
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(original);
                    if (painted != null) UnityEngine.Object.DestroyImmediate(painted);
                }
            }
            var catalog = AssetDatabase.LoadAssetAtPath<ObjectCollisionMasks>(OutputPath);
            if (catalog != null && Same(catalog.masks, masks)) return;
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ObjectCollisionMasks>();
                AssetDatabase.CreateAsset(catalog, OutputPath);
            }
            catalog.masks = masks;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ObjectCollision] Baked {masks.Count} keyed or opaque sprite masks ({masks.Sum(m => m.bits.Length)} mask bytes).");
        }

        private static bool IsObjectSource(string path)
        {
            if (!path.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf("_collision", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            string relative = path.Substring(Root.Length + 1);
            return relative.StartsWith("Environment/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Fence/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("House/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Interior/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("OutdoorObjects/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Plants/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Trees/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Tiles/Animation/Objects/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Tiles/Animation/Rocks/", StringComparison.OrdinalIgnoreCase);
        }

        private static Texture2D Read(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false)) return texture;
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidDataException("Cannot read PNG: " + path);
        }

        /// <summary>Explicit keyed paint always blocks, including where the original art is transparent. Exclude the legend pixel.</summary>
        public static byte[] Bake(Color32[] painted, int width, int height, RectInt rect)
        {
            if (width <= 0 || height <= 0 || painted.Length != width * height ||
                rect.width <= 0 || rect.height <= 0 || rect.xMin < 0 || rect.yMin < 0 || rect.xMax > width || rect.yMax > height)
                throw new ArgumentException("Invalid PNG dimensions or sprite rectangle.");
            int legend = (height - 1) * width; // Unity pixels start at bottom-left.
            Color32 key = painted[legend];
            if (key.a == 0) throw new InvalidDataException("[ObjectCollision] Top-left palette pixel must have a visible color.");
            var bits = new byte[(rect.width * rect.height + 7) / 8];
            bool any = false;
            for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                {
                    int source = (rect.y + y) * width + rect.x + x;
                    Color32 color = painted[source];
                    if (source == legend || color.a == 0 ||
                        color.r != key.r || color.g != key.g || color.b != key.b) continue;
                    int i = y * rect.width + x;
                    bits[i >> 3] |= (byte)(1 << (i & 7));
                    any = true;
                }
            return any ? bits : null;
        }

        /// <summary>Fallback for object art without keyed collision paint: only fully opaque source pixels block.</summary>
        public static byte[] BakeOpaque(Color32[] sourcePixels, int width, int height, RectInt rect)
        {
            if (width <= 0 || height <= 0 || sourcePixels.Length != width * height ||
                rect.width <= 0 || rect.height <= 0 || rect.xMin < 0 || rect.yMin < 0 || rect.xMax > width || rect.yMax > height)
                throw new ArgumentException("Invalid PNG dimensions or sprite rectangle.");
            var bits = new byte[(rect.width * rect.height + 7) / 8];
            bool any = false;
            for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                {
                    if (sourcePixels[(rect.y + y) * width + rect.x + x].a != byte.MaxValue) continue;
                    int i = y * rect.width + x;
                    bits[i >> 3] |= (byte)(1 << (i & 7));
                    any = true;
                }
            return any ? bits : null;
        }

        private static bool Same(List<ObjectCollisionMasks.Mask> a, List<ObjectCollisionMasks.Mask> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].sprite != b[i].sprite || a[i].usesKeyedPaint != b[i].usesKeyedPaint ||
                    a[i].width != b[i].width || a[i].height != b[i].height ||
                    a[i].pivot != b[i].pivot || a[i].pixelsPerUnit != b[i].pixelsPerUnit || !a[i].bits.SequenceEqual(b[i].bits)) return false;
            return true;
        }
    }
}
