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

        /// <summary>NPC별 호감도와 대화/선물 기록.</summary>
        public List<NpcStateData> npcs = new List<NpcStateData>();

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
        public int equippedHotbarIndex = 0; // 전체 슬롯 기준 번호 (0~29)
        public int backpackLevel = 0;       // 0=기본 10칸, 1=20칸, 2=30칸
        public int hotbarPage = 0;          // 퀵바가 지금 보여주는 배낭 페이지
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
        public List<RockData> rocks = new List<RockData>();
        public List<WorldItemData> droppedItems = new List<WorldItemData>();
        public bool initialized = false;

        /// <summary>바위는 나중에 추가된 기능이라, 기존 세이브에도 한 번은 깔리도록 따로 표시한다.</summary>
        public bool rocksInitialized = false;

        /// <summary>
        /// 마커로 이미 한 번 만들어 낸 칸들. 나무·바위는 저장되는 지형지물이라 맵에 들어올 때마다
        /// 다시 만들면 베어 낸 나무가 되살아난다. 그렇다고 맵 전체에 "한 번 했음" 표시 하나만 두면
        /// 세이브가 생긴 뒤에 새로 칠한 마커가 영영 나오지 않는다 — 그래서 <b>칸 단위</b>로 기억한다.
        /// </summary>
        public List<MarkerSpawnData> spawnedMarkers = new List<MarkerSpawnData>();

        /// <summary>spawnedMarkers가 없던 예전 세이브를 한 번 옮겼는지.</summary>
        public bool markersMigrated = false;
    }

    /// <summary>마커로 이미 만들어 낸 칸 하나 (LocationData.spawnedMarkers).</summary>
    [Serializable]
    public class MarkerSpawnData
    {
        public int x, y;
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
        public string treeId = TreeDatabase.DefaultTreeId;
        public int growthStage = 3; // 예전 세이브의 나무는 다 자란 상태로 본다
        public int dayCounter;
    }

    /// <summary>맵에 놓인 바위 하나.</summary>
    [Serializable]
    public class RockData
    {
        public int x, y;
        public int hp;
        public int variant;
    }

    /// <summary>NPC 한 명과의 관계 상태. 호감도 100당 하트 1개.</summary>
    [Serializable]
    public class NpcStateData
    {
        public string npcId;
        public int affection;         // 0 ~ 1000
        public int lastTalkDay = -1;  // 마지막으로 대화한 날 (하루 한 번 제한)
        public int giftWeek = -1;     // 선물 횟수를 세고 있는 주차
        public int giftsThisWeek;     // 그 주에 준 선물 수 (주 2회 제한)
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
