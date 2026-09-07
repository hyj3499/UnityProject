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
        // Interior furniture
        public static Sprite Bed, Door, Fireplace, Plant;
        // Crops
        public static Sprite[] StrawberryStages;
        public static Sprite StrawberrySeed, StrawberryFruit;
        // Tools
        public static Sprite Hoe, WateringCan, Axe;

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
