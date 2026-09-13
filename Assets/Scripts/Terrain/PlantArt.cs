using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 풀 그림을 이름 규칙 하나로 찾는다 (TreeArt와 같은 방식).
    ///
    ///   Resources/Sprites/Plants/{시트}_{계절}.png   (45x12 · 15x12 조각 세 개)
    ///     {시트}_{계절}_0 · _1 · _2
    ///
    /// 조각의 기준점은 <b>아래 가운데</b>다. 한 칸(16x16)에 15x12짜리를 여러 포기 어긋나게
    /// 심기 때문에, 기준점이 밑동이어야 어디에 놓든 땅에 붙어 보이고 흔들리는 축도 밑동이 된다.
    ///
    /// <b>겨울판은 일부러 없다</b> — 겨울이 되면 잔디도 잡초도 사라지는 것이 규칙이라
    /// (PlantDef.seasons) 그릴 일이 없다. 그래서 다른 계절 그림으로 대신하지도 않는다.
    /// 그 계절 그림이 없으면 그냥 아무것도 그리지 않는다.
    /// </summary>
    public static class PlantArt
    {
        /// <summary>시트 한 장에 든 조각 수 (잔디는 성장 단계, 잡초는 생김새 종류).</summary>
        public const int Variants = 3;

        private const string Root = "Sprites/Plants/";

        public static Sprite Stage(string sheet, int index, Season season)
        {
            if (string.IsNullOrEmpty(sheet)) return null;
            return AssetLibrary.GetSpriteOptional(
                Root + sheet + "_" + Seasons.Key(season).ToLowerInvariant()
                     + "_" + Mathf.Clamp(index, 0, Variants - 1));
        }
    }
}
