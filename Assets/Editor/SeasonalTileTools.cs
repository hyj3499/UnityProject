#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP.EditorTools
{
    /// <summary>
    /// 이미 칠해 둔 바닥 타일맵을 계절 타일(SeasonalTile)로 바꿔 준다.
    ///
    /// 에셋 팩의 사계절 잔디 타일셋은 격자 배치가 완전히 같다(실루엣 픽셀 차이 0, 잘린 rect도 전부 동일).
    /// Unity가 자동 슬라이스하면 스프라이트 이름이 "Tileset Grass Spring_123" 처럼 <시트이름>_<번호>가
    /// 되므로, 경로에서 계절만 갈아 끼우고 같은 번호를 찾으면 짝이 나온다.
    /// 폴더로 나눠 두었든(Tiles/Spring/, Tiles/Winter/) 파일 이름에 붙였든 상관없다.
    ///
    /// 그래서 맵을 계절마다 다시 칠할 필요가 없다 — 한 번 칠한 것을 이 도구가 계절 타일로 승급시키고,
    /// 게임에서는 SeasonalTile이 그때그때 맞는 그림을 내준다.
    ///
    /// 쓰는 법: 4계절 타일셋을 모두 Sprite Mode: Multiple, 16x16 그리드로 임포트한 뒤
    ///          씬을 열고 Tools/Farm 메뉴를 실행한다. 없는 계절은 그냥 봄 그림으로 대체된다.
    /// </summary>
    public static class SeasonalTileTools
    {
        private const string OutDir = "Assets/Resources/Tiles/Seasonal";

        /// <summary>시트 이름 안에서 갈아 끼울 계절 낱말. 순서는 Season enum과 같아야 한다.</summary>
        private static readonly string[] SeasonWords = { "Spring", "Summer", "Fall", "Winter" };

        [MenuItem("Tools/Farm/칠해 둔 바닥을 계절 타일로 바꾸기")]
        public static void ConvertPaintedTilemaps()
        {
            var tilemaps = Object.FindObjectsOfType<Tilemap>();
            if (tilemaps.Length == 0)
            {
                Debug.LogWarning("[계절 타일] 씬에 Tilemap이 없습니다.");
                return;
            }

            EnsureFolder(OutDir);

            var cache = new Dictionary<string, SeasonalTile>();
            int converted = 0, skipped = 0, alreadySeasonal = 0;
            var unmatched = new HashSet<string>();

            foreach (var tm in tilemaps)
            {
                tm.CompressBounds();
                var bounds = tm.cellBounds;

                foreach (var cell in bounds.allPositionsWithin)
                {
                    var tile = tm.GetTile(cell);
                    if (tile == null) continue;
                    if (tile is SeasonalTile) { alreadySeasonal++; continue; }

                    var plain = tile as Tile;
                    if (plain == null || plain.sprite == null) { skipped++; continue; }

                    var seasonal = GetOrCreate(plain, cache, unmatched);
                    if (seasonal == null) { skipped++; continue; }

                    tm.SetTile(cell, seasonal);
                    converted++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[계절 타일] {converted}칸을 계절 타일로 바꿨습니다. " +
                      $"(이미 계절 타일 {alreadySeasonal}칸, 건너뜀 {skipped}칸, 만든 타일 {cache.Count}종)");
            if (unmatched.Count > 0)
            {
                Debug.LogWarning("[계절 타일] 다른 계절 짝을 못 찾은 스프라이트가 있습니다 — 그 칸은 네 계절이 " +
                                 "같은 그림으로 나옵니다: " + string.Join(", ", unmatched));
            }
        }

        /// <summary>
        /// 이 스프라이트에 대응하는 SeasonalTile을 만들거나 재사용한다.
        /// 봄 시트의 "..._123" 을 여름/가을/겨울 시트의 같은 번호와 짝지어 준다.
        /// </summary>
        private static SeasonalTile GetOrCreate(Tile source, Dictionary<string, SeasonalTile> cache,
                                                HashSet<string> unmatched)
        {
            string spriteName = source.sprite.name;
            if (cache.TryGetValue(spriteName, out var cached)) return cached;

            string assetName = "Seasonal_" + Sanitize(spriteName);
            string path = $"{OutDir}/{assetName}.asset";

            var tile = AssetDatabase.LoadAssetAtPath<SeasonalTile>(path);
            bool isNew = tile == null;
            if (isNew) tile = ScriptableObject.CreateInstance<SeasonalTile>();

            tile.seasonSprites = new Sprite[Seasons.SeasonCount];
            tile.colliderType = source.colliderType;

            int found = 0;
            for (int i = 0; i < Seasons.SeasonCount; i++)
            {
                var s = FindSeasonalSprite(source.sprite, i);
                tile.seasonSprites[i] = s;
                if (s != null) found++;
            }

            if (found == 0) { if (isNew) Object.DestroyImmediate(tile); return null; }
            if (found < Seasons.SeasonCount) unmatched.Add(spriteName);

            if (isNew) AssetDatabase.CreateAsset(tile, path);
            else EditorUtility.SetDirty(tile);

            cache[spriteName] = tile;
            return tile;
        }

        /// <summary>
        /// 원본 스프라이트가 든 시트의 짝(다른 계절 시트)을 찾아, 그 안에서 같은 번호의 스프라이트를 꺼낸다.
        ///
        /// 짝을 찾는 방법은 <b>경로에서 계절 낱말을 갈아 끼우는 것</b>이다. 폴더든 파일 이름이든
        /// 어디에 있어도 되고, 둘 다 있어도 된다:
        ///   Tiles/Spring/Tileset Grass Spring.png  ->  Tiles/Winter/Tileset Grass Winter.png
        ///   Tiles/Spring/ground.png                ->  Tiles/Winter/ground.png
        ///   Tiles/ground Spring.png                ->  Tiles/ground Winter.png
        /// 후보를 만들어 실제로 존재하는 것을 쓰므로, 폴더만 나눠도 되고 이름만 나눠도 된다.
        /// </summary>
        private static Sprite FindSeasonalSprite(Sprite source, int seasonIndex)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath)) return null;

            foreach (var candidate in SeasonPathCandidates(sourcePath, seasonIndex))
            {
                if (candidate == sourcePath) return source;
                if (!AssetDatabase.LoadAssetAtPath<Texture2D>(candidate)) continue;

                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(candidate))
                {
                    if (obj is Sprite sprite && SameIndex(sprite.name, source.name)) return sprite;
                }
                // 시트는 있는데 같은 번호가 없다 = 아직 16x16으로 자르지 않았다는 뜻
                return null;
            }
            return null;
        }

        /// <summary>
        /// 계절 낱말을 갈아 끼운 경로 후보들. 전부 바꾼 것 → 폴더만 → 파일 이름만 순서로,
        /// 실제로 존재하는 첫 번째가 쓰인다. 계절 낱말이 아예 없으면 아무것도 내놓지 않는다
        /// (그 시트는 사계절 공용으로 본다).
        /// </summary>
        private static IEnumerable<string> SeasonPathCandidates(string path, int seasonIndex)
        {
            int slash = path.LastIndexOf('/');
            string dir = slash >= 0 ? path.Substring(0, slash + 1) : "";
            string file = slash >= 0 ? path.Substring(slash + 1) : path;
            string target = SeasonWords[seasonIndex];

            for (int i = 0; i < SeasonWords.Length; i++)
            {
                string word = SeasonWords[i];
                bool inDir = dir.Contains(word), inFile = file.Contains(word);
                if (!inDir && !inFile) continue;

                if (inDir && inFile) yield return dir.Replace(word, target) + file.Replace(word, target);
                if (inDir) yield return dir.Replace(word, target) + file;
                if (inFile) yield return dir + file.Replace(word, target);
                yield break;   // 원본에 든 계절은 하나뿐이다
            }
        }

        /// <summary>"Tileset Grass Spring_123" 과 "Tileset Grass Winter_123" 의 끝 번호가 같은지.</summary>
        private static bool SameIndex(string a, string b)
        {
            int ai = a.LastIndexOf('_'), bi = b.LastIndexOf('_');
            if (ai < 0 || bi < 0) return a == b;
            return a.Substring(ai) == b.Substring(bi);
        }

        private static string Sanitize(string name)
        {
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] == ' ' || chars[i] == '/' || chars[i] == '\\') chars[i] = '_';
            return new string(chars);
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
