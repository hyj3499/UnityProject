using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 나무 종류 하나의 정의. 새 나무 = 그림 네 장(계절별) + 여기 Register 한 줄.
    /// 씨앗 아이템·드랍 테이블·상점 판매·타일 팔레트 마커는 전부 이것에서 자동으로 만들어진다.
    /// </summary>
    public class TreeDef
    {
        public string treeId;
        public string name;

        /// <summary>Resources/Sprites/Trees/{sheet}_{계절}.png 의 앞부분 (TreeArt 참고).</summary>
        public string sheet;

        public int maxGrowthStage = TreeDatabase.FullGrowth;
        public int daysPerStage;     // 한 단계 자라는 데 걸리는 날
        public int maxHp;            // 다 자란 나무를 쓰러뜨리는 데 필요한 타격 수
        public int stumpHp;          // 남은 그루터기를 마저 치우는 데 필요한 타격 수

        public int woodMin, woodMax; // 쓰러뜨렸을 때 나오는 나무 개수
        public int stumpWood;        // 그루터기를 치웠을 때 나오는 나무 개수

        public int seedPrice;        // 상점 판매가
        public int seedSellPrice;    // 배송함에 넣었을 때
        public float seedDropChance; // 벴을 때 씨앗이 같이 나올 확률

        /// <summary>All Crops.png 안에서 쓸 씨앗 봉지 조각 이름.</summary>
        public string seedSpriteName;

        public string SeedItemId => treeId + "_seed";
        public string DropTableId => "tree_" + treeId;
        public string StumpDropTableId => "stump_" + treeId;
    }

    /// <summary>
    /// 나무 카탈로그. 종류를 추가하려면 여기에 Register 한 줄만 더하면 되고,
    /// 성장/베기/렌더링 로직은 그대로 재사용된다.
    /// </summary>
    public static class TreeDatabase
    {
        /// <summary>다 자란 단계. 시트가 0·1·2 그림과 "그루터기+윗부분"으로 되어 있어 4단계다.</summary>
        public const int FullGrowth = 3;

        public const string DefaultTreeId = "tree1";

        private static readonly Dictionary<string, TreeDef> _defs = new Dictionary<string, TreeDef>();
        private static readonly List<TreeDef> _all = new List<TreeDef>();
        private static bool _init;

        /// <summary>등록된 나무 전부. 나무 종류마다 배치 마커를 자동으로 만드는 데 쓰인다.</summary>
        public static IReadOnlyList<TreeDef> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;

            Register(new TreeDef
            {
                treeId = "tree1",
                name = "벚나무",
                sheet = "tree1",
                daysPerStage = 2,
                maxHp = 3,
                stumpHp = 2,
                woodMin = 3, woodMax = 5,
                stumpWood = 2,
                seedPrice = 120,
                seedSellPrice = 30,
                seedDropChance = 0.3f,
                seedSpriteName = "Tree1Seed",
            });

            Register(new TreeDef
            {
                treeId = "tree2",
                name = "자작나무",
                sheet = "tree2",
                daysPerStage = 3,
                maxHp = 4,
                stumpHp = 2,
                woodMin = 4, woodMax = 6,
                stumpWood = 2,
                seedPrice = 150,
                seedSellPrice = 40,
                seedDropChance = 0.3f,
                seedSpriteName = "Tree2Seed",
            });
        }

        private static void Register(TreeDef d)
        {
            _defs[d.treeId] = d;
            _all.Add(d);
        }

        public static TreeDef Get(string treeId)
        {
            Init();
            if (!string.IsNullOrEmpty(treeId) && _defs.TryGetValue(treeId, out var d)) return d;
            return _defs[DefaultTreeId];
        }

        /// <summary>
        /// 쓸 수 있는 나무 id로 바꿔 준다. 없어진 종류를 가리키는 예전 세이브("apricot")가
        /// 기본 나무로 옮겨 오고, 그 뒤로는 저장 데이터에도 새 id가 적힌다.
        /// </summary>
        public static string ResolveId(string treeId) => Get(treeId).treeId;
    }
}
