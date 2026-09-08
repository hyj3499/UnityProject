using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 작물 카탈로그 — 작물에 관한 모든 것의 <b>유일한 출처</b>다.
    /// 여기에 한 줄 추가하면 씨앗/수확물 아이템(ItemDatabase), 드랍 테이블(LootTableDatabase),
    /// 상점의 씨앗 판매(ShopDatabase)가 전부 따라 만들어진다 — FishDatabase와 같은 방식.
    ///
    /// 그림은 이름 규칙으로만 찾는다 (CropDef 주석 참고):
    ///   Resources/Sprites/Crops/{sheet}.png 를 잘라 "{sheet}_0" ... "{sheet}_(마지막 번호)"
    ///   씨앗 봉지는 All Crops.png 안의 "{공백없는sheet}Seed"
    ///   바닥 드랍 그림은 Sprites/Crops/Drops/{공백없는sheet} (없으면 수확물 아이콘을 그대로 쓴다)
    /// </summary>
    public static class CropDatabase
    {
        private static readonly Dictionary<string, CropDef> _defs = new Dictionary<string, CropDef>();
        private static readonly List<CropDef> _order = new List<CropDef>();
        private static bool _init;

        /// <summary>등록된 순서대로의 모든 작물 (상점 목록 등이 쓴다).</summary>
        public static IReadOnlyList<CropDef> All { get { Init(); return _order; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;

            // ---------------- 봄 ----------------
            // Asparagus_5 처럼 시트마다 단계 개수가 다르므로 단계 번호를 그대로 적는다.
            Add("asparagus", "아스파라거스", "Asparagus", Stages(0, 4), 5, 2, SeasonFlags.Spring, 120, 90, regrow: true);
            Add("blueberry", "블루베리", "Blueberry", Stages(0, 5), 6, 2, SeasonFlags.Spring, 160, 80, regrow: true);
            Add("broccoli", "브로콜리", "Broccoli", Stages(0, 4), 5, 2, SeasonFlags.Spring, 70, 110);
            // Cabbage_1 / Carrot_1 은 쓰지 않는 칸이라 "_nouse"로 빠져 있다.
            Add("cabbage", "양배추", "Cabbage", new[] { 0, 2, 3, 4, 5, 6 }, 7, 2, SeasonFlags.Spring, 80, 130);
            Add("carrot", "당근", "Carrot", new[] { 0, 2, 3, 4, 5 }, 6, 1, SeasonFlags.Spring, 40, 70);
            Add("cauliflower", "콜리플라워", "Cauliflower", Stages(0, 5), 6, 2, SeasonFlags.Spring, 100, 160);
            Add("onion", "양파", "Onion", Stages(0, 5), 6, 1, SeasonFlags.Spring, 50, 85);
            Add("parsnip", "파스닙", "Parsnip", Stages(0, 4), 5, 1, SeasonFlags.Spring, 30, 55);
            Add("potato", "감자", "Potato", Stages(0, 5), 6, 1, SeasonFlags.Spring, 60, 95);
            Add("rice", "쌀", "Rice", Stages(0, 5), 6, 1, SeasonFlags.Spring, 45, 75);
            Add("spring_onion", "봄양파", "Spring Onion", Stages(0, 5), 6, 1, SeasonFlags.Spring, 35, 60);
            Add("strawberry", "딸기", "Strawberry", Stages(0, 5), 6, 1, SeasonFlags.Spring, 100, 80, regrow: true);

            // ---------------- 여름 ----------------
            Add("adzuki_bean", "팥", "Adzuki Bean", Stages(0, 6), 7, 1, SeasonFlags.Summer, 130, 90, regrow: true);
            Add("blackberry", "블랙베리", "Blackberry", Stages(0, 6), 7, 1, SeasonFlags.Summer, 140, 85, regrow: true);
            Add("cucumber", "오이", "Cucumber", Stages(0, 5), 6, 1, SeasonFlags.Summer, 110, 70, regrow: true);
            Add("green_beans", "완두콩", "Green Beans", Stages(0, 6), 7, 1, SeasonFlags.Summer, 100, 65, regrow: true);
            Add("hot_pepper", "고추", "Hot Pepper", Stages(0, 6), 7, 1, SeasonFlags.Summer, 90, 60, regrow: true);
            Add("melon", "멜론", "Melon", Stages(0, 5), 6, 2, SeasonFlags.Summer, 150, 250);
            Add("pineapple", "파인애플", "Pineapple", Stages(0, 5), 6, 3, SeasonFlags.Summer, 200, 340);
            Add("sunflower", "해바라기", "Sunflower", Stages(0, 5), 6, 2, SeasonFlags.Summer, 90, 120);
            Add("tomato", "토마토", "Tomato", Stages(0, 6), 7, 1, SeasonFlags.Summer, 120, 75, regrow: true);
            Add("watermelon", "수박", "Watermelon", Stages(0, 7), 8, 2, SeasonFlags.Summer, 180, 300);

            // 파프리카는 세 가지 색 중 하나로 랜덤하게 자란다 — 0~4단계는 같고, 다 자란 모습과
            // 수확물만 색마다 다르다 (그래서 마지막 단계는 stageIndices에 넣지 않는다).
            Add("bell_pepper", "파프리카", "Bell Pepper", Stages(0, 4), 8, 1, SeasonFlags.Summer, 110, 95,
                regrow: true,
                variants: new[]
                {
                    Variant("bell_pepper_orange", "주황 파프리카", 5, 8, "BellPepperOrange"),
                    Variant("bell_pepper_red", "빨강 파프리카", 6, 10, "BellPepperRed"),
                    Variant("bell_pepper_green", "초록 파프리카", 7, 9, "BellPepperGreen"),
                });

            // ---------------- 가을 ----------------
            Add("aloe", "알로에", "Aloe", Stages(0, 5), 6, 2, SeasonFlags.Fall, 100, 150);
            Add("beetroot", "비트", "Beetroot", Stages(0, 5), 6, 1, SeasonFlags.Fall, 70, 110);
            Add("corn", "옥수수", "Corn", Stages(0, 7), 8, 1, SeasonFlags.Fall, 160, 100, regrow: true);
            Add("eggplant", "가지", "Eggplant", Stages(0, 5), 6, 1, SeasonFlags.Fall, 90, 70, regrow: true);
            // 포도는 빈 칸을 건너뛰고 잘라 두었으므로 번호가 0~4로 이어진다.
            Add("grapes", "포도", "Grapes", Stages(0, 4), 5, 2, SeasonFlags.Fall, 170, 120, regrow: true);
            Add("pumpkin", "호박", "Pumpkin", Stages(0, 4), 5, 3, SeasonFlags.Fall, 150, 380);
        }

        /// <summary>from..to 를 하나씩 늘린 번호 목록 — 시트가 연속으로 잘려 있을 때 쓴다.</summary>
        private static int[] Stages(int from, int to)
        {
            var a = new int[to - from + 1];
            for (int i = 0; i < a.Length; i++) a[i] = from + i;
            return a;
        }

        private static CropVariant Variant(string id, string name, int ripeStageIndex, int fruitIndex, string dropSpriteName)
            => new CropVariant
            {
                id = id,
                name = name,
                ripeStageIndex = ripeStageIndex,
                fruitIndex = fruitIndex,
                dropSpriteName = dropSpriteName
            };

        private static void Add(string cropId, string name, string sheet, int[] stageIndices, int fruitIndex,
                                int daysPerStage, SeasonFlags seasons, int seedPrice, int sellPrice,
                                bool regrow = false, CropVariant[] variants = null)
        {
            var def = new CropDef
            {
                cropId = cropId,
                name = name,
                sheet = sheet,
                stageIndices = stageIndices,
                fruitIndex = fruitIndex,
                daysPerStage = daysPerStage,
                seasons = seasons,
                seedPrice = seedPrice,
                sellPrice = sellPrice,
                regrow = regrow,
                variants = variants
            };
            _defs[cropId] = def;
            _order.Add(def);
        }

        public static CropDef Get(string id)
        {
            Init();
            return id != null && _defs.TryGetValue(id, out var d) ? d : null;
        }
    }
}
