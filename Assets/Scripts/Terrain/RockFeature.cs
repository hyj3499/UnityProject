using System;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>바위마법으로 부술 수 있는 바위 하나. 부수면 돌이 나온다.</summary>
    [Serializable]
    public class RockFeature
    {
        public const int MaxHp = 2;
        public const string DropTableId = "rock_basic";

        public int x, y;
        public int hp;
        public int variant; // 스프라이트 종류 (AssetLibrary.RockVariants 인덱스)

        public RockFeature(int x, int y, int variant)
        {
            this.x = x;
            this.y = y;
            this.variant = variant;
            hp = MaxHp;
        }

        public bool IsAlive => hp > 0;

        public Sprite GetSprite()
        {
            var sprites = AssetLibrary.RockVariants;
            if (sprites == null || sprites.Length == 0) return null;
            return sprites[Mathf.Clamp(variant, 0, sprites.Length - 1)];
        }

        /// <summary>한 번 친다. 이번 타격으로 부서졌으면 true.</summary>
        public bool Hit()
        {
            hp--;
            return hp <= 0;
        }
    }
}
