using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FarmMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class ObjectCollisionChecks
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static int checks;
    private static readonly List<string> results = new List<string>();
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }
    private static object Call(object instance, string method, params object[] args)
        => instance.GetType().GetMethod(method, Private).Invoke(instance, args);
    private static void Set(object instance, string field, object value)
        => instance.GetType().GetField(field, Private).SetValue(instance, value);

    public static void Run()
    {
        try
        {
            if (!File.Exists("object-collision-validation-project")) throw new Exception("Requires isolated project.");
            CheckBakeRules();
            ObjectCollisionBaker.Rebuild();
            byte[] firstBake = File.ReadAllBytes(ObjectCollisionBaker.OutputPath);
            ObjectCollisionBaker.Rebuild();
            Check(firstBake.SequenceEqual(File.ReadAllBytes(ObjectCollisionBaker.OutputPath)), "Bake must be deterministic.");
            var sprites = Resources.LoadAll<Sprite>("Sprites/Environment/Wood_spring").OrderBy(s => s.name).ToArray();
            Check(sprites.Length == 4, "Test PNG must have four slices.");
            for (int i = 0; i < 4; i++)
            {
                var mask = ObjectCollisionMasks.Find(sprites[i]);
                Check(mask != null, "Every object slice has either keyed paint or its opaque silhouette: " + sprites[i].name);
                Check(mask.usesKeyedPaint == (i >= 2), "Only red-painted Wood slices use foreground depth bands: " + sprites[i].name);
            }
            CheckOriginalPixels(sprites);
            CheckWorldAndOwnership(sprites);
            CheckDebris(sprites);
            CheckFixed(sprites);
            CheckMarkers(sprites);
            CheckPlaceables();
            CheckTreeAndRock();
            CheckMovement();
            CheckFootContact();
            CheckLayerBuilds(sprites);
            CheckMaskDeletion();
            results.Add($"PASS: {checks} assertions.");
            File.WriteAllLines("result.txt", results);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("result.txt", "FAIL: " + ex);
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckBakeRules()
    {
        var red = new Color32(255, 0, 0, 255);
        var paint = new Color32[12];
        paint[8] = red; // Top-left legend on an opaque source pixel; never collision.
        paint[0] = red; paint[1] = red;
        paint[2] = new Color32(254, 0, 0, 255); // Near but not exact key.
        paint[3] = new Color32(255, 0, 0, 0); // Transparent red must not count.
        var bytes = ObjectCollisionBaker.Bake(paint, 4, 3, new RectInt(0, 0, 4, 3));
        Check(bytes[0] == 3 && bytes[1] == 0, "Only legend, transparent paint and non-key colors excluded.");
        Check(ObjectCollisionBaker.Bake(paint, 4, 3, new RectInt(1, 0, 3, 3))[0] == 1,
            "Paint is preserved independently of source alpha.");
        Check(ObjectCollisionBaker.Bake(paint, 4, 3, new RectInt(2, 0, 2, 3)) == null,
            "An unpainted companion slice has no keyed pixels before silhouette fallback.");
        var source = new Color32[12];
        source[2] = new Color32(1, 2, 3, 255);
        source[7] = new Color32(4, 5, 6, 255);
        var opaque = ObjectCollisionBaker.BakeOpaque(source, 4, 3, new RectInt(1, 0, 3, 2));
        Check(opaque[0] == 34, "An object without a companion uses only its opaque source outline.");
        bool rejected = false;
        paint[8] = default;
        try { ObjectCollisionBaker.Bake(paint, 4, 3, new RectInt(0, 0, 4, 3)); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Missing visible color key must not silently remove collision.");
    }

    private static Texture2D Read(string path)
    {
        var texture = new Texture2D(2, 2);
        ImageConversion.LoadImage(texture, File.ReadAllBytes(path));
        return texture;
    }

    private static void CheckOriginalPixels(Sprite[] sprites)
    {
        var source = Read("Assets/Resources/Sprites/Environment/Wood_spring.png");
        var paint = Read("Assets/Resources/Sprites/Environment/Wood_spring_collision.png");
        var original = source.GetPixels32();
        var painted = paint.GetPixels32();
        var key = painted[(paint.height - 1) * paint.width];
        int outsideSource = 0;
        foreach (var sprite in sprites)
        {
            var mask = ObjectCollisionMasks.Find(sprite);
            int count = 0;
            for (int y = 0; y < mask.height; y++)
                for (int x = 0; x < mask.width; x++)
                {
                    int i = ((int)sprite.rect.y + y) * source.width + (int)sprite.rect.x + x;
                    var p = painted[i];
                    bool keyed = i != (paint.height - 1) * paint.width && p.a != 0 && p.r == key.r && p.g == key.g && p.b == key.b;
                    bool expected = sprite.name.EndsWith("_2") || sprite.name.EndsWith("_3") ? keyed : original[i].a != 0;
                    Check(mask.Contains(x, y) == expected, sprite.name + " painted pixel mismatch.");
                    if (keyed && original[i].a == 0) outsideSource++;
                    if (expected) count++;
                }
            results.Add($"{sprite.name}: {count} solid pixels, {mask.bits.Length} bytes.");
        }
        Check(outsideSource > 0, "Real PNG must retain paint outside the original silhouette.");
        results.Add($"Included {outsideSource} painted pixels outside source alpha; legend still excluded.");
        UnityEngine.Object.DestroyImmediate(source);
        UnityEngine.Object.DestroyImmediate(paint);
    }

    private static GameLocation Location()
    {
        var location = new GameObject("Collision check").AddComponent<GameLocation>();
        location.width = location.height = 40;
        Set(location, "_blocked", new bool[40, 40]);
        Set(location, "_featureRoot", new GameObject("Features").transform);
        return location;
    }

    private static SpriteRenderer Renderer(Sprite sprite, Vector2 position)
    {
        var sr = new GameObject("Object").AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.transform.position = position;
        return sr;
    }

    private static void CheckWorldAndOwnership(Sprite[] sprites)
    {
        var location = Location();
        var sr = Renderer(sprites[3], new Vector2(10.25f, 10.75f));
        var footprint = new RectInt(10, 10, 5, 2);
        Call(location, "RegisterObjectCollision", sr, footprint, true, null, null);
        CheckSpriteWorld(location, sr);
        var solid = SolidPoint(sr);
        var cell = new Vector2Int(Mathf.RoundToInt(solid.x), Mathf.RoundToInt(solid.y));
        Check(location.IsBlocked(cell.x, cell.y), "NPC placement must avoid partial collision cells.");
        Check(location.IsBlockedForStory(cell, null), "Story paths must respect object masks.");
        var other = Renderer(null, cell);
        Call(location, "RegisterObjectCollision", other, new RectInt(cell.x, cell.y, 1, 1), true, null, null);
        Call(location, "RemoveObjectCollision", sr);
        Check(location.IsBlockedAtWorld(solid.x, solid.y), "Removing a mask must preserve another object's fallback footprint.");
        Call(location, "SetBlocked", cell.x, cell.y, true);
        Call(location, "RemoveObjectCollision", other);
        Check(location.IsBlockedAtWorld(solid.x, solid.y), "Removing an object must preserve terrain/water/block masks.");
        Call(location, "SetBlocked", cell.x, cell.y, false);
        Check(!location.IsBlockedAtWorld(solid.x, solid.y), "All owners removed.");
        Call(location, "RegisterObjectCollision", sr, footprint, true, null, null);
        location.SetNpcBlocked(cell);
        Call(location, "RemoveObjectCollision", sr);
        Check(location.IsBlockedAtWorld(solid.x, solid.y), "NPC blocks preserved after object removal.");
        location.ClearNpcBlocks();
        Check(!location.IsBlockedAtWorld(solid.x, solid.y), "No stale mask remains.");
        Call(location, "RegisterObjectCollision", other, new RectInt(cell.x, cell.y, 1, 1), true, cell, null);
        Check(!location.IsBlockedAtWorld(cell.x, cell.y), "Explicit doorway remains open.");
    }

    private static Vector2 PixelPoint(SpriteRenderer sr, int x, int y)
        => (Vector2)sr.transform.position + (new Vector2(x + 0.5f, y + 0.5f) - sr.sprite.pivot) / sr.sprite.pixelsPerUnit;

    private static Vector2 SolidPoint(SpriteRenderer sr)
    {
        var mask = ObjectCollisionMasks.Find(sr.sprite);
        for (int y = 0; y < mask.height; y++)
            for (int x = 0; x < mask.width; x++)
                if (mask.Contains(x, y)) return PixelPoint(sr, x, y);
        throw new Exception("Expected solid mask pixel.");
    }

    private static void CheckSpriteWorld(GameLocation location, SpriteRenderer sr)
    {
        var mask = ObjectCollisionMasks.Find(sr.sprite);
        int solid = 0, open = 0;
        for (int y = 0; y < mask.height; y++)
            for (int x = 0; x < mask.width; x++)
            {
                Vector2 point = PixelPoint(sr, x, y);
                bool expected = mask.Contains(x, y);
                Check(location.IsBlockedAtWorld(point.x, point.y) == expected, sr.sprite.name + " world-to-pixel mismatch.");
                if (expected) solid++; else open++;
            }
        Check(solid > 0 && open > 0, "Must exercise both blocked and walkable parts.");
    }

    private static void CheckDebris(Sprite[] sprites)
    {
        for (int variant = 0; variant < 4; variant++)
        {
            var location = Location();
            var def = DebrisDatabase.Get(DebrisKind.Wood, variant);
            Call(location, "AddDebris", 10, 10, def, def.maxHp);
            var renderers = (Dictionary<Vector2Int, SpriteRenderer>)typeof(GameLocation).GetField("_debrisRenderers", Private).GetValue(location);
            var sr = renderers[new Vector2Int(10, 10)];
            Check(sr.sprite == sprites[variant], "Debris resolves the intended sliced original.");
            CheckSpriteWorld(location, sr);
            Check(location.HasDebris(10 + def.size.x - 1, 10, DebrisKind.Wood), "Interaction footprint preserved.");
            Vector2 point = SolidPoint(sr);
            Call(location, "RemoveDebris", new Vector2Int(10, 10));
            Check(!location.IsBlockedAtWorld(point.x, point.y), "Destroyed debris removes collision immediately.");
        }
        results.Add("All debris variants use their keyed paint or opaque silhouettes at rendered positions, including removal.");
    }

    private static void CheckFixed(Sprite[] sprites)
    {
        var location = Location();
        var grid = new GameObject("Grid", typeof(Grid));
        grid.transform.position = new Vector3(-0.5f, -0.5f, 0);
        var go = new GameObject("Fixed_Farm", typeof(Tilemap));
        go.transform.SetParent(grid.transform, false);
        var map = go.GetComponent<Tilemap>();
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprites[2];
        map.SetTile(new Vector3Int(10, 10, 0), tile);
        var maps = new Tilemap[Depth.MaxOverlay + 1];
        maps[0] = map;
        var type = typeof(GameLocation).Assembly.GetType("FarmMVP.PaintedObjectPlacer");
        type.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { location, maps, null, null });
        var root = (Transform)typeof(GameLocation).GetField("_featureRoot", Private).GetValue(location);
        CheckSpriteWorld(location, root.GetComponentInChildren<SpriteRenderer>());
        results.Add("Fixed tilemap placement uses the same sprite mask.");
    }

    private static void CheckMarkers(Sprite[] sprites)
    {
        var location = Location();
        var type = typeof(GameLocation).Assembly.GetType("FarmMVP.ObjectMarkerPlacer");
        var def = new ObjectMarkerDef { spritePath = "Sprites/Environment/Wood_spring_3", offset = new Vector2(0.5f, 1), size = new Vector2Int(5, 2) };
        var args = new object[] { location, def, 10, 10, null, new HashSet<Vector2Int>(), 0 };
        type.GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        var root = (Transform)typeof(GameLocation).GetField("_featureRoot", Private).GetValue(location);
        CheckSpriteWorld(location, root.GetComponentInChildren<SpriteRenderer>());
        results.Add("Object markers apply masks with their configured rendering offsets.");
    }

    private static void CheckPlaceables()
    {
        var location = Location();
        var def = PlaceableDatabase.All.First(d => d.blocks && !d.isGate);
        Check(location.PlaceAt(10, 10, def.id), "Place fallback furniture/fence.");
        var renderers = (Dictionary<Vector2Int, SpriteRenderer>)typeof(GameLocation).GetField("_placedRenderers", Private).GetValue(location);
        Check(location.IsBlockedAtWorld(SolidPoint(renderers[new Vector2Int(10, 10)]).x,
            SolidPoint(renderers[new Vector2Int(10, 10)]).y), "Placed opaque silhouette blocks.");
        Call(location, "SetBlocked", 10, 10, true);
        Check(location.RemovePlaced(10, 10, out _), "Remove fallback placeable.");
        Check(location.IsBlockedAtWorld(10, 10), "Removal preserves independently painted terrain.");
        var gate = PlaceableDatabase.All.First(d => d.isGate);
        Check(location.PlaceAt(15, 10, gate.id) && location.PlaceAt(16, 10, gate.id), "Place paired gates.");
        Check(location.ToggleGate(15, 10), "Open gate pair.");
        Check(!location.IsBlocked(15, 10) && !location.IsBlocked(16, 10), "Open gates release collision.");
        Check(location.ToggleGate(15, 10), "Close gate pair.");
        Check(location.IsBlocked(15, 10) && location.IsBlocked(16, 10), "Closed gates restore collision.");
    }

    private static void CheckMovement()
    {
        // One pixel at 64 PPU must not be crossed by a long movement step.
        var texture = new Texture2D(1, 64);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 64), Vector2.zero, 64);
        var cache = (Dictionary<Sprite, ObjectCollisionMasks.Mask>)typeof(ObjectCollisionMasks)
            .GetField("cache", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        cache[sprite] = new ObjectCollisionMasks.Mask { sprite = sprite, width = 1, height = 64,
            pivot = Vector2.zero, pixelsPerUnit = 64, bits = Enumerable.Repeat((byte)255, 8).ToArray() };
        var location = Location();
        var edge = Renderer(sprite, new Vector2(10.5f, 10.5f));
        Call(location, "RegisterObjectCollision", edge, new RectInt(10, 10, 1, 1), true, null, null);
        Check(location.IsBlockedAtWorld(10.5f, 10.5f), "Exact lower pixel boundary must be indexed even when cell rounding ties to even.");
        Check(!location.IsBlockedAtWorld(10.5f + 1f / 64f, 10.5f), "Exact upper pixel boundary is outside.");
        Call(location, "RemoveObjectCollision", edge);
        var sr = Renderer(sprite, new Vector2(10.01f, 10));
        Call(location, "RegisterObjectCollision", sr, new RectInt(10, 10, 1, 1), true, null, null);
        var manager = new GameObject("Manager").AddComponent<GameManager>();
        typeof(GameManager).GetProperty("CurrentLocation").SetValue(manager, location);
        var player = new GameObject("Player").AddComponent<PlayerController>();
        Set(player, "_game", manager);
        player.facing = Direction.Right;
        Vector2 rightFoot = FarmerBodyCollision.IdleFootProbe(Direction.Right);
        player.transform.position = new Vector3(9.5f - rightFoot.x, 10.25f - rightFoot.y, 0);
        Call(player, "TryMove", new Vector2(2, 0));
        Check(player.transform.position.x + rightFoot.x < 10.01f && player.transform.position.x + rightFoot.x > 9.9f,
            "Fast movement stops before a 1px mask at the right idle foot edge.");
        player.transform.position = new Vector3(9.5f - rightFoot.x, 11.25f - rightFoot.y, 0);
        Call(player, "TryMove", new Vector2(2, 0));
        Check(Mathf.Abs(player.transform.position.x + rightFoot.x - 11.5f) < 0.001f,
            "Movement above painted region remains free.");
        results.Add("Fast player movement cannot cross a 1px/64-PPU obstacle at the idle foot edge; unpainted region remains traversable.");
    }

    private static void CheckTreeAndRock()
    {
        var location = Location();
        var treePos = new Vector2Int(10, 10);
        location.trees[treePos] = new TreeFeature(10, 10, TreeDatabase.DefaultTreeId, 0);
        location.RenderTree(treePos);
        Check(location.IsBlockedAtWorld(10.49f, 10.49f), "Unmasked tree keeps one-cell footprint.");
        Call(location, "SetBlocked", 10, 10, true);
        location.trees.Remove(treePos);
        location.RenderTree(treePos);
        Check(location.IsBlockedAtWorld(10, 10), "Chopping a tree preserves underlying terrain.");
        var rockPos = new Vector2Int(15, 10);
        location.rocks[rockPos] = new RockFeature(15, 10, 0);
        location.RenderRock(rockPos);
        Check(location.IsBlockedAtWorld(15.49f, 10.49f), "Unmasked rock keeps one-cell footprint.");
        location.rocks.Remove(rockPos);
        location.RenderRock(rockPos);
        Check(!location.IsBlockedAtWorld(15, 10), "Destroying a rock releases its footprint.");
    }

    private static void CheckFootContact()
    {
        // Check the requested point against the real idle art, not just the collision implementation.
        var body = Read("Assets/Resources/Sprites/Farmer/Base/base_animations.png");
        var pixels = body.GetPixels32();
        foreach (Direction facing in Enum.GetValues(typeof(Direction)))
        {
            var expected = IdleFootFromArt(pixels, body.width, body.height, facing);
            var probe = FarmerBodyCollision.IdleFootProbe(facing);
            Check((probe - expected).sqrMagnitude < 0.0000001f, facing + " uses the requested idle foot edge.");
        }
        UnityEngine.Object.DestroyImmediate(body);

        var location = Location();
        var texture = new Texture2D(64, 1);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 64, 1), Vector2.zero, 64);
        var cache = (Dictionary<Sprite, ObjectCollisionMasks.Mask>)typeof(ObjectCollisionMasks)
            .GetField("cache", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        cache[sprite] = new ObjectCollisionMasks.Mask { sprite = sprite, width = 64, height = 1,
            pivot = Vector2.zero, pixelsPerUnit = 64, bits = Enumerable.Repeat((byte)255, 8).ToArray() };
        var sr = Renderer(sprite, new Vector2(10, 10.25f));
        Call(location, "RegisterObjectCollision", sr, new RectInt(10, 10, 1, 1), true, null, null);
        var manager = new GameObject("Foot test manager").AddComponent<GameManager>();
        typeof(GameManager).GetProperty("CurrentLocation").SetValue(manager, location);
        var player = new GameObject("Foot test player").AddComponent<PlayerController>();
        Set(player, "_game", manager);
        player.facing = Direction.Up;
        player.transform.position = new Vector3(10.5f, 9.5f, 0);
        Call(player, "TryMove", new Vector2(0, 2));
        float foot = player.transform.position.y + FarmerBodyCollision.IdleFootProbe(Direction.Up).y;
        Check(foot < 10.25f && foot >= 10.25f - 1f / 64f, "Walking up stops at the painted line with visible feet, not the old anchor 8px above.");
        player.facing = Direction.Down;
        player.transform.position = new Vector3(10.5f, 12, 0);
        Call(player, "TryMove", new Vector2(0, -2));
        foot = player.transform.position.y + FarmerBodyCollision.IdleFootProbe(Direction.Down).y;
        Check(foot >= 10.25f + 1f / 64f && foot < 10.25f + 2f / 64f, "Walking down also stops with feet at the upper mask edge.");

        // A one-pixel obstacle makes the horizontal coordinate observable.  The player moves
        // upward while retaining its side-facing idle art, which is the case that regressed.
        var pointTexture = new Texture2D(1, 1);
        var pointSprite = Sprite.Create(pointTexture, new Rect(0, 0, 1, 1), Vector2.zero, 64);
        cache[pointSprite] = new ObjectCollisionMasks.Mask { sprite = pointSprite, width = 1, height = 1,
            pivot = Vector2.zero, pixelsPerUnit = 64, bits = new byte[] { 1 } };
        const float pointX = 15.5f + 0.5f / 64f;
        const float pointY = 10.25f;
        var point = Renderer(pointSprite, new Vector2(15.5f, pointY));
        Call(location, "RegisterObjectCollision", point, new RectInt(15, 10, 1, 1), true, null, null);
        foreach (var side in new[] { Direction.Right, Direction.Left })
        {
            var sideFoot = FarmerBodyCollision.IdleFootProbe(side);
            player.facing = side;
            player.transform.position = new Vector3(pointX - sideFoot.x, 9.5f - sideFoot.y, 0);
            Call(player, "TryMove", new Vector2(0, 2));
            foot = player.transform.position.y + sideFoot.y;
            Check(foot < pointY && foot >= pointY - 1f / 64f,
                side + " facing upward movement stops at that side's idle foot edge.");
        }

        // Any direction of painted-mask contact draws the player over that object.
        Vector2 renderFoot = FarmerBodyCollision.IdleFootProbe(Direction.Up);
        player.facing = Direction.Up;
        player.transform.position = new Vector3(pointX - renderFoot.x, pointY - 0.1f - renderFoot.y, 0);
        Call(player, "TryMove", Vector2.up * 0.2f);
        int forcedOrder = (int)typeof(PlayerController).GetField("_maskFrontOrder", Private).GetValue(player);
        Check(forcedOrder > point.sortingOrder, "Bottom-to-top painted-mask contact brings the player in front of the object.");
        Call(player, "LateUpdate");
        forcedOrder = (int)typeof(PlayerController).GetField("_maskFrontOrder", Private).GetValue(player);
        Check(forcedOrder > point.sortingOrder, "Idle at a painted-mask contact keeps the player in front of the object.");
        player.transform.position = new Vector3(17f, 17f, 0f);
        Call(player, "TryMove", Vector2.right * 0.1f);
        forcedOrder = (int)typeof(PlayerController).GetField("_maskFrontOrder", Private).GetValue(player);
        Check(forcedOrder == int.MinValue, "Walking away from a painted mask returns to normal depth sorting.");
        renderFoot = FarmerBodyCollision.IdleFootProbe(Direction.Down);
        player.facing = Direction.Down;
        player.transform.position = new Vector3(pointX - renderFoot.x, pointY + 0.1f - renderFoot.y, 0);
        Call(player, "TryMove", Vector2.down * 0.2f);
        forcedOrder = (int)typeof(PlayerController).GetField("_maskFrontOrder", Private).GetValue(player);
        Check(forcedOrder > point.sortingOrder, "Top-to-bottom painted-mask contact also brings the player in front of the object.");

        // Old saves could contain a player that entered through the former one-point gap.
        // It may move out or along an edge only when each step does not add painted sole pixels.
        Vector2 escapeFoot = FarmerBodyCollision.IdleFootProbe(Direction.Right);
        player.facing = Direction.Right;
        player.transform.position = new Vector3(pointX - escapeFoot.x, pointY + 0.001f - escapeFoot.y, 0);
        Call(player, "TryMove", Vector2.down * 0.1f);
        Check(player.transform.position.y + escapeFoot.y < pointY,
            "A player already inside a painted mask can leave it instead of becoming trapped.");

        // While recovering from an old overlap, moving along a wide mask edge must stay possible.
        Vector2 slideFoot = FarmerBodyCollision.IdleFootProbe(Direction.Right);
        player.facing = Direction.Right;
        player.transform.position = new Vector3(10.5f - slideFoot.x, 10.251f - slideFoot.y, 0);
        float slideStart = player.transform.position.x;
        Call(player, "TryMove", Vector2.right * 0.1f);
        Check(player.transform.position.x > slideStart + 0.05f,
            "A player touching a painted mask can slide along it instead of becoming trapped.");

        var fallback = Renderer(null, new Vector2(20, 20));
        Call(location, "RegisterObjectCollision", fallback, new RectInt(20, 20, 1, 1), true, null, null);
        Check((bool)Call(location, "IsPlayerBlockedByNonCliffAtWorld", 20f, 20.49f, Direction.Right), "Unpainted fallback keeps old rectangular boundary.");
        Check(!(bool)Call(location, "IsPlayerBlockedByNonCliffAtWorld", 20f, 20.51f, Direction.Right), "Foot adjustment must not enlarge legacy rectangles.");
        results.Add("Painted masks stop at the requested idle foot edge from above and below; old rectangles unchanged.");
    }

    private static void CheckMaskDeletion()
    {
        const string path = "Assets/Resources/Sprites/Environment/Wood_spring_collision.png";
        var data = File.ReadAllBytes(path);
        try
        {
            File.Delete(path); // Isolated copy only.
            ObjectCollisionBaker.Rebuild();
            var catalog = AssetDatabase.LoadAssetAtPath<ObjectCollisionMasks>(ObjectCollisionBaker.OutputPath);
            Check(catalog.masks.Count(mask => mask.sprite != null && mask.sprite.name.StartsWith("Wood_spring")) == 4,
                "Deleting companion restores all four Wood source silhouettes.");
        }
        finally { File.WriteAllBytes(path, data); ObjectCollisionBaker.Rebuild(); }
    }

    private static void CheckLayerBuilds(Sprite[] sprites)
    {
        foreach (string layerName in new[] { "Objects_Valley", "object_Valley", "Fixed_Valley", "fixed_1_Valley" })
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var grid = new GameObject("Grid", typeof(Grid));
            var groundGo = new GameObject("Location_Valley", typeof(Tilemap));
            groundGo.transform.SetParent(grid.transform, false);
            var ground = groundGo.GetComponent<Tilemap>();
            var groundTile = ScriptableObject.CreateInstance<Tile>();
            groundTile.sprite = sprites[0];
            ground.SetTile(Vector3Int.zero, groundTile);
            ground.SetTile(new Vector3Int(39, 39, 0), groundTile);
            var objectGo = new GameObject(layerName, typeof(Tilemap));
            objectGo.transform.SetParent(grid.transform, false);
            var map = objectGo.GetComponent<Tilemap>();
            for (int i = 0; i < 4; i++)
            {
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = sprites[i].name;
                tile.sprite = sprites[i];
                map.SetTile(new Vector3Int(4 + 8 * i, 10, 0), tile);
            }
            // Arbitrary tile asset names must also retain the sprite's collision mask.
            var custom = ScriptableObject.CreateInstance<Tile>();
            custom.name = "UserPaintedLargeLog";
            custom.sprite = sprites[3];
            map.SetTile(new Vector3Int(10, 25, 0), custom);
            var location = new GameObject("Location").AddComponent<GameLocation>();
            location.id = LocationId.Valley;
            location.Build(new GameData());
            var renderers = location.FeatureRoot.GetComponentsInChildren<SpriteRenderer>();
            Check(renderers.Length == 5, layerName + " must place known and arbitrary sprite tiles.");
            var visualPlayer = new GameObject("Visual depth player").AddComponent<PlayerController>();
            int painted = 0;
            foreach (var renderer in renderers)
            {
                if (ObjectCollisionMasks.Find(renderer.sprite) == null) continue;
                Check(renderer.GetComponentsInChildren<SpriteRenderer>(true).Length == 1 &&
                    renderer.maskInteraction == SpriteMaskInteraction.None,
                    renderer.sprite.name + " keeps one complete renderer for vertical depth bands.");
                CheckVisualDepthBands(location, renderer, visualPlayer);
                CheckSpriteWorld(location, renderer);
                CheckPlayerCrossing(location, renderer);
                painted++;
            }
            UnityEngine.Object.DestroyImmediate(visualPlayer.gameObject);
            Check(painted == 5, layerName + " must use keyed paint or opaque silhouettes for every object slice.");
            results.Add(layerName + ": full Build pipeline, five silhouette/keyed-mask objects, player crossing blocked.");
        }
    }

    private static void CheckVisualDepthBands(GameLocation location, SpriteRenderer renderer, PlayerController player)
    {
        var collision = ObjectCollisionMasks.Find(renderer.sprite);
        int paintedRow = Enumerable.Range(0, collision.height).FirstOrDefault(collision.HasPaintedRow);
        int unpaintedRow = Enumerable.Range(0, collision.height).FirstOrDefault(y => !collision.HasPaintedRow(y));
        Check(collision.HasPaintedRow(paintedRow) && !collision.HasPaintedRow(unpaintedRow),
            renderer.sprite.name + " has both painted and unpainted vertical bands for depth testing.");
        int normalOrder = renderer.sortingOrder;
        player.transform.position = new Vector3(renderer.transform.position.x,
            renderer.transform.position.y + (unpaintedRow + 0.5f - collision.pivot.y) / collision.pixelsPerUnit -
            FarmerBodyCollision.IdleFootProbe(player.facing).y, 0);
        int foreground = (int)Call(location, "PlayerForegroundMaskSortingOrderAtWorld", player.transform.position.x,
            player.transform.position.y + FarmerBodyCollision.IdleFootProbe(player.facing).y);
        if (collision.usesKeyedPaint)
            Check(foreground == normalOrder, renderer.sprite.name + " covers the player in an unpainted keyed Y band.");
        else
            Check(foreground == int.MinValue, renderer.sprite.name + " keeps its object order fixed without keyed paint.");
        Check(renderer.sortingOrder == normalOrder, renderer.sprite.name + " never changes its own order for player depth.");
        player.transform.position = new Vector3(renderer.transform.position.x,
            renderer.transform.position.y + (paintedRow + 0.5f - collision.pivot.y) / collision.pixelsPerUnit -
            FarmerBodyCollision.IdleFootProbe(player.facing).y, 0);
        foreground = (int)Call(location, "PlayerForegroundMaskSortingOrderAtWorld", player.transform.position.x,
            player.transform.position.y + FarmerBodyCollision.IdleFootProbe(player.facing).y);
        Check(renderer.sortingOrder == normalOrder,
            renderer.sprite.name + " returns to normal depth in a painted Y band.");
        Check(foreground == int.MinValue, renderer.sprite.name + " does not cover the player in a painted Y band.");
        float belowFootY = renderer.transform.position.y + (-0.5f - collision.pivot.y) / collision.pixelsPerUnit;
        player.transform.position = new Vector3(renderer.transform.position.x,
            belowFootY - FarmerBodyCollision.IdleFootProbe(player.facing).y, 0);
        foreground = (int)Call(location, "PlayerForegroundMaskSortingOrderAtWorld", player.transform.position.x,
            player.transform.position.y + FarmerBodyCollision.IdleFootProbe(player.facing).y);
        Check(renderer.sortingOrder == normalOrder && foreground == int.MinValue,
            renderer.sprite.name + " stays at its fixed order below every mask row.");
    }

    private static void CheckPlayerCrossing(GameLocation location, SpriteRenderer renderer)
    {
        var mask = ObjectCollisionMasks.Find(renderer.sprite);
        Vector2 solid = SolidPoint(renderer);
        float left = renderer.transform.position.x - mask.pivot.x / mask.pixelsPerUnit - 0.25f;
        float right = left + mask.width / mask.pixelsPerUnit + 0.5f;
        var manager = new GameObject("Crossing manager").AddComponent<GameManager>();
        typeof(GameManager).GetProperty("CurrentLocation").SetValue(manager, location);
        var player = new GameObject("Crossing player").AddComponent<PlayerController>();
        Set(player, "_game", manager);
        player.facing = Direction.Right;
        Vector2 foot = FarmerBodyCollision.IdleFootProbe(Direction.Right);
        player.transform.position = new Vector3(left - foot.x, solid.y - foot.y, 0);
        Call(player, "TryMove", new Vector2(right - left, 0));
        Check(player.transform.position.x + foot.x < solid.x, "Player cannot cross the painted row on " + renderer.sprite.name);

        // The large Wood_spring_3 trunk is approached this way in play: from below while
        // retaining a side-facing sprite. Check every painted column so an edge or hollow
        // cannot let a player cross the painted lower trunk.
        foreach (var facing in new[] { Direction.Right, Direction.Left, Direction.Up })
        {
            foot = FarmerBodyCollision.IdleFootProbe(facing);
            for (int x = 0; x < mask.width; x++)
            {
                int bottom = -1;
                for (int y = 0; y < mask.height; y++)
                    if (mask.Contains(x, y)) { bottom = y; break; }
                if (bottom < 0) continue;

                Vector2 point = PixelPoint(renderer, x, bottom);
                float surface = point.y - 0.5f / mask.pixelsPerUnit;
                player.facing = facing;
                player.transform.position = new Vector3(point.x - foot.x, surface - 0.1f - foot.y, 0f);
                Call(player, "TryMove", Vector2.up * 3f);
                Check(player.transform.position.y + foot.y < surface + 0.0001f,
                    renderer.sprite.name + " blocks bottom-to-top movement at every painted column while facing " + facing);
            }
        }
        UnityEngine.Object.DestroyImmediate(player.gameObject);
        UnityEngine.Object.DestroyImmediate(manager.gameObject);
    }

    private static Vector2 IdleFootFromArt(Color32[] pixels, int textureWidth, int textureHeight, Direction facing)
    {
        var clip = FarmerPoses.Get(FarmerAnim.Idle, facing);
        int bottom = textureHeight - (clip.row + 1) * FarmerPoses.FrameHeight;
        int footRow = FarmerPoses.FrameHeight, left = FarmerPoses.FrameWidth, right = -1;
        for (int y = 0; y < FarmerPoses.FrameHeight; y++)
            for (int x = 0; x < FarmerPoses.FrameWidth; x++)
            {
                int sourceX = FarmerPoses.Mirrored(facing) ? FarmerPoses.FrameWidth - 1 - x : x;
                if (pixels[(bottom + y) * textureWidth + sourceX].a == 0) continue;
                if (y < footRow) { footRow = y; left = right = x; }
                else if (y == footRow) { left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
            }
        float xPoint = facing == Direction.Right ? right + 1.001f
            : facing == Direction.Down ? (left + right + 1f) * 0.5f : left - 0.001f;
        return new Vector2((xPoint - FarmerPoses.FrameWidth * 0.5f) / FarmerPoses.PixelsPerUnit,
            (footRow - FarmerPoses.FrameHeight * 0.5f - 0.001f) / FarmerPoses.PixelsPerUnit + FarmerPoses.BodyOffsetY);
    }
}
