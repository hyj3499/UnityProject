using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Per-object collision ownership keeps overlapping objects and terrain independent.
    /// Index once on placement; movement probes only the objects touching the player's cell.
    /// Positions are captured at placement, so visual hit/wobble effects do not move collision.
    /// </summary>
    public sealed class ObjectCollisionMap
    {
        private sealed class Shape
        {
            public Object owner;
            public ObjectCollisionMasks.Mask mask;
            public Vector2 origin;
            public readonly HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        }

        private readonly Dictionary<Object, Shape> shapes = new Dictionary<Object, Shape>();
        private readonly Dictionary<Vector2Int, List<Shape>> cells = new Dictionary<Vector2Int, List<Shape>>();
        public float MovementStep { get; private set; } = CliffPixelCollision.MovementStep;

        public void Clear()
        {
            shapes.Clear();
            cells.Clear();
            MovementStep = CliffPixelCollision.MovementStep;
        }

        public void Set(Object owner, Sprite sprite, Vector2 origin, RectInt fallback, bool blocks = true,
            Vector2Int? doorway = null)
        {
            Remove(owner);
            if (!blocks) return;
            var shape = new Shape { owner = owner, mask = ObjectCollisionMasks.Find(sprite), origin = origin };
            if (shape.mask == null)
            {
                foreach (var cell in fallback.allPositionsWithin) shape.cells.Add(cell);
            }
            else
            {
                var mask = shape.mask;
                MovementStep = Mathf.Min(MovementStep, 0.5f / mask.pixelsPerUnit);
                // Include every tile touched by each solid pixel, even across a cell boundary.
                for (int y = 0; y < mask.height; y++)
                    for (int x = 0; x < mask.width; x++)
                    {
                        if (!mask.Contains(x, y)) continue;
                        Vector2 min = origin + (new Vector2(x, y) - mask.pivot) / mask.pixelsPerUnit;
                        Vector2 max = min + Vector2.one / mask.pixelsPerUnit;
                        // Match the world's RoundToInt at exact half-cell boundaries (ties go to even).
                        for (int cy = Mathf.RoundToInt(min.y); cy <= Mathf.FloorToInt(max.y + 0.5f); cy++)
                            for (int cx = Mathf.RoundToInt(min.x); cx <= Mathf.FloorToInt(max.x + 0.5f); cx++)
                                shape.cells.Add(new Vector2Int(cx, cy));
                    }
            }
            if (doorway.HasValue) shape.cells.Remove(doorway.Value);
            shapes.Add(owner, shape);
            foreach (var cell in shape.cells)
            {
                if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = new List<Shape>();
                list.Add(shape);
            }
        }

        public void Remove(Object owner)
        {
            if (ReferenceEquals(owner, null) || !shapes.TryGetValue(owner, out var shape)) return;
            foreach (var cell in shape.cells)
            {
                var list = cells[cell];
                list.Remove(shape);
                if (list.Count == 0) cells.Remove(cell);
            }
            shapes.Remove(owner);
        }

        /// <summary>Explicit map exits/doors open this cell, as with the original block grid.</summary>
        public void OpenCell(Vector2Int cell)
        {
            if (!cells.TryGetValue(cell, out var list)) return;
            foreach (var shape in list) shape.cells.Remove(cell);
            cells.Remove(cell);
        }

        // Tile-based NPC routes and placement remain conservative around partially blocked cells.
        public bool HasAny(int x, int y) => cells.ContainsKey(new Vector2Int(x, y));

        public bool IsBlocked(float x, float y, Vector2 maskOffset)
        {
            // Legacy rectangular cells retain their existing anchor; painted masks use the visible feet.
            return IsBlockedAtPoint(x, y, false) || IsBlockedAtPoint(x + maskOffset.x, y + maskOffset.y, true);
        }

        public bool IsBlocked(float x, float y, Vector2[] maskOffsets)
        {
            // Rectangles retain the original anchor. For painted shapes, inspect the whole sole
            // at one Y coordinate so a single foot point cannot slip through a narrow painted part.
            if (IsBlockedAtPoint(x, y, false)) return true;
            if (maskOffsets == null) return false;
            foreach (var offset in maskOffsets)
                if (IsBlockedAtPoint(x + offset.x, y + offset.y, true)) return true;
            return false;
        }

        /// <summary>
        /// Recovery from an old overlap may retain already-touching sole samples, but it must
        /// never make a previously free sole sample touch a painted shape.
        /// </summary>
        public bool CanResolveMaskOverlap(Vector2 from, Vector2 to, Vector2[] maskOffsets)
        {
            if (maskOffsets == null) return false;
            bool wasOverlapping = false;
            foreach (var offset in maskOffsets)
            {
                bool before = IsBlockedAtPoint(from.x + offset.x, from.y + offset.y, true);
                bool after = IsBlockedAtPoint(to.x + offset.x, to.y + offset.y, true);
                if (before) wasOverlapping = true;
                else if (after) return false;
            }
            return wasOverlapping;
        }

        /// <summary>Highest renderer order among painted shapes currently touched by the sole.</summary>
        public int MaskSortingOrderAt(float x, float y, Vector2[] maskOffsets)
        {
            if (maskOffsets == null) return int.MinValue;
            int result = int.MinValue;
            foreach (var offset in maskOffsets)
            {
                var cell = new Vector2Int(Mathf.RoundToInt(x + offset.x), Mathf.RoundToInt(y + offset.y));
                if (!cells.TryGetValue(cell, out var nearby)) continue;
                foreach (var shape in nearby)
                {
                    if (shape.mask == null || !(shape.owner is SpriteRenderer renderer)) continue;
                    var mask = shape.mask;
                    Vector2 pixel = (new Vector2(x + offset.x, y + offset.y) - shape.origin) * mask.pixelsPerUnit + mask.pivot;
                    if (mask.Contains(Mathf.FloorToInt(pixel.x), Mathf.FloorToInt(pixel.y)))
                        result = Mathf.Max(result, renderer.sortingOrder);
                }
            }
            return result;
        }

        /// <summary>
        /// Highest static order of explicitly painted objects whose unpainted Y band covers the player.
        /// The player, never the object, changes its order to move behind this foreground band.
        /// </summary>
        public int ForegroundMaskSortingOrderAt(float worldX, float worldY)
        {
            int result = int.MinValue;
            foreach (var shape in shapes.Values)
            {
                if (shape.mask == null || !shape.mask.usesKeyedPaint || !(shape.owner is SpriteRenderer renderer)) continue;
                int x = Mathf.FloorToInt((worldX - shape.origin.x) * shape.mask.pixelsPerUnit + shape.mask.pivot.x);
                int y = Mathf.FloorToInt((worldY - shape.origin.y) * shape.mask.pixelsPerUnit + shape.mask.pivot.y);
                if (x >= 0 && x < shape.mask.width && y >= 0 && y < shape.mask.height &&
                    !shape.mask.HasPaintedRow(y)) result = Mathf.Max(result, renderer.sortingOrder);
            }
            return result;
        }

        private bool IsBlockedAtPoint(float x, float y, bool? masksOnly)
        {
            var cell = new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
            if (!cells.TryGetValue(cell, out var nearby)) return false;
            foreach (var shape in nearby)
            {
                if (masksOnly.HasValue && (shape.mask != null) != masksOnly.Value) continue;
                if (shape.mask == null) return true;
                var mask = shape.mask;
                Vector2 pixel = (new Vector2(x, y) - shape.origin) * mask.pixelsPerUnit + mask.pivot;
                if (mask.Contains(Mathf.FloorToInt(pixel.x), Mathf.FloorToInt(pixel.y))) return true;
            }
            return false;
        }
    }
}
