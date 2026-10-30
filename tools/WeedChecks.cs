using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FarmMVP;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public static class WeedChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }
    private static object Call(GameLocation location, string method, params object[] args)
        => typeof(GameLocation).GetMethod(method, Private).Invoke(location, args);
    private static void Set(GameLocation location, string field, object value)
        => typeof(GameLocation).GetField(field, Private).SetValue(location, value);

    private static GameLocation Location(int size = 40)
    {
        var location = new GameObject("Weed check").AddComponent<GameLocation>();
        location.id = LocationId.Farm;
        location.width = location.height = size;
        Set(location, "_blocked", new bool[size, size]);
        Set(location, "_suspendPlantRender", true);
        var root = new GameObject("Features").transform;
        root.SetParent(location.transform);
        Set(location, "_featureRoot", root);
        return location;
    }

    public static void Run()
    {
        try
        {
            if (!File.Exists("weed-validation-project")) throw new Exception("Requires isolated project.");
            Seasons.SetSilently(1);
            CheckArt();
            CheckPatches();
            CheckNaturalDistribution();
            CheckSpreadAndPersistence();
            File.WriteAllText("result.txt", $"PASS: {checks} assertions (sprites, connected patches, exclusions, cap, spread, persistence, winter).");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("result.txt", "FAIL: " + ex);
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckArt()
    {
        Check(PlantArt.VariantCount("weed", Season.Spring) == 4, "Spring must load all four weed variants.");
        for (int i = 0; i < 4; i++)
        {
            var s = PlantArt.Stage("weed", i, Season.Spring);
            Check(s.name == "weed_spring_" + i, "Stable numeric slice ordering.");
            Check(s.rect == new Rect(i * 14, 0, 14, 10), "Slice must match the actual 56x10 PNG.");
            Check(s.pivot == new Vector2(7, 0) && s.pixelsPerUnit == 16, "Bottom-center pivot and tile scale.");
        }
        Check(PlantArt.VariantCount("weed", Season.Summer) == 3, "Legacy summer sheet.");
        Check(PlantArt.VariantCount("weed", Season.Fall) == 3, "Legacy fall sheet.");
        Check(PlantArt.Stage("weed", 0, Season.Winter) == null, "No winter fallback.");
        var location = Location();
        var plant = new PlantFeature(3, 4, PlantDatabase.WeedId, 3);
        location.plants.Add(new Vector2Int(3, 4), plant);
        Set(location, "_suspendPlantRender", false);
        location.RenderPlant(new Vector2Int(3, 4));
        var renderers = location.FeatureRoot.GetComponentsInChildren<SpriteRenderer>();
        Check(renderers.Length >= 1 && renderers.Length <= 6, "One to six tufts per tile.");
        Check(renderers.Select(r => r.sprite).Distinct().Count() == Math.Min(4, renderers.Length), "Mix available shapes without requiring every tile to be dense.");
        Check(plant.Tufts().SequenceEqual(plant.Tufts()), "Stable visual positions when redrawn.");
        var grass = new PlantFeature(3, 4, PlantDatabase.GrassId, 1);
        Check(grass.GetSprite(0) == grass.GetSprite(4), "Grass tufts retain their growth stage.");
        Seasons.SetSilently(29);
        Check(plant.GetSprite(0) != null && plant.GetSprite(4) != null, "Saved fourth variant remains visible in summer.");
        Seasons.SetSilently(1);
        UnityEngine.Object.DestroyImmediate(location.gameObject);
    }

    private static void CheckPatches()
    {
        for (int seed = 0; seed < 12; seed++)
        {
            Random.InitState(seed);
            var location = Location();
            int added = (int)Call(location, "GrowWeedPatch", new Vector2Int(20, 20), 32);
            Check(added == 32 && location.plants.Count == 32, "Patch meets its tile budget.");
            var reached = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(20, 20));
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (!location.plants.ContainsKey(p) || !reached.Add(p)) continue;
                foreach (var direction in Directions) queue.Enqueue(p + direction);
            }
            Check(reached.Count == 32, "Every tile connects by an edge to its patch.");
            UnityEngine.Object.DestroyImmediate(location.gameObject);
        }

        var field = Location(20);
        var path = new bool[20, 20];
        var water = new bool[20, 20];
        var cliff = new bool[20, 20];
        var tillable = new bool[20, 20];
        for (int x = 0; x < 20; x++)
            for (int y = 0; y < 20; y++)
            {
                path[x, y] = x == 10;
                water[x, y] = x == 11;
                cliff[x, y] = x == 12;
                tillable[x, y] = x != 13;
            }
        Set(field, "_path", path);
        Set(field, "_water", water);
        Set(field, "_cliff", cliff);
        Set(field, "_tillable", tillable);
        field.hoeDirts.Add(new Vector2Int(3, 3), new HoeDirt(3, 3));
        Call(field, "SetBlocked", 4, 4, true);
        field.AddExit(7, 7, LocationId.FarmHouse);
        Call(field, "Scatter", 0f, 0f, 1f, 0);
        Check(field.plants.Count > 200, "Populate available land densely.");
        foreach (var p in field.plants.Keys)
            Check(p.x != 10 && p.x != 11 && p.x != 12 && p.x != 13
                && p != new Vector2Int(3, 3) && p != new Vector2Int(4, 4)
                && (Mathf.Abs(p.x - 7) > 1 || Mathf.Abs(p.y - 7) > 1), "Respect paths, water, cliffs, tillable mask, soil, blockers and entrances.");
        UnityEngine.Object.DestroyImmediate(field.gameObject);

        var large = Location();
        Call(large, "Scatter", 0f, 0f, 1f, 0);
        Check(large.plants.Count == 400, "Location cap.");
        Call(large, "Scatter", 0f, 0f, 1f, 0);
        Check(large.plants.Count == 400, "Repeated seeding must not exceed cap.");
        UnityEngine.Object.DestroyImmediate(large.gameObject);
        var seasonal = Location();
        Call(seasonal, "Scatter", 0f, 0f, 0f, 0);
        Check(seasonal.plants.Count == 0, "Only the first farm generation and spring 1 mass generation add weeds.");
        UnityEngine.Object.DestroyImmediate(seasonal.gameObject);
        Seasons.SetSilently(85);
        var winter = Location();
        Call(winter, "Scatter", 0f, 0f, 1f, 0);
        Check(winter.plants.Count == 0, "Do not seed winter weeds.");
        UnityEngine.Object.DestroyImmediate(winter.gameObject);
        Seasons.SetSilently(1);
    }

    private static void CheckNaturalDistribution()
    {
        for (int seed = 0; seed < 24; seed++)
        {
            Random.InitState(seed);
            var location = Location();
            Call(location, "Scatter", 0f, 0f, 0.2f, 0);
            Check(location.plants.Count == 320, "Mixing layouts must preserve the tile budget.");
            int isolated = 0, loneTufts = 0, isolatedBunches = 0;
            foreach (var entry in location.plants)
            {
                bool neighbor = false;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if ((dx != 0 || dy != 0) && location.plants.ContainsKey(entry.Key + new Vector2Int(dx, dy))) neighbor = true;
                int count = entry.Value.Tufts().Length;
                Check(count >= 1 && count <= 6, "Tuft count stays in range.");
                Check(entry.Value.Tufts().SequenceEqual(entry.Value.Tufts()), "Density and offsets remain stable on reload.");
                if (neighbor) continue;
                isolated++;
                if (count == 1) loneTufts++;
                if (count >= 2 && count <= 4) isolatedBunches++;
            }
            Check(isolated >= 50, "Isolated tiles must be common, including separation from diagonal neighbors.");
            Check(loneTufts > 0 && isolatedBunches > 0, "Include both a single tuft and several tufts on isolated tiles.");

            var remaining = new HashSet<Vector2Int>(location.plants.Keys);
            var sizes = new List<int>();
            while (remaining.Count > 0)
            {
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(remaining.First());
                int size = 0;
                while (queue.Count > 0)
                {
                    var pos = queue.Dequeue();
                    if (!remaining.Remove(pos)) continue;
                    size++;
                    foreach (var direction in Directions) queue.Enqueue(pos + direction);
                }
                sizes.Add(size);
            }
            Check(sizes.Any(n => n >= 16), "Retain large patches.");
            Check(sizes.Any(n => n >= 2 && n <= 5), "Include small groups as well as large patches.");
            for (int quadrant = 0; quadrant < 4; quadrant++)
                Check(location.plants.Keys.Any(p => (p.x >= 20 ? 1 : 0) + (p.y >= 20 ? 2 : 0) == quadrant), "Distribute weeds across the field.");
            UnityEngine.Object.DestroyImmediate(location.gameObject);
        }
    }

    private static void CheckSpreadAndPersistence()
    {
        var location = Location();
        var from = new Vector2Int(20, 20);
        location.plants[from] = new PlantFeature(20, 20, PlantDatabase.WeedId, 3);
        Call(location, "SetBlocked", 19, 20, true);
        Call(location, "SetBlocked", 20, 19, true);
        Call(location, "SetBlocked", 20, 21, true);
        int successes = 0;
        Random.InitState(321);
        for (int i = 0; i < 2000; i++)
        {
            object[] args = { from, Vector2Int.zero };
            if (!(bool)Call(location, "TrySpreadWeedFrom", args)) continue;
            successes++;
            Check((Vector2Int)args[1] == from + Vector2Int.right, "A successful spread can only use the one unblocked target.");
        }
        // With only one of four targets free, 4% of 2,000 attempts yields about 20 spreads.
        // A fallback that searched other directions would be near 80 and fail this range.
        Check(successes >= 8 && successes <= 35, "Weeds use a 4% one-target spread attempt with no direction fallback.");
        var saved = new LocationData { debrisDay = 1, plantDay = 1, grassSeeded = true };
        Call(location, "SavePlantsInto", saved);
        Call(location, "AdvanceDebris", saved);
        Check(location.plants.Count == 1, "Re-entering the same day does not reseed existing saves.");
        var restored = Location();
        Call(restored, "RestorePlants", saved);
        Check(restored.plants.Count == 1 && restored.plants[from].stage == 3, "Save/load preserves existing positions and variants.");
        Set(location, "_suspendPlantRender", true);
        Check(location.CutPlant(20, 20, out var drop) && drop == "plant_weed", "Cutting keeps fiber drop behavior.");
        Call(location, "AdvanceDebris", saved);
        Check(location.plants.Count == 0, "Cut weeds do not respawn on same-day re-entry.");
        UnityEngine.Object.DestroyImmediate(location.gameObject);
        UnityEngine.Object.DestroyImmediate(restored.gameObject);
    }
}
