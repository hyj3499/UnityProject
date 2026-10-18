using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    internal static class TransparentTileCleaner
    {
        internal static readonly string[] Layers =
        {
            "Location", "Water", "Decor", "Cliff", "deepforest", "Fixed", "Breakable",
            "Objects", "Blocked", "Tillable", "Grass", "Path"
        };

        internal static Tilemap[] LoadedMaps()
        {
            var maps = new List<Tilemap>();
            // Only ordinary loaded scenes: exclude palette previews, prefab stage and project assets.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
                foreach (var root in scene.GetRootGameObjects())
                    maps.AddRange(root.GetComponentsInChildren<Tilemap>(true));
            }
            return maps.Where(m => !EditorUtility.IsPersistent(m))
                .OrderBy(m => m.gameObject.scene.name).ThenBy(m => m.name).ToArray();
        }

        internal static bool MatchesName(string name, string location, string layer)
        {
            foreach (string season in new[] { "Spring_", "Summer_", "Fall_", "Winter_" })
                if (name.StartsWith(season, StringComparison.Ordinal))
                { name = name.Substring(season.Length); break; }

            foreach (string candidate in Enum.GetNames(typeof(LocationId)))
            {
                if (location != null && candidate != location) continue;
                foreach (string kind in Layers)
                {
                    if (layer != null && kind != layer) continue;
                    string legacy = kind == "Location" ? "Ground" : kind == "deepforest" ? "DeepForest" : kind;
                    if (name == kind + "_" + candidate || name == candidate + legacy) return true;
                    string prefix = kind + "_", suffix = "_" + candidate;
                    if (name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        int length = name.Length - prefix.Length - suffix.Length;
                        if (length > 0 && int.TryParse(name.Substring(prefix.Length, length), out int number) && number > 0)
                            return true;
                    }
                }
            }
            return false;
        }

        internal sealed class MapResult
        {
            internal Tilemap Map;
            internal readonly List<Vector3Int> Cells = new List<Vector3Int>();
        }

        internal sealed class ScanResult
        {
            internal readonly List<MapResult> Maps = new List<MapResult>();
            internal readonly List<string> Warnings = new List<string>();
            internal int Inspected;
            internal int PreservedUnknown;
            internal int Count => Maps.Sum(m => m.Cells.Count);
        }

        internal static ScanResult Scan(IEnumerable<Tilemap> maps, BoundsInt? area, TileBase onlyTile,
            bool includeMissingSprites, Func<string, float, bool> cancel = null)
        {
            var result = new ScanResult();
            var targets = maps.Where(m => m != null).Distinct().ToArray();
            using (var detector = new AlphaDetector(includeMissingSprites, result.Warnings))
            {
                for (int index = 0; index < targets.Length; index++)
                {
                    var map = targets[index];
                    var entry = new MapResult { Map = map };
                    var bounds = map.cellBounds;
                    if (area.HasValue)
                    {
                        var a = area.Value;
                        var min = Vector3Int.Max(bounds.min, a.min);
                        var max = Vector3Int.Min(bounds.max, a.max);
                        if (max.x <= min.x || max.y <= min.y || max.z <= min.z) continue;
                        bounds = new BoundsInt(min, max - min);
                    }
                    long volume = (long)bounds.size.x * bounds.size.y * bounds.size.z;
                    long visited = 0;
                    foreach (var cell in bounds.allPositionsWithin)
                    {
                        if ((visited++ % 1024) == 0 && cancel != null &&
                            cancel(map.name, (index + (float)((double)visited / Math.Max(1L, volume))) / targets.Length))
                            throw new OperationCanceledException();
                        var tile = map.GetTile(cell);
                        if (tile == null || (onlyTile != null && tile != onlyTile)) continue;
                        result.Inspected++;
                        bool? transparent = detector.IsTransparent(tile);
                        if (!transparent.HasValue) result.PreservedUnknown++;
                        else if (transparent.Value) entry.Cells.Add(cell);
                    }
                    result.Maps.Add(entry);
                }
            }
            return result;
        }

        // The caller scans again immediately before Apply; cached previews are never used to delete.
        internal static int Apply(ScanResult result)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이 모드에서는 지울 수 없습니다.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("완전 투명 타일 지우기");
            int removed = 0;
            try
            {
                foreach (var entry in result.Maps)
                {
                    var map = entry.Map;
                    if (map == null || entry.Cells.Count == 0) continue;
                    Undo.RegisterCompleteObjectUndo(map, "완전 투명 타일 지우기");
                    map.SetTiles(entry.Cells.ToArray(), new TileBase[entry.Cells.Count]);
                    map.CompressBounds();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(map);
                    EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                    var collider = map.GetComponent<TilemapCollider2D>();
                    if (collider != null && collider.enabled && collider.hasTilemapChanges)
                        collider.ProcessTilemapChanges();
                    removed += entry.Cells.Count;
                }
                Undo.CollapseUndoOperations(group);
                SceneView.RepaintAll();
                return removed;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        internal sealed class AlphaDetector : IDisposable
        {
            private readonly bool includeMissing;
            private readonly List<string> warnings;
            private readonly Dictionary<TileBase, bool?> tiles = new Dictionary<TileBase, bool?>();
            private readonly Dictionary<Sprite, bool?> sprites = new Dictionary<Sprite, bool?>();
            private readonly Dictionary<Texture2D, Color[]> pixels = new Dictionary<Texture2D, Color[]>();

            internal AlphaDetector(bool includeMissing, List<string> warnings)
            { this.includeMissing = includeMissing; this.warnings = warnings; }

            internal bool? IsTransparent(TileBase tile)
            {
                if (tiles.TryGetValue(tile, out var cached)) return cached;
                bool? result;
                try { result = InspectTile(tile); }
                catch (Exception e)
                {
                    warnings.Add(tile.name + ": " + e.Message);
                    result = null;
                }
                tiles[tile] = result;
                return result;
            }

            private bool? InspectTile(TileBase tile)
            {
                // Unknown scripted tiles may generate images or game objects dynamically.
                string type = tile.GetType().FullName;
                if (type != "UnityEngine.Tilemaps.Tile" && type != "UnityEngine.Tilemaps.AnimatedTile" &&
                    type != "UnityEngine.Tilemaps.RuleTile" && type != "FarmMVP.SeasonalTile" &&
                    type != "FarmMVP.SlicedAnimationTile") return null;

                bool foundSprite = false, unknown = false;
                using (var serialized = new SerializedObject(tile))
                {
                    var property = serialized.GetIterator();
                    // Inspect ALL animation frames, seasons and rule outputs, not just today's sprite.
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var value = property.objectReferenceValue;
                        if (value is GameObject) return false;
                        if (!(value is Sprite sprite)) continue;
                        foundSprite = true;
                        var transparent = InspectSprite(sprite);
                        if (transparent == false) return false;
                        if (!transparent.HasValue) unknown = true;
                        if (type == "UnityEngine.Tilemaps.Tile" && transparent == true)
                        {
                            // AnimationTileset converts ordinary painted Tiles on entering Play mode.
                            string path = AssetDatabase.GetAssetPath(sprite);
                            if (path.Contains("/Resources/" + AnimationTileset.ResourceRoot + "/"))
                                foreach (var frame in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                                {
                                    var frameAlpha = InspectSprite(frame);
                                    if (frameAlpha == false) return false;
                                    if (!frameAlpha.HasValue) unknown = true;
                                }
                            // SeasonalTileset resolves ordinary Tiles by sheet/sprite names at runtime.
                            // Conservatively retain possible auto-conversion sources until explicitly
                            // authored as SeasonalTile, whose complete sprite set is inspected above.
                            foreach (string season in new[] { "Spring", "Summer", "Fall", "Winter" })
                                if (path.Contains("/" + season + "/") || sprite.texture.name.Contains(season) ||
                                    sprite.name.EndsWith("_" + season, StringComparison.Ordinal)) unknown = true;
                        }
                    }
                }
                if (unknown) return null;
                return foundSprite || includeMissing ? true : (bool?)null;
            }

            private bool? InspectSprite(Sprite sprite)
            {
                if (sprites.TryGetValue(sprite, out var cached)) return cached;
                var texture = SpriteUtility.GetSpriteTexture(sprite, false);
                if (texture == null) return sprites[sprite] = null;
                var rect = sprite.rect;
                int x0 = Mathf.FloorToInt(rect.xMin), y0 = Mathf.FloorToInt(rect.yMin);
                int x1 = Mathf.CeilToInt(rect.xMax), y1 = Mathf.CeilToInt(rect.yMax);
                // Unexpected atlas/scaling coordinates are not evidence of transparency.
                if (x0 < 0 || y0 < 0 || x1 > texture.width || y1 > texture.height || x1 <= x0 || y1 <= y0)
                    return sprites[sprite] = null;
                if (!pixels.TryGetValue(texture, out var data))
                {
                    data = ReadPixels(texture);
                    pixels.Add(texture, data);
                }
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        // Keep even one faint pixel. Tint/renderer visibility is deliberately irrelevant.
                        if (data[y * texture.width + x].a != 0f) return sprites[sprite] = false;
                return sprites[sprite] = true;
            }

            private static Color[] ReadPixels(Texture2D texture)
            {
                if (texture.isReadable) return texture.GetPixels();
                if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
                    throw new InvalidOperationException("텍스처 알파를 읽을 수 없어 보존했습니다.");
                var previous = RenderTexture.active;
                var target = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                    RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                Texture2D copy = null;
                try
                {
                    // No importer changes, reimports or Read/Write setting changes.
                    Graphics.Blit(texture, target);
                    RenderTexture.active = target;
                    copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, true);
                    copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                    copy.Apply();
                    return copy.GetPixels();
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
                    RenderTexture.ReleaseTemporary(target);
                }
            }

            public void Dispose() { pixels.Clear(); sprites.Clear(); tiles.Clear(); }
        }
    }
}
