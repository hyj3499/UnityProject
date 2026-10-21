// Executed in the disposable project made by Check-TransparentTileCleaner.ps1.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FarmMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class UnknownCleanerTile : Tile { }

public static class TransparentTileCleanerChecks
{
    private static int checks;
    public static void Run()
    {
        try
        {
            if (!File.Exists("transparent-cleaner-validation-project")) throw new Exception("Isolated project required.");
            var blank = MakeSprite("blank", 0, false);
            var faint = MakeSprite("faint", 1, false);
            var solid = MakeSprite("solid", 255, false);
            var tile = MakeTile(blank);
            var visible = MakeTile(solid);
            var missing = MakeTile(null);
            var warnings = new List<string>();
            using (var detector = new TransparentTileCleaner.AlphaDetector(false, warnings))
            {
                Assert(!blank.texture.isReadable, "Non-readable imported sprite fixture");
                Assert(detector.IsTransparent(tile) == true, "GPU readback detects all-zero alpha");
                Assert(detector.IsTransparent(MakeTile(faint)) == false, "One alpha=1/255 pixel is preserved");
                visible.color = Color.clear;
                Assert(detector.IsTransparent(visible) == false, "Transparent tint cannot erase visible image");
                Assert(detector.IsTransparent(missing) == null, "Missing image is protected by default");
                var prefabTile = MakeTile(blank);
                prefabTile.gameObject = new GameObject("Spawned visual");
                Assert(detector.IsTransparent(prefabTile) == false, "GameObject-producing tiles are protected");
                var custom = ScriptableObject.CreateInstance<UnknownCleanerTile>();
                custom.sprite = blank;
                Assert(detector.IsTransparent(custom) == null, "Unknown scripted tile is protected");
                var seasonal = ScriptableObject.CreateInstance<SeasonalTile>();
                seasonal.seasonSprites = new[] { blank, blank, solid, blank };
                Assert(detector.IsTransparent(seasonal) == false, "Visible later season is protected");
                var seasonalBlank = ScriptableObject.CreateInstance<SeasonalTile>();
                seasonalBlank.seasonSprites = new[] { blank, blank };
                Assert(detector.IsTransparent(seasonalBlank) == true, "All-transparent seasons are removable");
                var animation = ScriptableObject.CreateInstance<SlicedAnimationTile>();
                animation.sprite = blank;
                animation.frames = new[] { blank, solid };
                Assert(detector.IsTransparent(animation) == false, "Visible later animation frame is protected");
                var blankAnimation = ScriptableObject.CreateInstance<SlicedAnimationTile>();
                blankAnimation.sprite = blank;
                blankAnimation.frames = new[] { blank, blank };
                Assert(detector.IsTransparent(blankAnimation) == true, "All-transparent animation is removable");
            }
            Assert(warnings.Count == 0, "No pixel-read failures");
            Assert(!((TextureImporter)AssetImporter.GetAtPath("Assets/blank.png")).isReadable,
                "Scan leaves importer Read/Write settings unchanged");
            using (var detector = new TransparentTileCleaner.AlphaDetector(true, warnings))
                Assert(detector.IsTransparent(missing) == true, "Missing image opt-in works");

            CheckSlicedSheet();
            var map = MakeMap("Cliff_Farm");
            var other = MakeMap("Cliff_Valley");
            var inactive = MakeMap("Winter_Cliff_2_Farm");
            inactive.gameObject.SetActive(false);
            var previewScene = EditorSceneManager.NewPreviewScene();
            var previewMap = MakeMap("Palette");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(previewMap.transform.root.gameObject, previewScene);
            Assert(TransparentTileCleaner.LoadedMaps().Contains(inactive), "Inactive scene layers included");
            Assert(!TransparentTileCleaner.LoadedMaps().Contains(previewMap), "Palette preview excluded");
            EditorSceneManager.ClosePreviewScene(previewScene);
            Assert(TransparentTileCleaner.MatchesName(inactive.name, "Farm", "Cliff"), "Season and numbered overlay matching");
            Assert(TransparentTileCleaner.MatchesName("FarmGround", "Farm", "Location"), "Legacy ground name matching");
            Assert(!TransparentTileCleaner.MatchesName("Cliff_Valley", "Farm", "Cliff"), "Other region excluded");
            Assert(!TransparentTileCleaner.MatchesName("Water_Farm", "Farm", "Cliff"), "Other layer excluded");

            var p = new Vector3Int(-2, -3, 0);
            map.SetTile(p, tile);
            map.SetTile(p + Vector3Int.right, visible);
            map.SetTile(p + Vector3Int.up, tile);
            map.SetTile(p + new Vector3Int(0, 0, 1), tile);
            map.SetTileFlags(p, TileFlags.None);
            map.SetColor(p, Color.magenta);
            var rotation = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90));
            map.SetTransformMatrix(p, rotation);
            map.SetTileFlags(p, TileFlags.LockAll);
            other.SetTile(p, tile);
            var region = new BoundsInt(p, new Vector3Int(2, 1, 1));
            var scan = TransparentTileCleaner.Scan(new[] { map }, region, null, false);
            Assert(scan.Count == 1 && map.HasTile(p), "Preview finds one tile without mutating scene");
            Assert(TransparentTileCleaner.Scan(new[] { map }, region, visible, false).Count == 0, "Exact tile asset filter");
            Assert(TransparentTileCleaner.Scan(new[] { map }, null, null, false).Count == 3, "Whole-map scan includes every Z layer");
            bool cancelled = false;
            try { TransparentTileCleaner.Scan(new[] { map }, null, null, false, (_, __) => true); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert(cancelled && map.HasTile(p), "Cancellation leaves scene untouched");
            Undo.ClearAll();
            Assert(TransparentTileCleaner.Apply(scan) == 1 && !map.HasTile(p), "Deletes targeted invisible cell");
            Assert(map.GetColliderType(p) == Tile.ColliderType.None, "Erased cell no longer carries collision");
            Assert(map.HasTile(p + Vector3Int.up) && map.HasTile(p + new Vector3Int(0, 0, 1)), "Outside rectangle and Z preserved");
            Assert(other.HasTile(p) && map.HasTile(p + Vector3Int.right), "Other map and visible cells preserved");
            Assert(map.gameObject.scene.isDirty, "Edited scene marked dirty");
            Undo.PerformUndo();
            Assert(map.GetTile(p) == tile && map.GetColor(p) == Color.magenta &&
                map.GetTransformMatrix(p) == rotation && map.GetTileFlags(p) == TileFlags.LockAll,
                "Undo restores tile, tint, transform and flags");
            Undo.PerformRedo();
            Assert(!map.HasTile(p), "Redo removes tile again");
            var both = TransparentTileCleaner.Scan(new[] { map, other }, null, tile, false);
            Assert(TransparentTileCleaner.Apply(both) == 3, "One operation can erase multiple maps");
            Undo.PerformUndo();
            Assert(map.HasTile(p + Vector3Int.up) && other.HasTile(p), "One Undo restores all affected maps");
            CheckPrefab(blank);
            File.WriteAllText("result.txt", "PASS: " + checks + " transparent tile cleaner checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("result.txt", "FAIL after " + checks + " checks: " + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckSlicedSheet()
    {
        var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
        texture.SetPixels32(new Color32[8]);
        texture.SetPixel(3, 1, Color.white);
        texture.Apply();
        File.WriteAllBytes("Assets/sliced.png", texture.EncodeToPNG());
        AssetDatabase.ImportAsset("Assets/sliced.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/sliced.png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
#pragma warning disable 618
        importer.spritesheet = new[]
        {
            new SpriteMetaData { name = "blank_slice", rect = new Rect(0, 0, 2, 2), pivot = Vector2.one * .5f },
            new SpriteMetaData { name = "visible_slice", rect = new Rect(2, 0, 2, 2), pivot = Vector2.one * .5f }
        };
#pragma warning restore 618
        importer.SaveAndReimport();
        var slices = AssetDatabase.LoadAllAssetsAtPath("Assets/sliced.png").OfType<Sprite>().ToArray();
        using (var detector = new TransparentTileCleaner.AlphaDetector(false, new List<string>()))
        {
            Assert(detector.IsTransparent(MakeTile(slices.Single(s => s.name == "blank_slice"))) == true,
                "Slice is checked independently of visible neighboring slice");
            Assert(detector.IsTransparent(MakeTile(slices.Single(s => s.name == "visible_slice"))) == false,
                "Slice-edge visible pixel is preserved");
        }
        Directory.CreateDirectory("Assets/Resources/Sprites/Tiles/Animation");
        AssetDatabase.Refresh();
        Assert(AssetDatabase.MoveAsset("Assets/sliced.png", "Assets/Resources/Sprites/Tiles/Animation/sliced.png") == "",
            "Move animation fixture into runtime animation folder");
        using (var detector = new TransparentTileCleaner.AlphaDetector(false, new List<string>()))
            Assert(detector.IsTransparent(MakeTile(slices.Single(s => s.name == "blank_slice"))) == false,
                "Ordinary painted animation tile with a visible later frame is protected");
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void CheckPrefab(Sprite blank)
    {
        var tile = MakeTile(blank);
        AssetDatabase.CreateAsset(tile, "Assets/prefabTile.asset");
        var template = MakeMap("Cliff_Valley");
        template.SetTile(Vector3Int.zero, tile);
        var prefab = PrefabUtility.SaveAsPrefabAsset(template.transform.root.gameObject, "Assets/cleaner.prefab");
        UnityEngine.Object.DestroyImmediate(template.transform.root.gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var map = instance.GetComponentInChildren<Tilemap>();
        var result = TransparentTileCleaner.Scan(new[] { map }, null, null, false);
        Assert(TransparentTileCleaner.Apply(result) == 1, "Prefab instance cleanup");
        Assert(prefab.GetComponentInChildren<Tilemap>().HasTile(Vector3Int.zero), "Source prefab is unchanged");
        Assert(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), "Deletion recorded as prefab instance override");
        Undo.PerformUndo();
        Assert(map.HasTile(Vector3Int.zero), "Undo restores prefab instance tile");
    }

    private static Sprite MakeSprite(string name, byte alpha, bool readable)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var colors = new Color32[4];
        colors[3] = new Color32(255, 255, 255, alpha);
        texture.SetPixels32(colors);
        texture.Apply();
        string path = "Assets/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = readable;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Tile MakeTile(Sprite sprite)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.Grid;
        return tile;
    }

    private static Tilemap MakeMap(string name)
    {
        var grid = new GameObject("Grid", typeof(Grid));
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid.transform);
        return go.GetComponent<Tilemap>();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
        Debug.Log("PASS " + message);
    }
}
