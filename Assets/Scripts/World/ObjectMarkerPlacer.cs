using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// "Objects_{맵}" 레이어를 읽어 실제 오브젝트를 놓는다. GameLocation에서 떼어낸 이유는
    /// 오브젝트가 늘어날수록 이쪽만 길어지기 때문이고, 실제로 늘어나는 것은 <b>표(ObjectMarkerDatabase)</b>
    /// 뿐이라 이 파일은 오브젝트를 아무리 추가해도 길이가 그대로다.
    ///
    /// 마스크 레이어와 달리 여기서는 "무엇을 칠했는지"가 중요하다 — 칠한 Tile 에셋의 이름이
    /// 무엇을 놓을지 정한다. 이름 규칙:
    ///   Obj_{그림 파일 이름}  ObjectMarkerDatabase에 등록된 오브젝트 (Obj_ShopCart, Obj_Rock_0)
    ///   Obj_{나무 id}         TreeDatabase의 나무 (Obj_Tree1)
    ///   Obj_Exit{맵이름}      밟으면 그 맵으로 넘어가는 칸 (Obj_ExitFarm2)
    ///
    /// "Obj_"는 붙여도 되고 안 붙여도 되며, <b>시트에서 잘라 낸 조각 이름을 그대로 써도 된다</b>
    /// ("tree1_spring_top" → 나무 tree1). 그래서 나무를 하나 더 넣어도 마커 타일 에셋을 손으로
    /// 만들 필요 없이, 시트의 아무 조각이나 팔레트에 끌어다 칠하면 된다.
    ///
    /// 이 레이어에는 <b>특별한 일을 하는 것만</b> 칠한다 — 집·문·맵 이동 출구·나무·바위처럼
    /// 코드가 알아야 하는 것들. 평범한 배경 오브젝트는 "Fixed_{맵}", 부수고 다시 놓을 수 있는
    /// 것은 "Breakable_{맵}" 레이어에 칠하면 되고 그 둘은 등록할 표가 없다
    /// (PaintedObjectPlacer가 칠한 그림을 그대로 쓴다).
    /// </summary>
    internal static class ObjectMarkerPlacer
    {
        /// <summary>
        /// 마커를 전부 읽어 배치한다. 나무와 바위는 저장되는 데이터라 <b>칸마다 딱 한 번만</b> 넣는다 —
        /// 맵에 들어올 때마다 넣으면 베어 낸 나무가 다시 들어올 때마다 되살아나고, 반대로 맵 전체에
        /// "한 번 했음" 표시 하나만 두면 세이브가 생긴 뒤 새로 칠한 마커가 영영 나오지 않는다.
        /// 어느 칸을 이미 만들었는지는 LocationData.spawnedMarkers에 남는다.
        /// </summary>
        public static void Apply(GameLocation loc, Tilemap markers, LocationData locData)
        {
            if (loc == null || markers == null) return;
            ObjectMarkerDatabase.Init();

            var spawned = LoadSpawned(loc, locData);
            int placed = 0, created = 0;
            var unknown = new HashSet<string>();

            for (int x = 0; x < loc.width; x++)
                for (int y = 0; y < loc.height; y++)
                {
                    var tile = markers.GetTile(markers.WorldToCell(new Vector3(x, y, 0f)));
                    if (tile == null) continue;

                    string name = ObjectMarkerDatabase.StripPrefix(tile.name);
                    if (string.IsNullOrEmpty(name)) continue;

                    // "Obj_ExitFarm2" 처럼 목적지 이름이 붙은 마커 — 밟으면 그 맵으로 간다.
                    if (name.StartsWith("Exit", System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryParseLocationName(name.Substring(4), out var target)) { loc.AddExit(x, y, target); placed++; }
                        else unknown.Add(tile.name);
                        continue;
                    }

                    var def = ObjectMarkerDatabase.Find(tile.name);
                    if (def == null) { unknown.Add(tile.name); continue; }

                    if (Place(loc, def, x, y, locData, spawned, ref created)) placed++;
                }

            Debug.Log($"[ObjectMarkers] {markers.name}: 마커 {placed}개를 배치했습니다." +
                      (created > 0 ? $" 그중 나무/바위 {created}개는 이번에 새로 만들었습니다." : ""));
            if (unknown.Count > 0)
            {
                // 울타리·길·가구를 여기 칠한 것은 흔한 실수라, 어느 레이어로 옮기면 되는지 짚어 준다.
                var misplaced = new List<string>();
                var nameless = new List<string>();
                foreach (var n in unknown)
                    (PlaceableDatabase.FindByTileName(ObjectMarkerDatabase.StripPrefix(n)) != null ? misplaced : nameless).Add(n);

                if (misplaced.Count > 0)
                {
                    Debug.LogWarning($"[ObjectMarkers] {markers.name}: 설치물을 마커 레이어에 칠했습니다 — " +
                                     string.Join(", ", misplaced) + $". 울타리·길·가구는 \"Breakable_{loc.id}\" " +
                                     "레이어에 칠해야 걷어내고 다시 놓을 수 있습니다.");
                }
                if (nameless.Count > 0)
                {
                    Debug.LogWarning($"[ObjectMarkers] {markers.name}: 이름을 알 수 없는 마커 타일 — " +
                                     string.Join(", ", nameless) + ". 이 레이어는 특별한 일을 하는 것만 " +
                                     "알아봅니다 (집·문·맵 이동 출구·나무·바위). 그냥 놓아 두는 장식이라면 " +
                                     $"\"Fixed_{loc.id}\" 레이어에 칠하세요 — 거기는 등록이 필요 없습니다.");
                }
            }

            if (!loc.doorExitTile.HasValue && (loc.id == LocationId.Farm1 || loc.id == LocationId.FarmHouse))
            {
                Debug.LogWarning($"[ObjectMarkers] {markers.name}: 출입구가 없습니다. " +
                                 "Farm1에는 Obj_House를, FarmHouse에는 Obj_Door를 칠해야 집을 드나들 수 있습니다.");
            }
        }

        /// <summary>
        /// 이미 만들어 낸 칸들을 읽어 온다. spawnedMarkers가 없던 예전 세이브는 여기서 한 번 옮긴다 —
        /// <b>지금 남아 있는</b> 나무·바위의 자리를 이미 만든 것으로 본다. 그래야 그 세이브에서
        /// 이미 자라 있던 것이 두 번 생기지 않으면서, 세이브가 생긴 뒤 칠해 둔 마커는 이제라도 나온다.
        /// (그 대신 예전에 베어 낸 마커 나무는 이 한 번만 다시 나타날 수 있다.)
        /// </summary>
        private static HashSet<Vector2Int> LoadSpawned(GameLocation loc, LocationData locData)
        {
            var spawned = new HashSet<Vector2Int>();
            if (locData == null) return spawned;

            foreach (var m in locData.spawnedMarkers) spawned.Add(new Vector2Int(m.x, m.y));

            if (!locData.markersMigrated)
            {
                locData.markersMigrated = true;
                int moved = 0;
                foreach (var t in locData.trees) if (MarkSpawned(locData, spawned, t.x, t.y)) moved++;
                foreach (var r in locData.rocks) if (MarkSpawned(locData, spawned, r.x, r.y)) moved++;

                if (moved > 0)
                {
                    Debug.Log($"[ObjectMarkers] {loc.id}: 예전 세이브라, 지금 남아 있는 나무/바위 " +
                              $"{moved}개의 자리를 '이미 만든 칸'으로 옮겼습니다. 이 뒤로는 " +
                              "새로 칠한 마커만 만들어집니다.");
                }
            }
            return spawned;
        }

        /// <summary>이 칸을 이미 만든 것으로 기록한다. 이미 기록돼 있으면 false.</summary>
        private static bool MarkSpawned(LocationData locData, HashSet<Vector2Int> spawned, int x, int y)
        {
            if (locData == null || !spawned.Add(new Vector2Int(x, y))) return false;
            locData.spawnedMarkers.Add(new MarkerSpawnData { x = x, y = y });
            return true;
        }

        /// <summary>
        /// 오브젝트 하나를 놓는다. 그림 그리기와 발판 막기는 표만 보고 처리하고,
        /// 특별한 등록이 필요한 것들만 role로 갈라진다.
        /// </summary>
        private static bool Place(GameLocation loc, ObjectMarkerDef def, int x, int y,
                                  LocationData locData, HashSet<Vector2Int> spawned, ref int created)
        {
            // 저장되는 지형지물은 그림을 여기서 놓지 않는다 — 데이터로 넣으면 RestoreFeatures가 그린다.
            // 한 칸은 한 번만 만든다: 베어 낸 나무는 되살아나지 않고, 새로 칠한 마커는 그때 나온다.
            if (def.role == MarkerRole.Tree)
            {
                if (!MarkSpawned(locData, spawned, x, y)) return false;
                GameLocation.AddTree(locData, x, y, def.treeId);
                created++;
                return true;
            }
            if (def.role == MarkerRole.Rock)
            {
                if (!MarkSpawned(locData, spawned, x, y)) return false;
                GameLocation.AddRock(locData, x, y, def.rockVariant);
                created++;
                return true;
            }

            // 그림은 offset만큼 올려 그리되, 앞뒤는 발밑(마커를 찍은 칸)으로 정한다.
            var sr = loc.PlaceObject(def.GetSprite(), x + def.offset.x, y + def.offset.y, y, def.sortBias);
            if (def.floorDecor) sr.sortingOrder = Depth.FloorDecor;

            if (def.blocks)
            {
                // 발판은 "치울 수 없는" 막힘이다 — 이 위에 선 나무를 베어도 집은 계속 막아야 한다.
                int x0 = x + def.footprintAnchor.x, y0 = y + def.footprintAnchor.y;
                for (int fx = 0; fx < def.size.x; fx++)
                    for (int fy = 0; fy < def.size.y; fy++)
                        loc.SetObjectBlocked(x0 + fx, y0 + fy);
            }

            switch (def.role)
            {
                case MarkerRole.House:
                    // 아래줄 가운데 칸이 농가로 들어가는 문. 발판에서 이 한 칸만 다시 뚫는다.
                    loc.doorExitTile = new Vector2Int(x + def.size.x / 2, y);
                    loc.SetBlocked(loc.doorExitTile.Value.x, loc.doorExitTile.Value.y, false);
                    break;

                case MarkerRole.ShippingBox:
                    loc.shippingBoxTile = new Vector2Int(x, y);
                    loc.SetShippingBoxRenderer(sr);
                    break;

                case MarkerRole.Shop:
                    loc.shopTile = new Vector2Int(x, y);
                    break;

                case MarkerRole.Workbench:
                    loc.workbenchTile = new Vector2Int(x, y);
                    break;

                case MarkerRole.Bed:
                    loc.bedTile = new Vector2Int(x, y);
                    break;

                case MarkerRole.Door:
                    loc.doorExitTile = new Vector2Int(x, y);
                    loc.SetBlocked(x, y, false);
                    loc.SetBlocked(x, y - 1, false);   // 문 아래 벽줄을 열어 준다
                    break;
            }
            return true;
        }

        /// <summary>"Farm2" -> LocationId.Farm2 (대소문자 무시).</summary>
        private static bool TryParseLocationName(string name, out LocationId locId)
        {
            string key = ObjectMarkerDatabase.Normalize(name);
            foreach (LocationId candidate in System.Enum.GetValues(typeof(LocationId)))
            {
                if (key == ObjectMarkerDatabase.Normalize(candidate.ToString())) { locId = candidate; return true; }
            }
            locId = default;
            return false;
        }
    }
}
