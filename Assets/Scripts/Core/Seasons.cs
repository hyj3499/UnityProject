using System;

namespace FarmMVP
{
    public enum Season { Spring, Summer, Fall, Winter }

    /// <summary>
    /// 여러 계절을 한 값으로 표현한다 (작물이 자라는 계절, 물고기가 잡히는 계절 등).
    /// 계절이 하나뿐인 것도 SeasonFlags.Spring 처럼 그대로 쓴다.
    /// </summary>
    [Flags]
    public enum SeasonFlags
    {
        None = 0,
        Spring = 1 << 0,
        Summer = 1 << 1,
        Fall = 1 << 2,
        Winter = 1 << 3,
        All = Spring | Summer | Fall | Winter,
    }

    /// <summary>
    /// 달력. 계절은 저장하지 않고 <b>날짜에서 계산</b>한다 — 저장 데이터에 같은 사실이 두 번 들어가면
    /// 언젠가 어긋나기 때문이다. currentDay 하나만 늘리면 연/계절/계절 내 날짜가 전부 따라온다.
    /// </summary>
    public static class Seasons
    {
        public const int DaysPerSeason = 28;
        public const int SeasonCount = 4;
        public const int DaysPerYear = DaysPerSeason * SeasonCount;
        public const int DaysPerWeek = 7;

        /// <summary>요일 이름. 스케줄 JSON의 when 태그로 그대로 쓴다.</summary>
        private static readonly string[] WeekdayKeys = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        private static readonly string[] WeekdayNames = { "월", "화", "수", "목", "금", "토", "일" };

        /// <summary>
        /// 지금 계절. GameManager가 날짜를 바꿀 때마다 갱신한다.
        /// 작물/물고기/타일이 전부 이 값을 보므로 인자로 계절을 들고 다니지 않아도 된다.
        /// </summary>
        public static Season Current { get; private set; } = Season.Spring;

        /// <summary>계절이 바뀌는 순간 불린다 (타일 새로 그리기, 시든 작물 정리 등).</summary>
        public static event Action<Season> OnSeasonChanged;

        /// <summary>날짜로부터 계절을 다시 계산한다. 바뀌었으면 true.</summary>
        public static bool SyncToDay(int day)
        {
            var next = Of(day);
            if (next == Current) return false;
            Current = next;
            OnSeasonChanged?.Invoke(next);
            return true;
        }

        /// <summary>이벤트를 쏘지 않고 값만 맞춘다 (세이브를 막 불러왔을 때).</summary>
        public static void SetSilently(int day) => Current = Of(day);

        public static Season Of(int day) => (Season)(((Math.Max(1, day) - 1) / DaysPerSeason) % SeasonCount);

        /// <summary>그 계절의 며칠째인지 (1~28).</summary>
        public static int DayOfSeason(int day) => ((Math.Max(1, day) - 1) % DaysPerSeason) + 1;

        public static int YearOf(int day) => ((Math.Max(1, day) - 1) / DaysPerYear) + 1;

        /// <summary>그 날의 요일 (0=월 ... 6=일). 한 계절이 28일이라 계절마다 같은 요일로 시작한다.</summary>
        public static int DayOfWeek(int day) => (Math.Max(1, day) - 1) % DaysPerWeek;

        /// <summary>스케줄 JSON에 쓰는 요일 이름 ("Mon" ... "Sun").</summary>
        public static string WeekdayKey(int day) => WeekdayKeys[DayOfWeek(day)];

        /// <summary>화면에 쓰는 요일 이름 ("월" ... "일").</summary>
        public static string WeekdayName(int day) => WeekdayNames[DayOfWeek(day)];

        /// <summary>그런 이름의 요일이 있는지 (스케줄 검사용).</summary>
        public static bool IsWeekdayKey(string key) => Array.IndexOf(WeekdayKeys, key) >= 0;

        public static SeasonFlags ToFlag(Season s) => (SeasonFlags)(1 << (int)s);

        /// <summary>이 계절이 허용되는지. mask가 None이면 (설정을 깜빡한 것으로 보고) 모든 계절 허용.</summary>
        public static bool Allows(SeasonFlags mask, Season s)
            => mask == SeasonFlags.None || (mask & ToFlag(s)) != 0;

        public static bool AllowsNow(SeasonFlags mask) => Allows(mask, Current);

        public static string Name(Season s)
        {
            switch (s)
            {
                case Season.Spring: return "봄";
                case Season.Summer: return "여름";
                case Season.Fall: return "가을";
                case Season.Winter: return "겨울";
                default: return "";
            }
        }

        /// <summary>"봄", "봄·여름", "사계절" 처럼 여러 계절을 한 줄로 (작물이 자라는 계절 표시).</summary>
        public static string Label(SeasonFlags mask)
        {
            if (mask == SeasonFlags.None || mask == SeasonFlags.All) return "사계절";

            var parts = new System.Collections.Generic.List<string>(SeasonCount);
            for (int i = 0; i < SeasonCount; i++)
            {
                var s = (Season)i;
                if (Allows(mask, s)) parts.Add(Name(s));
            }
            return string.Join("·", parts);
        }

        /// <summary>스프라이트/타일맵 이름에 붙는 접두사 ("Winter_Location_Farm1").</summary>
        public static string Key(Season s) => s.ToString();   // Spring / Summer / Fall / Winter

        public static string NameOfDay(int day) => Name(Of(day));
    }
}
