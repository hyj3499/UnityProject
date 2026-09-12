using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>잡화 수레에 걸리는 품목 하나.</summary>
    public class ShopEntry
    {
        public string itemId;
        public int price;

        /// <summary>목록에서 어느 묶음에 들어가는지 (계절 이름 등). 같은 값끼리 붙어서 머리글 하나로 묶인다.</summary>
        public string group;

        /// <summary>가격 아래 한 줄 설명 (자라는 데 걸리는 날짜 등).</summary>
        public string note;

        public ItemDef Def => ItemDatabase.Get(itemId);
    }

    /// <summary>
    /// 상점에 무엇이 걸리는지를 정하는 곳. 씨앗은 CropDatabase에서 그대로 끌어오므로,
    /// 작물을 추가할 때 이 파일은 손대지 않아도 저절로 상점에 올라온다.
    /// </summary>
    public static class ShopDatabase
    {
        private static List<ShopEntry> _entries;

        /// <summary>지금 계절 것이 맨 위에 오도록 정렬된 판매 목록.</summary>
        public static IReadOnlyList<ShopEntry> Entries
        {
            get
            {
                if (_entries == null) Build();
                Sort();
                return _entries;
            }
        }

        private static void Build()
        {
            _entries = new List<ShopEntry>();
            foreach (var crop in CropDatabase.All)
            {
                _entries.Add(new ShopEntry
                {
                    itemId = crop.SeedItemId,
                    price = crop.seedPrice,
                    group = Seasons.Label(crop.seasons),
                    note = $"다 자라기까지 {crop.MaxGrowthStage * crop.daysPerStage}일"
                           + (crop.regrow ? " · 다작" : "")
                });
            }

            // 나무 씨앗도 TreeDatabase에서 그대로 따라온다 — 나무를 추가하면 상점에도 따라 올라온다.
            foreach (var tree in TreeDatabase.All)
            {
                _entries.Add(new ShopEntry
                {
                    itemId = tree.SeedItemId,
                    price = tree.seedPrice,
                    group = "나무",
                    note = $"밭이 아닌 빈 땅에 심는다 · 다 자라기까지 {tree.maxGrowthStage * tree.daysPerStage}일"
                });
            }
        }

        /// <summary>
        /// 지금 계절에 심을 수 있는 씨앗을 맨 위로 올린다 — 목록이 길어서, 오늘 쓸 수 있는 것이
        /// 스크롤 없이 바로 보이는 편이 낫다. 묶음 안의 순서(CropDatabase 등록 순서)는 그대로 둔다.
        /// </summary>
        private static void Sort()
        {
            int Rank(ShopEntry e)
            {
                var crop = CropDatabase.Get(e.Def?.cropId);
                if (crop == null) return 2;                              // 나무 씨앗 등은 맨 아래
                return Seasons.AllowsNow(crop.seasons) ? 0 : 1;
            }

            // List.Sort는 안정 정렬이 아니라서, 같은 순위면 원래 자리를 보고 순서를 지킨다.
            var order = new Dictionary<ShopEntry, int>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++) order[_entries[i]] = i;
            _entries.Sort((a, b) =>
            {
                int r = Rank(a).CompareTo(Rank(b));
                return r != 0 ? r : order[a].CompareTo(order[b]);
            });
        }
    }
}
