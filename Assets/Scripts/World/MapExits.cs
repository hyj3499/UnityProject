using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>Reads painted exits only. An empty layer never creates a connection.</summary>
    public static class MapExits
    {
        public static bool TryParseTarget(string name, out LocationId target)
        {
            target = default;
            name = ObjectMarkerDatabase.StripPrefix(name);
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Exit", StringComparison.OrdinalIgnoreCase))
                return false;
            string key = name.Substring(4).TrimStart('_');
            return Enum.TryParse(key, true, out target) && Enum.IsDefined(typeof(LocationId), target)
                && string.Equals(key, target.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        // Exit_Valley under Farm's Grid, or Exit_Valley_Farm in a shared Grid.
        public static bool TryGetLayer(Tilemap layer, out LocationId owner, out LocationId target)
        {
            owner = target = default;
            string name = layer.name;
            if (!name.StartsWith("Exit_", StringComparison.OrdinalIgnoreCase)) return false;
            foreach (LocationId id in Enum.GetValues(typeof(LocationId)))
            {
                string suffix = "_" + id;
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    && TryParseTarget(name.Substring(0, name.Length - suffix.Length), out target))
                { owner = id; return owner != target; }
            }
            if (!TryParseTarget(name, out target)) return false;
            for (var parent = layer.transform.parent; parent != null; parent = parent.parent)
                foreach (LocationId id in Enum.GetValues(typeof(LocationId)))
                    if (string.Equals(parent.name, id.ToString(), StringComparison.OrdinalIgnoreCase)
                        || parent.name.StartsWith(id + " (", StringComparison.OrdinalIgnoreCase))
                    { owner = id; return owner != target; }

            var grid = layer.GetComponentInParent<Grid>();
            if (grid == null) return false;
            LocationId? found = null;
            foreach (var sibling in grid.GetComponentsInChildren<Tilemap>(true))
                foreach (LocationId id in Enum.GetValues(typeof(LocationId)))
                    if (string.Equals(sibling.name, "Location_" + id, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(sibling.name, id + "Ground", StringComparison.OrdinalIgnoreCase))
                    {
                        if (found.HasValue && found.Value != id) return false;
                        found = id;
                    }
            if (!found.HasValue) return false;
            owner = found.Value;
            return owner != target;
        }

        public static void Apply(GameLocation location)
        {
            foreach (var exit in WorldGrid.PaintedExits(location.id))
                if (MapGraph.AreConnected(location.id, exit.Value))
                    location.AddExit(exit.Key.x, exit.Key.y, exit.Value);
        }
    }
}
