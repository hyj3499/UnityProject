using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 하루가 몇 시에 시작해서 몇 시에 끝나는지, 시간이 얼마나 빨리 흐르는지를 적어 둔 <b>한 곳</b>.
    ///
    /// 하루는 <b>06:00에 시작해 다음 날 02:00에 끝난다</b>(20시간). 자정을 넘긴 시각은 24를 더해
    /// 이어서 센다 — 01:30은 25*60=1500분이다. 그래야 "늦은 시각일수록 큰 수"라는 규칙이 하루 내내
    /// 깨지지 않아서, 스케줄·이벤트 조건을 그냥 크기 비교로 다룰 수 있다. 화면에는 24로 나눈
    /// 나머지를 보여 주므로 플레이어에게는 그냥 01:30이다.
    ///
    /// 시간은 <b>10분 단위로 뚝뚝 끊어서</b> 흐른다 (06:00 → 06:10 → 06:20 ...).
    /// </summary>
    public static class DayClock
    {
        /// <summary>하루가 시작하는 시각 (06:00).</summary>
        public const int DayStart = 6 * 60;

        /// <summary>하루가 끝나는 시각 (다음 날 02:00). 여기 닿으면 강제로 잠든다.</summary>
        public const int DayEnd = 26 * 60;

        /// <summary>시계가 한 번에 건너뛰는 분.</summary>
        public const int Step = 10;

        /// <summary>그 한 칸이 현실로 몇 초인지. 10분 = 7초 → 1시간 ≈ 42초, 하루(20시간) ≈ 14분.</summary>
        public const float RealSecondsPerStep = 7f;

        /// <summary>현실 1초가 게임 몇 분인지 (≈1.43).</summary>
        public const float MinutesPerRealSecond = Step / RealSecondsPerStep;

        /// <summary>낮으로 치는 시간대 (해가 떠 있는 동안).</summary>
        public const int DuskMinutes = 18 * 60;

        /// <summary>현실 초를 게임 분으로. NPC가 몇 칸 걷는 데 게임으로 몇 분이 드는지 등에 쓴다.</summary>
        public static float MinutesForSeconds(float seconds) => seconds * MinutesPerRealSecond;

        /// <summary>10분 칸에 맞춰 내림한다.</summary>
        public static int Snap(int minutes) => minutes - Mod(minutes, Step);

        /// <summary>
        /// 10분 칸에 맞춰 <b>올림</b>한다. 시계가 칸 위에만 서기 때문에, 칸 사이에 잡아 둔 시각은
        /// 실제로는 다음 칸에 닥친다 — 그 어긋남만큼 늦어지지 않도록 미리 칸에 맞춰 둘 때 쓴다.
        /// </summary>
        public static int SnapUp(int minutes) => minutes + Mod(Step - Mod(minutes, Step), Step);

        /// <summary>하루 범위 안으로 가둔다.</summary>
        public static int Clamp(int minutes) => Mathf.Clamp(minutes, DayStart, DayEnd);

        public static bool IsDaytime(int minutes) => minutes >= DayStart && minutes < DuskMinutes;

        /// <summary>화면에 보여 줄 "HH:MM". 자정을 넘긴 25:30은 01:30으로 보인다.</summary>
        public static string Format(int minutes) => $"{Hour(minutes):00}:{Mod(minutes, 60):00}";

        public static int Hour(int minutes) => Mod(minutes / 60, 24);

        /// <summary>
        /// 콘텐츠 json에 적은 "HH:MM"을 분으로. <b>06:00보다 이른 시각은 자정을 넘긴 것으로 본다</b> —
        /// 하루가 06:00에 시작하므로 "01:30"이라고 쓰면 그 날 <b>끝</b>의 새벽 1시 30분이다.
        /// </summary>
        public static int Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return DayStart;

            int colon = text.IndexOf(':');
            int minutes;
            if (colon < 0)
            {
                if (!int.TryParse(text, out minutes)) return DayStart;
            }
            else
            {
                int.TryParse(text.Substring(0, colon), out int h);
                int.TryParse(text.Substring(colon + 1), out int m);
                minutes = h * 60 + m;
            }
            return minutes < DayStart ? minutes + 24 * 60 : minutes;
        }

        private static int Mod(int value, int divisor)
        {
            int r = value % divisor;
            return r < 0 ? r + divisor : r;
        }
    }
}
