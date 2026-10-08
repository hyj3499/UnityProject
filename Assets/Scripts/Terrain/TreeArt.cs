using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 나무 그림을 <b>조각 이름만으로</b> 찾는다. 나무를 한 종류 더 넣을 때 손댈 곳이
    /// TreeDatabase 한 줄뿐이도록, 어떤 그림이 어디 있는지는 전부 여기서 계산한다.
    /// 시트(캔버스) 크기도 조각 크기도 보지 않는다 — 스프라이트 에디터에서 자른 그대로 쓴다.
    ///
    ///   Resources/Sprites/Trees/{시트}_{계절}.png
    ///
    /// 조각을 찾는 방법은 둘이고, 시트마다 편한 쪽을 쓰면 된다.
    ///
    ///  1) <b>순서</b> — 스프라이트 에디터가 붙여 주는 이름({시트}_{계절}_0, _1, ...)을 그대로 둔다.
    ///     번호가 작은 것부터 아래 순서로 읽는다 (번호가 중간에 비어도 순서만 본다):
    ///       레벨0 · 레벨1 · 레벨2 · 나뭇잎들 · 그루터기 · 레벨3 윗부분 · 다 자란 나무 한 장
    ///     앞의 셋과 뒤의 셋이 고정이고 그 사이가 전부 나뭇잎이라, 잎은 몇 장이어도(0장이어도) 된다.
    ///
    ///  2) <b>이름</b> — 조각에 직접 이름을 붙인다. 있으면 이쪽이 먼저다.
    ///       {시트}_{계절}_stage0 · _stage1 · _stage2   레벨 0(묘목) ~ 2
    ///       {시트}_{계절}_stump                        그루터기
    ///       {시트}_{계절}_top                          레벨3 윗부분 — 그루터기 위에 겹쳐 그린다
    ///       {시트}_{계절}_leaf0 .. _leaf3              벨 때 흩날리는 나뭇잎
    ///       {시트}_{계절}_full                         다 자란 나무 한 장 (그루터기+윗부분을 미리 겹친 것)
    ///
    /// 크기는 자유지만 <b>기준점(pivot)</b>은 맞춰야 한다 — 나뭇잎을 뺀 조각은 기준점이 곧 밑동이
    /// 땅에 닿는 자리다. 그 점이 칸의 아래 가운데에 놓이므로 자라면서 그림만 갈아 끼우면 되고,
    /// 윗부분도 그루터기와 같은 자리에 놓이며 쓰러질 때는 그 점을 축으로 돈다.
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

        /// <summary>그루터기를 칠 때 흩날리는 전용 잎(leaf0) 장수.</summary>
        private const int StumpLeafCount = 2;

        /// <summary>순서로 읽을 때 앞에 오는 조각 수(레벨 0~2)와 뒤에 오는 조각 수(그루터기·윗부분·한 장).</summary>
        private const int HeadCuts = LastStageSprite + 1, TailCuts = 3;

        /// <summary>순서로 읽을 때 훑어보는 번호의 끝 — 조각을 지웠다 다시 잘라 번호가 건너뛰어도 찾도록 넉넉히 둔다.</summary>
        private const int MaxCutNumber = 64;

        /// <summary>"tree1_spring" -> 번호 순으로 늘어놓은 조각들. 시트마다 한 번만 훑는다.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, Sprite[]> _ordered =
            new System.Collections.Generic.Dictionary<string, Sprite[]>();

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
        /// 벨 때 흩날릴 나뭇잎. 잎 그림 네 장(leaf0~3) 중
        ///   · leaf0 은 <b>그루터기를 칠 때만</b> 쓴다 (StumpLeafCount장).
        ///   · 그 밖에는 leaf1~3 을 돌려 쓰고, 단계가 오를수록 장수가 는다:
        ///     레벨0 → 1장 · 레벨1 → 2장 · 레벨2 → 3장 · 레벨3(다 자란 나무) → LeafCount장.
        /// 그 계절 시트에 비어 있는 칸(잎이 다 진 겨울나무)은 <b>만들어 두지 않았으므로</b> 있는 것만 쓰고,
        /// 눈 덮인 나무에서 초록 잎이 나오지도 않는다. 그 계절 잎이 하나도 없으면 봄 잎으로 대신한다.
        /// </summary>
        /// <param name="level">치기 <b>전</b>의 성장 단계 (쓰러뜨리는 순간에도 레벨3으로 센다).</param>
        /// <param name="stump">치기 전에 이미 그루터기였는지.</param>
        public static Sprite[] Leaves(string sheet, Season season, int level, bool stump)
        {
            var slots = Collect(sheet, season);
            if (!AnyLeaf(slots) && season != Season.Spring) slots = Collect(sheet, Season.Spring);
            if (!AnyLeaf(slots)) return System.Array.Empty<Sprite>();

            var pool = new System.Collections.Generic.List<Sprite>(LeafCount);
            if (stump)
            {
                var own = slots[0] ?? FirstLeaf(slots);
                var two = new Sprite[StumpLeafCount];
                for (int i = 0; i < StumpLeafCount; i++) two[i] = own;
                return two;
            }

            for (int i = 1; i < LeafCount; i++) if (slots[i] != null) pool.Add(slots[i]);
            if (pool.Count == 0) pool.Add(FirstLeaf(slots));   // leaf1~3 이 없는 시트는 leaf0 으로라도

            int count = Mathf.Clamp(level + 1, 1, LeafCount);
            var leaves = new Sprite[count];
            for (int i = 0; i < count; i++) leaves[i] = pool[i % pool.Count];
            return leaves;
        }

        /// <summary>leaf0 ~ leaf(LeafCount-1) 칸. 비어 있는 칸은 null.</summary>
        private static Sprite[] Collect(string sheet, Season season)
        {
            var slots = new Sprite[LeafCount];
            for (int i = 0; i < LeafCount; i++)
            {
                // 잎은 계절 대신이 위험하다 (겨울 눈송이 자리에 봄 잎을 넣게 된다) — 그 계절 것만 본다.
                slots[i] = Find(sheet, "leaf" + i, season);
            }
            return slots;
        }

        private static bool AnyLeaf(Sprite[] slots) => FirstLeaf(slots) != null;

        private static Sprite FirstLeaf(Sprite[] slots)
        {
            foreach (var s in slots) if (s != null) return s;
            return null;
        }

        private static Sprite Part(string sheet, string part, Season season)
        {
            if (string.IsNullOrEmpty(sheet)) return null;
            var s = Find(sheet, part, season);
            if (s != null) return s;
            return season == Season.Spring ? null : Find(sheet, part, Season.Spring);
        }

        /// <summary>그 계절 시트에서 조각 하나. 이름이 붙어 있으면 그것, 없으면 순서로 찾는다.</summary>
        private static Sprite Find(string sheet, string part, Season season)
        {
            var named = AssetLibrary.GetSpriteOptional(Path(sheet, part, season));
            if (named != null) return named;

            var cuts = Ordered(SheetName(sheet, season));
            int i = OrderIndex(part, cuts.Length);
            return i >= 0 ? cuts[i] : null;
        }

        /// <summary>조각이 n개인 시트에서 그 부위가 몇 번째인지. 없으면 -1.</summary>
        private static int OrderIndex(string part, int n)
        {
            if (n < HeadCuts + TailCuts) return -1;
            switch (part)
            {
                case "stump": return n - 3;
                case "top": return n - 2;
                case "full": return n - 1;
            }
            if (part.StartsWith("stage")) return int.Parse(part.Substring(5));

            // 나뭇잎은 앞의 셋과 뒤의 셋 사이에 있는 만큼만 있다.
            int leaf = HeadCuts + int.Parse(part.Substring(4));
            return leaf < n - TailCuts ? leaf : -1;
        }

        private static Sprite[] Ordered(string sheetName)
        {
            if (_ordered.TryGetValue(sheetName, out var cached)) return cached;

            var found = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < MaxCutNumber; i++)
            {
                var s = AssetLibrary.GetSpriteOptional(Root + sheetName + "_" + i);
                if (s != null) found.Add(s);
            }
            if (found.Count > 0 && found.Count < HeadCuts + TailCuts)
                Debug.LogWarning($"[TreeArt] {sheetName}: 조각이 {found.Count}개뿐이다 — 레벨0·1·2, (나뭇잎), 그루터기, 윗부분, 한 장 순으로 최소 {HeadCuts + TailCuts}개가 있어야 한다.");

            var cuts = found.ToArray();
            _ordered[sheetName] = cuts;
            return cuts;
        }

        /// <summary>"tree1_winter" — 파일 이름은 소문자 계절이다.</summary>
        private static string SheetName(string sheet, Season season)
            => sheet + "_" + Seasons.Key(season).ToLowerInvariant();

        /// <summary>"Sprites/Trees/tree1_winter_top"</summary>
        private static string Path(string sheet, string part, Season season)
            => Root + SheetName(sheet, season) + "_" + part;
    }
}
