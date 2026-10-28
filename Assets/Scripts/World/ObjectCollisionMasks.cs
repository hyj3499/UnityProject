using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>Editor-baked masks for supported object sprite slices.</summary>
    public sealed class ObjectCollisionMasks : ScriptableObject
    {
        public const string ResourceName = "ObjectCollisionMasks";

        [Serializable]
        public sealed class Mask
        {
            public Sprite sprite;
            /// <summary>True only when this slice has explicit keyed paint in a _collision companion.</summary>
            public bool usesKeyedPaint;
            public int width, height;
            public Vector2 pivot;
            public float pixelsPerUnit;
            public byte[] bits;
            [NonSerialized] private bool[] paintedRows;

            public bool Contains(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return false;
                int i = y * width + x;
                return (bits[i >> 3] & (1 << (i & 7))) != 0;
            }

            /// <summary>A visual depth band is painted when any collision pixel occupies that row.</summary>
            public bool HasPaintedRow(int y)
            {
                if (y < 0 || y >= height) return false;
                if (paintedRows == null || paintedRows.Length != height)
                {
                    paintedRows = new bool[height];
                    for (int row = 0; row < height; row++)
                        for (int x = 0; x < width; x++)
                            if (Contains(x, row)) { paintedRows[row] = true; break; }
                }
                return paintedRows[y];
            }
        }

        public List<Mask> masks = new List<Mask>();
        private static Dictionary<Sprite, Mask> cache;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cache = null;

        public static Mask Find(Sprite sprite)
        {
            if (sprite == null) return null;
            if (cache == null)
            {
                cache = new Dictionary<Sprite, Mask>();
                var catalog = Resources.Load<ObjectCollisionMasks>(ResourceName);
                if (catalog != null)
                    foreach (var mask in catalog.masks)
                        if (mask.sprite != null) cache[mask.sprite] = mask;
            }
            return cache.TryGetValue(sprite, out var found) ? found : null;
        }
    }
}
