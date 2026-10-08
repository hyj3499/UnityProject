using System;

namespace FarmMVP
{
    /// <summary>
    /// 오늘의 날씨. NPC 스케줄·대사·연출이 이 값을 본다.
    /// <b>순서는 WheatherIcon.png의 스프라이트 번호(0~9)와 같다</b> — 순서를 바꾸면 HUD 아이콘이 어긋난다.
    /// </summary>
    public enum Weather
    {
        VerySunny,     // 0 매우 맑음
        Sunny,         // 1 맑음
        Cloudy,        // 2 흐림
        Windy,         // 3 바람
        Thunder,       // 4 번개
        ThunderRain,   // 5 번개 + 비
        Rain,          // 6 비
        Blizzard,      // 7 폭설
        Storm,         // 8 폭풍
        Snow,          // 9 눈
    }

    /// <summary>
    /// 날씨는 <b>저장하지 않고 날짜에서 계산한다</b> — 계절과 같은 원칙이다(<see cref="Seasons"/>).
    /// 세이브에는 게임마다 한 번 뽑는 씨앗(<see cref="GameData.weatherSeed"/>) 하나만 들어가고,
    /// 며칠 뒤 날씨까지 <b>미리</b> 알 수 있어서 일기예보도 계산 한 번이면 된다.
    ///
    /// 날씨를 억지로 정하고 싶은 날(축제·이벤트)은 <see cref="Fixed"/>에 한 줄 적는다.
    /// </summary>
    public static class WeatherSystem
    {
        /// <summary>아래 확률표의 열 순서. Sunny는 나머지 전부라서 표에 없다.</summary>
        private static readonly Weather[] Rolled =
        {
            Weather.Blizzard, Weather.Snow, Weather.Storm, Weather.ThunderRain, Weather.Thunder,
            Weather.Rain, Weather.Windy, Weather.Cloudy, Weather.VerySunny,
        };

        /// <summary>계절(행)마다 각 날씨(열)가 될 확률. 위에서부터 차례로 뽑고 안 걸리면 맑음.</summary>
        private static readonly float[][] Chances =
        {
            //          폭설   눈     폭풍   번개비  번개   비     바람   흐림   매우맑음
            new[] { 0.00f, 0.00f, 0.02f, 0.03f, 0.01f, 0.20f, 0.08f, 0.15f, 0.15f },   // 봄 — 비가 잦다
            new[] { 0.00f, 0.00f, 0.05f, 0.06f, 0.04f, 0.10f, 0.04f, 0.10f, 0.30f },   // 여름 — 쨍하고 가끔 쏟아진다
            new[] { 0.00f, 0.00f, 0.03f, 0.03f, 0.01f, 0.15f, 0.15f, 0.20f, 0.10f },   // 가을 — 바람
            new[] { 0.08f, 0.30f, 0.02f, 0.00f, 0.00f, 0.00f, 0.08f, 0.15f, 0.05f },   // 겨울 — 비 대신 눈
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
            var row = Chances[(int)season];
            for (int i = 0; i < Rolled.Length; i++)
            {
                if (roll < row[i]) return Rolled[i];
                roll -= row[i];
            }
            return Weather.Sunny;
        }

        /// <summary>비·눈이 내리는가 (폭풍 포함). 작물에 물을 주는 처리 등에 쓸 수 있다.</summary>
        public static bool IsWet(Weather w)
            => w == Weather.Rain || w == Weather.ThunderRain || w == Weather.Storm;

        /// <summary>
        /// 스케줄의 when 태그가 오늘 날씨에 맞는가. 옛 태그도 계속 통한다:
        /// Sunny는 매우 맑음도, Rain은 번개+비도, Snow는 폭설도, Storm은 번개도 포함한다.
        /// </summary>
        public static bool Matches(Weather tag, Weather today)
        {
            if (tag == today) return true;
            switch (tag)
            {
                case Weather.Sunny: return today == Weather.VerySunny;
                case Weather.Rain: return today == Weather.ThunderRain;
                case Weather.Snow: return today == Weather.Blizzard;
                case Weather.Storm: return today == Weather.Thunder;
                default: return false;
            }
        }

        public static string Name(Weather w)
        {
            switch (w)
            {
                case Weather.VerySunny: return "매우 맑음";
                case Weather.Cloudy: return "흐림";
                case Weather.Windy: return "바람";
                case Weather.Thunder: return "번개";
                case Weather.ThunderRain: return "번개와 비";
                case Weather.Rain: return "비";
                case Weather.Blizzard: return "폭설";
                case Weather.Storm: return "폭풍";
                case Weather.Snow: return "눈";
                default: return "맑음";
            }
        }

        /// <summary>스케줄 JSON의 when 태그로 쓰는 이름 (Weather 이름 그대로).</summary>
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
