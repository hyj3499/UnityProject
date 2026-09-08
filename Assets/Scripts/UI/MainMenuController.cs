using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FarmMVP
{
    /// <summary>
    /// Title screen shown before gameplay starts: 새 게임 / 이어하기 / 설정 / 종료.
    /// Built entirely from code (same approach as UIManager) so no scene/prefab setup is needed.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private const string VolumePrefKey = "farm_mvp_volume";

        private GameBootstrap _bootstrap;
        private Canvas _canvas;
        private Font _font;

        private GameObject _mainPanel;
        private GameObject _settingsPanel;
        private GameObject _confirmPanel;

        private Text _confirmText;
        private Action _confirmYesAction;

        private Button _continueButton;
        private Slider _volumeSlider;

        public void Boot(GameBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            _font = Resources.Load<Font>("Fonts/PF스타더스트 3.0");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            AudioListener.volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);

            BuildCanvas();
            BuildMainPanel();
            BuildSettingsPanel();
            BuildConfirmPanel();
        }

        // ---------- canvas / background ----------
        private void BuildCanvas()
        {
            var go = new GameObject("TitleCanvas");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            go.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var bgGo = new GameObject("Background");
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.SetParent(_canvas.transform, false);
            Stretch(bgRt);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.10f, 0.14f, 0.12f, 1f);
        }

        // ---------- main panel ----------
        private void BuildMainPanel()
        {
            _mainPanel = new GameObject("MainPanel");
            var rt = _mainPanel.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(280, 340);

            var title = Label(rt, "농장 이야기", 36, new Vector2(0, 130), TextAnchor.MiddleCenter);
            title.rectTransform.sizeDelta = new Vector2(280, 60);

            MakeMenuButton(rt, "새 게임", new Vector2(0, 40), OnNewGameClicked);
            _continueButton = MakeMenuButton(rt, "이어하기", new Vector2(0, -20), OnContinueClicked);
            _continueButton.interactable = SaveSystem.HasSave;
            MakeMenuButton(rt, "설정", new Vector2(0, -80), OnSettingsClicked);
            MakeMenuButton(rt, "종료", new Vector2(0, -140), OnQuitClicked);
        }

        private void OnNewGameClicked()
        {
            if (SaveSystem.HasSave)
            {
                ShowConfirm("기존 저장 데이터를 삭제하고 새로 시작하시겠습니까?", () =>
                {
                    SaveSystem.DeleteSave();
                    StartGame();
                });
            }
            else
            {
                StartGame();
            }
        }

        private void OnContinueClicked() => StartGame();

        private void OnSettingsClicked()
        {
            _mainPanel.SetActive(false);
            _volumeSlider.value = AudioListener.volume;
            _settingsPanel.SetActive(true);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void StartGame()
        {
            Destroy(_canvas.gameObject);
            _bootstrap.StartGame();
            Destroy(gameObject);
        }

        // ---------- settings panel ----------
        private void BuildSettingsPanel()
        {
            _settingsPanel = new GameObject("SettingsPanel");
            var rt = _settingsPanel.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320, 220);

            var bg = _settingsPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.10f, 0.96f);

            var title = Label(rt, "설정", 26, new Vector2(0, 80), TextAnchor.MiddleCenter);
            title.rectTransform.sizeDelta = new Vector2(320, 40);

            var volLabel = Label(rt, "음량", 18, new Vector2(-110, 10), TextAnchor.MiddleLeft);
            volLabel.rectTransform.sizeDelta = new Vector2(60, 30);

            _volumeSlider = MakeSlider(rt, new Vector2(20, 10));
            _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            MakeMenuButton(rt, "뒤로", new Vector2(0, -70), OnSettingsBackClicked, new Vector2(140, 44));

            _settingsPanel.SetActive(false);
        }

        private void OnVolumeChanged(float v)
        {
            AudioListener.volume = v;
            PlayerPrefs.SetFloat(VolumePrefKey, v);
        }

        private void OnSettingsBackClicked()
        {
            PlayerPrefs.Save();
            _settingsPanel.SetActive(false);
            _mainPanel.SetActive(true);
        }

        private Slider MakeSlider(RectTransform parent, Vector2 pos)
        {
            var container = new GameObject("VolumeSlider");
            var rt = container.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(160, 20);

            var bg = new GameObject("BG").AddComponent<Image>();
            bg.transform.SetParent(rt, false);
            bg.color = new Color(0.2f, 0.2f, 0.24f, 0.9f);
            Stretch(bg.rectTransform);

            var fillArea = new GameObject("Fill").AddComponent<Image>();
            fillArea.transform.SetParent(rt, false);
            fillArea.color = new Color(0.4f, 0.7f, 0.4f, 1f);
            Stretch(fillArea.rectTransform);

            var handleGo = new GameObject("Handle");
            var handleRt = handleGo.AddComponent<RectTransform>();
            handleRt.SetParent(rt, false);
            handleRt.sizeDelta = new Vector2(12, 26);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;

            var slider = container.AddComponent<Slider>();
            slider.fillRect = fillArea.rectTransform;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        // ---------- confirm panel (used for the "new game overwrites save" warning) ----------
        private void BuildConfirmPanel()
        {
            _confirmPanel = new GameObject("ConfirmPanel");
            var rt = _confirmPanel.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360, 160);

            var bg = _confirmPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.10f, 0.96f);

            _confirmText = Label(rt, "", 20, Vector2.zero, TextAnchor.MiddleCenter);
            _confirmText.rectTransform.anchorMin = new Vector2(0, 0);
            _confirmText.rectTransform.anchorMax = new Vector2(1, 1);
            _confirmText.rectTransform.offsetMin = new Vector2(16, 56);
            _confirmText.rectTransform.offsetMax = new Vector2(-16, -16);
            _confirmText.rectTransform.anchoredPosition = Vector2.zero;

            MakeMenuButton(rt, "예", new Vector2(-90, 20), () => OnConfirmClicked(true), new Vector2(120, 40));
            MakeMenuButton(rt, "아니오", new Vector2(90, 20), () => OnConfirmClicked(false), new Vector2(120, 40));

            _confirmPanel.SetActive(false);
        }

        private void ShowConfirm(string message, Action onYes)
        {
            _confirmText.text = message;
            _confirmYesAction = onYes;
            _mainPanel.SetActive(false);
            _confirmPanel.SetActive(true);
        }

        private void OnConfirmClicked(bool yes)
        {
            _confirmPanel.SetActive(false);
            var action = _confirmYesAction;
            _confirmYesAction = null;
            if (yes) action?.Invoke();
            else _mainPanel.SetActive(true);
        }

        // ---------- small UI builders ----------
        private Button MakeMenuButton(RectTransform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction onClick, Vector2? size = null)
        {
            var go = new GameObject("Button_" + text);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size ?? new Vector2(200, 44);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.24f, 0.22f, 0.95f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var label = Label(rt, text, 20, Vector2.zero, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.raycastTarget = false;

            return btn;
        }

        private Text Label(RectTransform parent, string text, int size, Vector2 pos, TextAnchor anchor)
        {
            var go = new GameObject("Label");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(200, 30);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private void Stretch(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
