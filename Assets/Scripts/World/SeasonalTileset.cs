using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmMVP
{
    /// <summary>
    /// 칠해 둔 바닥 타일을 <b>실행 중에</b> 계절 타일로 바꿔 준다. 에디터에서 미리 변환하거나
    /// 계절 타일 에셋을 만들어 둘 필요가 없다.
    ///
    /// 짝짓기 규칙은 두 가지고, 시트를 어떻게 만들었느냐에 따라 알아서 골라 쓴다.
    ///
    /// (1) <b>계절마다 시트가 따로</b> 있는 경우 — 시트 이름에서 계절 낱말을 갈아 끼우고 계절 폴더에서 찾는다:
    ///   Sprites/Tiles/Spring/Tileset Grass Spring  ->  Sprites/Tiles/Winter/Tileset Grass Winter
    ///   Sprites/Tiles/Spring/ground                ->  Sprites/Tiles/Winter/ground
    /// 잘린 스프라이트 이름이 "<시트>_<번호>" 라서 번호가 같은 것을 짝으로 본다. 네 시트의 격자가
    /// 같은 위치로 잘려 있기만 하면 된다.
    ///
    /// (2) <b>한 시트 안에 네 계절이 다 들어 있는</b> 경우 — 슬라이스 이름에 계절을 붙여 짝을 짓는다:
    ///   ALL props seasons_0  ->  ALL props seasons_0_Summer / _Fall / _Winter
    /// 접미사가 없는 이름은 그 시트가 들어 있는 계절 폴더(Sprites/Tiles/Spring/... 이면 봄)의 그림으로 본다.
    /// 프롭처럼 계절 변형이 시트 안에 흩어져 있어 격자를 맞출 수 없는 그림에 쓴다. Sprite Editor에서
    /// 슬라이스 이름만 붙이면 되고, 그림을 계절별로 자를 필요가 없다.
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

        // 시트 이름 -> 그 시트 (규칙 2용. 슬라이스를 번호가 아니라 이름 그대로 찾는다).
        private static readonly Dictionary<string, Sheet> _byName = new Dictionary<string, Sheet>();

        /// <summary>이름으로 찾은 시트 하나. season은 이 시트가 들어 있던 계절 폴더.</summary>
        private class Sheet
        {
            public Dictionary<string, Sprite> sprites;
            public int season;
        }

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
                // 규칙 1(계절 폴더의 짝 시트)이 없으면 규칙 2(한 시트 안의 "_계절" 슬라이스)를 본다.
                var sprites = FindSeasonalFrames(source.sprite, i, anim)
                              ?? FindSuffixFrames(source.sprite, i);
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

            // "..._0_Fall" 처럼 계절 접미사가 붙은 슬라이스는 규칙 2의 몫이다. 여기서 처리하면
            // IndexKey가 "_Fall"이 되어 같은 접미사를 가진 아무 슬라이스나 짝으로 걸린다.
            if (StripSeasonSuffix(source.name) != source.name) return null;

            foreach (string path in Candidates(texture.name, SeasonWords[seasonIndex]))
            {
                var sheet = LoadSheet(path);
                if (sheet == null) continue;
                return CollectFrames(sheet, source.name, anim);
            }
            return null;
        }

        /// <summary>
        /// 규칙 2 — 같은 시트 안에서 "<이름>_<계절>" 슬라이스를 찾는다. 접미사가 없는 이름은
        /// 시트가 들어 있는 계절 폴더의 그림으로 본다. 계절 변형이 시트 안에 흩어져 있어 번호로는
        /// 짝을 지을 수 없는 그림(프롭 모음집 등)을 위한 규칙이라 프레임은 항상 한 장이다.
        /// </summary>
        private static Sprite[] FindSuffixFrames(Sprite source, int seasonIndex)
        {
            var texture = source.texture;
            if (texture == null || string.IsNullOrEmpty(texture.name)) return null;

            var sheet = FindSheetByName(texture.name);
            if (sheet == null) return null;

            string baseName = StripSeasonSuffix(source.name);
            if (sheet.sprites.TryGetValue(baseName + "_" + SeasonWords[seasonIndex], out var s))
                return new[] { s };
            // 시트가 놓인 폴더의 계절은 접미사 없이 쓴다 (Spring 폴더의 "..._0" = 봄 그림).
            if (seasonIndex == sheet.season && sheet.sprites.TryGetValue(baseName, out var bare))
                return new[] { bare };
            return null;
        }

        /// <summary>이 시트가 어느 계절 폴더에 있든 찾아서, 슬라이스를 이름 그대로 담아 둔다.</summary>
        private static Sheet FindSheetByName(string sheetName)
        {
            if (_byName.TryGetValue(sheetName, out var cached)) return cached;

            Sheet found = null;
            for (int i = 0; i < SeasonWords.Length && found == null; i++)
                found = TryLoadNamedSheet($"{Root}/{SeasonWords[i]}/{sheetName}", i);
            if (found == null) found = TryLoadNamedSheet($"{Root}/{sheetName}", 0);

            _byName[sheetName] = found;
            return found;
        }

        private static Sheet TryLoadNamedSheet(string path, int season)
        {
            var all = Resources.LoadAll<Sprite>(path);
            if (all == null || all.Length == 0) return null;

            var map = new Dictionary<string, Sprite>(all.Length);
            foreach (var sp in all) map[sp.name] = sp;
            return new Sheet { sprites = map, season = season };
        }

        /// <summary>"ALL props seasons_0_Fall" -> "ALL props seasons_0". 계절 접미사가 없으면 그대로.</summary>
        private static string StripSeasonSuffix(string spriteName)
        {
            foreach (string word in SeasonWords)
            {
                string suffix = "_" + word;
                if (spriteName.EndsWith(suffix, System.StringComparison.Ordinal))
                    return spriteName.Substring(0, spriteName.Length - suffix.Length);
            }
            return spriteName;
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
