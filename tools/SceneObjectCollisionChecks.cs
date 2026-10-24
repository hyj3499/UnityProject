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

public static class SceneObjectCollisionChecks
{
    public static void Run()
    {
        var lines = new List<string>();
        try
        {
            if (!File.Exists("object-collision-validation-project")) throw new Exception("Isolated project required.");
            ObjectCollisionBaker.Rebuild();
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var loc = new GameObject("Scene collision check").AddComponent<GameLocation>();
            loc.id = LocationId.Farm;
            var data = new GameData();
            data.farm1.debrisDay = 1;
            loc.Build(data);
            var renderers = loc.FeatureRoot.GetComponentsInChildren<SpriteRenderer>();
            int count = 0;
            foreach (var sr in renderers.Where(s => s.sprite != null && s.sprite.name.StartsWith("Wood_spring")))
            {
                var mask = ObjectCollisionMasks.Find(sr.sprite);
                int painted = 0, missed = 0, otherBlocked = 0, clear = 0;
                if (mask != null)
                    for (int y = 0; y < mask.height; y++)
                        for (int x = 0; x < mask.width; x++)
                        {
                            var point = (Vector2)sr.transform.position + (new Vector2(x + .5f, y + .5f) - sr.sprite.pivot) / sr.sprite.pixelsPerUnit;
                            bool blocked = loc.IsBlockedAtWorld(point.x, point.y);
                            if (mask.Contains(x, y)) { painted++; if (!blocked) missed++; }
                            else if (blocked) otherBlocked++; else clear++;
                        }
                lines.Add($"{sr.sprite.name} position={sr.transform.position} mask={mask != null} solid={painted} missed={missed} unpaintedBlocked={otherBlocked} clear={clear}");
                if (mask != null && missed != 0) throw new Exception("Painted collision missing in actual scene.");
                count++;
            }
            lines.Add($"Scene objects checked: {count}; terrain: {loc.width}x{loc.height}");
            if (count == 0) throw new Exception("Scene must include Wood test objects.");
            File.WriteAllLines("scene-result.txt", lines);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            lines.Add("FAIL: " + ex);
            File.WriteAllLines("scene-result.txt", lines);
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }
}
