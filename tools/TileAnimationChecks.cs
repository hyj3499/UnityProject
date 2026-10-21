// Run only in the isolated project created by Check-TileAnimations.ps1.
using System;
using System.IO;
using System.Linq;
using FarmMVP;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TileAnimationChecks
{
    private static int checks;

    public static void Run()
    {
        try
        {
            if (!File.Exists("tile-animation-validation-project"))
                throw new Exception("This check requires the isolated validation project.");

            CreateSheet("Sprites/Tiles/Animation/A/same", 16, 16, 8, 1);
            CreateSheet("Sprites/Tiles/Animation/B/same", 32, 32, 3, 1);
            CreateSheet("Sprites/Tiles/Animation/tall", 16, 32, 3, 1);
            CreateSheet("Sprites/Tiles/Animation/grid", 32, 48, 2, 2);
            CreateSheet("Sprites/Tiles/Animation/still", 16, 16, 1, 1);
            CreateSheet("Sprites/Tiles/Outside", 16, 16, 8, 1);
            CreateSheet("Sprites/Tiles/Spring/season", 16, 16, 1, 1);
            CreateSheet("Sprites/Tiles/Winter/season", 16, 16, 1, 1);
            var grid = new GameObject("Grid", typeof(Grid));
            var mapObject = new GameObject("Map", typeof(Tilemap), typeof(TilemapRenderer));
            mapObject.transform.SetParent(grid.transform);
            var map = mapObject.GetComponent<Tilemap>();

            var beach = Load("Animation/A/same");
            var source = MakeTile(beach[0]);
            source.colliderType = Tile.ColliderType.Grid;
            source.flags = TileFlags.LockColor;
            source.color = Color.cyan;
            source.transform = Matrix4x4.Scale(new Vector3(-1, 1, 1));
            map.SetTile(Vector3Int.zero, source);
            map.SetTile(Vector3Int.right, source);
            map.SetTileFlags(Vector3Int.zero, TileFlags.None);
            var cellTransform = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90));
            map.SetTransformMatrix(Vector3Int.zero, cellTransform);
            map.SetColor(Vector3Int.zero, Color.magenta);
            map.SetTileFlags(Vector3Int.zero, TileFlags.LockAll);

            Assert(AnimationTileset.Apply(map) == 2, "Both painted cells convert");
            var animated = map.GetTile<SlicedAnimationTile>(Vector3Int.zero);
            Assert(animated != null && animated.frames.Length == 8, "Eight imported frames");
            Assert(animated.frames.SequenceEqual(beach), "Spatial order, independent of reversed names");
            Assert(map.GetTile(Vector3Int.right) == animated, "Same source reuses one runtime tile");
            Assert(source.sprite == beach[0] && source.GetType() == typeof(Tile), "Original asset unchanged");
            Assert(animated.colliderType == source.colliderType && animated.flags == source.flags &&
                   animated.color == source.color && animated.transform == source.transform, "Tile properties preserved");
            Assert(map.GetColor(Vector3Int.zero) == Color.magenta &&
                   map.GetTransformMatrix(Vector3Int.zero) == cellTransform &&
                   map.GetTileFlags(Vector3Int.zero) == TileFlags.LockAll, "Cell properties preserved");
            var data = new TileAnimationData();
            Assert(animated.GetTileAnimationData(Vector3Int.zero, null, ref data) &&
                   data.animatedSprites.Length == 8 && data.animationSpeed == 8 &&
                   data.animationStartTime == 0, "Unity animation callback returns looping frame data");
            Assert(AnimationTileset.Apply(map) == 0, "Repeated application is idempotent");
            Assert(SeasonalTileset.Apply(map) == 0 && map.GetTile(Vector3Int.zero) == animated,
                   "Seasonal conversion leaves animation intact");

            CheckSheet(map, "Animation/B/same", 3, new Vector2(32, 32));
            CheckSheet(map, "Animation/tall", 3, new Vector2(16, 32));
            CheckSheet(map, "Animation/grid", 4, new Vector2(32, 48));
            var laterFrame = MakeTile(beach[6]);
            map.ClearAllTiles();
            map.SetTile(Vector3Int.zero, laterFrame);
            AnimationTileset.Apply(map);
            Assert(map.GetTile<SlicedAnimationTile>(Vector3Int.zero).frames[0] == beach[0],
                   "Painting a later frame still starts at the first slice");

            foreach (var path in new[] { "Animation/still", "Outside" })
            {
                map.ClearAllTiles();
                var plain = MakeTile(Load(path)[0]);
                map.SetTile(Vector3Int.zero, plain);
                Assert(AnimationTileset.Apply(map) == 0 && map.GetTile(Vector3Int.zero) == plain,
                       "Unanimated: " + path);
            }

            map.ClearAllTiles();
            map.SetTile(Vector3Int.zero, MakeTile(Load("Spring/season")[0]));
            Assert(SeasonalTileset.Apply(map) == 1, "Seasonal static conversion still works");
            var seasonal = map.GetTile<SeasonalTile>(Vector3Int.zero);
            Assert(seasonal.SpriteFor(Season.Spring) == Load("Spring/season")[0] &&
                   seasonal.SpriteFor(Season.Winter) == Load("Winter/season")[0] &&
                   seasonal.SpriteFor(Season.Summer) == Load("Spring/season")[0], "Season selection and fallback");
            Assert(!seasonal.GetTileAnimationData(Vector3Int.zero, null, ref data), "Seasonal tile has no legacy animation");
            Assert(AnimationTileset.Apply(null) == 0, "Null map is safe");

            CheckObjects(map, beach);

            Debug.Log("TILE_ANIMATION_CHECKS_PASSED: " + checks);
            File.WriteAllText("result.txt", "PASS: " + checks + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText("result.txt", error.ToString());
            EditorApplication.Exit(1);
        }
    }

    private static void CheckObjects(Tilemap map, Sprite[] frames)
    {
        var locationObject = new GameObject("Animation object checks");
        var location = locationObject.AddComponent<GameLocation>();
        var featureRoot = new GameObject("Features").transform;
        featureRoot.SetParent(location.transform);
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance |
                                                      System.Reflection.BindingFlags.NonPublic;
        typeof(GameLocation).GetField("_featureRoot", hidden).SetValue(location, featureRoot);
        typeof(GameLocation).GetMethod("ResetBlocked", hidden).Invoke(location, null);
        map.ClearAllTiles();
        map.name = "Fixed_Farm";
        var original = MakeTile(frames[6]);
        map.SetTile(Vector3Int.zero, original);
        var plain = Load("Outside")[0];
        map.SetTile(Vector3Int.right, MakeTile(plain));
        var overlayObject = new GameObject("Fixed_1_Farm", typeof(Tilemap), typeof(TilemapRenderer));
        overlayObject.transform.SetParent(map.transform.parent);
        var overlay = overlayObject.GetComponent<Tilemap>();
        var tall = Load("Animation/tall");
        overlay.SetTile(Vector3Int.zero, MakeTile(tall[2]));
        var placer = typeof(GameLocation).Assembly.GetType("FarmMVP.PaintedObjectPlacer");
        placer.GetMethod("Apply").Invoke(null, new object[] { location, new[] { map, overlay }, null, null });

        var renderers = featureRoot.GetComponentsInChildren<SpriteRenderer>();
        Assert(renderers.Length == 3, "Fixed and overlay maps produce three objects");
        var animated = renderers.Single(sr => sr.sprite == frames[0]);
        var top = renderers.Single(sr => sr.sprite == tall[0]);
        Assert(animated.GetComponent<SlicedSpriteAnimation>() != null &&
               top.GetComponent<SlicedSpriteAnimation>() != null, "Both Fixed layers animate automatically");
        Assert(renderers.Single(sr => sr.sprite == plain).GetComponent<SlicedSpriteAnimation>() == null,
               "Ordinary Fixed object stays static");
        var world = map.GetCellCenterWorld(Vector3Int.zero);
        Assert(animated.transform.position == world && top.transform.position == world,
               "Object position and imported pivot placement preserved");
        Assert(top.sortingOrder > animated.sortingOrder, "Fixed overlay stays in front");
        Assert(location.IsBlocked(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y)),
               "Animated Fixed object still blocks its cell");
        Assert(map.GetTile(Vector3Int.zero) == original && original.sprite == frames[6],
               "Fixed source tile remains unchanged");

        var player = animated.GetComponent<SlicedSpriteAnimation>();
        var advance = typeof(SlicedSpriteAnimation).GetMethod("Advance", hidden);
        int order = animated.sortingOrder;
        animated.color = Color.magenta;
        animated.flipX = true;
        advance.Invoke(player, new object[] { 0.125f });
        Assert(animated.sprite == frames[1], "Object advances at eight frames per second");
        advance.Invoke(player, new object[] { 0f });
        Assert(animated.sprite == frames[1], "Zero game delta pauses animation");
        advance.Invoke(player, new object[] { 0.875f });
        Assert(animated.sprite == frames[0], "Object loops to first frame");
        advance.Invoke(player, new object[] { 2.25f });
        Assert(animated.sprite == frames[2], "Large frame delta wraps correctly");
        Assert(animated.transform.position == world && animated.sortingOrder == order &&
               animated.color == Color.magenta && animated.flipX, "Animation preserves renderer settings");
        SlicedSpriteAnimation.Apply(animated);
        Assert(animated.GetComponents<SlicedSpriteAnimation>().Length == 1, "Reapplying does not duplicate component");
        animated.sprite = plain;
        advance.Invoke(player, new object[] { 0.125f });
        Assert(animated.sprite == plain && !player.enabled, "External static sprite change stops old animation");
        animated.sprite = tall[2];
        SlicedSpriteAnimation.Apply(animated);
        advance.Invoke(player, new object[] { 0.125f });
        Assert(player.enabled && animated.sprite == tall[1], "Rebinding resumes with new sheet");
        var still = new GameObject("Single slice").AddComponent<SpriteRenderer>();
        still.sprite = Load("Animation/still")[0];
        SlicedSpriteAnimation.Apply(still);
        Assert(still.GetComponent<SlicedSpriteAnimation>() == null, "Single-slice object stays static");
        SlicedSpriteAnimation.Apply(null);
        UnityEngine.Object.DestroyImmediate(still.gameObject);
        UnityEngine.Object.DestroyImmediate(locationObject);
        UnityEngine.Object.DestroyImmediate(overlayObject);
    }
    private static void CheckSheet(Tilemap map, string path, int count, Vector2 size)
    {
        map.ClearAllTiles();
        var frames = Load(path);
        map.SetTile(Vector3Int.zero, MakeTile(frames[0]));
        Assert(AnimationTileset.Apply(map) == 1, "Converts " + path);
        var tile = map.GetTile<SlicedAnimationTile>(Vector3Int.zero);
        Assert(tile.frames.Length == count && tile.frames.SequenceEqual(frames) &&
               tile.frames.All(sprite => sprite.rect.size == size), "Imported size/order retained: " + path);
    }

    private static Sprite[] Load(string path) => Resources.LoadAll<Sprite>("Sprites/Tiles/" + path)
        .OrderByDescending(sprite => sprite.rect.yMax).ThenBy(sprite => sprite.rect.xMin).ToArray();

    private static Tile MakeTile(Sprite sprite)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        return tile;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("FAILED: " + message);
        checks++;
    }

    private static void CreateSheet(string resourcePath, int width, int height, int columns, int rows)
    {
        string path = "Assets/Resources/" + resourcePath + ".png";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var texture = new Texture2D(width * columns, height * rows);
        texture.SetPixels(Enumerable.Repeat(Color.white, texture.width * texture.height).ToArray());
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        var slices = new SpriteMetaData[columns * rows];
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < columns; col++)
        {
            int index = row * columns + col;
            slices[index] = new SpriteMetaData
            {
                name = "frame_" + (slices.Length - index),
                rect = new Rect(col * width, (rows - row - 1) * height, width, height),
                pivot = new Vector2(0.5f, 0.5f),
            };
        }
#pragma warning disable CS0618
        importer.spritesheet = slices;
#pragma warning restore CS0618
        importer.SaveAndReimport();
    }
}
