using System;

namespace FarmMVP
{
    /// <summary>
    /// A tilled soil tile (design doc §7.1). Holds watered state and an optional crop.
    /// </summary>
    [Serializable]
    public class HoeDirt
    {
        public int x, y;      // tile coordinates
        public bool watered;
        public Crop crop;     // null if nothing planted

        public HoeDirt(int x, int y)
        {
            this.x = x;
            this.y = y;
            watered = false;
            crop = null;
        }

        public bool HasCrop => crop != null;

        public bool Plant(string cropId)
        {
            if (HasCrop) return false;
            crop = new Crop(cropId);
            return true;
        }

        public void Water() => watered = true;

        /// <summary>Called at day end: grow crop, then reset water (design doc §9).</summary>
        public void OnNewDay()
        {
            if (crop != null) crop.Grow(watered);
            watered = false;
        }

        /// <summary>Harvest crop if ready. Returns produced item id (or null).</summary>
        public string Harvest(out int amount)
        {
            amount = 0;
            if (crop == null || !crop.IsHarvestable) return null;
            var def = crop.Def;
            amount = def.harvestAmount;
            string item = def.harvestItemId;
            crop = null; // strawberries removed after harvest in MVP
            return item;
        }
    }
}
