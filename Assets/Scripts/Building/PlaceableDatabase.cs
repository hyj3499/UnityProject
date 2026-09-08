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
        }

        /// <summary>FenceDatabase / RoadDatabase가 자기 항목을 넣을 때 부른다.</summary>
        internal static void Register(PlaceableDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            _defs[def.id] = def;
            _all.Add(def);
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
