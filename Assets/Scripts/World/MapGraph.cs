using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>맵 가장자리의 네 방향. 맵과 맵을 잇는 출구가 어느 변에 붙는지 가리킨다.</summary>
    public enum MapSide { Left, Right, Bottom, Top }

    /// <summary>맵 둘을 잇는 통로 하나. 양쪽 맵에서 각각 어느 변으로 나가는지를 함께 들고 있다.</summary>
    public struct MapLink
    {
        public LocationId a, b;
        public MapSide sideA, sideB;
    }

    /// <summary>
    /// 맵끼리 어떻게 이어지는지를 적어 둔 <b>단 하나의 표</b>.
    ///
    /// 여기 적은 통로는 기본적으로 <b>맵의 변</b>이 된다 — 그 변에서 맵 끝을 넘어가면 이어진 맵으로
    /// 넘어간다 (<see cref="MapExits"/>). 타일을 한 장도 칠하지 않아도 세계 전체를 걸어 다닐 수 있고,
    /// 다리나 동굴 입구처럼 <b>특정한 칸</b>으로만 드나들게 하고 싶으면 그 맵의 "Objects_{맵}" 레이어에
    /// "Obj_Exit{맵}" 마커를 칠하면 된다 — 칠한 쪽이 변을 대신한다.
    ///
    /// 통로를 하나 더 놓고 싶으면 <see cref="_links"/>에 한 줄 적으면 된다. 양방향은 저절로
    /// 생기므로 반대쪽을 또 적지 않는다. side는 그 맵의 <b>어느 변</b>으로 나가는지다 —
    /// 한 변은 한 곳으로만 이어질 수 있으니, 한 맵에서 두 통로에 같은 변을 주면 안 된다
    /// (에디터 메뉴 "맵 연결 검사"가 잡아 준다).
    /// </summary>
    public static class MapGraph
    {
        private static readonly MapLink[] _links =
        {
            // 목장에서 뻗어 나가는 두 갈래
            Link(LocationId.Farm1,         MapSide.Top,    LocationId.Mountain1,    MapSide.Bottom),
            Link(LocationId.Farm1,         MapSide.Left,   LocationId.Valley,       MapSide.Right),

            // 계곡에서 두 마을로
            Link(LocationId.Valley,        MapSide.Left,   LocationId.PigeonVillage, MapSide.Right),
            Link(LocationId.Valley,        MapSide.Top,    LocationId.HawkVillage,   MapSide.Bottom),

            // 초원은 숲과 두 마을에 닿아 있다
            Link(LocationId.Meadow,        MapSide.Left,   LocationId.DeepForest,    MapSide.Right),
            Link(LocationId.Meadow,        MapSide.Top,    LocationId.PigeonVillage, MapSide.Bottom),
            Link(LocationId.Meadow,        MapSide.Right,  LocationId.HawkVillage,   MapSide.Left),

            // 비둘기 마을 뒤편이 산길 입구
            Link(LocationId.PigeonVillage, MapSide.Top,    LocationId.Mountain1,     MapSide.Left),

            // 매 마을 너머는 바다
            Link(LocationId.HawkVillage,   MapSide.Right,  LocationId.Sea,           MapSide.Left),

            // 산줄기 — 산1에서 둘로 갈라졌다가 산3에서 정상으로 이어진다
            Link(LocationId.Mountain1,     MapSide.Top,    LocationId.Mountain2,     MapSide.Bottom),
            Link(LocationId.Mountain1,     MapSide.Right,  LocationId.Mountain3,     MapSide.Bottom),
            Link(LocationId.Mountain2,     MapSide.Right,  LocationId.Mountain3,     MapSide.Left),
            Link(LocationId.Mountain3,     MapSide.Top,    LocationId.MountainPeak,  MapSide.Bottom),
        };

        public static IReadOnlyList<MapLink> Links => _links;

        /// <summary>이 맵과 이어진 맵들을, 이 맵의 어느 변으로 나가는지와 함께 돌려준다.</summary>
        public static List<KeyValuePair<LocationId, MapSide>> Neighbors(LocationId id)
        {
            var result = new List<KeyValuePair<LocationId, MapSide>>();
            foreach (var link in _links)
            {
                if (link.a == id) result.Add(new KeyValuePair<LocationId, MapSide>(link.b, link.sideA));
                else if (link.b == id) result.Add(new KeyValuePair<LocationId, MapSide>(link.a, link.sideB));
            }
            return result;
        }

        /// <summary>from에서 to로 나가는 변. 이어져 있지 않으면 false.</summary>
        public static bool TryGetSide(LocationId from, LocationId to, out MapSide side)
        {
            foreach (var link in _links)
            {
                if (link.a == from && link.b == to) { side = link.sideA; return true; }
                if (link.b == from && link.a == to) { side = link.sideB; return true; }
            }
            side = MapSide.Left;
            return false;
        }

        public static bool AreConnected(LocationId a, LocationId b) => TryGetSide(a, b, out _);

        /// <summary>로그와 에디터 창에 쓰는 한글 이름. 적어 두지 않은 맵은 영어 id 그대로.</summary>
        public static string DisplayName(LocationId id)
        {
            switch (id)
            {
                case LocationId.Farm1:         return "목장";
                case LocationId.Farm2:         return "두 번째 농장";
                case LocationId.FarmHouse:     return "농가";
                case LocationId.Valley:        return "계곡";
                case LocationId.PigeonVillage: return "비둘기 마을";
                case LocationId.HawkVillage:   return "매 마을";
                case LocationId.Meadow:        return "초원";
                case LocationId.DeepForest:    return "깊은 숲";
                case LocationId.Sea:           return "바다";
                case LocationId.Mountain1:     return "산 1";
                case LocationId.Mountain2:     return "산 2";
                case LocationId.Mountain3:     return "산 3";
                case LocationId.MountainPeak:  return "산 정상";
                default:                       return id.ToString();
            }
        }

        /// <summary>
        /// 실내 맵인지. 실내는 가장자리로 걸어 나가는 통로가 없고 문으로만 드나든다 —
        /// 그래서 임시 출구도 만들지 않는다.
        /// </summary>
        public static bool IsIndoor(LocationId id) => id == LocationId.FarmHouse;

        private static MapLink Link(LocationId a, MapSide sideA, LocationId b, MapSide sideB)
            => new MapLink { a = a, b = b, sideA = sideA, sideB = sideB };
    }
}
