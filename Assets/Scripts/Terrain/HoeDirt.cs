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

        /// <summary>
        /// 수확 가능하면 그 작물의 dropTableId를 반환한다 (없으면 null). 다작 작물은 자동으로
        /// 재성장 단계로 되돌아가고, 단일 수확 작물은 이 타일에서 완전히 제거된다.
        /// 실제 아이템 스폰은 호출자가 ItemDropSpawner로 한다.
        /// </summary>
        public string Harvest()
        {
            if (crop == null || !crop.IsHarvestable) return null;
            string dropTableId = crop.Def.dropTableId;
            if (!crop.HarvestAndRegrow())
                crop = null; // 단일 수확 작물
            return dropTableId;
        }
    }
}
