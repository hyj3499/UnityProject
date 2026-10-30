using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 풀 그림을 이름 규칙 하나로 찾는다 (TreeArt와 같은 방식).
    ///
    ///   Resources/Sprites/Plants/{시트}_{계절}.png
    ///     {시트}_{계절}_0 · _1 · ... (봄 잡초는 14x10 조각 네 개)
    ///
    /// 조각의 기준점은 <b>아래 가운데</b>다. 한 칸(16x16)에 여러 포기를 어긋나게
    /// 심기 때문에, 기준점이 밑동이어야 어디에 놓든 땅에 붙어 보이고 흔들리는 축도 밑동이 된다.
    ///
    /// <b>겨울판은 일부러 없다</b> — 겨울이 되면 잔디도 잡초도 사라지는 것이 규칙이라
    /// (PlantDef.seasons) 그릴 일이 없다. 그래서 다른 계절 그림으로 대신하지도 않는다.
    /// 그 계절 그림이 없으면 그냥 아무것도 그리지 않는다.
    /// </summary>
    public static class PlantArt
    {
        private const string Root = "Sprites/Plants/";
        private static readonly Dictionary<string, Sprite[]> Sheets = new Dictionary<string, Sprite[]>();

        /// <summary>계절마다 실제로 잘라 둔 조각 수를 쓴다. 기존 여름·가을 3종도 지원한다.</summary>
        public static int VariantCount(string sheet, Season season) => LoadSheet(sheet, season).Length;

        public static Sprite Stage(string sheet, int index, Season season)
        {
            var sprites = LoadSheet(sheet, season);
            return sprites.Length == 0 ? null : sprites[Mathf.Clamp(index, 0, sprites.Length - 1)];
        }

        private static Sprite[] LoadSheet(string sheet, Season season)
        {
            if (string.IsNullOrEmpty(sheet)) return Array.Empty<Sprite>();
            string name = sheet + "_" + Seasons.Key(season).ToLowerInvariant();
            if (Sheets.TryGetValue(name, out var cached)) return cached;

            var all = Resources.LoadAll<Sprite>(Root + name);
            var ordered = new List<Sprite>();
            for (int i = 0; i < all.Length; i++)
            {
                var sprite = Array.Find(all, s => s.name == name + "_" + i);
                if (sprite != null) ordered.Add(sprite);
            }
            return Sheets[name] = ordered.ToArray();
        }
    }
}
