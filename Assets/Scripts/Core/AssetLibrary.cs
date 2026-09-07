using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Central registry that loads every sprite from Resources at runtime and
    /// exposes them by logical name. Sprites live under Assets/Resources/Sprites
    /// (a copy of Assets/Sprites) so Resources.Load can find them without manual
    /// scene wiring.
    /// </summary>
    public static class AssetLibrary
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private static bool _loaded;

        // Player animation frame arrays
        public static Sprite[] IdleDown, IdleUp, IdleSide;
        public static Sprite[] WalkDown, WalkUp, WalkSide;

        // Tiles
        public static Sprite Grass, Dirt, Tilled, TilledWatered;
        // Interior tiles
        public static Sprite Floor, Wall, Rug;
        // Environment
        public static Sprite Tree, House, Wood;
        public static Sprite ShippingBox, ShippingBoxOpen, ShopCart;
        public static Sprite[] RockVariants;      // 이끼 없는 바위 5종
        public static Sprite Stone, ApricotSeed;
        public static Sprite[] ApricotStages;     // 씨앗 → 묘목 → 성장 → 다 자람
        // Interior furniture
        public static Sprite Bed, Door, Fireplace, Plant;
        // Crops
        public static Sprite[] StrawberryStages;
        public static Sprite StrawberrySeed, StrawberryFruit;
        // Tools
        public static Sprite Hoe, WateringCan, Axe;
        // UI (Farm RPG Tiny Asset Pack 에서 잘라낸 조각들)
        public static Sprite UiBook, UiHudInfo, UiHudMoney, UiIconSun, UiIconMoon;
        public static Sprite UiSlot, UiSlotSelected, UiFrame, UiDialoguePanel;
        public static Sprite UiBookmarkGreen, UiBookmarkOrange, UiBookmarkBlue;
        public static Sprite UiBagLv1, UiBagLv2;
        // 낚시
        public static Sprite UiAlert, UiFishTrack, UiFishZone, UiFishMarker;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            IdleDown = LoadRange("Sprites/Player/Idle_Down_", 4);
            IdleUp = LoadRange("Sprites/Player/Idle_Up_", 4);
            IdleSide = LoadRange("Sprites/Player/Idle_Side_", 4);
            WalkDown = LoadRange("Sprites/Player/Walk_Down_", 6);
            WalkUp = LoadRange("Sprites/Player/Walk_Up_", 6);
            WalkSide = LoadRange("Sprites/Player/Walk_Side_", 6);

            Grass = Load("Sprites/Tiles/Grass");
            Dirt = Load("Sprites/Tiles/Dirt");
            Tilled = Load("Sprites/Tiles/Tilled");
            TilledWatered = Load("Sprites/Tiles/TilledWatered");
            Floor = Load("Sprites/Interior/Floor");
            Wall = Load("Sprites/Interior/Wall");
            Rug = Load("Sprites/Interior/Rug");

            Tree = Load("Sprites/Environment/Tree");
            House = Load("Sprites/Environment/House");
            Wood = Load("Sprites/Environment/Wood");
            ShippingBox = Load("Sprites/Environment/ShippingBox");
            ShippingBoxOpen = Load("Sprites/Environment/ShippingBoxOpen");
            ShopCart = Load("Sprites/Environment/ShopCart");
            RockVariants = LoadRange("Sprites/Environment/Rock_", 5);
            Stone = Load("Sprites/Environment/Stone");
            ApricotSeed = Load("Sprites/Environment/ApricotSeed");
            ApricotStages = LoadRange("Sprites/Environment/Apricot_", 4);

            Bed = Load("Sprites/Interior/Bed");
            Door = Load("Sprites/Interior/Door");
            Fireplace = Load("Sprites/Interior/Fireplace");
            Plant = Load("Sprites/Interior/Plant");

            StrawberryStages = LoadRange("Sprites/Crops/Strawberry_", 6);
            StrawberrySeed = Load("Sprites/Crops/StrawberrySeed");
            StrawberryFruit = Load("Sprites/Crops/StrawberryFruit");

            Hoe = Load("Sprites/Tools/Hoe");
            WateringCan = Load("Sprites/Tools/WateringCan");
            Axe = Load("Sprites/Tools/Axe");

            UiBook = Load("Sprites/UI/Book");
            UiHudInfo = Load("Sprites/UI/HudInfo");
            UiHudMoney = Load("Sprites/UI/HudMoney");
            UiIconSun = Load("Sprites/UI/IconSun");
            UiIconMoon = Load("Sprites/UI/IconMoon");
            UiSlot = Load("Sprites/UI/Slot");
            UiSlotSelected = Load("Sprites/UI/SlotSelected");
            UiFrame = Load("Sprites/UI/Frame");
            UiDialoguePanel = Load("Sprites/UI/DialoguePanel");
            UiBookmarkGreen = Load("Sprites/UI/BookmarkGreen");
            UiBookmarkOrange = Load("Sprites/UI/BookmarkOrange");
            UiBookmarkBlue = Load("Sprites/UI/BookmarkBlue");
            UiBagLv1 = Load("Sprites/UI/BagLv1");
            UiBagLv2 = Load("Sprites/UI/BagLv2");

            UiAlert = Load("Sprites/UI/Alert");
            UiFishTrack = Load("Sprites/UI/FishTrack");
            UiFishZone = Load("Sprites/UI/FishZone");
            UiFishMarker = Load("Sprites/UI/FishMarker");
        }

        /// <summary>
        /// Resources 경로로 스프라이트를 직접 가져온다 (캐시됨). 물고기처럼 종류가 많아
        /// 필드를 하나씩 두기 곤란한 것들이 쓴다.
        /// </summary>
        public static Sprite GetSprite(string resourcePath)
        {
            EnsureLoaded();
            return Load(resourcePath);
        }

        private static Sprite Load(string path)
        {
            if (_cache.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>(path);
            if (s == null)
                Debug.LogWarning($"[AssetLibrary] Missing sprite: {path}");
            _cache[path] = s;
            return s;
        }

        private static Sprite[] LoadRange(string prefix, int count)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < count; i++)
            {
                var s = Load(prefix + i);
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }
    }
}
