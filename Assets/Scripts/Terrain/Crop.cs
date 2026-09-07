using System;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>Static crop definition (design doc §17). New crops = new CropDef.</summary>
    public class CropDef
    {
        public string cropId;
        public string name;
        public int maxGrowthStage;   // index of harvestable(다 자란) stage
        public int daysPerStage;     // days needed to advance one stage

        /// <summary>수확 시 무엇이 드랍되는지 (LootTableDatabase 키). 실제 스폰은 ItemDropSpawner가 한다.</summary>
        public string dropTableId;

        /// <summary>
        /// -1이면 단일 수확 작물: 수확하면 완전히 사라진다.
        /// 0 이상이면 다작 작물: 수확 후 이 단계로 되돌아가 계속 자라며, 다시 maxGrowthStage에
        /// 도달하면 또 수확할 수 있다 (예: 딸기).
        /// </summary>
        public int regrowStage = -1;

        /// <summary>이 작물을 심을 수 있는 계절. 다른 계절이 되면 심어 둔 것이 시들어 사라진다.</summary>
        public SeasonFlags seasons = SeasonFlags.All;

        public Func<Sprite[]> stageSprites;
    }

    /// <summary>Runtime crop instance planted in a HoeDirt tile.</summary>
    [Serializable]
    public class Crop
    {
        public string cropId;
        public int growthStage;   // 0 = seed
        public int dayCounter;    // days accumulated toward next stage

        public Crop(string cropId)
        {
            this.cropId = cropId;
            growthStage = 0;
            dayCounter = 0;
        }

        public CropDef Def => CropDatabase.Get(cropId);
        public bool IsHarvestable => growthStage >= Def.maxGrowthStage;
        public bool IsRegrowable => Def.regrowStage >= 0;

        /// <summary>Advance growth by one day if watered.</summary>
        public void Grow(bool watered)
        {
            if (!watered) return;
            if (IsHarvestable) return;
            dayCounter++;
            if (dayCounter >= Def.daysPerStage)
            {
                dayCounter = 0;
                growthStage = Mathf.Min(growthStage + 1, Def.maxGrowthStage);
            }
        }

        public Sprite GetSprite()
        {
            var sprites = Def.stageSprites();
            int idx = Mathf.Clamp(growthStage, 0, sprites.Length - 1);
            return sprites[idx];
        }

        /// <summary>
        /// 수확 처리. 다작 작물이면 regrowStage로 되돌리고 계속 살아남아 true를 반환한다.
        /// 단일 수확 작물이면 아무것도 바꾸지 않고 false를 반환 — 호출자(HoeDirt)가 제거해야 한다.
        /// </summary>
        public bool HarvestAndRegrow()
        {
            if (!IsRegrowable) return false;
            growthStage = Def.regrowStage;
            dayCounter = 0;
            return true;
        }
    }
}
