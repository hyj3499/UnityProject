using System;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>심고 자라고 벨 수 있는 나무 한 그루 (design doc §10).</summary>
    [Serializable]
    public class TreeFeature
    {
        public int x, y;
        public string treeId;
        public int growthStage;
        public int dayCounter;
        public int hp;

        public TreeFeature(int x, int y, string treeId, int growthStage)
        {
            this.x = x;
            this.y = y;
            this.treeId = string.IsNullOrEmpty(treeId) ? TreeDatabase.DefaultTreeId : treeId;
            this.growthStage = growthStage;
            hp = Def.maxHp;
        }

        public TreeDef Def => TreeDatabase.Get(treeId);
        public bool IsAlive => hp > 0;
        public bool IsMature => growthStage >= Def.maxGrowthStage;

        public Sprite GetSprite()
        {
            var sprites = Def.stageSprites();
            if (sprites == null || sprites.Length == 0) return null;
            return sprites[Mathf.Clamp(growthStage, 0, sprites.Length - 1)];
        }

        /// <summary>하루가 지나 한 단계씩 자란다.</summary>
        public void Grow()
        {
            if (IsMature) return;

            dayCounter++;
            if (dayCounter < Def.daysPerStage) return;

            dayCounter = 0;
            growthStage = Mathf.Min(growthStage + 1, Def.maxGrowthStage);
        }

        /// <summary>
        /// 한 번 벤다. 이번 타격으로 쓰러졌으면 true.
        /// 아직 다 자라지 않은 나무는 한 번에 뽑히고 아무것도 남기지 않는다.
        /// </summary>
        public bool Chop()
        {
            if (!IsMature)
            {
                hp = 0;
                return true;
            }
            hp--;
            return hp <= 0;
        }
    }
}
