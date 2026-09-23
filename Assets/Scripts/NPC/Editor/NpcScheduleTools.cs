using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP.Editor
{
    /// <summary>
    /// NPC 일과표(스케줄)를 씬에 <b>칠해서</b> 만들기 위한 도구.
    ///
    ///   자리 타일 만들기   json의 스케줄에 적힌 spot 이름마다 "Obj_Spot{이름}" 타일을 만들고
    ///                     마커 팔레트에 넣는다. 그다음 씬의 "Objects_{맵}" 레이어에 칠하면 끝이다.
    ///   스케줄 검사        when에 적은 말이 알아들을 수 있는지, 스케줄이 부르는 자리를 실제로
    ///                     칠해 뒀는지, 그 칸이 막혀 있지는 않은지 한꺼번에 훑는다.
    ///
    /// 즉 작업 순서는 <b>json에 이름을 적고 → 타일을 만들고 → 씬에 칠하고 → 검사</b> 네 걸음이다.
    /// 좌표는 한 번도 적지 않는다.
    /// </summary>
    internal static class NpcScheduleTools
    {
        private const string MenuRoot = "Tools/FarmMVP/";
        private const string MarkerFolder = "Assets/Resources/Sprites/Tiles/Markers";
        private const string PalettePath = MarkerFolder + "/object.prefab";

        /// <summary>자리 타일은 따로 모아 둔다 — 출구·집·나무 마커와 섞이지 않게.</summary>
        private const string SpotFolder = "Assets/Resources/Sprites/Tiles/Spot";

        /// <summary>자리 마커를 팔레트에서 한눈에 알아보게 하는 색 (출구는 주황이라 이쪽은 하늘색).</summary>
        private static readonly Color SpotColor = new Color(0.35f, 0.75f, 1f, 1f);

        [MenuItem(MenuRoot + "NPC 자리 타일 만들기", false, 20)]
        private static void CreateSpotTiles()
        {
            var wanted = new SortedSet<string>();
            foreach (var use in CollectSpots()) wanted.Add(use.spot);

            if (wanted.Count == 0)
            {
                Debug.LogWarning("[NpcSchedule] json의 schedule에서 spot 이름을 하나도 찾지 못했습니다.");
                return;
            }

            var sprite = MarkerSprite();
            if (sprite == null)
            {
                Debug.LogWarning($"[NpcSchedule] 마커 그림을 찾지 못했습니다 ({MarkerFolder}/Obj_ExitFarm1.asset).");
                return;
            }

            var tiles = new List<TileBase>();
            var created = new List<string>();
            foreach (var spot in wanted)
            {
                string path = $"{SpotFolder}/Obj_Spot{spot}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.color = SpotColor;
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.CreateAsset(tile, path);
                    created.Add("Obj_Spot" + spot);
                }
                tiles.Add(tile);
            }
            AssetDatabase.SaveAssets();

            int added = AddToPalette(tiles);
            Debug.Log($"[NpcSchedule] 자리 타일 {wanted.Count}개 중 {created.Count}개를 새로 만들었습니다" +
                      (created.Count > 0 ? " (" + string.Join(", ", created) + ")" : "") +
                      $". 팔레트에 {added}개를 넣었습니다.\n" +
                      "씬의 \"Objects_{맵}\" 레이어에 칠한 뒤 \"NPC 스케줄 검사\"로 확인하세요.");
        }

        [MenuItem(MenuRoot + "NPC 스케줄 검사", false, 21)]
        private static void Validate()
        {
            var report = new StringBuilder("[NpcSchedule] 스케줄 검사\n");
            int problems = 0;

            var painted = PaintedSpots();

            foreach (var def in NpcDatabase.All)
            {
                report.AppendLine($"· {def.id} ({def.displayName})");

                if (NpcDatabase.Portrait(def.id, def.defaultPortrait) == null)
                { report.AppendLine("   ! 초상화를 한 장도 찾지 못했습니다 (Portraits 폴더와 .meta 확인)."); problems++; }

                foreach (var name in UsedPortraits(def))
                {
                    if (NpcDatabase.Portrait(def.id, name) != null) continue;
                    report.AppendLine($"   ! 초상화 \"{name}\"이 없습니다. 있는 것: " +
                                      string.Join(", ", NpcDatabase.PortraitNames(def.id)));
                    problems++;
                }

                if (def.schedule == null || def.schedule.IsEmpty)
                { report.AppendLine("   (스케줄 없음 — home에 서 있습니다)"); continue; }

                bool hasDefault = false;
                foreach (var routine in def.schedule.routines)
                {
                    if (routine == null) continue;
                    var bad = NpcSchedule.UnknownTags(routine.when);
                    if (bad.Count > 0)
                    { report.AppendLine($"   ! when \"{routine.when}\": 모르는 말 {string.Join(", ", bad)} — 이 일과는 영영 쓰이지 않습니다."); problems++; }
                    if (string.IsNullOrWhiteSpace(routine.when)) hasDefault = true;
                    if (routine.stops == null || routine.stops.Length == 0)
                    { report.AppendLine($"   ! when \"{routine.when}\": 정류장이 없습니다."); problems++; }
                }
                if (!hasDefault)
                { report.AppendLine("   ! when을 비운 기본 일과가 없습니다 — 조건이 안 맞는 날엔 home에 서 있게 됩니다."); problems++; }
            }

            foreach (var use in CollectSpots())
            {
                if (painted.TryGetValue(use.map, out var spots) && spots.Contains(use.spot.ToLowerInvariant())) continue;
                report.AppendLine($"   ! {use.npc}: {use.map} 맵에 \"Obj_Spot{use.spot}\"이 칠해져 있지 않습니다.");
                problems++;
            }

            // 어느 맵에도 안 쓰이는 자리 타일은 지워도 되는 것이라 알려만 준다.
            var used = new HashSet<string>(CollectSpots().Select(u => u.spot.ToLowerInvariant()));
            foreach (var pair in painted)
                foreach (var spot in pair.Value)
                    if (!used.Contains(spot))
                        report.AppendLine($"   (참고) {pair.Key}에 칠한 \"Obj_Spot{spot}\"을 부르는 스케줄이 없습니다.");

            report.AppendLine(problems == 0 ? "문제 없음." : $"문제 {problems}건.");
            if (problems == 0) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        // ---------- 읽기 ----------
        private struct SpotUse { public string npc; public LocationId map; public string spot; }

        /// <summary>모든 NPC의 스케줄에서 (맵, 자리 이름)을 모은다. map을 비운 정류장은 앞 정류장을 따라간다.</summary>
        private static List<SpotUse> CollectSpots()
        {
            var result = new List<SpotUse>();
            foreach (var def in NpcDatabase.All)
            {
                if (def.schedule == null || def.schedule.routines == null) continue;
                foreach (var routine in def.schedule.routines)
                {
                    if (routine == null || routine.stops == null) continue;
                    var map = def.HomeLocation;
                    foreach (var stop in routine.stops)
                    {
                        if (stop == null) continue;
                        if (!string.IsNullOrEmpty(stop.map) && System.Enum.TryParse(stop.map, out LocationId parsed)) map = parsed;
                        if (stop.HasTile || string.IsNullOrEmpty(stop.spot)) continue;
                        result.Add(new SpotUse { npc = def.id, map = map, spot = stop.spot });
                    }
                }
            }
            return result;
        }

        /// <summary>대사·선물에 적어 둔 초상화 이름 전부.</summary>
        private static IEnumerable<string> UsedPortraits(NpcDefinition def)
        {
            var names = new List<string>();
            void Add(string n) { if (!string.IsNullOrEmpty(n)) names.Add(n); }

            Add(def.alreadyTalkedPortrait);
            Add(def.giftLimitPortrait);
            if (def.giftLines != null)
                foreach (GiftTier tier in System.Enum.GetValues(typeof(GiftTier))) Add(def.giftLines.PortraitFor(tier));
            if (def.itemGiftLines != null)
                foreach (var l in def.itemGiftLines) if (l != null) Add(l.portrait);
            if (def.dialogues != null)
                foreach (var d in def.dialogues)
                {
                    if (d == null) continue;
                    Add(d.portrait);
                    if (d.choices == null) continue;
                    foreach (var c in d.choices) if (c != null) Add(c.portrait);
                }
            return names.Distinct();
        }

        /// <summary>씬의 "Objects_{맵}" 레이어에 칠해 둔 자리 이름들 (소문자로 모은다).</summary>
        private static Dictionary<LocationId, HashSet<string>> PaintedSpots()
        {
            var byName = new Dictionary<string, Tilemap>();
            foreach (var tm in Object.FindObjectsOfType<Tilemap>(true)) byName[tm.name] = tm;

            var result = new Dictionary<LocationId, HashSet<string>>();
            foreach (LocationId id in System.Enum.GetValues(typeof(LocationId)))
            {
                Tilemap tm;
                if (!byName.TryGetValue($"Objects_{id}", out tm) && !byName.TryGetValue($"{id}Objects", out tm)) continue;

                var spots = new HashSet<string>();
                tm.CompressBounds();
                foreach (var pos in tm.cellBounds.allPositionsWithin)
                {
                    var tile = tm.GetTile(pos);
                    if (tile == null) continue;
                    string name = ObjectMarkerDatabase.StripPrefix(tile.name);
                    if (name != null && name.Length > 4 && name.StartsWith("Spot", System.StringComparison.OrdinalIgnoreCase))
                        spots.Add(name.Substring(4).ToLowerInvariant());
                }
                result[id] = spots;
            }
            return result;
        }

        // ---------- 팔레트 ----------
        private static Sprite MarkerSprite()
        {
            // 출구 마커와 같은 그림을 쓴다 — 어차피 색으로 구분하고, 그림을 새로 만들 이유가 없다.
            var exit = AssetDatabase.LoadAssetAtPath<Tile>($"{MarkerFolder}/Obj_ExitFarm1.asset");
            return exit != null ? exit.sprite : null;
        }

        private static int AddToPalette(List<TileBase> tiles)
        {
            var contents = PrefabUtility.LoadPrefabContents(PalettePath);
            if (contents == null) { Debug.LogWarning($"[NpcSchedule] 마커 팔레트를 찾지 못했습니다: {PalettePath}"); return 0; }
            try
            {
                var map = contents.GetComponentInChildren<Tilemap>();
                if (map == null) { Debug.LogWarning("[NpcSchedule] 팔레트에 Tilemap이 없습니다."); return 0; }

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
                if (added > 0) PrefabUtility.SaveAsPrefabAsset(contents, PalettePath);
                return added;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
