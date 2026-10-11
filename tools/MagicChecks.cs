// Run with Check-Magic.ps1 in an isolated Unity project; never loads or writes a save.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FarmMVP;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MagicChecks
{
    private static int checks;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        try
        {
            if (!File.Exists("magic-validation-project")) throw new Exception("Isolated project required.");
            CheckRanges();
            CheckSheets();
            CheckCasts();
            CheckTargetEffects();
            if (File.Exists("render-preview")) RenderPreview();
            File.WriteAllText("result.txt", $"PASS: {checks} magic assertions");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText("result.txt", "FAIL: " + error);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckRanges()
    {
        var counts = new[] { 1, 3, 9, 18, 36, 54 };
        var origin = new Vector2Int(20, 20);
        foreach (Direction direction in Enum.GetValues(typeof(Direction)))
        {
            var previous = new HashSet<Vector2Int>();
            var forward = MagicCharge.Forward(direction);
            for (int stage = 0; stage < counts.Length; stage++)
            {
                var tiles = new List<Vector2Int>();
                MagicCharge.FillTiles(origin + forward, direction, stage, tiles);
                Check(tiles.Count == counts[stage] && tiles.Distinct().Count() == counts[stage], "Unique area size");
                Check(previous.IsSubsetOf(tiles), "Growing charge preserves earlier tiles");
                Check(tiles.All(p => Vector2.Dot(p - origin, forward) >= 1), "All tiles in front of player");
                Check(!tiles.Contains(origin), "Never targets the player tile");
                previous = new HashSet<Vector2Int>(tiles);
            }
        }
        Check(MagicCharge.Stage(0.49f, MagicType.Water) == 0, "Before first threshold");
        Check(MagicCharge.Stage(0.5f, MagicType.Water) == 1, "At first threshold");
        Check(MagicCharge.Stage(2.5f, MagicType.Wind) == 5, "Full charge at 2.5 seconds");
        Check(MagicCharge.Stage(100, MagicType.Wind) == 5, "Charge capped");
        Check(MagicCharge.Stage(100, MagicType.Fishing) == 0, "Fishing remains single tile");
    }

    private static void CheckSheets()
    {
        var counts = new[] { 13, 9, 7, 8, 14, 10, 12, 12, 13, 15 };
        var widths = new[] { 64, 64, 64, 64, 48, 64, 80, 128, 64, 128 };
        var heights = new[] { 64, 64, 64, 64, 48, 64, 48, 128, 64, 64 };
        var root = new GameObject("FX checks");
        for (int n = 0; n < counts.Length; n++)
        {
            var frames = MagicEffect.Frames(n + 1);
            Check(frames.Length == counts[n], "Correct frame count: magic" + (n + 1));
            for (int i = 0; i < frames.Length; i++)
                Check(frames[i].rect == new Rect(i * widths[n], 0, widths[n], heights[n]), "Left-to-right frame slicing");
            Check(frames[0].texture.filterMode == FilterMode.Point, "Pixel filtering");
            Check(frames[0].texture.mipmapCount == 1, "No mipmaps");
        }
        foreach (Direction direction in Enum.GetValues(typeof(Direction)))
        {
            MagicEffect.Play(root.transform, MagicType.Blade, Vector3.zero, 4, direction);
            var effect = root.GetComponentsInChildren<MagicEffect>().Last();
            var actual = effect.transform.right;
            var expected = MagicCharge.Forward(direction);
            Check(Vector2.Distance(actual, expected) < 0.001f, "Blade points in cast direction");
            foreach (var sprite in MagicEffect.Frames(MagicType.Blade))
            {
                Check(Vector2.Distance(sprite.pivot, new Vector2(40, 8)) < 0.001f, "magic5 anchors the bottom-right 16x16 cell");
                Vector2 localHit = (new Vector2(40, 8) - sprite.pivot) / sprite.pixelsPerUnit;
                Check(Vector3.Distance(effect.transform.TransformPoint(localHit), effect.transform.position) < 0.001f,
                    "magic5 hit point stays on target in every direction and frame");
                Check(sprite.pixelsPerUnit == 32, "magic5 uses the restored default effect scale");
                var corner = effect.transform.TransformPoint((new Vector2(32, 0) - sprite.pivot) / sprite.pixelsPerUnit);
                Check(Mathf.Abs(Mathf.Abs(corner.x) - 0.25f) < 0.001f && Mathf.Abs(Mathf.Abs(corner.y) - 0.25f) < 0.001f,
                    "magic5 bottom-right cell center remains on target after rotation");
            }
            typeof(MagicEffect).GetField("_elapsed", Private).SetValue(effect, 0.25f);
            typeof(MagicEffect).GetMethod("Update", Private).Invoke(effect, null);
            Check(effect.GetComponent<SpriteRenderer>().sprite != MagicEffect.Frames(MagicType.Blade)[0], "Animation advances");
        }
        MagicEffect.Play(root.transform, MagicType.Water, Vector3.zero, 3, Direction.Up);
        var renderers = root.GetComponentsInChildren<SpriteRenderer>();
        Check(renderers.Last().sortingOrder > renderers[0].sortingOrder, "Front effect covers back effect");
        Check(renderers.Last().sortingOrder > Depth.YSort(3), "Effect covers its own target");
        Check(renderers.Last().sortingOrder < Depth.YSort(2), "Effect stays behind next row");
        Object.DestroyImmediate(root);
    }

    private static void CheckTargetEffects()
    {
        foreach (DebrisKind kind in Enum.GetValues(typeof(DebrisKind)))
        {
            for (int variant = 0; variant < 4; variant++)
            {
                var root = new GameObject("Target effect checks");
                var location = root.AddComponent<GameLocation>();
                location.width = location.height = 20;
                typeof(GameLocation).GetField("_featureRoot", Private).SetValue(location, root.transform);
                typeof(GameLocation).GetField("_blocked", Private).SetValue(location, new bool[20, 20]);
                var def = DebrisDatabase.Get(kind, variant);
                var tile = new Vector2Int(10, 11);
                typeof(GameLocation).GetMethod("AddDebris", Private).Invoke(location, new object[] { tile.x, tile.y, def, 10 });
                var type = kind == DebrisKind.Wood ? MagicType.Blade : MagicType.Rock;
                int sheet = kind == DebrisKind.Wood ? (variant < 2 ? 5 : variant == 2 ? 9 : 10)
                    : (variant < 2 ? 3 : variant == 2 ? 7 : 8);
                Check(location.MagicEffectSheet(type, tile) == sheet, "Target variant selects magic" + sheet);
                var farCell = tile + def.size - Vector2Int.one;
                Check(location.MagicEffectSheet(type, farCell) == sheet, "Every footprint cell selects same effect");
                Check(location.MagicEffectPosition(type, farCell) == location.MagicEffectPosition(type, tile),
                    "Every footprint cell hits same object position");

                var manager = root.AddComponent<GameManager>();
                var playerGo = new GameObject("Player");
                playerGo.transform.SetParent(root.transform);
                var player = playerGo.AddComponent<PlayerController>();
                player.transform.position = new Vector3(10, 10, 0);
                player.facing = Direction.Up;
                typeof(GameManager).GetProperty("Data").SetValue(manager, new GameData());
                typeof(GameManager).GetProperty("CurrentLocation").SetValue(manager, location);
                typeof(GameManager).GetProperty("Player").SetValue(manager, player);
                manager.SelectMagic(type);
                var hitPosition = location.MagicEffectPosition(type, tile);
                Check(manager.CastMagicOnFacingArea(player, 2), "Target effect cast succeeds");
                var effects = root.GetComponentsInChildren<MagicEffect>();
                Check(effects.Length == 1, "One effect per object even across multiple target tiles");
                Check(effects[0].GetComponent<SpriteRenderer>().sprite == MagicEffect.Frames(sheet)[0],
                    "Cast renders chosen sheet, independent of cached effects of same magic type");
                Check(effects[0].transform.position == hitPosition, "Effect is anchored to target object");
                if (sheet == 9 || sheet == 10)
                {
                    Check(effects[0].transform.rotation == Quaternion.identity, "Rising lightning stays upright");
                    var objectRenderers = (Dictionary<Vector2Int, SpriteRenderer>)typeof(GameLocation)
                        .GetField("_debrisRenderers", Private).GetValue(location);
                    Check(Mathf.Abs(effects[0].GetComponent<SpriteRenderer>().bounds.min.y - objectRenderers[tile].bounds.min.y) < 0.001f,
                        "Lightning canvas bottom matches actual object bottom");
                }
                if (sheet >= 7)
                {
                    Vector2 objectSize = def.GetSprite().bounds.size;
                    Vector2 effectSize = effects[0].GetComponent<SpriteRenderer>().bounds.size;
                    float expectedMultiplier = sheet == 7 ? 2f : 1f;
                    Check(effectSize.x >= objectSize.x * expectedMultiplier && effectSize.y >= objectSize.y * expectedMultiplier,
                        "Effect covers requested object dimensions");
                    Check(Mathf.Approximately(effects[0].transform.localScale.x, effects[0].transform.localScale.y),
                        "Object fitting preserves effect aspect ratio");
                }
                Object.DestroyImmediate(root);
            }
        }

        var ground = new GameObject("Earth and trees checks");
        var map = ground.AddComponent<GameLocation>();
        typeof(GameLocation).GetField("_featureRoot", Private).SetValue(map, ground.transform);
        typeof(GameLocation).GetField("_blocked", Private).SetValue(map, new bool[20, 15]);
        var target = new Vector2Int(5, 6);
        map.dugTiles.Add(target);
        Check(MagicSystem.TryApply(MagicType.Earth, map, target.x, target.y, out _), "Earth tills target");
        Check(map.MagicEffectSheet(MagicType.Earth, target) == 6, "Earth selects magic6");
        foreach (var type in new[] { MagicType.Water, MagicType.Wind, MagicType.Shovel, MagicType.Earth })
        {
            var position = map.MagicEffectPosition(type, target);
            Check(position == new Vector3(5, 6, 0), "Ground magic originates at tile center");
            MagicEffect.Play(ground.transform, type, position, target.y, Direction.Up);
            var effect = ground.GetComponentsInChildren<MagicEffect>().Last();
            Check(Mathf.Abs(effect.GetComponent<SpriteRenderer>().bounds.min.y - target.y) < 0.001f,
                "Ground effect emission line passes through tile center");
        }
        foreach (var tree in TreeDatabase.All)
        {
            map.trees[target] = new TreeFeature(target.x, target.y, tree.treeId, TreeDatabase.FullGrowth);
            Check(map.MagicEffectSheet(MagicType.Blade, target) == 9, "Every tree type uses magic9");
            Check(map.MagicEffectSize(MagicType.Blade, target) == new Vector2(4, 4), "Trees use uniform 64x64 pixel effect");
            var treePosition = map.MagicEffectPosition(MagicType.Blade, target);
            Check(treePosition == new Vector3(5, 5.5f, 0), "Tree lightning starts at foot of tile");
            MagicEffect.Play(ground.transform, MagicType.Blade, treePosition, target.y, Direction.Up, 9,
                map.MagicEffectSize(MagicType.Blade, target));
            var treeEffect = ground.GetComponentsInChildren<MagicEffect>().Last().GetComponent<SpriteRenderer>();
            Check(Vector2.Distance(treeEffect.bounds.size, new Vector2(4, 4)) < 0.001f, "Rendered tree effect remains 64x64 at world PPU");
            map.trees[target].isStump = true;
            Check(map.MagicEffectSheet(MagicType.Blade, target) == 9, "Tree stump retains magic9");
        }
        // Last-hit destruction removes the target data; the cast must capture its sheet beforehand.
        map.trees.Clear();
        Check(map.MagicEffectSheet(MagicType.Blade, target) == 5, "Missing tree falls back after removal");
        Object.DestroyImmediate(ground);
    }

    // A real Unity render for checking scale/alignment with the supplied object artwork.
    private static void RenderPreview()
    {
        var root = new GameObject("Effect alignment preview");
        var location = root.AddComponent<GameLocation>();
        location.width = 60;
        location.height = 30;
        typeof(GameLocation).GetField("_featureRoot", Private).SetValue(location, root.transform);
        typeof(GameLocation).GetField("_blocked", Private).SetValue(location, new bool[60, 30]);
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 6; col++)
            {
                var tile = new Vector2Int(3 + col * 8, 3 + row * 9);
                var type = col < 2 ? MagicType.Rock : MagicType.Blade;
                if (col == 5)
                {
                    location.trees[tile] = new TreeFeature(tile.x, tile.y, TreeDatabase.DefaultTreeId, TreeDatabase.FullGrowth);
                    location.RenderTree(tile);
                }
                else
                {
                    var kind = col < 2 ? DebrisKind.Stone : DebrisKind.Wood;
                    int variant = col < 4 ? 2 + col % 2 : 0;
                    typeof(GameLocation).GetMethod("AddDebris", Private).Invoke(location,
                        new object[] { tile.x, tile.y, DebrisDatabase.Get(kind, variant), 10 });
                }
                if (row == 1)
                {
                    int sheet = location.MagicEffectSheet(type, tile);
                    MagicEffect.Play(root.transform, type, location.MagicEffectPosition(type, tile), tile.y,
                        Direction.Right, sheet, location.MagicEffectSize(type, tile));
                    root.GetComponentsInChildren<MagicEffect>().Last().GetComponent<SpriteRenderer>().sprite = MagicEffect.Frames(sheet)[6];
                }
            }
        }
        var camera = new GameObject("Preview Camera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 11;
        camera.transform.position = new Vector3(24, 10, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.22f, 0.25f);
        var output = new RenderTexture(1920, 880, 24);
        camera.targetTexture = output;
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = output;
        var capture = new Texture2D(1920, 880, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0, 0, 1920, 880), 0, 0);
        capture.Apply();
        File.WriteAllBytes("effect-alignment.png", capture.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.DestroyImmediate(capture);
        Object.DestroyImmediate(output);
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(root);
    }

    private static void CheckCasts()
    {
        var root = new GameObject("Cast checks");
        var location = root.AddComponent<GameLocation>();
        location.width = location.height = 20;
        typeof(GameLocation).GetField("_featureRoot", Private).SetValue(location, root.transform);
        typeof(GameLocation).GetField("_blocked", Private).SetValue(location, new bool[20, 20]);
        var manager = root.AddComponent<GameManager>();
        var playerGo = new GameObject("Player");
        playerGo.transform.SetParent(root.transform);
        var player = playerGo.AddComponent<PlayerController>();
        player.transform.position = new Vector3(10, 10, 0);
        player.facing = Direction.Up;
        var data = new GameData();
        Check(data.farmer.maxMp == FarmerData.DefaultMaxMp && data.farmer.mp == FarmerData.DefaultMaxMp,
            "New farmer starts with a full 10,000 MP pool");
        typeof(GameManager).GetProperty("Data").SetValue(manager, data);
        typeof(GameManager).GetProperty("CurrentLocation").SetValue(manager, location);
        typeof(GameManager).GetProperty("Player").SetValue(manager, player);
        manager.SelectMagic(MagicType.Water);
        data.farmer.mp = 1000;
        int minutesBeforeCasting = data.currentMinutes;
        var timeAccum = typeof(GameManager).GetField("_timeAccum", Private);
        timeAccum.SetValue(manager, 9.5f);

        var tiles = new List<Vector2Int>();
        MagicCharge.FillTiles(player.FacingTile(), player.facing, 5, tiles);
        foreach (var tile in tiles) location.hoeDirts[tile] = new HoeDirt(tile.x, tile.y);
        location.hoeDirts[tiles[0]].watered = true;
        Check(manager.CastMagicOnFacingArea(player, 5), "Full area cast succeeds");
        Check(location.hoeDirts.Values.All(d => d.watered), "All 54 tiles watered together");
        Check(data.farmer.mp == 1000 - 53 * MagicSystem.Water.mpCost, "Only valid targets consume MP");
        Check(root.GetComponentsInChildren<MagicEffect>().Length == 53, "One effect for every changed tile");
        Check(data.currentMinutes == minutesBeforeCasting && (float)timeAccum.GetValue(manager) == 9.5f,
            "Charged cast does not advance the clock or accumulate hidden time costs");
        Check(root.GetComponentsInChildren<MagicEffect>().All(f =>
            f.GetComponent<SpriteRenderer>().sprite == MagicEffect.Frames(MagicType.Water)[0]), "Simultaneous first frames");
        int mp = data.farmer.mp;
        Check(!manager.CastMagicOnFacingArea(player, 5) && data.farmer.mp == mp, "Invalid area costs nothing");
        Check(data.currentMinutes == minutesBeforeCasting && (float)timeAccum.GetValue(manager) == 9.5f,
            "Invalid cast preserves clock progress");

        foreach (var dirt in location.hoeDirts.Values) dirt.watered = false;
        data.farmer.mp = 5;
        Check(manager.CastMagicOnFacingArea(player, 5), "Partial cast succeeds with insufficient MP");
        Check(location.hoeDirts[new Vector2Int(10, 11)].watered && location.hoeDirts[new Vector2Int(9, 11)].watered,
            "Partial cast starts with nearest targets");
        Check(location.hoeDirts.Values.Count(d => d.watered) == 2 && data.farmer.mp == 1,
            "Partial cast applies only affordable targets and retains remainder MP");
        data.farmer.mp = 1000;
        manager.Paused = true;
        Check(!manager.CastMagicOnFacingArea(player, 5), "Paused game rejects cast");
        manager.Paused = false;

        location.hoeDirts.Clear();
        var crop = new Crop("carrot");
        crop.growthStage = crop.Def.MaxGrowthStage;
        location.hoeDirts[new Vector2Int(10, 11)] = new HoeDirt(10, 11) { crop = crop };
        Check(MagicSystem.CanApply(MagicType.Wind, location, 10, 11), "Wind previews ripe crop");
        Check(MagicSystem.TryApply(MagicType.Wind, location, 10, 11, out var drop) && drop != null, "Wind harvest returns loot");
        Check(!location.hoeDirts[new Vector2Int(10, 11)].HasCrop, "Single harvest crop removed");
        location.hoeDirts[new Vector2Int(10, 11)].crop = new Crop("carrot");
        Check(!MagicSystem.CanApply(MagicType.Wind, location, 10, 11), "Unripe crop protected");

        var bigRock = DebrisDatabase.Get(DebrisKind.Stone, 2);
        typeof(GameLocation).GetMethod("AddDebris", Private).Invoke(location, new object[] { 9, 11, bigRock, 10 });
        manager.SelectMagic(MagicType.Rock);
        mp = data.farmer.mp;
        Check(manager.CastMagicOnFacingArea(player, 2), "Multi-tile rock cast succeeds");
        Check(location.debris[new Vector2Int(9, 11)].hp == 9, "Multi-tile rock hit exactly once");
        Check(data.farmer.mp == mp - MagicSystem.Rock.mpCost, "Multi-tile rock charged once");
        for (int click = 0; click < 4; click++)
            Check(manager.CastMagicOnFacingTile(player), "Repeated object hit succeeds");
        Check(location.debris[new Vector2Int(9, 11)].hp == 5, "Repeated clicks still damage the object");
        Check(data.farmer.mp == mp - 5 * MagicSystem.Rock.mpCost, "Each successful object hit still consumes MP");
        Check(data.currentMinutes == minutesBeforeCasting && (float)timeAccum.GetValue(manager) == 9.5f,
            "Repeated clicks and partial casts never advance time");
        typeof(GameManager).GetMethod("AdvanceTime", Private).Invoke(manager, new object[] { 0.5f });
        Check(data.currentMinutes == minutesBeforeCasting + DayClock.Step && (float)timeAccum.GetValue(manager) == 0f,
            "Natural clock still advances at the next ten-minute boundary");

        var indicator = TargetIndicator.Create();
        MagicCharge.FillTiles(Vector2Int.zero, Direction.Down, 5, tiles);
        indicator.ShowMagic(tiles, MagicType.Water, location);
        Check(indicator.GetComponentsInChildren<SpriteRenderer>().All(sr =>
            !sr.enabled || (sr.transform.position.x >= 0 && sr.transform.position.y >= 0)), "Preview clips map boundary");
        indicator.Show(Vector2Int.one, Color.green);
        Check(indicator.GetComponentsInChildren<SpriteRenderer>().Count(sr => sr.enabled) == 1, "Seed preview clears extra charge cells");
        indicator.Hide();
        Check(!indicator.gameObject.activeSelf, "Indicator hides on cancel");
        Object.DestroyImmediate(indicator.gameObject);
        Object.DestroyImmediate(root);
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
}
