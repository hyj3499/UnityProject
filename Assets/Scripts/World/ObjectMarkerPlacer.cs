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
    ///   Obj_{나무 id}         TreeDatabase의 나무 (Obj_Apricot)
    ///   Obj_Exit{맵이름}      밟으면 그 맵으로 넘어가는 칸 (Obj_ExitFarm2)
    /// </summary>
    internal static class ObjectMarkerPlacer
    {
        /// <summary>
        /// 마커를 전부 읽어 배치한다. 나무와 바위는 저장되는 데이터라 <b>새 게임일 때만</b> 넣는다 —
        /// 맵에 들어올 때마다 넣으면 베어 낸 나무가 다시 들어올 때마다 되살아난다.
        /// </summary>
        public static void Apply(GameLocation loc, Tilemap markers, LocationData locData)
        {
            if (loc == null || markers == null) return;
            ObjectMarkerDatabase.Init();

            bool addTrees = locData != null && !locData.initialized;
            bool addRocks = locData != null && !locData.rocksInitialized;
            int placed = 0;
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

                    if (Place(loc, def, x, y, locData, addTrees, addRocks)) placed++;
                }

            if (locData != null)
            {
                if (addTrees) locData.initialized = true;
                if (addRocks) locData.rocksInitialized = true;
            }

            Debug.Log($"[ObjectMarkers] {markers.name}: 마커 {placed}개를 배치했습니다.");
            if (unknown.Count > 0)
            {
                Debug.LogWarning($"[ObjectMarkers] {markers.name}: 이름을 알 수 없는 마커 타일 — " +
                                 string.Join(", ", unknown) + " — 마커 이름은 그림 파일 이름 그대로입니다 " +
                                 "(Sprites/Environment/ShopCart.png -> Obj_ShopCart). " +
                                 "Tools/Farm 메뉴로 다시 만들면 이름이 맞춰집니다.");
            }

            if (!loc.doorExitTile.HasValue && (loc.id == LocationId.Farm1 || loc.id == LocationId.FarmHouse))
            {
                Debug.LogWarning($"[ObjectMarkers] {markers.name}: 출입구가 없습니다. " +
                                 "Farm1에는 Obj_House를, FarmHouse에는 Obj_Door를 칠해야 집을 드나들 수 있습니다.");
            }
        }

        /// <summary>
        /// 오브젝트 하나를 놓는다. 그림 그리기와 발판 막기는 표만 보고 처리하고,
        /// 특별한 등록이 필요한 것들만 role로 갈라진다.
        /// </summary>
        private static bool Place(GameLocation loc, ObjectMarkerDef def, int x, int y,
                                  LocationData locData, bool addTrees, bool addRocks)
        {
            // 저장되는 지형지물은 그림을 여기서 놓지 않는다 — 데이터로 넣으면 RestoreFeatures가 그린다.
            if (def.role == MarkerRole.Tree)
            {
                if (!addTrees || locData == null) return false;
                GameLocation.AddTree(locData, x, y, def.treeId);
                return true;
            }
            if (def.role == MarkerRole.Rock)
            {
                if (!addRocks || locData == null) return false;
                GameLocation.AddRock(locData, x, y, def.rockVariant);
                return true;
            }

            var sr = loc.PlaceObject(def.GetSprite(), x + def.offset.x, y + def.offset.y, def.sortingOrder);
            sr.sortingOrder = def.sortingOrder;

            if (def.blocks)
            {
                int x0 = x + def.footprintAnchor.x, y0 = y + def.footprintAnchor.y;
                for (int fx = 0; fx < def.size.x; fx++)
                    for (int fy = 0; fy < def.size.y; fy++)
                        loc.SetBlocked(x0 + fx, y0 + fy, true);
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
