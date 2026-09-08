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
        /// <summary>폴더별 "스프라이트 이름 -> 스프라이트". 잘라 놓은 시트 안을 이름으로 찾기 위한 것.</summary>
        private static readonly Dictionary<string, Dictionary<string, Sprite>> _folderIndex =
            new Dictionary<string, Dictionary<string, Sprite>>();
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
        public static Sprite ShippingBox, ShippingBoxOpen, ShopCart, Workbench;
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

            Tilled = Load("Sprites/Tiles/Tilled");
            TilledWatered = Load("Sprites/Tiles/TilledWatered");
            Floor = Load("Sprites/Interior/Floor");
            Wall = Load("Sprites/Interior/Wall");
            Rug = Load("Sprites/Interior/Rug");

            House = Load("Sprites/Environment/House");
            Wood = Load("Sprites/Environment/Wood");
            ShippingBox = Load("Sprites/Environment/ShippingBox");
            ShippingBoxOpen = Load("Sprites/Environment/ShippingBoxOpen");
            ShopCart = Load("Sprites/Environment/ShopCart");
            Workbench = Load("Sprites/Environment/Workbench");
            Stone = Load("Sprites/Environment/Stone");
            ApricotSeed = Load("Sprites/Environment/ApricotSeed");

            Bed = Load("Sprites/Interior/Bed");
            Door = Load("Sprites/Interior/Door");
            Fireplace = Load("Sprites/Interior/Fireplace");
            Plant = Load("Sprites/Interior/Plant");

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

            LoadSeasonalAssets(Seasons.Current);
        }

        /// <summary>
        /// 계절이 바뀔 때 부른다. 계절에 따라 달라지는 스프라이트들만 다시 읽어 <b>같은 필드에</b> 넣으므로,
        /// AssetLibrary.Tree 처럼 쓰는 쪽은 한 줄도 고칠 필요가 없다.
        /// 그 다음 위치를 다시 로드하면 화면이 새 그림으로 바뀐다.
        /// </summary>
        public static void ApplySeason(Season season)
        {
            if (!_loaded) { EnsureLoaded(); return; }
            LoadSeasonalAssets(season);
        }

        /// <summary>
        /// 계절판(파일이름_Spring/_Summer/_Fall/_Winter)이 있으면 그것, 없으면 기본 그림.
        /// 예: Sprites/Environment/Tree_Winter.png 를 넣으면 겨울에만 그 나무가 나온다.
        /// </summary>
        private static void LoadSeasonalAssets(Season season)
        {
            Grass = GetSeasonalRaw("Sprites/Tiles/Grass", season);
            Dirt = GetSeasonalRaw("Sprites/Tiles/Dirt", season);
            Tree = GetSeasonalRaw("Sprites/Environment/Tree", season);
            RockVariants = LoadRangeSeasonal("Sprites/Environment/Rock_", 5, season);
            ApricotStages = LoadRangeSeasonal("Sprites/Environment/Apricot_", 4, season);
            StrawberryStages = LoadRangeSeasonal("Sprites/Crops/Strawberry_", 6, season);
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

        /// <summary>
        /// 계절별 스프라이트. 파일 이름 뒤에 계절을 붙여 두면 그 계절에만 그게 쓰인다:
        ///
        ///   Sprites/Environment/Apricot_3.png        (기본 · 계절판이 없으면 이게 나온다)
        ///   Sprites/Environment/Apricot_3_Fall.png   (가을에만)
        ///   Sprites/Environment/Apricot_3_Winter.png (겨울에만)
        ///
        /// 그래서 지금처럼 계절판이 하나도 없어도 네 계절이 전부 기본 그림으로 나오고,
        /// 바꾸고 싶은 것만 옆에 넣으면 된다 (같은 파일 네 벌을 만들 필요가 없다).
        /// 찾은 결과는 없는 경우까지 캐시되므로 Resources 접근은 경로당 딱 한 번이다.
        /// </summary>
        public static Sprite GetSeasonal(string resourcePath, Season season)
        {
            EnsureLoaded();
            return GetSeasonalRaw(resourcePath, season);
        }

        /// <summary>EnsureLoaded 안에서도 쓸 수 있는 판(재진입하지 않는다).</summary>
        private static Sprite GetSeasonalRaw(string resourcePath, Season season)
        {
            var seasonal = LoadQuiet(resourcePath + "_" + Seasons.Key(season));
            return seasonal != null ? seasonal : Load(resourcePath);
        }

        public static Sprite GetSeasonal(string resourcePath) => GetSeasonal(resourcePath, Seasons.Current);

        /// <summary>없어도 경고하지 않는 로드 — 계절 그림처럼 "있으면 쓰고 없으면 만다"에 쓴다.</summary>
        private static Sprite LoadQuiet(string path)
        {
            if (_cache.TryGetValue(path, out var cached)) return cached;
            var s = Resolve(path);
            _cache[path] = s;
            return s;
        }

        /// <summary>
        /// 스프라이트 하나를 찾는다. 먼저 그 경로의 <b>독립된 파일</b>을 보고, 없으면 같은 폴더의
        /// <b>잘라 놓은 시트 안</b>을 이름으로 뒤진다.
        ///
        /// 스프라이트 에디터로 큰 시트를 잘라 조각마다 이름을 붙이는 방식(Apricot Tree.png 안의
        /// "Apricot_0", "Apricot_3_Fall" ...)을 쓰면 파일이 따로 존재하지 않기 때문에
        /// Resources.Load(경로)만으로는 찾을 수 없다. 그 경우를 여기서 받아 준다.
        /// </summary>
        private static Sprite Resolve(string path)
        {
            var direct = Resources.Load<Sprite>(path);
            if (direct != null) return direct;

            int slash = path.LastIndexOf('/');
            if (slash < 0) return null;

            var index = FolderSpriteIndex(path.Substring(0, slash));
            if (index == null) return null;
            return index.TryGetValue(path.Substring(slash + 1), out var s) ? s : null;
        }

        /// <summary>
        /// 폴더 안의 모든 스프라이트를 이름으로 찾을 수 있게 한 번 훑어 둔다 (잘라 놓은 시트의 조각 포함).
        ///
        /// Resources.LoadAll은 하위 폴더까지 훑기 때문에 Sprites/Tiles 는 제외한다 — 그 아래에는
        /// 계절 타일셋 네 벌(각 934조각)이 있어서 통째로 읽으면 낭비다. 타일은 SeasonalTileset이
        /// 시트 단위로 따로 읽으므로 여기서 볼 일이 없다.
        /// </summary>
        private static Dictionary<string, Sprite> FolderSpriteIndex(string folder)
        {
            if (folder.StartsWith("Sprites/Tiles")) return null;
            if (_folderIndex.TryGetValue(folder, out var cached)) return cached;

            var all = Resources.LoadAll<Sprite>(folder);
            Dictionary<string, Sprite> map = null;
            if (all != null && all.Length > 0)
            {
                map = new Dictionary<string, Sprite>(all.Length);
                foreach (var sprite in all)
                    if (sprite != null && !map.ContainsKey(sprite.name)) map[sprite.name] = sprite;
            }
            _folderIndex[folder] = map;
            return map;
        }

        private static Sprite Load(string path)
        {
            if (_cache.TryGetValue(path, out var s)) return s;
            s = Resolve(path);
            if (s == null)
                Debug.LogWarning($"[AssetLibrary] Missing sprite: {path}");
            _cache[path] = s;
            return s;
        }

        /// <summary>LoadRange의 계절판. "Apricot_0_Winter"가 있으면 그걸, 없으면 "Apricot_0".</summary>
        private static Sprite[] LoadRangeSeasonal(string prefix, int count, Season season)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < count; i++)
            {
                var s = GetSeasonalRaw(prefix + i, season);
                if (s != null) list.Add(s);
            }
            return list.ToArray();
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
