using System;
using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// Whole-game serializable state (design doc §17). Everything needed to save/load
    /// is reachable from here so we can JSON-serialize the entire game.
    /// </summary>
    [Serializable]
    public class GameData
    {
        public int currentDay = 1;
        public int currentMinutes = 6 * 60; // 06:00
        public LocationId currentLocation = LocationId.Farm1;

        public FarmerData farmer = new FarmerData();

        /// <summary>배송함에 넣어 둔, 다음 날 아침에 팔릴 아이템들.</summary>
        public List<SlotData> shippingBox = new List<SlotData>();

        public LocationData farm1 = new LocationData();
        public LocationData farm2 = new LocationData();
        public LocationData farmHouse = new LocationData();

        public LocationData GetLocation(LocationId id)
        {
            switch (id)
            {
                case LocationId.Farm1: return farm1;
                case LocationId.Farm2: return farm2;
                case LocationId.FarmHouse: return farmHouse;
                default: return farm1;
            }
        }
    }

    [Serializable]
    public class FarmerData
    {
        public float posX, posY;
        public int direction = (int)Direction.Down;
        public int hp = 100, maxHp = 100;
        public int mp = 50, maxMp = 50;
        public int money = 500;
        public int equippedHotbarIndex = 0;
        public int currentMagic = 0; // 0=Earth, 1=Water, 2=Blade

        // Inventory stored as parallel arrays for robust JSON serialization
        public List<SlotData> slots = new List<SlotData>();
    }

    [Serializable]
    public class SlotData
    {
        public int index;
        public string itemId;
        public int count;
    }

    [Serializable]
    public class LocationData
    {
        public List<HoeDirtData> hoeDirts = new List<HoeDirtData>();
        public List<TreeData> trees = new List<TreeData>();
        public List<WorldItemData> droppedItems = new List<WorldItemData>();
        public bool initialized = false;
    }

    [Serializable]
    public class HoeDirtData
    {
        public int x, y;
        public bool watered;
        public bool hasCrop;
        public string cropId;
        public int growthStage;
        public int dayCounter;
    }

    [Serializable]
    public class TreeData
    {
        public int x, y;
        public int hp;
        public string dropTableId = TreeFeature.DefaultDropTableId;
    }

    /// <summary>바닥에 떨어진 채 아직 줍지 않은 월드 아이템 하나 (WorldItem의 저장 형태).</summary>
    [Serializable]
    public class WorldItemData
    {
        public float x, y;
        public string itemId;
        public int count;
    }
}
