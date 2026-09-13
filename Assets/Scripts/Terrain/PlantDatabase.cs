using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 땅을 덮는 풀 한 종류의 정의 (잔디·잡초). 나무(TreeDef)와 달리 칸을 막지 않고,
    /// 밟고 지나갈 수 있으며, 한 칸 안에 여러 포기가 조금씩 어긋나게 심긴다.
    ///
    /// 새 풀 = 그림 {sheet}_{계절}.png (45x12) + 여기 Register 한 줄.
    /// </summary>
    public class PlantDef
    {
        public string plantId;
        public string name;

        /// <summary>Resources/Sprites/Plants/{sheet}_{계절}.png 의 앞부분 (PlantArt 참고).</summary>
        public string sheet;

        /// <summary>마지막 성장 단계. 0이면 자라지 않는다 (잡초) — 그때는 조각 3개가 생김새 3종이 된다.</summary>
        public int maxStage;
        public int daysPerStage;

        /// <summary>살 수 있는 계절. 여기 없는 계절이 되면 그 자리에서 사라진다.</summary>
        public SeasonFlags seasons = SeasonFlags.Spring | SeasonFlags.Summer | SeasonFlags.Fall;

        // ---- 번지기 (잔디) ----
        /// <summary>다 자란 포기가 하루에 옆 칸으로 번질 확률.</summary>
        public float spreadChance;

        // ---- 저절로 나기 (잡초) ----
        /// <summary>하루에 몇 칸이나 새로 나 볼지. 경작 가능 구역 중 빈 칸에만 난다.</summary>
        public int sowAttemptsPerDay;

        /// <summary>한 맵에 이 종류가 몇 포기까지 있을 수 있는지. 그림이 칸마다 여러 장이라 상한이 필요하다.</summary>
        public int maxPerLocation = 200;

        /// <summary>한 칸에 심기는 포기 수의 범위 (칸마다 이 사이에서 정해진다).</summary>
        public int tuftsMin = 5, tuftsMax = 7;

        /// <summary>
        /// 한 칸 안에서 포기가 흩어지는 폭 (칸 단위). 가로는 칸을 조금 넘어가도 좋다 —
        /// 옆 칸의 풀과 맞물려야 16px 격자가 보이지 않는다. 키가 큰 풀은 세로로 덜 흩어야 정돈돼 보인다.
        /// </summary>
        public float scatterX = 0.45f, scatterY = 0.5f;

        /// <summary>벨 때 무언가 나오려면 최소 이 단계는 되어야 한다.</summary>
        public int minDropStage;

        public string DropTableId => "plant_" + plantId;
    }

    /// <summary>
    /// 풀 카탈로그. 아이템·드랍 테이블·상점은 여기서 자동으로 따라 만들어진다
    /// (TreeDatabase / CropDatabase와 같은 방식).
    /// </summary>
    public static class PlantDatabase
    {
        public const string GrassId = "grass";
        public const string WeedId = "weed";

        private static readonly Dictionary<string, PlantDef> _defs = new Dictionary<string, PlantDef>();
        private static readonly List<PlantDef> _all = new List<PlantDef>();
        private static bool _init;

        public static IReadOnlyList<PlantDef> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;

            Register(new PlantDef
            {
                plantId = GrassId,
                name = "잔디",
                // 그림 파일이 glass_*.png 다. 이름을 grass_*로 바꾸면 이 줄만 고치면 된다.
                sheet = "glass",
                maxStage = 2,
                daysPerStage = 2,
                spreadChance = 0.22f,
                maxPerLocation = 220,
                minDropStage = 1,       // 갓 난 잔디는 벨 것이 없다
                // 잔디는 15x20으로 키가 커서 세로로 덜 흩는다 — 안 그러면 벽처럼 겹쳐 보인다.
                // 바닥을 덮는 쪽이라 포기 수는 넉넉하게.
                tuftsMin = 5, tuftsMax = 6,
                scatterX = 0.45f, scatterY = 0.34f,
            });

            Register(new PlantDef
            {
                plantId = WeedId,
                name = "잡초",
                sheet = "weed",
                maxStage = 0,           // 자라지 않는다 — 조각 3개는 생김새 3종으로 쓴다
                daysPerStage = 0,
                sowAttemptsPerDay = 6,
                maxPerLocation = 60,
                // 잡초는 15x12로 납작해서 세로로 넓게 흩어도 뭉쳐 보이지 않는다.
                tuftsMin = 4, tuftsMax = 5,
                scatterX = 0.4f, scatterY = 0.45f,
            });
        }

        private static void Register(PlantDef d)
        {
            _defs[d.plantId] = d;
            _all.Add(d);
        }

        public static PlantDef Get(string plantId)
        {
            Init();
            return plantId != null && _defs.TryGetValue(plantId, out var d) ? d : null;
        }
    }
}
