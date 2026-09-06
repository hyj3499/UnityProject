using System;

namespace FarmMVP
{
    /// <summary>A choppable tree (design doc §10).</summary>
    [Serializable]
    public class TreeFeature
    {
        public int x, y;
        public int hp;

        /// <summary>
        /// 나무 종류를 나타내는 LootTableDatabase 키. 이 값만 다르게 주면 사과나무/오렌지나무 등
        /// 새 종류를 추가할 수 있다 (예: "tree_apple") — 청소/파괴 로직은 그대로 재사용된다.
        /// </summary>
        public string dropTableId;

        public const int MaxHp = 3;
        public const string DefaultDropTableId = "tree_oak";

        public TreeFeature(int x, int y, string dropTableId = DefaultDropTableId)
        {
            this.x = x;
            this.y = y;
            hp = MaxHp;
            this.dropTableId = dropTableId;
        }

        public bool IsAlive => hp > 0;

        /// <summary>Chop once. Returns true if the tree was destroyed this hit.</summary>
        public bool Chop()
        {
            hp--;
            return hp <= 0;
        }
    }
}
