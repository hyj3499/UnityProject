// Compiled only in the disposable project created by Check-MapExits.ps1.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using FarmMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class MapExitChecks
{
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Tile paint;

    public static void Run()
    {
        try
        {
            if (!File.Exists("map-exit-validation-project")) throw new Exception("Requires isolated project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            paint = ScriptableObject.CreateInstance<Tile>();
            paint.name = "Ground";
            CheckConnections();
            CheckSerialization();
            CheckSceneAndPalettes();
            File.WriteAllText("result.txt", $"PASS: {checks} map exit checks (runtime + editor scripts compiled).");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("result.txt", "FAIL: " + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }

    private static Tilemap Layer(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Tilemap>();
    }

    private static Transform Map(LocationId id, Vector3 offset, Vector3Int min)
    {
        var grid = new GameObject(id.ToString(), typeof(Grid)).transform;
        grid.position = offset;
        var ground = Layer(grid, "Location_" + id);
        for (int x = 0; x < 6; x++)
            for (int y = 0; y < 5; y++) ground.SetTile(min + new Vector3Int(x, y, 0), paint);
        return grid;
    }

    private static void CheckConnections()
    {
        var farm = Map(LocationId.Farm, new Vector3(30, 40, 0), new Vector3Int(-3, -2, 0));
        var valley = Map(LocationId.Valley, new Vector3(-40, 100, 0), new Vector3Int(7, -8, 0));
        var campsite = Map(LocationId.Campsite1, new Vector3(100, 0, 0), Vector3Int.zero);
        var exitFarm = Layer(farm, "Exit_valley");
        var exitValley = Layer(valley, "Exit_Farm");
        WorldGrid.Invalidate();
        Check(!MapGraph.AreConnected(LocationId.Farm, LocationId.Valley), "Empty Exit layers do not connect maps");
        Check(!WorldGrid.TryGetExitTile(LocationId.Farm, LocationId.Valley, Vector2Int.zero, out _), "No edge fallback");
        Check(!WorldGrid.TryGetEntryFrom(LocationId.Valley, LocationId.Farm, Vector2Int.zero, out _), "No map-center arrival fallback");
        exitFarm.SetTile(new Vector3Int(-3, 0, 0), paint); // local (0,2)
        WorldGrid.Invalidate();
        Check(WorldGrid.HasExitTo(LocationId.Farm, LocationId.Valley), "Lowercase Exit_valley resolves to Valley");
        Check(!MapGraph.AreConnected(LocationId.Farm, LocationId.Valley), "One-sided exit cannot be used");
        var loc = new GameObject("RuntimeFarm").AddComponent<GameLocation>();
        loc.id = LocationId.Farm;
        loc.width = 6; loc.height = 5;
        typeof(GameLocation).GetField("_blocked", Private).SetValue(loc, new bool[6, 5]);
        MapExits.Apply(loc);
        Check(!loc.TryGetExit(0, 2, out _), "One-sided exit is not registered for the player");

        exitValley.SetTile(new Vector3Int(12, -5, 0), paint); // local (5,3)
        exitValley.gameObject.SetActive(false);
        WorldGrid.Invalidate();
        Check(MapGraph.AreConnected(LocationId.Farm, LocationId.Valley), "Reciprocal exits work, including inactive maps");
        Check(WorldGrid.TryGetEntryFrom(LocationId.Valley, LocationId.Farm, Vector2Int.zero, out var entry)
            && entry == new Vector2Int(5, 3), "Arrival is exactly the painted tile with offset grid and negative bounds");
        MapExits.Apply(loc);
        Check(loc.TryGetExit(0, 2, out var target) && target == LocationId.Valley, "Player exit uses actual painted source cell");
        Check(!loc.TryGetExit(0, 0, out _), "Other boundary cells are not exits");
        Check(!loc.TryFindEntryFrom(LocationId.Sea, Vector2Int.zero, out _), "Runtime arrival refuses missing return Exit");

        var objectsValley = Layer(valley, "Objects_Valley");
        var objectsCamp = Layer(campsite, "Objects_Campsite1");
        var toCamp = ScriptableObject.CreateInstance<Tile>(); toCamp.name = "Obj_ExitCampsite1";
        var toValley = ScriptableObject.CreateInstance<Tile>(); toValley.name = "Exit_valley";
        objectsValley.SetTile(new Vector3Int(8, -7, 0), toCamp);
        objectsCamp.SetTile(new Vector3Int(1, 1, 0), toValley);
        objectsCamp.SetTile(new Vector3Int(2, 2, 0), toCamp);
        WorldGrid.Invalidate();
        var campLocation = new GameObject("RuntimeCamp").AddComponent<GameLocation>();
        campLocation.id = LocationId.Campsite1;
        typeof(GameLocation).GetField("_blocked", Private).SetValue(campLocation, new bool[20, 15]);
        MapExits.Apply(campLocation);
        Check(!campLocation.HasExitTo(LocationId.Campsite1), "Self-targeting painted marker is not a map connection");
        UnityEngine.Object.DestroyImmediate(campLocation.gameObject);
        Check(MapGraph.TryGetNextHop(LocationId.Farm, LocationId.Campsite1, out var next) && next == LocationId.Valley,
            "NPC route follows painted multi-map connections and existing marker tiles");
        Check(!MapGraph.TryGetNextHop(LocationId.Farm, LocationId.Mountain1, out _), "Old hardcoded Farm-to-Mountain route removed");
        objectsCamp.ClearAllTiles(); WorldGrid.Invalidate();
        Check(!MapGraph.TryGetNextHop(LocationId.Farm, LocationId.Campsite1, out _), "Missing return tile breaks NPC route");

        var explicitExit = Layer(campsite, "Exit_Sea_Campsite1");
        Check(MapExits.TryGetLayer(explicitExit, out var owner, out target) && owner == LocationId.Campsite1 && target == LocationId.Sea,
            "Explicit source suffix supports shared Grids");
        foreach (var name in new[] { "Exit_Farm2", "Obj_ExitMountain3", "Exit_1", "Exit_Unknown" })
            Check(!MapExits.TryParseTarget(name, out _), "Invalid target rejected: " + name);

        var gm = new GameObject("Manager").AddComponent<GameManager>();
        typeof(GameManager).GetField("<CurrentLocation>k__BackingField", Private).SetValue(gm, loc);
        gm.ChangeLocationThroughExit(LocationId.Valley, new Vector2Int(0, 0));
        Check(gm.CurrentLocation == loc, "Manager refuses transitions from non-exit cells");
        gm.ChangeLocationThroughExit(LocationId.Sea, new Vector2Int(0, 2));
        Check(gm.CurrentLocation == loc, "Manager refuses mismatched destination");
        exitValley.ClearAllTiles(); WorldGrid.Invalidate();
        gm.ChangeLocationThroughExit(LocationId.Valley, new Vector2Int(0, 2));
        Check(gm.CurrentLocation == loc, "Manager refuses return exit removed after source was built");

        var player = new GameObject("Player").AddComponent<PlayerController>();
        var checkExits = typeof(PlayerController).GetMethod("CheckExits", Private);
        var waiting = typeof(PlayerController).GetField("_waitUntilOffExit", Private);
        player.SuppressExitUntilLeave();
        player.transform.position = new Vector3(0, 2, 0);
        checkExits.Invoke(player, new object[] { loc });
        Check((bool)waiting.GetValue(player), "Arrival tile does not immediately send player back");
        loc.AddExit(1, 2, LocationId.Valley);
        player.transform.position = new Vector3(1, 2, 0);
        checkExits.Invoke(player, new object[] { loc });
        Check((bool)waiting.GetValue(player), "Moving within a painted exit strip does not bounce");
        player.transform.position = new Vector3(2, 2, 0);
        checkExits.Invoke(player, new object[] { loc });
        Check(!(bool)waiting.GetValue(player), "Leaving Exit region re-enables return travel");

        UnityEngine.Object.DestroyImmediate(farm.gameObject);
        UnityEngine.Object.DestroyImmediate(valley.gameObject);
        UnityEngine.Object.DestroyImmediate(campsite.gameObject);
        UnityEngine.Object.DestroyImmediate(loc.gameObject);
        UnityEngine.Object.DestroyImmediate(gm.gameObject);
        UnityEngine.Object.DestroyImmediate(player.gameObject);
        WorldGrid.Invalidate();
    }

    private static void CheckSerialization()
    {
        Check((int)LocationId.Farm == 0 && (int)LocationId.FarmHouse == 2 && (int)LocationId.Campsite1 == 4
            && (int)LocationId.Campsite2 == 5 && (int)LocationId.MountainPeak == 12, "Serialized map IDs preserved");
        Check(!Enum.IsDefined(typeof(LocationId), 1) && !Enum.IsDefined(typeof(LocationId), 11), "Removed maps not enumerated");
        var data = JsonUtility.FromJson<GameData>("{\"currentLocation\":1,\"farmer\":{\"posX\":30,\"posY\":40},\"locations\":[{\"id\":\"PigeonVillage\",\"data\":{}},{\"id\":\"HawkVillage\",\"data\":{}},{\"id\":\"Mountain3\",\"data\":{}}]}");
        data.MigrateLocations();
        Check(data.currentLocation == LocationId.Farm && data.farmer.posX == 0 && data.farmer.posY == 0,
            "Loading save in deleted map does not recreate it");
        Check(data.locations.Count == 2 && data.locations[0].id == "Campsite1" && data.locations[1].id == "Campsite2",
            "Saved campsite data follows renamed IDs");
    }

    private static void CheckSceneAndPalettes()
    {
        // Ground art is intentionally omitted; these checks inspect scene hierarchy and exit assets.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Check(scene.IsValid() && scene.isLoaded, "Edited Main scene loads in Unity");
        var tilemaps = UnityEngine.Object.FindObjectsOfType<Tilemap>(true);
        foreach (var id in new[] { LocationId.Farm, LocationId.Campsite1, LocationId.Campsite2 })
            Check(Array.Exists(tilemaps, map => map.name == "Location_" + id), "Renamed ground exists: " + id);
        foreach (var old in new[] { "Farm1", "Farm2", "Mountain3", "PigeonVillage", "HawkVillage" })
            Check(!Array.Exists(tilemaps, map => map.name.Contains(old)), "Removed map name absent: " + old);
        foreach (var id in new[] { "Farm", "Campsite1", "Campsite2" })
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Resources/Sprites/Tiles/Markers/Obj_Exit" + id + ".asset");
            Check(tile != null && tile.name == "Obj_Exit" + id, "Renamed marker asset loads: " + id);
        }
        foreach (var palette in new[] { "exit", "object" })
        {
            var contents = PrefabUtility.LoadPrefabContents("Assets/Resources/Sprites/Tiles/Markers/" + palette + ".prefab");
            try
            {
                var map = contents.GetComponentInChildren<Tilemap>();
                Check(map != null, "Edited palette loads: " + palette);
                bool containsDeletedDestination = false;
                foreach (var pos in map.cellBounds.allPositionsWithin)
                {
                    var tile = map.GetTile(pos);
                    if (tile != null && (tile.name == "Obj_ExitFarm2" || tile.name == "Obj_ExitMountain3"))
                        containsDeletedDestination = true;
                }
                Check(!containsDeletedDestination, "Deleted destination not painted in palette: " + palette);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }
}
#endif
