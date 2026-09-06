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
    }
}
