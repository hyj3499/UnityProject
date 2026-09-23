using System;
using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 하루 동안 들르는 <b>정류장</b> 하나. 사이 경로는 적지 않는다 — 길은 BFS가 알아서 찾는다
    /// (<see cref="StoryPathfinder"/>). 그래서 걸음 좌표를 한 칸씩 찍을 필요가 없다.
    /// </summary>
    [Serializable]
    public class ScheduleStop
    {
        /// <summary>
        /// "HH:MM". 이 시각에 <b>출발한다</b> (도착이 아니다). 첫 정류장은 아침부터 거기 있다.
        /// 하루가 06:00에 시작하므로 그보다 이른 시각("01:30")은 <b>자정을 넘긴 그 날 끝</b>이다.
        /// </summary>
        public string time = "06:00";

        /// <summary>LocationId 이름. 비우면 앞 정류장과 같은 맵.</summary>
        public string map;

        /// <summary>"Objects_{맵}" 레이어에 칠해 둔 "Obj_Spot{이름}" 마커의 이름.</summary>
        public string spot;

        /// <summary>마커 대신 좌표로 찍고 싶을 때 (둘 다 -1이면 spot을 쓴다).</summary>
        public int x = -1, y = -1;

        /// <summary>도착해서 바라볼 방향 (Down/Up/Left/Right). 비우면 걸어온 방향 그대로.</summary>
        public string facing;

        /// <summary>다음 시각까지 이 반경(칸) 안을 어슬렁거린다. 0이면 가만히 서 있는다.</summary>
        public int wander;

        public int Minutes => DayClock.Parse(time);

        /// <summary>좌표로 찍은 정류장인지. spot을 적어 두면 좌표는 무시한다 (이름 쪽이 언제나 이긴다).</summary>
        public bool HasTile => string.IsNullOrEmpty(spot) && x >= 0 && y >= 0;

        public Direction? Facing =>
            Enum.TryParse(facing, out Direction d) ? d : (Direction?)null;
    }

    /// <summary>
    /// 하루치 일과 하나. <see cref="when"/>에 적은 조건이 <b>전부</b> 맞는 날에 쓰인다.
    ///
    /// when에 쓸 수 있는 말 (띄어쓰기로 여러 개, 전부 맞아야 한다):
    ///   계절  Spring Summer Fall Winter
    ///   요일  Mon Tue Wed Thu Fri Sat Sun
    ///   날씨  Sunny Cloudy Rain Storm Snow
    /// 비워 두면 <b>언제나</b> 맞는다 — 기본 일과라서 목록의 맨 뒤에 둔다.
    ///
    /// 고르는 방법은 "<b>위에서부터 처음 맞는 것</b>" 하나뿐이다. 우선순위 표 같은 걸 외울 필요 없이
    /// 특별한 날을 위에, 평범한 날을 아래에 적으면 된다.
    /// </summary>
    [Serializable]
    public class ScheduleRoutine
    {
        public string when;
        public ScheduleStop[] stops;

        public bool Matches(Season season, string weekday, Weather weather)
        {
            foreach (var tag in Tags(when))
            {
                if (Enum.TryParse(tag, out Season s)) { if (s != season) return false; continue; }
                if (Enum.TryParse(tag, out Weather w)) { if (w != weather) return false; continue; }
                if (Seasons.IsWeekdayKey(tag)) { if (tag != weekday) return false; continue; }
                return false;   // 모르는 말 — 이 일과는 영영 쓰이지 않는다 (검사 도구가 잡아 준다)
            }
            return true;
        }

        /// <summary>when을 낱말로 쪼갠다. 띄어쓰기든 쉼표든 상관없다.</summary>
        public static string[] Tags(string when)
            => string.IsNullOrWhiteSpace(when)
                ? Array.Empty<string>()
                : when.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>NPC 한 명의 일과표.</summary>
    [Serializable]
    public class NpcSchedule
    {
        public ScheduleRoutine[] routines;

        public bool IsEmpty => routines == null || routines.Length == 0;

        /// <summary>그 날 쓸 일과. 맞는 것이 없으면 null (그러면 home에 서 있는다).</summary>
        public ScheduleRoutine Pick(int day, Weather weather)
        {
            if (routines == null) return null;
            var season = Seasons.Of(day);
            string weekday = Seasons.WeekdayKey(day);
            foreach (var r in routines)
                if (r != null && r.stops != null && r.stops.Length > 0 && r.Matches(season, weekday, weather))
                    return r;
            return null;
        }

        /// <summary>when에 적힌 말이 전부 알아들을 수 있는 것인지 (에디터 검사용).</summary>
        public static List<string> UnknownTags(string when)
        {
            var bad = new List<string>();
            foreach (var tag in ScheduleRoutine.Tags(when))
            {
                if (Enum.TryParse(tag, out Season _)) continue;
                if (Enum.TryParse(tag, out Weather _)) continue;
                if (Seasons.IsWeekdayKey(tag)) continue;
                bad.Add(tag);
            }
            return bad;
        }
    }
}
