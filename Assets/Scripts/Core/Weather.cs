using System;

namespace FarmMVP
{
    /// <summary>오늘의 날씨. NPC 스케줄·대사·연출이 이 값을 본다.</summary>
    public enum Weather { Sunny, Cloudy, Rain, Storm, Snow }

    /// <summary>
    /// 날씨는 <b>저장하지 않고 날짜에서 계산한다</b> — 계절과 같은 원칙이다(<see cref="Seasons"/>).
    /// 세이브에는 게임마다 한 번 뽑는 씨앗(<see cref="GameData.weatherSeed"/>) 하나만 들어가고,
    /// 며칠 뒤 날씨까지 <b>미리</b> 알 수 있어서 일기예보도 계산 한 번이면 된다.
    ///
    /// 날씨를 억지로 정하고 싶은 날(축제·이벤트)은 <see cref="Fixed"/>에 한 줄 적는다.
    /// </summary>
    public static class WeatherSystem
    {
        /// <summary>계절마다 (흐림, 비, 폭풍, 눈)일 확률. 나머지는 맑음.</summary>
        private static readonly float[,] Chances =
        {
            // 흐림  비    폭풍  눈
            { 0.20f, 0.25f, 0.03f, 0.00f },   // 봄   — 비가 잦다
            { 0.15f, 0.12f, 0.08f, 0.00f },   // 여름 — 가끔 쏟아진다
            { 0.25f, 0.20f, 0.04f, 0.00f },   // 가을
            { 0.20f, 0.00f, 0.00f, 0.35f },   // 겨울 — 비 대신 눈
        };

        /// <summary>날씨가 정해져 있는 날 (계절 안에서의 날짜 → 날씨). 축제나 인트로에 쓴다.</summary>
        private static readonly (Season season, int dayOfSeason, Weather weather)[] Fixed =
        {
            (Season.Spring, 1, Weather.Sunny),   // 첫날은 항상 맑음
        };

        /// <summary>그 날의 날씨. 같은 날짜·같은 씨앗이면 언제 물어봐도 같은 답이 나온다.</summary>
        public static Weather Of(int day, int seed)
        {
            day = Math.Max(1, day);
            var season = Seasons.Of(day);
            int dayOfSeason = Seasons.DayOfSeason(day);

            foreach (var f in Fixed)
                if (f.season == season && f.dayOfSeason == dayOfSeason) return f.weather;

            float roll = Roll(day, seed);
            float cloudy = Chances[(int)season, 0];
            float rain = Chances[(int)season, 1];
            float storm = Chances[(int)season, 2];
            float snow = Chances[(int)season, 3];

            if (roll < snow) return Weather.Snow;
            roll -= snow;
            if (roll < storm) return Weather.Storm;
            roll -= storm;
            if (roll < rain) return Weather.Rain;
            roll -= rain;
            return roll < cloudy ? Weather.Cloudy : Weather.Sunny;
        }

        /// <summary>비가 오는가 (폭풍 포함). 작물에 물을 주는 처리 등에 쓸 수 있다.</summary>
        public static bool IsWet(Weather w) => w == Weather.Rain || w == Weather.Storm;

        public static string Name(Weather w)
        {
            switch (w)
            {
                case Weather.Cloudy: return "흐림";
                case Weather.Rain: return "비";
                case Weather.Storm: return "폭풍";
                case Weather.Snow: return "눈";
                default: return "맑음";
            }
        }

        /// <summary>스케줄 JSON의 when 태그로 쓰는 이름 (Sunny/Cloudy/Rain/Storm/Snow).</summary>
        public static string Key(Weather w) => w.ToString();

        /// <summary>날짜와 씨앗을 섞어 0~1 값을 만든다 (해시라 순서에 상관없이 고르게 흩어진다).</summary>
        private static float Roll(int day, int seed)
        {
            unchecked
            {
                uint h = (uint)(day * 73856093) ^ (uint)(seed * 19349663);
                h ^= h >> 13; h *= 0x85EBCA6B; h ^= h >> 16;
                return (h % 100000u) / 100000f;
            }
        }
    }
}
