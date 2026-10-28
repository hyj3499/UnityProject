using System;
using System.Collections.Generic;
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
        private List<GameData> _saves = new List<GameData>();
        private int _saveIndex;
        private Texture2D _savedPortrait;
        private List<Vector2Int> _resolutions;

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
            SettingsDropdown.Ensure(_view);
            LocalizationBindings.Apply(_view.gameObject);
            ConfigureSettings();
            _view.gameObject.SetActive(true);
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            _view.ShowMain();
            RefreshAvailability();
            AudioListener.volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
            _view.volume.SetValueWithoutNotify(AudioListener.volume);
            _creator = gameObject.AddComponent<CharacterCreatorUI>();
            _creator.Bind(_view, this);
            _view.newGame.onClick.AddListener(NewGame);
            _view.continueGame.onClick.AddListener(Continue);
            _view.closeContinue.onClick.AddListener(CloseContinue);
            _view.previousSave.onClick.AddListener(PreviousSave);
            _view.nextSave.onClick.AddListener(NextSave);
            _view.startSave.onClick.AddListener(StartSelectedSave);
            _view.deleteSave.onClick.AddListener(DeleteSelectedSave);
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
            _view.modalYes.GetComponentInChildren<Text>().text = Localization.Get("ui.mainmenucontroller.yes");
            _view.modalPanel.SetActive(true);
            _view.modalYes.Select();
        }
        public void Alert(string message)
        {
            Confirm(message, null);
            _view.modalNo.gameObject.SetActive(false);
            _view.modalYes.GetComponentInChildren<Text>().text = Localization.Get("ui.mainmenucontroller.ok");
        }
        private void ConfirmYes()
        {
            _view.modalPanel.SetActive(false);
            var action = _confirmed; _confirmed = null; action?.Invoke();
        }
        private void ConfirmNo() { _view.modalPanel.SetActive(false); _confirmed = null; }
        private void ConfigureSettings()
        {
            _resolutions = BookDisplaySettings.Resolutions();
            var current = new Vector2Int(PlayerPrefs.GetInt("farm_display_width", Screen.width), PlayerPrefs.GetInt("farm_display_height", Screen.height));
            if (!_resolutions.Contains(current)) current = new Vector2Int(Screen.width, Screen.height);
            _view.resolution.ClearOptions();
            _view.resolution.AddOptions(_resolutions.ConvertAll(size => size.x + " x " + size.y));
            _view.resolution.SetValueWithoutNotify(Mathf.Max(0, _resolutions.IndexOf(current)));
            _view.language.ClearOptions();
            var names = new List<string>(); foreach (var locale in Localization.Languages) names.Add(locale.nativeName);
            _view.language.AddOptions(names);
            _view.language.SetValueWithoutNotify(Array.FindIndex(Localization.Languages, l => l.code == Localization.Language));
            _view.resolution.onValueChanged.AddListener(ChangeResolution);
            _view.language.onValueChanged.AddListener(ChangeLanguage);
        }
        private void ChangeResolution(int index)
        {
            if (index < 0 || index >= _resolutions.Count) return;
            int mode = Array.IndexOf(BookDisplaySettings.Modes, Screen.fullScreenMode);
            BookDisplaySettings.Apply(_resolutions[index], Mathf.Max(0, mode));
        }
        private void ChangeLanguage(int index)
        {
            if (index < 0 || index >= Localization.Languages.Length) return;
            Localization.SetLanguage(Localization.Languages[index].code);
            if (_view.continuePanel.activeSelf && _saves.Count > 0) ShowSelectedSave();
        }
        private void Settings() { _view.mainMenu.SetActive(false); _view.settingsPanel.SetActive(true); }
        private void SettingsBack() { PlayerPrefs.Save(); _view.ShowMain(); }
        private void Volume(float value) { AudioListener.volume = value; PlayerPrefs.SetFloat(VolumePrefKey, value); }
        private void RefreshAvailability()
        {
            bool available = SaveSystem.HasSave;
            _view.continueGame.interactable = available;
            // SpriteSwap uses the same normal sprite when disabled; fade the entire button.
            var fade = _view.continueGame.GetComponent<CanvasGroup>();
            if (fade == null) fade = _view.continueGame.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = available ? 1f : .35f;
        }
        private void NewGame()
        {
            if (!SaveSystem.CanCreate) { Alert(Localization.Get("ui.mainmenucontroller.you_can_have_up_to_5_saves")); return; }
            _creator.Begin();
        }
        private void Continue()
        {
            _saves = SaveSystem.GetSaves();
            RefreshAvailability();
            if (_saves.Count == 0) { CloseContinue(); return; }
            _saveIndex = 0;
            _view.ShowMain();
            _view.mainMenu.SetActive(false);
            _view.continuePanel.SetActive(true);
            ShowSelectedSave();
        }
        private void CloseContinue()
        {
            ReleaseSavedPortrait();
            _view.ShowMain();
            RefreshAvailability();
        }
        private void PreviousSave() => MoveSave(-1);
        private void NextSave() => MoveSave(1);
        private void MoveSave(int step)
        {
            if (_saves.Count == 0) return;
            _saveIndex = (_saveIndex + step + _saves.Count) % _saves.Count;
            ShowSelectedSave();
        }
        private void ShowSelectedSave()
        {
            var data = _saves[_saveIndex];
            var farmer = data.farmer;
            _view.savePosition.text = Localization.Format("ui.mainmenucontroller.save_0_1_up_to_2", _saveIndex + 1, _saves.Count, SaveSystem.MaxSaves);
            _view.saveName.text = farmer.playerName;
            _view.saveBirthday.text = farmer.birthdayDay > 0
                ? Localization.Format("ui.mainmenucontroller.0_1", Seasons.Name((Season)farmer.birthdaySeason), farmer.birthdayDay) : Localization.Get("ui.mainmenucontroller.not_set");
            _view.saveDate.text = Localization.Format("ui.mainmenucontroller.year_0_1_2", Seasons.YearOf(data.currentDay), Seasons.NameOfDay(data.currentDay), Seasons.DayOfSeason(data.currentDay));
            _view.saveMoney.text = $"{farmer.money:N0} G";
            long minutes = (long)(Math.Max(0, data.playTimeSeconds) / 60);
            _view.savePlayTime.text = Localization.Format("ui.mainmenucontroller.0_h_1_m", minutes / 60, minutes % 60);
            _view.previousSave.interactable = _view.nextSave.interactable = _saves.Count > 1;
            ReleaseSavedPortrait();
            _savedPortrait = StartScreenPortrait.Render(_view, farmer.appearance ?? new PlayerAppearance(), 0, false);
            _view.savedCharacterPreview.texture = _savedPortrait;
        }
        private void StartSelectedSave()
        {
            if (_saves.Count == 0) return;
            // Re-read exactly the selected ID; never silently load another character.
            var data = SaveSystem.Load(_saves[_saveIndex].saveId);
            if (data == null) { Continue(); Alert(Localization.Get("ui.mainmenucontroller.could_not_load_the_selected_save")); return; }
            StartGame(null, null, data);
        }
        private void DeleteSelectedSave()
        {
            if (_saves.Count == 0) return;
            var selected = _saves[_saveIndex];
            Confirm(Localization.Format("ui.mainmenucontroller.delete_the_save_for_0_this_cannot", selected.farmer.playerName), () =>
            {
                if (!SaveSystem.DeleteSave(selected.saveId)) { Alert(Localization.Get("ui.mainmenucontroller.could_not_delete_the_save")); return; }
                _saves = SaveSystem.GetSaves();
                RefreshAvailability();
                if (_saves.Count == 0) { CloseContinue(); return; }
                _saveIndex = Mathf.Min(_saveIndex, _saves.Count - 1);
                ShowSelectedSave();
            });
        }
        private void ReleaseSavedPortrait()
        {
            if (_view != null && _view.savedCharacterPreview != null) _view.savedCharacterPreview.texture = null;
            if (_savedPortrait != null) Destroy(_savedPortrait);
            _savedPortrait = null;
        }
        public void Complete(CharacterCreationDraft draft)
        {
            var error = draft.Profile.Validate();
            if (error != null) { Alert(error); return; }
            if (!SaveSystem.CanCreate) { Alert(Localization.Get("ui.mainmenucontroller.you_can_have_up_to_5_saves_354")); return; }
            draft.Discard();
            StartGame(draft.Appearance, draft.Profile);
        }
        private void StartGame(PlayerAppearance appearance, CharacterProfile profile, GameData savedData = null)
        {
            if (_starting) return;
            _starting = true;
            _creator.ReleasePreview();
            ReleaseSavedPortrait();
            _view.gameObject.SetActive(false);
            _bootstrap.StartGame(appearance, profile, savedData);
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
            else if (_view.continuePanel.activeSelf) CloseContinue();
            else if (_view.pickerPanel.activeSelf) _creator.ClosePicker();
            else if (_view.creatorPanel.activeSelf) _creator.RequestCancel();
            else if (_view.settingsPanel.activeSelf) SettingsBack();
        }
        private void OnDestroy()
        {
            ReleaseSavedPortrait();
            if (_view == null) return;
            _view.resolution.onValueChanged.RemoveListener(ChangeResolution);
            _view.language.onValueChanged.RemoveListener(ChangeLanguage);
            _view.newGame.onClick.RemoveListener(NewGame);
            _view.continueGame.onClick.RemoveListener(Continue);
            _view.closeContinue.onClick.RemoveListener(CloseContinue);
            _view.previousSave.onClick.RemoveListener(PreviousSave);
            _view.nextSave.onClick.RemoveListener(NextSave);
            _view.startSave.onClick.RemoveListener(StartSelectedSave);
            _view.deleteSave.onClick.RemoveListener(DeleteSelectedSave);
            _view.settings.onClick.RemoveListener(Settings);
            _view.exit.onClick.RemoveListener(Quit);
            _view.settingsBack.onClick.RemoveListener(SettingsBack);
            _view.volume.onValueChanged.RemoveListener(Volume);
            _view.modalYes.onClick.RemoveListener(ConfirmYes);
            _view.modalNo.onClick.RemoveListener(ConfirmNo);
        }
    }
}
