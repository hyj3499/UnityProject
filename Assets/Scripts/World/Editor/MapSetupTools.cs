using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// 맵을 하나 늘릴 때 손으로 하기 귀찮은 것들을 대신해 주는 에디터 도구.
    ///
    ///   맵 레이어 만들기        LocationId에 있는데 씬에 없는 맵마다 Grid와 레이어 타일맵을 통째로 만든다.
    ///                           이름 규칙(Location_/Blocked_/Objects_...)대로 만들어 주므로
    ///                           만들자마자 타일 팔레트로 칠하기 시작하면 된다.
    ///   맵 연결 검사            MapGraph에 적힌 통로마다 "Obj_Exit{맵}" 마커를 양쪽에 칠했는지 훑어본다.
    ///   출구 타일 팔레트에 넣기  Obj_Exit* 타일을 마커 팔레트(object.prefab)에 한 줄로 넣어 준다.
    ///
    /// 이미 있는 것은 건드리지 않으므로 몇 번을 눌러도 안전하다.
    /// </summary>
    internal static class MapSetupTools
    {
        private const string MenuRoot = "Tools/FarmMVP/";
        private const string MarkerFolder = "Assets/Resources/Sprites/Tiles/Markers";
        private const string PalettePath = MarkerFolder + "/object.prefab";

        /// <summary>씬에서 맵들을 나란히 늘어놓을 간격(칸). 서로 겹치지 않을 만큼만 띄운다.</summary>
        private const int LayoutStepX = 45, LayoutStepY = 40, LayoutColumns = 4;

        /// <summary>
        /// 만들어 줄 레이어와 그 정렬 순서. 이름은 GameLocation이 읽는 규칙 그대로다.
        /// outdoorOnly인 레이어는 실내 맵(농가)에는 만들지 않는다 — 물·절벽·잔디가 있을 리 없다.
        /// </summary>
        private static readonly (string prefix, int sortingOrder, bool outdoorOnly)[] Layers =
        {
            ("Location",  Depth.PaintedGround, false),   // 바닥 — 칠한 영역이 곧 맵 크기
            ("Water",     Depth.Water,         true),    // 물 (못 지나감 + 낚시)
            ("Decor",     Depth.Decor,         false),   // 꽃·풀 장식
            ("Cliff",     Depth.Cliff,         true),    // 절벽
            ("Fixed",     0,                   false),   // 못 부수는 배경 오브젝트
            ("Breakable", 0,                   false),   // 걷어낼 수 있는 울타리·길·가구
            ("Objects",   0,                   false),   // 집·문·출구·나무·바위 마커
            ("Blocked",   210,                 false),   // 통행 불가 마스크
            ("Tillable",  211,                 false),   // 경작 가능 마스크
            ("Grass",     212,                 true),    // 잔디를 처음 한 번 깔 자리
        };

        [MenuItem(MenuRoot + "맵 레이어 만들기", false, 1)]
        private static void CreateMapLayers()
        {
            var existing = FindTilemapsByName();
            var created = new List<string>();
            int index = 0;

            foreach (LocationId id in System.Enum.GetValues(typeof(LocationId)))
            {
                // 씬에서 차지할 자리. 이미 만들어 둔 맵도 자리를 하나 쓴 것으로 쳐 두면,
                // 나중에 맵을 더 넣고 다시 눌러도 기존 맵들이 있던 자리는 그대로다.
                var origin = new Vector3((index % LayoutColumns) * LayoutStepX,
                                         (index / LayoutColumns) * LayoutStepY, 0f);
                index++;

                var root = FindOrCreateGrid(id, origin, existing, created);

                foreach (var layer in Layers)
                {
                    if (layer.outdoorOnly && MapGraph.IsIndoor(id)) continue;

                    string name = $"{layer.prefix}_{id}";
                    // 옛 이름("Farm1Ground")으로 만들어 둔 레이어가 있으면 그것도 이미 있는 것으로 본다.
                    string legacy = layer.prefix == "Location" ? $"{id}Ground" : $"{id}{layer.prefix}";
                    if (existing.ContainsKey(name) || existing.ContainsKey(legacy)) continue;

                    CreateLayer(root, name, layer.sortingOrder);
                    created.Add(name);
                }
            }

            if (created.Count == 0)
            {
                Debug.Log("[MapSetup] 만들 것이 없습니다 — 모든 맵의 레이어가 이미 씬에 있습니다.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[MapSetup] {created.Count}개를 만들었습니다:\n  " + string.Join("\n  ", created) +
                      "\n타일 팔레트로 \"Location_{맵}\"에 바닥을 칠하면 칠한 영역이 곧 맵 크기가 됩니다. 씬을 저장하세요.");
        }

        [MenuItem(MenuRoot + "맵 연결 검사", false, 2)]
        private static void CheckMapLinks()
        {
            var tilemaps = FindTilemapsByName();
            var report = new System.Text.StringBuilder("[MapSetup] 맵 연결 상태\n");

            foreach (var link in MapGraph.Links)
            {
                bool forward = HasExitMarker(tilemaps, link.a, link.b);
                bool back = HasExitMarker(tilemaps, link.b, link.a);
                string mark = forward && back ? "O" : (forward || back ? "△" : "·");
                report.Append($"  {mark} {MapGraph.DisplayName(link.a)} ↔ {MapGraph.DisplayName(link.b)}");
                if (!forward) report.Append($"   ({link.a}에 Obj_Exit{link.b} 없음)");
                if (!back) report.Append($"   ({link.b}에 Obj_Exit{link.a} 없음)");
                report.AppendLine();
            }
            report.AppendLine("  O=양쪽 다 칠함, △=한쪽만, ·=아직 안 칠함 " +
                              "(안 칠한 통로는 그 변을 넘어가면 이동합니다 — 맵 끝에서 바깥쪽으로 한 발 더)");

            // 한 맵에서 두 통로가 같은 변을 쓰면 임시 출구가 겹쳐 한쪽으로만 이동하게 된다.
            foreach (LocationId id in System.Enum.GetValues(typeof(LocationId)))
            {
                var bySide = new Dictionary<MapSide, LocationId>();
                foreach (var neighbor in MapGraph.Neighbors(id))
                {
                    if (bySide.TryGetValue(neighbor.Value, out var other))
                        report.AppendLine($"  ! {id}의 {neighbor.Value} 변을 {other}와 {neighbor.Key}가 함께 씁니다 " +
                                          "— 한 변은 한 곳으로만 이어질 수 있어 한쪽이 묻힙니다. " +
                                          "MapGraph에서 변을 바꾸거나, 한쪽을 Obj_Exit 마커로 칠하세요.");
                    else bySide[neighbor.Value] = neighbor.Key;
                }
            }

            // 바닥을 네모나게 칠하지 않으면 그 빈 칸이 맵 안에 그대로 남는다 (걸어 다닐 수 있고,
            // 화면에도 빈 배경으로 보인다). 맵 크기는 칠한 영역의 <b>네모난 범위</b>라서 그렇다.
            foreach (LocationId id in System.Enum.GetValues(typeof(LocationId)))
            {
                Tilemap ground;
                if (!tilemaps.TryGetValue($"Location_{id}", out ground) &&
                    !tilemaps.TryGetValue($"{id}Ground", out ground)) continue;

                ground.CompressBounds();
                var bounds = ground.cellBounds;
                if (bounds.size.x <= 0 || bounds.size.y <= 0)
                {
                    report.AppendLine($"  · {id}: 바닥을 아직 안 칠했습니다 (실행하면 임시 풀밭 30x25가 깔립니다).");
                    continue;
                }

                int painted = 0;
                foreach (var pos in bounds.allPositionsWithin)
                    if (ground.GetTile(pos) != null) painted++;

                int holes = bounds.size.x * bounds.size.y - painted;
                report.Append($"  · {id}: {bounds.size.x}x{bounds.size.y} 칸");
                report.AppendLine(holes > 0
                    ? $", 그중 {holes}칸이 비어 있습니다 — 그 칸은 화면에 빈 배경으로 보이고 걸어 다닐 수도 있습니다."
                    : " (빈틈 없음).");
            }

            // 이름을 잘못 지은 레이어는 조용히 무시돼서 찾기 어렵다 — 여기서 짚어 준다.
            var unknown = tilemaps.Keys.Where(n => !IsKnownLayerName(n)).OrderBy(n => n).ToList();
            if (unknown.Count > 0)
            {
                report.AppendLine("  이름 규칙에 맞지 않아 무시되는 타일맵: " + string.Join(", ", unknown));
                report.AppendLine("  (\"{레이어}_{맵}\" 이어야 합니다. 예: Grass_Farm1)");
            }

            Debug.Log(report.ToString());
        }

        [MenuItem(MenuRoot + "출구 타일 팔레트에 넣기", false, 3)]
        private static void AddExitTilesToPalette()
        {
            var tiles = new List<TileBase>();
            foreach (LocationId id in System.Enum.GetValues(typeof(LocationId)))
            {
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>($"{MarkerFolder}/Obj_Exit{id}.asset");
                if (tile != null) tiles.Add(tile);
            }
            if (tiles.Count == 0) { Debug.LogWarning("[MapSetup] Obj_Exit* 타일을 찾지 못했습니다."); return; }

            var contents = PrefabUtility.LoadPrefabContents(PalettePath);
            if (contents == null) { Debug.LogWarning($"[MapSetup] 마커 팔레트를 찾지 못했습니다: {PalettePath}"); return; }
            try
            {
                var map = contents.GetComponentInChildren<Tilemap>();
                if (map == null) { Debug.LogWarning("[MapSetup] 팔레트에 Tilemap이 없습니다."); return; }

                map.CompressBounds();
                var already = new HashSet<TileBase>();
                foreach (var pos in map.cellBounds.allPositionsWithin)
                {
                    var t = map.GetTile(pos);
                    if (t != null) already.Add(t);
                }

                // 칠해 둔 것을 덮지 않도록 기존 영역 아래 빈 줄에 왼쪽부터 한 줄로 놓는다.
                int row = map.cellBounds.yMin - 2, x = map.cellBounds.xMin, added = 0;
                foreach (var tile in tiles)
                {
                    if (already.Contains(tile)) continue;
                    map.SetTile(new Vector3Int(x++, row, 0), tile);
                    added++;
                }

                if (added == 0) { Debug.Log("[MapSetup] 팔레트에 이미 모든 출구 타일이 들어 있습니다."); return; }
                PrefabUtility.SaveAsPrefabAsset(contents, PalettePath);
                Debug.Log($"[MapSetup] 출구 타일 {added}개를 마커 팔레트에 넣었습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ---------- helpers ----------

        /// <summary>이 맵의 레이어가 이미 하나라도 있으면 그 Grid를 쓰고, 없으면 새로 만든다.</summary>
        private static Transform FindOrCreateGrid(LocationId id, Vector3 origin,
                                                  Dictionary<string, Tilemap> existing, List<string> created)
        {
            foreach (var layer in Layers)
            {
                Tilemap tm;
                if (!existing.TryGetValue($"{layer.prefix}_{id}", out tm) &&
                    !existing.TryGetValue($"{id}{layer.prefix}", out tm)) continue;
                var grid = tm.GetComponentInParent<Grid>();
                if (grid != null) return grid.transform;
                return tm.transform.parent != null ? tm.transform.parent : tm.transform;
            }

            var go = new GameObject($"{id} ({MapGraph.DisplayName(id)})");
            var newGrid = go.AddComponent<Grid>();
            newGrid.cellSize = new Vector3(1f, 1f, 0f);
            go.transform.position = origin;
            Undo.RegisterCreatedObjectUndo(go, "맵 레이어 만들기");
            created.Add($"{go.name} — Grid");
            return go.transform;
        }

        private static void CreateLayer(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            Undo.RegisterCreatedObjectUndo(go, "맵 레이어 만들기");
        }

        private static Dictionary<string, Tilemap> FindTilemapsByName()
        {
            var result = new Dictionary<string, Tilemap>();
            // 꺼 둔 레이어도 찾아야 한다 — 실행 중에 다른 맵 레이어는 꺼지기 때문이다.
            foreach (var tm in Object.FindObjectsOfType<Tilemap>(true))
                result[tm.name] = tm;
            return result;
        }

        /// <summary>map의 Objects 레이어에 target으로 가는 출구 마커가 칠해져 있는지.</summary>
        private static bool HasExitMarker(Dictionary<string, Tilemap> tilemaps, LocationId map, LocationId target)
        {
            Tilemap tm;
            if (!tilemaps.TryGetValue($"Objects_{map}", out tm) &&
                !tilemaps.TryGetValue($"{map}Objects", out tm)) return false;

            tm.CompressBounds();
            string wanted = ObjectMarkerDatabase.Normalize("Exit" + target);
            foreach (var pos in tm.cellBounds.allPositionsWithin)
            {
                var tile = tm.GetTile(pos);
                if (tile == null) continue;
                if (ObjectMarkerDatabase.Normalize(ObjectMarkerDatabase.StripPrefix(tile.name)) == wanted) return true;
            }
            return false;
        }

        /// <summary>GameLocation이 알아보는 레이어 이름인지 (계절 접두사도 함께 본다).</summary>
        private static bool IsKnownLayerName(string name)
        {
            foreach (Season s in System.Enum.GetValues(typeof(Season)))
            {
                string prefix = Seasons.Key(s) + "_";
                if (name.StartsWith(prefix, System.StringComparison.Ordinal))
                    return IsKnownLayerName(name.Substring(prefix.Length));
            }

            foreach (LocationId id in System.Enum.GetValues(typeof(LocationId)))
            {
                if (name == $"{id}Ground") return true;
                foreach (var layer in Layers)
                    if (name == $"{layer.prefix}_{id}" || name == $"{id}{layer.prefix}") return true;
            }
            return false;
        }
    }
}
