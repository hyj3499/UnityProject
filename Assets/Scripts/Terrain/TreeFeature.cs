using System;

namespace FarmMVP
{
    /// <summary>A choppable tree (design doc §10).</summary>
    [Serializable]
    public class TreeFeature
    {
        public int x, y;
        public int hp;
        public const int MaxHp = 3;

        public TreeFeature(int x, int y)
        {
            this.x = x;
            this.y = y;
            hp = MaxHp;
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
