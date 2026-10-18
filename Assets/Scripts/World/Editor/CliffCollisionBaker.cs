using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>Read original PNG colors before import compression. Runtime texture Read/Write stays off.</summary>
    [InitializeOnLoad]
    public sealed class CliffCollisionBaker : AssetPostprocessor, IPreprocessBuildWithReport
    {
        public const string SourcePath = "Assets/Resources/Sprites/Tiles/Tileset_cliff_spring.png";
        public const string OutputPath = "Assets/Resources/CliffCollisionMask.bytes";
        public int callbackOrder => 0;

        static CliffCollisionBaker()
        {
            EditorApplication.delayCall += Rebuild;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Rebuild();
            };
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            if (Array.IndexOf(imported, SourcePath) >= 0)
                EditorApplication.delayCall += Rebuild;
        }

        public void OnPreprocessBuild(BuildReport report) => Rebuild();

        [MenuItem("Tools/Farm MVP/Rebuild Cliff Collision Mask")]
        public static void Rebuild()
        {
            if (!File.Exists(SourcePath)) return;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(SourcePath), false))
                    throw new InvalidDataException("Cannot read cliff palette PNG.");
                var data = Bake(texture.GetPixels32(), texture.width, texture.height);
                if (File.Exists(OutputPath) && Equal(File.ReadAllBytes(OutputPath), data)) return;
                File.WriteAllBytes(OutputPath, data);
                AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[CliffCollisionBaker] Baked {texture.width}x{texture.height}: {data.Length} bytes.");
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        public static byte[] Bake(Color32[] pixels, int width, int height)
        {
            if (width < 16 || height < 16 || pixels.Length != width * height)
                throw new ArgumentException("Cliff sheet must include a 16x16 palette cell.");
            var palette = new HashSet<int>();
            // (0,0) means the image's TOP LEFT cell, not Unity texture's bottom-left origin.
            for (int y = height - 16; y < height; y++)
                for (int x = 0; x < 16; x++)
                    if (pixels[y * width + x].a != 0) palette.Add(Rgb(pixels[y * width + x]));
            if (palette.Count == 0) throw new InvalidDataException("Cliff palette cell is empty.");

            var data = new byte[8 + (width * height + 7) / 8];
            Array.Copy(BitConverter.GetBytes(width), 0, data, 0, 4);
            Array.Copy(BitConverter.GetBytes(height), 0, data, 4, 4);
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].a != 0 && palette.Contains(Rgb(pixels[i])))
                    data[8 + (i >> 3)] |= (byte)(1 << (i & 7));
            return data;
        }

        private static int Rgb(Color32 color) => color.r | (color.g << 8) | (color.b << 16);
        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
