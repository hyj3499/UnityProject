using System;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>Static crop definition (design doc §17). New crops = new CropDef.</summary>
    public class CropDef
    {
        public string cropId;
        public string name;
        public int maxGrowthStage;   // index of harvestable stage
        public int daysPerStage;     // days needed to advance one stage
        public string harvestItemId; // item produced on harvest
        public int harvestAmount;
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
    }
}
