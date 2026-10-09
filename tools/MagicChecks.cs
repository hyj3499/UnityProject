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
        var types = new[] { MagicType.Water, MagicType.Wind, MagicType.Rock, MagicType.Shovel, MagicType.Blade };
        var counts = new[] { 13, 9, 7, 8, 10 };
        var root = new GameObject("FX checks");
        for (int n = 0; n < types.Length; n++)
        {
            var frames = MagicEffect.Frames(types[n]);
            Check(frames.Length == counts[n], "Correct frame count: " + types[n]);
            for (int i = 0; i < frames.Length; i++)
                Check(frames[i].rect == new Rect(i * 64, 0, 64, 64), "Left-to-right frame slicing");
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

        var tiles = new List<Vector2Int>();
        MagicCharge.FillTiles(player.FacingTile(), player.facing, 5, tiles);
        foreach (var tile in tiles) location.hoeDirts[tile] = new HoeDirt(tile.x, tile.y);
        location.hoeDirts[tiles[0]].watered = true;
        Check(manager.CastMagicOnFacingArea(player, 5), "Full area cast succeeds");
        Check(location.hoeDirts.Values.All(d => d.watered), "All 54 tiles watered together");
        Check(data.farmer.mp == 1000 - 53 * MagicSystem.Water.mpCost, "Only valid targets consume MP");
        Check(root.GetComponentsInChildren<MagicEffect>().Length == 53, "One effect for every changed tile");
        Check(root.GetComponentsInChildren<MagicEffect>().All(f =>
            f.GetComponent<SpriteRenderer>().sprite == MagicEffect.Frames(MagicType.Water)[0]), "Simultaneous first frames");
        int mp = data.farmer.mp;
        Check(!manager.CastMagicOnFacingArea(player, 5) && data.farmer.mp == mp, "Invalid area costs nothing");

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
