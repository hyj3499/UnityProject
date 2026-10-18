using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// Editor-baked, one-bit-per-pixel cliff collision. No texture reads or physics objects at runtime.
    /// Player movement probes the leading edge of the Base silhouette against this mask.
    /// </summary>
    public sealed class CliffPixelCollision
    {
        public const string SheetResource = "Sprites/Tiles/Tileset_cliff_spring";
        public const string MaskResource = "CliffCollisionMask";
        public const float MovementStep = 1f / 32f; // Half a pixel at the project's 16 PPU.

        private static byte[] bits;
        private static int sheetWidth, sheetHeight;
        private static HashSet<Sprite> sheetSprites;

        private sealed class Cell
        {
            public bool fallback;
            public bool anyBlocked;
            public RectInt rect;
            public Vector2 pivot;
            public float ppu;
            public Matrix4x4 worldToSprite;
        }

        private sealed class Layer
        {
            public Tilemap map;
            public readonly Dictionary<Vector3Int, Cell> cells = new Dictionary<Vector3Int, Cell>();
        }

        private readonly List<Layer> layers = new List<Layer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            bits = null;
            sheetSprites = null;
        }

        private static void Load()
        {
            if (sheetSprites != null) return;
            sheetSprites = new HashSet<Sprite>(Resources.LoadAll<Sprite>(SheetResource));
            var asset = Resources.Load<TextAsset>(MaskResource);
            if (asset != null)
            {
                var data = asset.bytes;
                if (data.Length >= 8)
                {
                    sheetWidth = BitConverter.ToInt32(data, 0);
                    sheetHeight = BitConverter.ToInt32(data, 4);
                    if (sheetWidth > 0 && sheetHeight > 0 &&
                        data.Length == 8L + ((long)sheetWidth * sheetHeight + 7) / 8)
                        bits = data;
                }
            }
            if (bits == null)
                Debug.LogError("[CliffPixelCollision] Missing/invalid baked mask. Run Tools/Farm MVP/Rebuild Cliff Collision Mask. Cliff cells remain blocked.");
        }

        public CliffPixelCollision(IEnumerable<Tilemap> maps)
        {
            Load();
            foreach (var map in maps)
            {
                var layer = new Layer { map = map };
                foreach (var position in map.cellBounds.allPositionsWithin)
                {
                    if (!map.HasTile(position)) continue;
                    var sprite = map.GetSprite(position);
                    var cell = new Cell { fallback = true, anyBlocked = true };
                    if (bits != null && sprite != null && sheetSprites.Contains(sprite))
                    {
                        var r = sprite.rect;
                        cell.rect = new RectInt(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y),
                            Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height));
                        cell.pivot = sprite.pivot;
                        cell.ppu = sprite.pixelsPerUnit;
                        var origin = map.CellToLocalInterpolated((Vector3)position + map.tileAnchor);
                        cell.worldToSprite = (map.transform.localToWorldMatrix * Matrix4x4.Translate(origin)
                            * map.orientationMatrix * map.GetTransformMatrix(position)).inverse;
                        cell.fallback = false;
                        cell.anyBlocked = false;
                        for (int y = cell.rect.yMin; y < cell.rect.yMax && !cell.anyBlocked; y++)
                            for (int x = cell.rect.xMin; x < cell.rect.xMax; x++)
                                if (PixelBlocked(x, y)) { cell.anyBlocked = true; break; }
                    }
                    layer.cells.Add(position, cell);
                }
                layers.Add(layer);
            }
        }

        private static bool PixelBlocked(int x, int y)
        {
            if (x < 0 || y < 0 || x >= sheetWidth || y >= sheetHeight) return false;
            int index = y * sheetWidth + x;
            return (bits[8 + (index >> 3)] & (1 << (index & 7))) != 0;
        }

        /// <summary>Conservative tile query for NPC routes and spawn positions.</summary>
        public bool HasBlockedPixels(Vector3 world)
        {
            foreach (var layer in layers)
                if (layer.cells.TryGetValue(layer.map.WorldToCell(world), out var cell) && cell.anyBlocked)
                    return true;
            return false;
        }

        public bool IsBlocked(Vector3 world)
        {
            foreach (var layer in layers)
            {
                if (!layer.cells.TryGetValue(layer.map.WorldToCell(world), out var cell)) continue;
                if (cell.fallback) return true; // Other tilesets retain their existing collision.
                if (!cell.anyBlocked) continue;
                var local = cell.worldToSprite.MultiplyPoint3x4(world);
                int x = Mathf.FloorToInt(local.x * cell.ppu + cell.pivot.x);
                int y = Mathf.FloorToInt(local.y * cell.ppu + cell.pivot.y);
                if (x >= 0 && y >= 0 && x < cell.rect.width && y < cell.rect.height &&
                    PixelBlocked(cell.rect.x + x, cell.rect.y + y)) return true;
            }
            return false;
        }
    }
}
