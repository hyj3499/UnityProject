using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FarmMVP;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class CliffCollisionChecks
{
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static void SetStatic(string field, object value) => typeof(CliffPixelCollision)
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);

    public static void Run()
    {
        try
        {
            if (!File.Exists("cliff-validation-project")) throw new Exception("Requires isolated project.");
            CliffCollisionBaker.Rebuild();
            var raw = new Texture2D(2, 2);
            ImageConversion.LoadImage(raw, File.ReadAllBytes(CliffCollisionBaker.SourcePath));
            var pixels = raw.GetPixels32();
            var palette = new HashSet<int>();
            for (int y = raw.height - 16; y < raw.height; y++)
                for (int x = 0; x < 16; x++)
                    if (pixels[y * raw.width + x].a != 0)
                    {
                        var color = pixels[y * raw.width + x];
                        palette.Add(color.r << 16 | color.g << 8 | color.b);
                    }
            Check(palette.Count > 0, "Current PNG must contain opaque palette colors.");
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.position = new Vector3(-0.5f, -0.5f, 0f);
            var mapObject = new GameObject("Cliff_2_Farm1", typeof(Tilemap));
            mapObject.transform.SetParent(grid.transform, false);
            var map = mapObject.GetComponent<Tilemap>();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.flags = TileFlags.None;
            var sprites = Resources.LoadAll<Sprite>(CliffPixelCollision.SheetResource);
            Check(sprites.Length > 0, "Source sprites imported.");
            foreach (var sprite in sprites)
            {
                tile.sprite = sprite;
                map.SetTile(Vector3Int.zero, tile);
                map.RefreshAllTiles();
                var collision = new CliffPixelCollision(new[] { map });
                var rect = sprite.rect;
                bool any = false;
                for (int y = 0; y < (int)rect.height; y++)
                    for (int x = 0; x < (int)rect.width; x++)
                    {
                        var color = pixels[((int)rect.y + y) * raw.width + (int)rect.x + x];
                        bool expected = color.a != 0 && palette.Contains(color.r << 16 | color.g << 8 | color.b);
                        any |= expected;
                        var world = new Vector3((x + 0.5f - sprite.pivot.x) / sprite.pixelsPerUnit,
                            (y + 0.5f - sprite.pivot.y) / sprite.pixelsPerUnit, 0f);
                        Check(collision.IsBlocked(world) == expected, sprite.name + " pixel " + x + "," + y);
                    }
                Check(collision.HasBlockedPixels(Vector3.zero) == any, "Conservative mask " + sprite.name);
            }

            // Synthetic one-pixel wall, with non-palette grass and transparent RGB matches.
            var synthetic = new Color32[32 * 16];
            var red = new Color32(80, 38, 41, 255);
            synthetic[15 * 32] = red;
            for (int y = 0; y < 16; y++)
                for (int x = 16; x < 32; x++) synthetic[y * 32 + x] = new Color32(80, 180, 80, 255);
            for (int y = 0; y < 16; y++) synthetic[y * 32 + 24] = red;
            synthetic[16] = new Color32(red.r, red.g, red.b, 0);
            var texture = new Texture2D(32, 16);
            var wall = Sprite.Create(texture, new Rect(16, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            SetStatic("sheetSprites", new HashSet<Sprite> { wall });
            SetStatic("bits", CliffCollisionBaker.Bake(synthetic, 32, 16));
            SetStatic("sheetWidth", 32);
            SetStatic("sheetHeight", 16);
            tile.sprite = wall;
            map.ClearAllTiles();
            var cell = new Vector3Int(1, 1, 0);
            map.SetTile(cell, tile);
            var masks = new CliffPixelCollision(new[] { map });
            Check(masks.IsBlocked(new Vector3(1.03125f, 1, 0)), "One-pixel wall blocks.");
            Check(!masks.IsBlocked(new Vector3(0.8f, 1, 0)), "Grass passes.");
            Check(!masks.IsBlocked(new Vector3(0.53125f, 0.53125f, 0)), "Transparent palette RGB passes.");
            Check(!masks.IsBlocked(new Vector3(2, 1, 0)), "Empty cell passes.");
            map.SetTransformMatrix(cell, Matrix4x4.Scale(new Vector3(-1, 1, 1)));
            var flipped = new CliffPixelCollision(new[] { map });
            Check(flipped.IsBlocked(new Vector3(0.96875f, 1, 0)), "Flipped wall moves left.");
            Check(!flipped.IsBlocked(new Vector3(1.03125f, 1, 0)), "Flipped wall old position clears.");
            map.SetTransformMatrix(cell, Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)));
            var rotated = new CliffPixelCollision(new[] { map });
            Check(rotated.IsBlocked(new Vector3(1, 1.03125f, 0)), "Rotated wall blocks.");
            map.SetTransformMatrix(cell, Matrix4x4.identity);

            var location = new GameObject("Location").AddComponent<GameLocation>();
            location.width = location.height = 3;
            location.id = LocationId.Farm1;
            var blocked = new bool[3, 3];
            typeof(GameLocation).GetField("_blocked", Private).SetValue(location, blocked);
            ((List<Tilemap>)typeof(GameLocation).GetField("_cliffMaps", Private).GetValue(location)).Add(map);
            typeof(GameLocation).GetMethod("ApplyMaskTilemaps", Private).Invoke(location, null);
            Check(location.IsBlocked(1, 1), "NPC/spawn avoids partially blocked tile.");
            Check(!location.IsBlockedAtWorld(0.8f, 1), "Player enters grass in cliff tile.");
            Check(location.IsBlockedAtWorld(1.03125f, 1), "Player blocked by cliff pixel.");
            blocked[1, 1] = true;
            Check(location.IsBlockedAtWorld(0.8f, 1), "Existing object/block mask preserved.");
            blocked[1, 1] = false;
            location.SetNpcBlocked(new Vector2Int(1, 1));
            Check(location.IsBlockedAtWorld(0.8f, 1), "NPC occupancy preserved.");
            location.ClearNpcBlocks();
            Check(location.IsBlockedAtWorld(-2, 0), "Map bounds preserved.");

            var manager = new GameObject("Manager").AddComponent<GameManager>();
            typeof(GameManager).GetProperty("CurrentLocation").SetValue(manager, location);
            var player = new GameObject("Player").AddComponent<PlayerController>();
            typeof(PlayerController).GetField("_game", Private).SetValue(player, manager);
            var move = typeof(PlayerController).GetMethod("TryMove", Private);
            CheckBodyEdges();
            location.width = location.height = 6;
            typeof(GameLocation).GetField("_blocked", Private).SetValue(location, new bool[6, 6]);
            foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            {
                bool horizontal = direction == Direction.Left || direction == Direction.Right;
                bool positive = direction == Direction.Right || direction == Direction.Up;
                map.ClearAllTiles();
                for (int i = 0; i < 6; i++)
                {
                    var wallCell = horizontal ? new Vector3Int(2, i, 0) : new Vector3Int(i, 2, 0);
                    map.SetTile(wallCell, tile);
                    if (!horizontal) map.SetTransformMatrix(wallCell, Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)));
                }
                typeof(GameLocation).GetMethod("ApplyMaskTilemaps", Private).Invoke(location, null);
                player.facing = direction;
                var start = horizontal ? new Vector3(positive ? 0.5f : 4, 1, 0)
                    : new Vector3(1, positive ? 0.4f : 4, 0);
                player.transform.position = start;
                if (direction == Direction.Up)
                {
                    move.Invoke(player, new object[] { new Vector2(0, 1f) });
                    Check(Mathf.Abs(player.transform.position.y - (start.y + 1f)) < 0.0001f,
                        "Back-facing movement must continue when the head crosses the wall but feet are still clear.");
                    player.transform.position = start;
                }
                var displacement = horizontal ? new Vector2(positive ? 4 : -4, 0)
                    : new Vector2(0, positive ? 4 : -4);
                move.Invoke(player, new object[] { displacement });
                var edge = FarmerBodyCollision.LeadingEdge(direction, direction);
                var offsets = edge.Select(p => horizontal ? p.x : p.y);
                float extent = positive ? offsets.Max() : offsets.Min();
                float position = horizontal ? player.transform.position.x : player.transform.position.y;
                float contact = position + extent;
                Check(positive ? contact < 2 && contact > 2 - CliffPixelCollision.MovementStep - 0.002f
                    : contact >= 2.0625f && contact < 2.0625f + CliffPixelCollision.MovementStep + 0.002f,
                    direction + " body edge, not center, must stop at 1px wall; contact=" + contact);
                Check(!location.IsBlockedAtWorld(player.transform.position.x, player.transform.position.y),
                    direction + " center still in walkable grass at contact.");

                if (direction == Direction.Right)
                {
                    var beforeSlide = player.transform.position;
                    move.Invoke(player, new object[] { new Vector2(0.2f, 0.2f) });
                    Check(player.transform.position.x + extent < 2f &&
                        Mathf.Abs(player.transform.position.x - beforeSlide.x) <= CliffPixelCollision.MovementStep &&
                        player.transform.position.y > beforeSlide.y + 0.19f,
                        "Body slides along wall diagonally: " + beforeSlide + " -> " + player.transform.position);
                }
                var beforeRetreat = player.transform.position;
                move.Invoke(player, new object[] { -displacement.normalized * 0.125f });
                Check(Vector3.Distance(beforeRetreat, player.transform.position) > 0.12f,
                    direction + " can retreat from contact without sticking.");
            }

            CheckSmallCliffSteps(map, location, player, move);

            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100000; i++) masks.IsBlocked(new Vector3(1.03125f, 1, 0));
            clock.Stop();
            File.WriteAllText("result.txt", $"PASS: {checks} checks; {sprites.Length} real sprites; baked {File.ReadAllBytes(CliffCollisionBaker.OutputPath).Length} bytes; 100000 point queries: {clock.ElapsedMilliseconds} ms.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText("result.txt", "FAIL: " + exception);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckSmallCliffSteps(Tilemap map, GameLocation location,
        PlayerController player, MethodInfo move)
    {
        const int size = 128;
        var texture = new Texture2D(size, size);
        var sprites = new HashSet<Sprite>();
        map.ClearAllTiles();
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                var sprite = Sprite.Create(texture, new Rect(x * 16, y * 16, 16, 16),
                    new Vector2(0.5f, 0.5f), 16);
                sprites.Add(sprite);
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                map.SetTile(new Vector3Int(x, y, 0), tile);
                map.SetTransformMatrix(new Vector3Int(x, y, 0), Matrix4x4.identity);
            }
        SetStatic("sheetSprites", sprites);
        SetStatic("sheetWidth", size);
        SetStatic("sheetHeight", size);
        location.width = location.height = 8;
        var ordinaryBlocked = new bool[8, 8];
        typeof(GameLocation).GetField("_blocked", Private).SetValue(location, ordinaryBlocked);

        foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            foreach (int wallSide in new[] { -1, 1 })
                for (int bump = 1; bump <= 3; bump++)
                    foreach (float frameDistance in new[] { 5.2f / 30f, 5.2f / 144f })
                    foreach (bool pressIntoWall in new[] { false, true })
                    {
                        bool horizontal = direction == Direction.Left || direction == Direction.Right;
                        bool positive = direction == Direction.Right || direction == Direction.Up;
                        // PlayerController prioritises vertical facing for equal diagonal input.
                        var bodyFacing = pressIntoWall && horizontal ? (wallSide > 0 ? Direction.Up : Direction.Down) : direction;
                        var sideEdge = FarmerBodyCollision.LeadingEdge(bodyFacing,
                            horizontal ? (wallSide > 0 ? Direction.Up : Direction.Down)
                                : (wallSide > 0 ? Direction.Right : Direction.Left));
                        float extent = wallSide > 0
                            ? sideEdge.Max(p => horizontal ? p.y : p.x)
                            : sideEdge.Min(p => horizontal ? p.y : p.x);
                        float boundary = wallSide > 0 ? 4 : 3;
                        float normal = boundary - extent - wallSide / 64f;
                        float tangent = positive ? 0.6f : 5.4f;
                        var start = horizontal ? new Vector3(tangent, normal, 0) : new Vector3(normal, tangent, 0);
                        BakeWall(map, location, horizontal, wallSide, bump, false);
                        typeof(PlayerController).GetMethod("ClearCliffSlide", Private).Invoke(player, null);
                        typeof(PlayerController).GetField("_cliffUseFeet", Private).SetValue(player, false);
                        typeof(PlayerController).GetField("_cliffFootprintLocation", Private).SetValue(player, null);
                        player.facing = bodyFacing;
                        player.cliffSlidePixels = 3;
                        player.transform.position = start;
                        float remaining = 4;
                        while (remaining > 0.00001f)
                        {
                            float amount = Mathf.Min(remaining, frameDistance);
                            remaining -= amount;
                            var input = horizontal ? new Vector2(positive ? amount : -amount, 0)
                                : new Vector2(0, positive ? amount : -amount);
                            if (pressIntoWall)
                                input += horizontal ? new Vector2(0, wallSide * amount) : new Vector2(wallSide * amount, 0);
                            var before = player.transform.position;
                            move.Invoke(player, new object[] { input });
                            var after = player.transform.position;
                            Check(Vector3.Distance(before, after) <= input.magnitude + 0.0001f,
                                "Slide must share input movement budget.");
                            foreach (Direction edgeDirection in Enum.GetValues(typeof(Direction)))
                                Check(!location.IsBlockedAtWorld(after.x, after.y,
                                    FarmerBodyCollision.LeadingEdge(bodyFacing, edgeDirection)),
                                    $"Slide penetrated wall: {direction}, side {wallSide}, bump {bump}, at {after}");
                        }
                        var end = player.transform.position;
                        float progress = horizontal ? Mathf.Abs(end.x - start.x) : Mathf.Abs(end.y - start.y);
                        float correction = horizontal ? end.y - start.y : end.x - start.x;
                        Check(progress > 3.5f,
                            $"Must glide past {bump}px bump: {direction}, side {wallSide}, diagonal {pressIntoWall}, step {frameDistance}, {start} -> {end}");
                        Check(correction * wallSide < 0 && Mathf.Abs(correction) < 3f / 16f + 1f / 32f,
                            "Correction stays small and moves away from the wall.");
                    }

        // Once the wall ends, a latched slide must release normal diagonal movement.
        var empty = new Color32[size * size];
        empty[(size - 1) * size] = new Color32(80, 38, 41, 255);
        SetStatic("bits", CliffCollisionBaker.Bake(empty, size, size));
        typeof(GameLocation).GetMethod("ApplyMaskTilemaps", Private).Invoke(location, null);
        var beforeFreeMove = player.transform.position;
        move.Invoke(player, new object[] { new Vector2(0.1f, 0.1f) });
        Check(Vector3.Distance(player.transform.position, beforeFreeMove + new Vector3(0.1f, 0.1f, 0)) < 0.0001f,
            "Normal input resumes when the wall ends.");

        // Setting the Inspector tolerance to zero restores strict pixel blocking.
        BakeWall(map, location, false, 1, 3, false);
        player.cliffSlidePixels = 0;
        player.facing = Direction.Up;
        float rightExtent = FarmerBodyCollision.LeadingEdge(Direction.Up, Direction.Right).Max(p => p.x);
        var noSlideStart = new Vector3(4 - rightExtent - 1f / 64f, 0.6f, 0);
        player.transform.position = noSlideStart;
        for (int i = 0; i < 80; i++) move.Invoke(player, new object[] { new Vector2(0, 0.05f) });
        Check(Mathf.Abs(player.transform.position.x - noSlideStart.x) < 0.0001f && player.transform.position.y < 4,
            "Zero tolerance disables correction and stops at a bump.");
        player.cliffSlidePixels = 3;

        // A broad wall remains impassable, even if only one pixel thick.
        BakeWall(map, location, false, 1, 0, true);
        player.facing = Direction.Right;
        player.transform.position = new Vector3(2, 3, 0);
        for (int i = 0; i < 100; i++) move.Invoke(player, new object[] { new Vector2(0.1f, 0) });
        Check(player.transform.position.x < 4 && Mathf.Abs(player.transform.position.y - 3) < 0.0001f,
            "Slide must not cross or drift along a one-pixel wall when walking directly into it.");

        // Existing tile blockers/NPCs are not candidates for cliff corner correction.
        ordinaryBlocked[2, 2] = true;
        player.facing = Direction.Up;
        player.transform.position = new Vector3(2, 1.4f, 0);
        move.Invoke(player, new object[] { new Vector2(0, 0.5f) });
        Check(player.transform.position.y < 1.5f && Mathf.Abs(player.transform.position.x - 2) < 0.0001f,
            "Ordinary blockers must stop movement without cliff correction.");
        ordinaryBlocked[2, 2] = false;
        location.SetNpcBlocked(new Vector2Int(2, 2));
        player.transform.position = new Vector3(2, 1.4f, 0);
        move.Invoke(player, new object[] { new Vector2(0, 0.5f) });
        Check(player.transform.position.y < 1.5f && Mathf.Abs(player.transform.position.x - 2) < 0.0001f,
            "NPC occupancy must stop movement without cliff correction.");
        location.ClearNpcBlocks();
        CheckCliffFacingTransitions(map, location, player, move);
    }

    private static void CheckCliffFacingTransitions(Tilemap map, GameLocation location,
        PlayerController player, MethodInfo move)
    {
        foreach (int tolerance in new[] { 0, 3 })
            foreach (float frameDistance in new[] { 5.2f / 30f, 5.2f / 144f })
            {
                BakeWall(map, location, true, 1, 0, false); // Filled cliff above world y=4.
                player.cliffSlidePixels = tolerance;
                player.transform.position = new Vector3(3, 2, 0);
                MoveInDirection(player, move, Direction.Up, 3, frameDistance);
                var contact = player.transform.position;
                float foot = FarmerBodyCollision.LeadingEdge(Direction.Up, Direction.Up).Max(p => p.y);
                Check(contact.y + foot < 4 && contact.y + foot > 4 - CliffPixelCollision.MovementStep - 0.001f,
                    "Upward approach stops with feet at the upper cliff.");
                Check(location.IsBlockedAtWorld(contact.x + 0.05f, contact.y,
                    FarmerBodyCollision.LeadingEdge(Direction.Right, Direction.Right)),
                    "Reproduce old trap: right-facing upper body already overlaps the cliff.");

                // Releasing movement clears slide correction, but must not expand the footprint.
                typeof(PlayerController).GetMethod("ClearCliffSlide", Private).Invoke(player, null);
                MoveInDirection(player, move, Direction.Right, 0.65f, frameDistance);
                Check(Vector3.Distance(player.transform.position, contact + new Vector3(0.65f, 0, 0)) < 0.0002f,
                    "Turn right after upward contact without getting trapped or pushed away.");
                MoveInDirection(player, move, Direction.Left, 1.3f, frameDistance);
                Check(Vector3.Distance(player.transform.position, contact + new Vector3(-0.65f, 0, 0)) < 0.0002f,
                    "Turn left along the same cliff without getting trapped.");
                Check((bool)typeof(PlayerController).GetField("_cliffUseFeet", Private).GetValue(player),
                    "Keep feet footprint while the upper body still overlaps the cliff.");

                var retreatStart = player.transform.position;
                MoveInDirection(player, move, Direction.Down, 2, frameDistance);
                Check(Vector3.Distance(player.transform.position, retreatStart + new Vector3(0, -2, 0)) < 0.0002f,
                    "Retreat from the cliff after turning without getting trapped.");
                Check(!(bool)typeof(PlayerController).GetField("_cliffUseFeet", Private).GetValue(player),
                    "Restore normal silhouette once the upper body is clear.");

                BakeWall(map, location, false, 1, 0, false);
                player.transform.position = new Vector3(2, 1, 0);
                MoveInDirection(player, move, Direction.Right, 3, frameDistance);
                float right = FarmerBodyCollision.LeadingEdge(Direction.Right, Direction.Right).Max(p => p.x);
                Check(player.transform.position.x + right < 4 &&
                    player.transform.position.x + right > 4 - CliffPixelCollision.MovementStep - 0.001f,
                    "A later side-wall approach uses the original full body edge again.");
            }

        // Feet mode is not a global collision bypass: a perpendicular wall at foot level still stops movement.
        var pixels = new Color32[128 * 128];
        var red = new Color32(80, 38, 41, 255);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
                if (y >= 72 || x >= 88) pixels[y * 128 + x] = red;
        SetStatic("bits", CliffCollisionBaker.Bake(pixels, 128, 128));
        typeof(GameLocation).GetMethod("ApplyMaskTilemaps", Private).Invoke(location, null);
        player.cliffSlidePixels = 3;
        player.transform.position = new Vector3(3, 2, 0);
        MoveInDirection(player, move, Direction.Up, 3, 0.05f);
        MoveInDirection(player, move, Direction.Right, 3, 0.05f);
        float footRight = FarmerBodyCollision.LeadingEdge(Direction.Up, Direction.Right).Max(p => p.x);
        Check(player.transform.position.x + footRight < 5 &&
            player.transform.position.x + footRight > 5 - CliffPixelCollision.MovementStep - 0.001f,
            "Turning at the upper cliff must still stop at a new wall touching the feet.");
    }

    private static void MoveInDirection(PlayerController player, MethodInfo move, Direction direction,
        float distance, float frameDistance)
    {
        player.facing = direction;
        Vector2 axis = direction == Direction.Up ? Vector2.up : direction == Direction.Down ? Vector2.down
            : direction == Direction.Right ? Vector2.right : Vector2.left;
        while (distance > 0.00001f)
        {
            float amount = Mathf.Min(distance, frameDistance);
            distance -= amount;
            move.Invoke(player, new object[] { axis * amount });
        }
    }

    private static void BakeWall(Tilemap map, GameLocation location, bool horizontal,
        int side, int bump, bool thin)
    {
        const int size = 128;
        var pixels = new Color32[size * size];
        var red = new Color32(80, 38, 41, 255);
        int boundary = side > 0 ? 72 : 56;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int tangent = horizontal ? x : y;
                int normal = horizontal ? y : x;
                int protrusion = tangent >= 48 && tangent < 64 ? bump : 0;
                bool solid = thin ? normal == boundary
                    : side > 0 ? normal >= boundary - protrusion : normal < boundary + protrusion;
                if (solid) pixels[y * size + x] = red;
            }
        // Reserve the top-left palette cell, away from all movement cases.
        for (int y = size - 16; y < size; y++)
            for (int x = 0; x < 16; x++) pixels[y * size + x] = default;
        pixels[(size - 1) * size] = red;
        SetStatic("bits", CliffCollisionBaker.Bake(pixels, size, size));
        typeof(GameLocation).GetMethod("ApplyMaskTilemaps", Private).Invoke(location, null);
    }

    private static void CheckBodyEdges()
    {
        var texture = Resources.Load<Texture2D>(FarmerPoses.SheetPath);
        Check(texture != null && texture.isReadable, "Real Base texture is readable.");
        var pixels = texture.GetPixels32();
        foreach (Direction facing in Enum.GetValues(typeof(Direction)))
        {
            int left = 32, right = -1, bottom = 32, top = -1;
            var occupied = new bool[32, 32];
            foreach (var anim in new[] { FarmerAnim.Idle, FarmerAnim.Walk, FarmerAnim.Run })
            {
                var clip = FarmerPoses.Get(anim, facing);
                for (int frame = 0; frame < clip.count; frame++)
                    for (int y = 0; y < 32; y++)
                        for (int x = 0; x < 32; x++)
                        {
                            int index = (texture.height - (clip.row + 1) * 32 + y) * texture.width + frame * 32 + x;
                            if (pixels[index].a == 0) continue;
                            int drawX = facing == Direction.Left ? 31 - x : x;
                            occupied[drawX, y] = true;
                            left = Math.Min(left, drawX); right = Math.Max(right, drawX);
                            bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                        }
            }
            var l = FarmerBodyCollision.LeadingEdge(facing, Direction.Left);
            var r = FarmerBodyCollision.LeadingEdge(facing, Direction.Right);
            var u = FarmerBodyCollision.LeadingEdge(facing, Direction.Up);
            var d = FarmerBodyCollision.LeadingEdge(facing, Direction.Down);
            if (facing == Direction.Up)
            {
                left = 32; right = -1;
                for (int x = 0; x < 32; x++)
                    if (occupied[x, bottom]) { left = Math.Min(left, x); right = Math.Max(right, x); }
                float feetY = (bottom - 16) / 16f + FarmerPoses.BodyOffsetY;
                foreach (var edge in new[] { l, r, u, d })
                    Check(edge.All(p => Mathf.Abs(p.y - feetY) < 0.0001f),
                        "Back-facing forward and slide probes must all remain at the actual foot tip.");
            }
            Check(Mathf.Abs(l.Min(v => v.x) - (left - 16 - 0.001f) / 16f) < 0.00001f, facing + " left opaque edge.");
            Check(Mathf.Abs(r.Max(v => v.x) - (right + 1 - 16 + 0.001f) / 16f) < 0.00001f, facing + " right opaque edge.");
            int upperReference = facing == Direction.Up ? bottom : top + 1;
            Check(Mathf.Abs(u.Max(v => v.y) - ((upperReference - 16 + 0.001f) / 16f + FarmerPoses.BodyOffsetY)) < 0.00001f,
                facing + " upward edge uses feet only for the back-facing pose.");
            Check(Mathf.Abs(d.Min(v => v.y) - ((bottom - 16 - 0.001f) / 16f + FarmerPoses.BodyOffsetY)) < 0.00001f,
                facing + " foot edge includes renderer offset.");
            Check(l.Length < 96 && u.Length < 96, facing + " transparent canvas padding excluded.");
            Check(ReferenceEquals(l, FarmerBodyCollision.LeadingEdge(facing, Direction.Left)), "Contour cached without per-frame allocation.");
        }
    }
}
