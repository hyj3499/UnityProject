using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// "무언가를 파괴/수확했을 때 무엇이 떨어지는가"를 한곳에서 관리하는 중앙 카탈로그
    /// (ItemDatabase/CropDatabase와 동일한 패턴). 드랍 원인이 되는 오브젝트(나무, 바위, ...)는
    /// 자신의 종류를 나타내는 문자열 id(예: "tree_oak")만 들고 있고, 실제로 무엇이 몇 개
    /// 나오는지는 전부 여기에서 정의한다. 그래서 새로운 나무/바위/작물 종류를 추가할 때
    /// 게임플레이 코드(GameLocation, MagicSystem, ItemDropSpawner 등)는 전혀 건드릴 필요가 없고,
    /// 아래에 Register 한 줄만 추가하면 된다.
    ///
    /// 사용 예 (추후 추가 시):
    ///   Register("tree_apple", new LootTable().Add("wood", 1, 2).Add("apple", 1, 3, 0.9f));
    ///   Register("tree_orange", new LootTable().Add("wood", 1, 2).Add("orange", 1, 3, 0.9f));
    ///   Register("rock_basic", new LootTable().Add("stone", 1, 3).Add("coal", 0, 1, 0.15f));
    /// (단, apple/orange/stone/coal 은 먼저 ItemDatabase에도 등록되어 있어야 한다.)
    /// </summary>
    public static class LootTableDatabase
    {
        private static readonly Dictionary<string, LootTable> _tables = new Dictionary<string, LootTable>();
        private static bool _init;

        public static void Init()
        {
            if (_init) return;
            _init = true;

            Register("tree_apricot", new LootTable().Add("wood", 3, 3).Add("apricot_seed", 1, 1, 0.3f));
            Register("rock_basic", new LootTable().Add("stone", 1, 3));
            Register("crop_strawberry", new LootTable().Add("strawberry", 1, 1));

            // 설치물은 철거하면 쓴 것을 그대로 돌려준다 — 종류를 추가해도 여기는 손대지 않는다.
            foreach (var p in PlaceableDatabase.All)
                Register(p.dropTableId, new LootTable().Add(p.id, 1, 1));
        }

        private static void Register(string dropTableId, LootTable table) => _tables[dropTableId] = table;

        public static LootTable Get(string dropTableId)
        {
            Init();
            if (string.IsNullOrEmpty(dropTableId)) return null;
            return _tables.TryGetValue(dropTableId, out var t) ? t : null;
        }
    }
}
