using UnityEngine;

namespace FarmMVP
{
    /// <summary>야외 색에 곱하는 햇빛. 흰색은 원래 색을 그대로 보존한다.</summary>
    public static class OutdoorLightingCycle
    {
        public const float Morning = 6 * 60;
        public const float Day = 9 * 60;
        public const float Evening = 17 * 60;
        public const float Sunset = 18 * 60;
        public const float Night = 20 * 60;

        public static readonly Color MorningTint = new Color(1f, 0.88f, 0.86f);
        public static readonly Color SunsetTint = new Color(1f, 0.58f, 0.36f);
        public static readonly Color DuskTint = new Color(0.55f, 0.24f, 0.23f);
        // 완전히 밤이 된 상태에서도 지형과 캐릭터의 윤곽은 읽을 수 있게 한다.
        public static readonly Color NightTint = new Color(0.12f, 0.15f, 0.24f);

        public static Color Evaluate(float minutes)
        {
            float m = Mathf.Repeat(minutes, 24 * 60);
            if (m < Morning || m >= Night) return NightTint;
            if (m < Day) return Blend(MorningTint, Color.white, Morning, Day, m);
            // 저녁은 17시에 시작하지만 붉은 노을은 18시부터 시작한다.
            if (m < Sunset) return Color.white;
            if (m < 19 * 60) return Blend(Color.white, SunsetTint, Sunset, 19 * 60, m);
            if (m < 19.5f * 60) return Blend(SunsetTint, DuskTint, 19 * 60, 19.5f * 60, m);
            return Blend(DuskTint, NightTint, 19.5f * 60, Night, m);
        }

        /// <summary>구름 그림자도 같은 아침/노을 시간표를 사용한다.</summary>
        public static float Sunlight(float minutes)
        {
            float m = Mathf.Repeat(minutes, 24 * 60);
            if (m < Morning || m >= Night) return 0f;
            if (m < Day) return Mathf.Lerp(0.7f, 1f, Progress(Morning, Day, m));
            if (m < Sunset) return 1f;
            return 1f - Progress(Sunset, Night, m);
        }

        private static Color Blend(Color a, Color b, float from, float to, float minutes)
            => Color.Lerp(a, b, Progress(from, to, minutes));

        private static float Progress(float from, float to, float minutes)
            => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, minutes));
    }
}
