#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP.EditorTools
{
    /// <summary>
    /// "Objects_{위치}" 레이어에 칠할 마커 타일을 만들어 준다.
    ///
    /// GameLocation.ApplyObjectMarkers는 칠한 Tile 에셋의 <b>이름</b>으로 무엇을 놓을지 정한다.
    /// 손으로 만들면 이름을 한 글자만 틀려도 조용히 무시되므로, 여기서 한 번에 만들어 이름이
    /// 어긋날 일을 없앤다. 각 마커에는 실제 오브젝트 스프라이트를 넣어 두기 때문에
    /// 타일 팔레트와 씬에서 배치가 그대로 미리 보인다.
    /// </summary>
    public static class ObjectMarkerTileGenerator
    {
        private const string OutDir = "Assets/Resources/Tiles/Markers";

        [MenuItem("Tools/Farm/오브젝트 마커 타일 만들기")]
        public static void Generate()
        {
            AssetLibrary.EnsureLoaded();

            var markers = new Dictionary<string, Sprite>
            {
                { "Obj_House", AssetLibrary.House },
                { "Obj_ShippingBox", AssetLibrary.ShippingBox },
                { "Obj_Shop", AssetLibrary.ShopCart },
                { "Obj_Tree", AssetLibrary.Tree },
                { "Obj_Rock", AssetLibrary.RockVariants != null && AssetLibrary.RockVariants.Length > 0
                                  ? AssetLibrary.RockVariants[0] : null },
                { "Obj_Bed", AssetLibrary.Bed },
                { "Obj_Door", AssetLibrary.Door },
                { "Obj_Fireplace", AssetLibrary.Fireplace },
                { "Obj_Plant", AssetLibrary.Plant },
                { "Obj_Rug", AssetLibrary.Rug },
            };

            // 맵 이동 출구 — 칸 전체를 덮는 흙 타일에 목적지별 색을 입혀 영역이 한눈에 보이게 한다.
            var exitTints = new Dictionary<string, Color>
            {
                { "Obj_ExitFarm1", new Color(0.45f, 0.9f, 0.45f) },
                { "Obj_ExitFarm2", new Color(1f, 0.65f, 0.25f) },
                { "Obj_ExitFarmHouse", new Color(0.45f, 0.65f, 1f) },
            };

            if (!AssetDatabase.IsValidFolder(OutDir))
            {
                Directory(OutDir);
            }

            int created = 0, updated = 0, missing = 0;
            foreach (var kv in markers)
            {
                if (kv.Value == null)
                {
                    Debug.LogWarning($"[마커 타일] {kv.Key}: 스프라이트를 찾지 못해 건너뜁니다.");
                    missing++;
                    continue;
                }

                string path = $"{OutDir}/{kv.Key}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = kv.Value;
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.CreateAsset(tile, path);
                    created++;
                }
                else
                {
                    tile.sprite = kv.Value;   // 스프라이트가 바뀌었으면 갱신
                    EditorUtility.SetDirty(tile);
                    updated++;
                }
            }

            foreach (var kv in exitTints)
            {
                var sprite = AssetLibrary.Dirt;
                if (sprite == null)
                {
                    Debug.LogWarning($"[마커 타일] {kv.Key}: 바탕 스프라이트(Dirt)를 찾지 못해 건너뜁니다.");
                    missing++;
                    continue;
                }

                string path = $"{OutDir}/{kv.Key}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                bool isNew = tile == null;
                if (isNew) tile = ScriptableObject.CreateInstance<Tile>();

                tile.sprite = sprite;
                tile.color = kv.Value;
                tile.flags = TileFlags.LockColor;   // 색을 고정해야 타일맵에서 그대로 보인다
                tile.colliderType = Tile.ColliderType.None;

                if (isNew) { AssetDatabase.CreateAsset(tile, path); created++; }
                else { EditorUtility.SetDirty(tile); updated++; }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[마커 타일] {OutDir} — 새로 만듦 {created}개, 갱신 {updated}개, 건너뜀 {missing}개. " +
                      "타일 팔레트에 이 폴더를 끌어다 놓고 \"Objects_Farm1\" 레이어에 칠하세요.");
        }

        /// <summary>중간 폴더까지 차례로 만든다 (AssetDatabase.CreateFolder는 한 단계씩만 된다).</summary>
        private static void Directory(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];              // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
