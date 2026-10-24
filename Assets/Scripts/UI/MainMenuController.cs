using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmMVP
{
    public class MainMenuController : MonoBehaviour
    {
        private const string VolumePrefKey = "farm_mvp_volume";
        private GameBootstrap _bootstrap;
        private StartScreenView _view;
        private CharacterCreatorUI _creator;
        private Action _confirmed;
        private bool _starting;

        public void Boot(GameBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            _view = FindFirstObjectByType<StartScreenView>(FindObjectsInactive.Include);
            if (_view == null)
            {
                var canvas = new GameObject("StartScreenCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                var prefab = Resources.Load<StartScreenView>("UI/StartScreenUI");
                _view = prefab != null ? Instantiate(prefab, canvas.transform, false) : StartScreenLayout.Build(canvas.transform);
            }
            _view.gameObject.SetActive(true);
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            _view.ShowMain();
            _view.continueGame.interactable = SaveSystem.HasSave;
            AudioListener.volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
            _view.volume.SetValueWithoutNotify(AudioListener.volume);
            _creator = gameObject.AddComponent<CharacterCreatorUI>();
            _creator.Bind(_view, this);
            _view.newGame.onClick.AddListener(_creator.Begin);
            _view.continueGame.onClick.AddListener(Continue);
            _view.settings.onClick.AddListener(Settings);
            _view.exit.onClick.AddListener(Quit);
            _view.settingsBack.onClick.AddListener(SettingsBack);
            _view.volume.onValueChanged.AddListener(Volume);
            _view.modalYes.onClick.AddListener(ConfirmYes);
            _view.modalNo.onClick.AddListener(ConfirmNo);
        }
        public void Confirm(string message, Action yes)
        {
            _confirmed = yes;
            _view.modalMessage.text = message;
            _view.modalNo.gameObject.SetActive(true);
            _view.modalYes.GetComponentInChildren<Text>().text = "예";
            _view.modalPanel.SetActive(true);
            _view.modalYes.Select();
        }
        public void Alert(string message)
        {
            Confirm(message, null);
            _view.modalNo.gameObject.SetActive(false);
            _view.modalYes.GetComponentInChildren<Text>().text = "확인";
        }
        private void ConfirmYes()
        {
            _view.modalPanel.SetActive(false);
            var action = _confirmed; _confirmed = null; action?.Invoke();
        }
        private void ConfirmNo() { _view.modalPanel.SetActive(false); _confirmed = null; }
        private void Settings() { _view.mainMenu.SetActive(false); _view.settingsPanel.SetActive(true); }
        private void SettingsBack() { PlayerPrefs.Save(); _view.ShowMain(); }
        private void Volume(float value) { AudioListener.volume = value; PlayerPrefs.SetFloat(VolumePrefKey, value); }
        private void Continue() { if (SaveSystem.HasSave) StartGame(null, null); }
        public void Complete(CharacterCreationDraft draft)
        {
            var error = draft.Profile.Validate();
            if (error != null) { Alert(error); return; }
            draft.Discard();
            if (SaveSystem.HasSave)
                Confirm("기존 저장 데이터 대신 새 게임을 시작하시겠습니까?", () => StartGame(draft.Appearance, draft.Profile));
            else StartGame(draft.Appearance, draft.Profile);
        }
        private void StartGame(PlayerAppearance appearance, CharacterProfile profile)
        {
            if (_starting) return;
            _starting = true;
            _creator.ReleasePreview();
            _view.gameObject.SetActive(false);
            _bootstrap.StartGame(appearance, profile);
            Destroy(gameObject);
        }
        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void Update()
        {
            if (_view == null || !Input.GetKeyDown(KeyCode.Escape)) return;
            if (_view.modalPanel.activeSelf) ConfirmNo();
            else if (_view.pickerPanel.activeSelf) _creator.ClosePicker();
            else if (_view.creatorPanel.activeSelf) _creator.RequestCancel();
            else if (_view.settingsPanel.activeSelf) SettingsBack();
        }
        private void OnDestroy()
        {
            if (_view == null) return;
            _view.newGame.onClick.RemoveListener(_creator.Begin);
            _view.continueGame.onClick.RemoveListener(Continue);
            _view.settings.onClick.RemoveListener(Settings);
            _view.exit.onClick.RemoveListener(Quit);
            _view.settingsBack.onClick.RemoveListener(SettingsBack);
            _view.volume.onValueChanged.RemoveListener(Volume);
            _view.modalYes.onClick.RemoveListener(ConfirmYes);
            _view.modalNo.onClick.RemoveListener(ConfirmNo);
        }
    }
}
