using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 울타리와 길을 id 하나로 찾기 위한 합본 표. 내용을 등록하는 곳은 FenceDatabase /
    /// RoadDatabase이고, 여기는 그것들을 모아 두기만 한다 — 그래서 설치·철거·저장 코드는
    /// "울타리인지 길인지"를 몰라도 되고, 종류를 추가해도 손댈 필요가 없다.
    /// </summary>
    public static class PlaceableDatabase
    {
        private static readonly Dictionary<string, PlaceableDef> _defs = new Dictionary<string, PlaceableDef>();
        private static readonly List<PlaceableDef> _all = new List<PlaceableDef>();
        private static bool _init;

        /// <summary>등록된 설치물 전부 (제작대 목록이 이 순서를 그대로 쓴다).</summary>
        public static IReadOnlyList<PlaceableDef> All { get { Init(); return _all; } }

        public static void Init()
        {
            if (_init) return;
            _init = true;
            FenceDatabase.Init();
            RoadDatabase.Init();
            FurnitureDatabase.Init();
        }

        /// <summary>
        /// 오브젝트 팔레트에 칠한 타일 이름으로 설치물을 되짚는다 — "Breakable_{맵}" 레이어가 쓴다.
        ///
        /// 팔레트 타일은 그림 조각에서 그대로 만들어지므로 이름이 조각 이름과 같다
        /// ("ShopCart", "WhiteFence_NS_L", "WoodRoad_EW"). 그래서 각 설치물이 <b>쓰는 조각 이름</b>을
        /// 한 번 훑어 표로 만들어 두고 거기서 찾는다. 대소문자와 밑줄은 무시한다.
        ///
        /// 울타리는 어느 조각을 칠했든 상관없다 — 이어진 모양은 놓인 뒤에 이웃을 보고 다시 정해지므로
        /// 여기서는 "어떤 울타리인지"만 알면 된다. 문 조각(GateOpenL ...)만 문으로 갈린다.
        /// </summary>
        public static PlaceableDef FindByTileName(string tileName)
        {
            Init();
            if (string.IsNullOrEmpty(tileName)) return null;
            if (_byTileName == null) BuildTileNameIndex();

            string key = Key(tileName);
            if (_byTileName.TryGetValue(key, out var exact)) return exact;

            // 딱 맞는 조각 이름이 없으면 뒤에서부터 한 마디씩 떼어 본다
            // ("WhiteFence_NS_Winter" -> "WhiteFence_NS" -> "WhiteFence").
            var parts = tileName.Split('_');
            for (int keep = parts.Length - 1; keep >= 1; keep--)
            {
                string shorter = Key(string.Join("_", parts, 0, keep));
                if (_byTileName.TryGetValue(shorter, out var def)) return def;
            }
            return null;
        }

        private static Dictionary<string, PlaceableDef> _byTileName;

        private static void BuildTileNameIndex()
        {
            _byTileName = new Dictionary<string, PlaceableDef>();
            foreach (var def in _all)
            {
                // id로도 찾을 수 있게 해 둔다 ("shop_cart" 라는 이름의 타일을 만들어도 통하도록).
                _byTileName[Key(def.id)] = def;
                foreach (var name in def.SpriteNames())
                {
                    string key = Key(name);
                    // 울타리와 문이 시트를 나눠 쓰지만 꼬리표가 달라 겹치지 않는다. 그래도 만약
                    // 겹치면 먼저 등록된 쪽을 남긴다 (Fence -> Gate 순서라 평범한 울타리가 이긴다).
                    if (!_byTileName.ContainsKey(key)) _byTileName[key] = def;
                }
            }
        }

        /// <summary>대소문자와 밑줄을 무시한 비교용 이름. "WhiteFence_NS" 와 "whitefencens" 는 같다.</summary>
        private static string Key(string name)
            => string.IsNullOrEmpty(name) ? "" : name.Replace("_", "").ToLowerInvariant();

        /// <summary>FenceDatabase / RoadDatabase가 자기 항목을 넣을 때 부른다.</summary>
        internal static void Register(PlaceableDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            _defs[def.id] = def;
            _all.Add(def);
            _byTileName = null;   // 표를 다시 만들게 한다
        }

        public static PlaceableDef Get(string id)
        {
            Init();
            if (string.IsNullOrEmpty(id)) return null;
            return _defs.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>이 아이템이 설치물인지 (설치물의 아이템 id는 설치물 id와 같다).</summary>
        public static bool IsPlaceable(string itemId) => Get(itemId) != null;
    }
}
