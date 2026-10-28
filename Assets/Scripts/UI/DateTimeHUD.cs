using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 씬(Canvas/HUD/DateTimePanel)에 직접 만들어 둔 날짜·시간·날씨 패널을 채운다.
    /// 배치/크기/색은 전부 유니티 에디터에서 만진다 — 이 스크립트는 글자와 아이콘만 바꾼다.
    ///   DateText    → "Fall 16, Mon"
    ///   TimeText    → "19:40 PM"
    ///   WeatherIcon → 오늘 날씨 아이콘 (WheatherPanel 안의 16x16 Image)
    /// 게임(GameManager)은 타이틀에서 시작을 누른 뒤에야 생기므로, 없는 동안은 패널을 숨긴다.
    /// </summary>
    public class DateTimeHUD : MonoBehaviour
    {
        [Header("Texts (TextMeshPro)")]
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text timeText;

        [Tooltip("선택 사항. 비워 두면 각 텍스트에 지정된 폰트를 그대로 쓴다.")]
        [SerializeField, HideInInspector] private TMP_FontAsset fontAsset; // Legacy scene data; LocalizedText now controls the font.

        [Header("Season icon (DatePanel 옆)")]
        [SerializeField] private Image seasonIcon;
        [Tooltip("SeasonIcons.png 0~3번: 봄, 여름, 가을, 겨울")]
        [SerializeField] private Sprite[] seasonSprites = new Sprite[4];

        [Header("Time icon (TimePanel 옆)")]
        [SerializeField] private Image timeIcon;
        [Tooltip("DayIcons.png 0~9번: 0~4 달(어두움→보름달, 밤), 5~9 해(주황→노랑, 낮)")]
        [SerializeField] private Sprite[] timeSprites = new Sprite[10];

        [Header("Weather icon")]
        [SerializeField] private Image weatherIcon;
        [Tooltip("Weather enum 순서(WheatherIcon.png 0~9번)대로: 매우맑음, 맑음, 흐림, 바람, 번개, 번개+비, 비, 폭설, 폭풍, 눈")]
        [SerializeField] private Sprite[] weatherSprites = new Sprite[10];

        private GameManager _game;
        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            ApplyFont();
            Localization.Changed += Refresh;
            SetVisible(false);
        }

        private void OnDestroy() { Localization.Changed -= Refresh; Unbind(); }

        private void Update()
        {
            // 타이틀로 나가면 GameManager가 파괴되고, 새 게임을 시작하면 새로 생긴다.
            if (_game == null)
            {
                if (_group.alpha > 0f) SetVisible(false);
                var found = FindObjectOfType<GameManager>();
                if (found != null && found.Data != null) Bind(found);
            }
        }

        private void Bind(GameManager game)
        {
            _game = game;
            _game.OnTimeChanged += Refresh;
            _game.OnDayChanged += Refresh;
            Refresh();
            SetVisible(true);
        }

        private void Unbind()
        {
            if (_game == null) return;
            _game.OnTimeChanged -= Refresh;
            _game.OnDayChanged -= Refresh;
            _game = null;
        }

        private void SetVisible(bool on)
        {
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
        }

        private void Refresh()
        {
            if (_game == null || _game.Data == null) return;
            int day = _game.Data.currentDay;
            int minutes = _game.Data.currentMinutes;

            // HUD 날짜는 선택 언어와 관계없이 게임 내 영문 표기를 유지한다.
            if (dateText != null)
                dateText.text = $"{Seasons.Key(Seasons.Of(day))} {Seasons.DayOfSeason(day)}, {Seasons.WeekdayKey(day)}";

            if (timeText != null)
                timeText.text = $"{DayClock.Format(minutes)} {(DayClock.Hour(minutes) >= 12 ? "PM" : "AM")}";

            SetIcon(seasonIcon, SeasonSprite(day));
            SetIcon(timeIcon, TimeSprite(minutes));

            if (weatherIcon != null)
            {
                var sprite = IconFor(_game.Today);
                if (sprite != null) weatherIcon.sprite = sprite;
                weatherIcon.enabled = sprite != null;
            }
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null) return;
            if (sprite != null) image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private Sprite SeasonSprite(int day)
        {
            int i = (int)Seasons.Of(day);
            return seasonSprites != null && i < seasonSprites.Length ? seasonSprites[i] : null;
        }

        /// <summary>
        /// 낮(06~18시)은 해: 정오에 가까울수록 밝은 노랑, 이른 아침·저녁은 어두운 주황.
        /// 밤(18시~02시)은 달: 자정에 가까울수록 보름달, 해가 막 졌거나 새벽 가까우면 어두운 달.
        /// </summary>
        private Sprite TimeSprite(int minutes)
        {
            if (timeSprites == null || timeSprites.Length < 10) return null;
            float hour = minutes / 60f;   // 자정을 넘기면 24, 25...로 이어진다
            int index;
            if (DayClock.IsDaytime(minutes))
            {
                float d = Mathf.Abs(hour - 12f);
                index = 5 + (d < 1.5f ? 4 : d < 3f ? 3 : d < 4.5f ? 2 : d < 5.5f ? 1 : 0);
            }
            else
            {
                float d = Mathf.Abs(hour - 24f);
                index = d < 1.5f ? 4 : d < 3f ? 3 : d < 4.5f ? 2 : d < 5.5f ? 1 : 0;
            }
            return timeSprites[index];
        }

        private Sprite IconFor(Weather w)
        {
            int i = (int)w;
            return weatherSprites != null && i < weatherSprites.Length ? weatherSprites[i] : null;
        }

        // 에디터에서 텍스트에 직접 지정한 폰트가 기본이고, 아래 슬롯을 채웠을 때만 덮어쓴다.
        private void ApplyFont()
        {
            if (dateText != null) LocalizedText.Attach(dateText, null, FontRole.System1);
            if (timeText != null) LocalizedText.Attach(timeText, null, FontRole.System1);
        }

    }
}
