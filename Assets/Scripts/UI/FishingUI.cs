using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 낚시 화면 표시 두 가지:
    ///  - 주인공 머리 위 느낌표 (월드 스프라이트. TargetIndicator와 같은 방식)
    ///  - 화면 아래쪽 타이밍 미니게임 바 ( -O-O-O- 위를 커서가 왕복 )
    /// 판정은 전혀 하지 않는다 — FishingController가 시킨 대로 그리기만 한다.
    /// </summary>
    public class FishingUI : MonoBehaviour
    {
        private const float TrackWidth = 520f;
        private const float TrackHeight = 34f;

        // 월드: 머리 위 느낌표
        private SpriteRenderer _alert;
        private Transform _alertFollow;

        // 캔버스: 미니게임 바
        private RectTransform _panel;
        private RectTransform _track;
        private RectTransform _marker;
        private Image _fishIcon;
        private Text _fishLabel, _missLabel;
        private Image _timeFill;
        private Image _panelBg;
        private readonly List<Image> _zones = new List<Image>();

        private float _flashTimer;
        private Color _flashColor;
        private int _missesAllowed;

        public static FishingUI Create(UIManager ui)
        {
            var go = new GameObject("FishingUI");
            var f = go.AddComponent<FishingUI>();
            f.BuildAlert();
            f.BuildBar(ui);
            f.Hide();
            return f;
        }

        // ---------- 만들기 ----------
        private void BuildAlert()
        {
            var go = new GameObject("FishingAlert");
            go.transform.SetParent(transform, false);
            _alert = go.AddComponent<SpriteRenderer>();
            _alert.sprite = AssetLibrary.UiAlert;
            _alert.sortingOrder = 1100;   // 플레이어(1000)보다 위
            go.SetActive(false);
        }

        private void BuildBar(UIManager ui)
        {
            var root = new GameObject("FishingBar");
            _panel = root.AddComponent<RectTransform>();
            _panel.SetParent(ui.CanvasRoot, false);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.anchoredPosition = new Vector2(0, 110);
            _panel.sizeDelta = new Vector2(TrackWidth + 40, 96);

            _panelBg = root.AddComponent<Image>();
            _panelBg.color = new Color(0f, 0f, 0f, 0.55f);
            _panelBg.raycastTarget = false;   // 미니게임 클릭은 월드에서 받는다

            // 잡고 있는 물고기 아이콘 + 이름
            var iconGo = new GameObject("FishIcon");
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.SetParent(_panel, false);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 1f);
            iconRt.pivot = new Vector2(0f, 1f);
            iconRt.anchoredPosition = new Vector2(14, -8);
            iconRt.sizeDelta = new Vector2(28, 28);
            _fishIcon = iconGo.AddComponent<Image>();
            _fishIcon.raycastTarget = false;
            _fishIcon.preserveAspect = true;

            _fishLabel = ui.Label(_panel, "", 18, new Vector2(0, 0), TextAnchor.UpperLeft);
            _fishLabel.rectTransform.anchorMin = _fishLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            _fishLabel.rectTransform.pivot = new Vector2(0f, 1f);
            _fishLabel.rectTransform.anchoredPosition = new Vector2(50, -10);
            _fishLabel.rectTransform.sizeDelta = new Vector2(300, 26);

            _missLabel = ui.Label(_panel, "", 18, Vector2.zero, TextAnchor.UpperRight);
            _missLabel.rectTransform.anchorMin = _missLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _missLabel.rectTransform.pivot = new Vector2(1f, 1f);
            _missLabel.rectTransform.anchoredPosition = new Vector2(-14, -10);
            _missLabel.rectTransform.sizeDelta = new Vector2(160, 26);

            // 트랙
            var trackGo = new GameObject("Track");
            _track = trackGo.AddComponent<RectTransform>();
            _track.SetParent(_panel, false);
            _track.anchorMin = _track.anchorMax = new Vector2(0.5f, 0f);
            _track.pivot = new Vector2(0.5f, 0f);
            _track.anchoredPosition = new Vector2(0, 26);
            _track.sizeDelta = new Vector2(TrackWidth, TrackHeight);
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.sprite = AssetLibrary.UiFishTrack;
            trackImg.type = Image.Type.Sliced;   // 좌우 테두리를 고정하고 가운데만 늘린다
            trackImg.raycastTarget = false;

            // 커서
            var markerGo = new GameObject("Marker");
            _marker = markerGo.AddComponent<RectTransform>();
            _marker.SetParent(_track, false);
            _marker.anchorMin = _marker.anchorMax = new Vector2(0f, 0.5f);
            _marker.pivot = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = new Vector2(12, TrackHeight + 8);
            var markerImg = markerGo.AddComponent<Image>();
            markerImg.sprite = AssetLibrary.UiFishMarker;
            markerImg.raycastTarget = false;

            // 남은 시간 줄
            var timeGo = new GameObject("TimeFill");
            var timeRt = timeGo.AddComponent<RectTransform>();
            timeRt.SetParent(_panel, false);
            timeRt.anchorMin = new Vector2(0f, 0f);
            timeRt.anchorMax = new Vector2(1f, 0f);
            timeRt.pivot = new Vector2(0f, 0f);
            timeRt.offsetMin = new Vector2(14, 10);
            timeRt.offsetMax = new Vector2(-14, 14);
            _timeFill = timeGo.AddComponent<Image>();
            _timeFill.color = new Color(0.45f, 0.7f, 1f, 0.85f);
            _timeFill.raycastTarget = false;
            _timeFill.type = Image.Type.Filled;
            _timeFill.fillMethod = Image.FillMethod.Horizontal;
        }

        // ---------- 상태 표시 ----------
        public void ShowCasting()
        {
            _panel.gameObject.SetActive(false);
            _alert.gameObject.SetActive(false);
            UIManager.Instance?.Toast("찌를 던졌다...");
        }

        public void ShowBite(Transform follow)
        {
            _alertFollow = follow;
            _alert.gameObject.SetActive(true);
            _alert.color = Color.white;
        }

        /// <summary>반응 시간이 줄어드는 동안 느낌표를 깜빡여서 급하다는 걸 보여 준다.</summary>
        public void UpdateBite(float remaining01)
        {
            float blink = Mathf.Lerp(14f, 4f, Mathf.Clamp01(remaining01));
            float a = Mathf.PingPong(Time.time * blink, 1f) * 0.5f + 0.5f;
            _alert.color = new Color(1f, 1f, 1f, a);
        }

        public void ShowMinigame(FishDef fish, float[] zoneCenters, float zoneWidth, int missesAllowed)
        {
            _alert.gameObject.SetActive(false);
            _panel.gameObject.SetActive(true);

            _fishIcon.sprite = fish.GetSprite();
            _fishIcon.enabled = _fishIcon.sprite != null;
            _fishLabel.text = "???";                       // 잡기 전엔 정체를 숨긴다
            _fishLabel.color = Color.white;
            _missesAllowed = missesAllowed;
            _missLabel.text = new string('O', missesAllowed);
            _missLabel.color = new Color(0.55f, 0.9f, 0.55f);

            foreach (var z in _zones) Destroy(z.gameObject);
            _zones.Clear();

            foreach (float center in zoneCenters)
            {
                var go = new GameObject("Zone");
                var rt = go.AddComponent<RectTransform>();
                rt.SetParent(_track, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(center * TrackWidth, 0);
                rt.sizeDelta = new Vector2(zoneWidth * TrackWidth, TrackHeight - 10);
                var img = go.AddComponent<Image>();
                img.sprite = AssetLibrary.UiFishZone;
                img.type = Image.Type.Sliced;
                img.raycastTarget = false;
                _zones.Add(img);
            }

            _marker.SetAsLastSibling();   // 커서가 존 위에 그려지도록
        }

        public void UpdateMinigame(float markerPos, bool[] zoneHit, int misses, float time01)
        {
            _marker.anchoredPosition = new Vector2(markerPos * TrackWidth, 0);
            _timeFill.fillAmount = Mathf.Clamp01(time01);

            for (int i = 0; i < _zones.Count && i < zoneHit.Length; i++)
                _zones[i].color = zoneHit[i] ? new Color(1f, 1f, 1f, 0.25f) : Color.white;

            _missLabel.text = new string('X', misses) + new string('O', Mathf.Max(0, _missesAllowed - misses));
            _missLabel.color = misses > 0 ? new Color(0.95f, 0.45f, 0.4f) : new Color(0.55f, 0.9f, 0.55f);

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                _panelBg.color = Color.Lerp(new Color(0f, 0f, 0f, 0.55f), _flashColor, _flashTimer / 0.18f);
            }
        }

        public void FlashHit(int zoneIndex)
        {
            _flashTimer = 0.18f;
            _flashColor = new Color(0.3f, 0.8f, 0.3f, 0.6f);
            if (zoneIndex >= 0 && zoneIndex < _zones.Count)
                _zones[zoneIndex].color = new Color(1f, 1f, 1f, 0.25f);
        }

        public void FlashMiss()
        {
            _flashTimer = 0.18f;
            _flashColor = new Color(0.85f, 0.25f, 0.25f, 0.6f);
        }

        public void Hide()
        {
            if (_panel != null) _panel.gameObject.SetActive(false);
            if (_alert != null) _alert.gameObject.SetActive(false);
            _alertFollow = null;
        }

        private void LateUpdate()
        {
            if (_alertFollow != null && _alert.gameObject.activeSelf)
                _alert.transform.position = _alertFollow.position + new Vector3(0f, 1.1f, 0f);
        }
    }
}
