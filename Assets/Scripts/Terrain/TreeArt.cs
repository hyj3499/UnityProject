using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 나무 그림을 <b>이름 규칙 하나로만</b> 찾는다. 나무를 한 종류 더 넣을 때 손댈 곳이
    /// TreeDatabase 한 줄뿐이도록, 어떤 그림이 어디 있는지는 전부 여기서 계산한다.
    ///
    ///   Resources/Sprites/Trees/{시트}_{계절}.png   (336x96 · 48x96 칸 일곱 개)
    ///     {시트}_{계절}_stage0 · _stage1 · _stage2   레벨 0(묘목) ~ 2
    ///     {시트}_{계절}_stump                        그루터기
    ///     {시트}_{계절}_top                          레벨3 윗부분 — 그루터기 위에 겹쳐 그린다
    ///     {시트}_{계절}_leaf0 .. _leaf3              벨 때 흩날리는 나뭇잎
    ///     {시트}_{계절}_full                         다 자란 나무 한 장 (그루터기+윗부분을 미리 겹친 것)
    ///
    /// 나뭇잎을 뺀 조각들은 기준점이 <b>아래 가운데</b>라 어느 것을 같은 자리에 놓아도 밑동이 맞는다.
    /// 그래서 자라면서 그림만 갈아 끼우면 되고, 다 자란 나무는 그루터기 위에 윗부분을 얹기만 하면 된다.
    /// (시트를 만드는 것은 tools/build_tree_sheets.py + tools/build_tree_metas.py)
    ///
    /// 그 계절 그림이 없으면 봄 그림으로 대신하므로, 계절판을 하나씩 채워 넣어도 아무것도 깨지지 않는다.
    /// </summary>
    public static class TreeArt
    {
        /// <summary>벨 때 흩날리는 나뭇잎 개수.</summary>
        public const int LeafCount = 4;

        /// <summary>레벨 0~2 그림이 있는 마지막 단계 (레벨3은 그루터기+윗부분으로 그린다).</summary>
        public const int LastStageSprite = 2;

        private const string Root = "Sprites/Trees/";

        public static Sprite Stage(string sheet, int level, Season season)
            => Part(sheet, "stage" + Mathf.Clamp(level, 0, LastStageSprite), season);

        public static Sprite Stump(string sheet, Season season) => Part(sheet, "stump", season);

        public static Sprite Top(string sheet, Season season) => Part(sheet, "top", season);

        /// <summary>
        /// 다 자란 나무를 <b>한 장으로</b> 그린 그림. 게임 안의 나무는 이것을 쓰지 않는다 —
        /// 벨 때 윗부분만 넘어뜨려야 해서 그루터기와 윗부분을 따로 그린다.
        /// 이건 겹쳐 그릴 줄 모르는 곳에서 쓴다: "Fixed_{맵}"에 칠하는 장식 나무, 타일 팔레트 미리보기.
        /// </summary>
        public static Sprite Full(string sheet, Season season) => Part(sheet, "full", season);

        /// <summary>
        /// 흩날릴 나뭇잎 LeafCount장. 그 계절 시트에 비어 있는 칸(잎이 다 진 겨울나무)은
        /// <b>만들어 두지 않았으므로</b>, 남아 있는 것을 돌려 가며 채운다 — 어느 계절에 베어도
        /// 흩날리는 잎의 수는 같고, 눈 덮인 나무에서 초록 잎이 나오지도 않는다.
        /// 그 계절 잎이 하나도 없으면 봄 잎으로 대신한다.
        /// </summary>
        public static Sprite[] Leaves(string sheet, Season season)
        {
            var found = Collect(sheet, season);
            if (found.Count == 0 && season != Season.Spring) found = Collect(sheet, Season.Spring);
            if (found.Count == 0) return System.Array.Empty<Sprite>();

            var leaves = new Sprite[LeafCount];
            for (int i = 0; i < LeafCount; i++) leaves[i] = found[i % found.Count];
            return leaves;
        }

        private static System.Collections.Generic.List<Sprite> Collect(string sheet, Season season)
        {
            var found = new System.Collections.Generic.List<Sprite>(LeafCount);
            for (int i = 0; i < LeafCount; i++)
            {
                // 잎은 계절 대신이 위험하다 (겨울 눈송이 자리에 봄 잎을 넣게 된다) — 그 계절 것만 본다.
                var s = AssetLibrary.GetSpriteOptional(Path(sheet, "leaf" + i, season));
                if (s != null) found.Add(s);
            }
            return found;
        }

        private static Sprite Part(string sheet, string part, Season season)
        {
            if (string.IsNullOrEmpty(sheet)) return null;
            var s = AssetLibrary.GetSpriteOptional(Path(sheet, part, season));
            if (s != null) return s;
            return season == Season.Spring ? null : AssetLibrary.GetSpriteOptional(Path(sheet, part, Season.Spring));
        }

        /// <summary>"Sprites/Trees/tree1_winter_top" — 파일 이름은 소문자 계절이다.</summary>
        private static string Path(string sheet, string part, Season season)
            => Root + sheet + "_" + Seasons.Key(season).ToLowerInvariant() + "_" + part;
    }
}
