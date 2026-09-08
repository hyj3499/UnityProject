using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// 칠해 둔 바닥 타일을 <b>실행 중에</b> 계절 타일로 바꿔 준다. 에디터에서 미리 변환하거나
    /// 계절 타일 에셋을 만들어 둘 필요가 없다.
    ///
    /// 짝짓기 규칙 — 스프라이트가 든 시트 이름에서 계절 낱말을 갈아 끼우고, 계절 폴더에서 찾는다:
    ///   Sprites/Tiles/Spring/Tileset Grass Spring  ->  Sprites/Tiles/Winter/Tileset Grass Winter
    ///   Sprites/Tiles/Spring/ground                ->  Sprites/Tiles/Winter/ground
    /// 잘린 스프라이트 이름이 "<시트>_<번호>" 라서 번호가 같은 것을 짝으로 본다. 네 시트의 격자가
    /// 같은 위치로 잘려 있기만 하면 된다.
    ///
    /// 비용: 위치를 로드할 때 칠해진 칸을 한 번 훑는다(수백 칸이면 무시할 수준). 한 번 바꿔 둔 칸은
    /// SeasonalTile이라 다음부터 건너뛰고, 계절이 바뀌면 RefreshAllTiles()만으로 그림이 바뀐다.
    /// 시트와 만들어 둔 타일은 정적으로 캐시하므로 Resources 접근은 시트당 딱 한 번이다.
    /// </summary>
    public static class SeasonalTileset
    {
        /// <summary>계절 폴더가 놓이는 Resources 안의 뿌리. Sprites/Tiles/{계절}/{시트}.</summary>
        private const string Root = "Sprites/Tiles";

        /// <summary>Season enum과 순서가 같아야 한다.</summary>
        private static readonly string[] SeasonWords = { "Spring", "Summer", "Fall", "Winter" };

        // 시트 경로 -> (번호 -> 스프라이트). 없는 시트는 null로 기억해서 두 번 찾지 않는다.
        private static readonly Dictionary<string, Dictionary<string, Sprite>> _sheets =
            new Dictionary<string, Dictionary<string, Sprite>>();

        // 원본 스프라이트 이름 -> 계절 타일. 짝이 없으면 null을 넣어 두고 다시 시도하지 않는다.
        private static readonly Dictionary<string, SeasonalTile> _tiles = new Dictionary<string, SeasonalTile>();

        /// <summary>
        /// 이 타일맵에서 아직 평범한 타일인 칸을 계절 타일로 바꾼다. 바꾼 칸 수를 돌려준다.
        /// 여러 번 불러도 안전하다 (이미 바뀐 칸은 건너뛴다).
        /// </summary>
        public static int Apply(Tilemap map)
        {
            if (map == null) return 0;

            map.CompressBounds();
            int converted = 0;

            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile(cell);
                if (tile == null || tile is SeasonalTile) continue;

                var plain = tile as Tile;
                if (plain == null || plain.sprite == null) continue;

                var seasonal = GetOrCreate(plain);
                if (seasonal == null) continue;   // 다른 계절 짝이 없는 타일은 그대로 둔다

                map.SetTile(cell, seasonal);
                converted++;
            }
            return converted;
        }

        private static SeasonalTile GetOrCreate(Tile source)
        {
            string key = source.sprite.name;
            if (_tiles.TryGetValue(key, out var cached)) return cached;

            var anim = TileAnimationDatabase.ForSheet(source.sprite.texture != null
                                                      ? source.sprite.texture.name : null);

            var frames = new SeasonalTile.Frames[Seasons.SeasonCount];
            int found = 0;
            bool animated = false;
            for (int i = 0; i < Seasons.SeasonCount; i++)
            {
                var sprites = FindSeasonalFrames(source.sprite, i, anim);
                if (sprites == null) continue;
                frames[i] = new SeasonalTile.Frames { sprites = sprites };
                found++;
                if (sprites.Length > 1) animated = true;
            }

            // 자기 계절 하나뿐이고 움직이지도 않으면 바꿀 이유가 없다 — 평범한 타일이 더 싸다.
            if (found <= 1 && !animated)
            {
                _tiles[key] = null;
                return null;
            }

            var tile = ScriptableObject.CreateInstance<SeasonalTile>();
            tile.name = "Seasonal_" + key;
            tile.seasonFrames = frames;
            tile.fps = anim != null ? anim.fps : 4f;
            tile.colliderType = source.colliderType;
            _tiles[key] = tile;
            return tile;
        }

        /// <summary>
        /// 이 계절에 해당하는 그림들. 애니메이션 시트면 한 벌(여러 장), 아니면 한 장.
        /// 짝이 되는 시트를 못 찾으면 null.
        /// </summary>
        private static Sprite[] FindSeasonalFrames(Sprite source, int seasonIndex, TileAnimationDef anim)
        {
            var texture = source.texture;
            if (texture == null || string.IsNullOrEmpty(texture.name)) return null;

            foreach (string path in Candidates(texture.name, SeasonWords[seasonIndex]))
            {
                var sheet = LoadSheet(path);
                if (sheet == null) continue;
                return CollectFrames(sheet, source.name, anim);
            }
            return null;
        }

        /// <summary>
        /// 시트에서 이 타일의 프레임들을 모은다. 애니메이션 시트가 아니면 한 장짜리 배열.
        ///
        /// 다음 프레임은 스프라이트 이름 번호의 일정한 간격 뒤에 있다 (TileAnimationDatabase 참고).
        /// </summary>
        private static Sprite[] CollectFrames(Dictionary<string, Sprite> sheet, string sourceName,
                                              TileAnimationDef anim)
        {
            string wanted = IndexKey(sourceName);
            if (!sheet.TryGetValue(wanted, out var first)) return null;
            if (anim == null) return new[] { first };

            if (anim.frameIndexStride <= 0 || !int.TryParse(wanted.TrimStart('_'), out int index))
                return new[] { first };

            // _93, _186, _279을 칠했어도 _0 계열의 같은 애니메이션으로 보정한다.
            int baseIndex = index % anim.frameIndexStride;

            var frames = new List<Sprite>(anim.frameCount);
            for (int k = 0; k < anim.frameCount; k++)
            {
                int frameIndex = baseIndex + k * anim.frameIndexStride;
                if (sheet.TryGetValue("_" + frameIndex, out var s)) frames.Add(s);
            }

            // Unity Tilemap은 배열 끝에서 처음으로 되돌아가므로, 역방향 프레임을 배열에 넣어
            // 0 -> 93 -> 186 -> 279 -> 186 -> 93 -> 0 순환을 데이터만으로 만든다.
            if (anim.pingPong)
                for (int k = frames.Count - 2; k > 0; k--) frames.Add(frames[k]);
            return frames.Count > 0 ? frames.ToArray() : new[] { first };
        }

        /// <summary>찾아볼 시트 경로들. 폴더로 나눈 경우와 이름에 계절을 붙인 경우를 모두 받는다.</summary>
        private static IEnumerable<string> Candidates(string sheetName, string targetSeason)
        {
            foreach (string word in SeasonWords)
            {
                if (!sheetName.Contains(word)) continue;
                string swapped = sheetName.Replace(word, targetSeason);
                yield return $"{Root}/{targetSeason}/{swapped}";   // Tiles/Winter/Tileset Grass Winter
                yield return $"{Root}/{swapped}";                  // Tiles/Tileset Grass Winter
                break;                                            // 시트 이름에 든 계절은 하나뿐이다
            }
            yield return $"{Root}/{targetSeason}/{sheetName}";     // 폴더만 다른 경우
        }

        private static Dictionary<string, Sprite> LoadSheet(string path)
        {
            if (_sheets.TryGetValue(path, out var cached)) return cached;

            var all = Resources.LoadAll<Sprite>(path);
            Dictionary<string, Sprite> byIndex = null;
            if (all != null && all.Length > 0)
            {
                byIndex = new Dictionary<string, Sprite>(all.Length);
                foreach (var s in all) byIndex[IndexKey(s.name)] = s;
            }
            _sheets[path] = byIndex;
            return byIndex;
        }

        /// <summary>"Tileset Grass Spring_123" -> "_123". 시트가 달라도 번호가 같으면 같은 칸이다.</summary>
        private static string IndexKey(string spriteName)
        {
            int at = spriteName.LastIndexOf('_');
            return at >= 0 ? spriteName.Substring(at) : spriteName;
        }
    }
}
