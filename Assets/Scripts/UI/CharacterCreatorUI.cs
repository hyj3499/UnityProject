using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FarmMVP
{
    public class CharacterCreatorUI : MonoBehaviour
    {
        private StartScreenView _view;
        private MainMenuController _menu;
        private CharacterCreationDraft _draft;
        private readonly List<System.Action> _unbind = new List<System.Action>();
        private readonly List<Button> _slots = new List<Button>();
        private readonly List<Texture2D> _slotTextures = new List<Texture2D>();
        private Texture2D _standing, _portrait;
        public CharacterCreationDraft Draft => _draft;

        public void Bind(StartScreenView view, MainMenuController menu)
        {
            _view = view; _menu = menu;
            Click(view.closeCreator, RequestCancel);
            Click(view.next, Next); Click(view.back, Back); Click(view.finish, Finish);
            Click(view.rotateLeft, () => Rotate(-1)); Click(view.rotateRight, () => Rotate(1));
            Click(view.editName, EditName);
            Click(view.seasonPrevious, () => { _draft.StepSeason(-1); RefreshBasic(); });
            Click(view.seasonNext, () => { _draft.StepSeason(1); RefreshBasic(); });
            Click(view.dayPrevious, () => { _draft.StepDay(-1); RefreshBasic(); });
            Click(view.dayNext, () => { _draft.StepDay(1); RefreshBasic(); });
            Click(view.pickerClose, ClosePicker);
            Click(view.pickerApply, () => { _draft.Commit(); ClosePicker(); });
            UnityAction<string> name = value => { if (_draft != null) _draft.Profile.playerName = value; };
            UnityAction<string> nameEnd = value => { view.nameInput.readOnly = true; };
            view.nameInput.onValueChanged.AddListener(name); view.nameInput.onEndEdit.AddListener(nameEnd);
            _unbind.Add(() => { view.nameInput.onValueChanged.RemoveListener(name); view.nameInput.onEndEdit.RemoveListener(nameEnd); });
            BindToggle(view.male, "male"); BindToggle(view.female, "female");
            for (int i = 0; i < view.parts.Length; i++)
            {
                int part = i; var row = view.parts[i];
                if (row.select != null) Click(row.select, () => OpenPicker(part));
                BindSlider(row.hue, () => ChangeColor(part)); BindSlider(row.saturation, () => ChangeColor(part)); BindSlider(row.value, () => ChangeColor(part));
                for (int j = 0; j < row.recommendations.Length; j++)
                {
                    int index = j;
                    Click(row.recommendations[j], () => SetColor(part, row.recommendedColors[index]));
                }
            }
        }
        private void Click(Button button, UnityAction action)
        {
            button.onClick.AddListener(action); _unbind.Add(() => button.onClick.RemoveListener(action));
        }
        private void BindSlider(Slider slider, System.Action action)
        {
            UnityAction<float> callback = _ => action();
            slider.onValueChanged.AddListener(callback); _unbind.Add(() => slider.onValueChanged.RemoveListener(callback));
        }
        private void BindToggle(Toggle toggle, string gender)
        {
            UnityAction<bool> callback = value => { if (value && _draft != null) { _draft.Profile.gender = gender; toggle.group.allowSwitchOff = false; _view.validation.text = ""; } };
            toggle.onValueChanged.AddListener(callback); _unbind.Add(() => toggle.onValueChanged.RemoveListener(callback));
        }
        public void Begin()
        {
            _draft = new CharacterCreationDraft();
            _view.mainMenu.SetActive(false); _view.creatorPanel.SetActive(true);
            _view.pickerPanel.SetActive(false); _view.modalPanel.SetActive(false);
            _view.nameInput.SetTextWithoutNotify("Ros"); _view.nameInput.readOnly = true;
            _view.male.group.allowSwitchOff = true;
            _view.male.SetIsOnWithoutNotify(false); _view.female.SetIsOnWithoutNotify(false);
            _view.validation.text = "";
            ShowStep(false); RefreshBasic(); RefreshColors(); RefreshPreview();
        }
        private void EditName()
        {
            _view.nameInput.readOnly = false;
            _view.nameInput.Select(); _view.nameInput.ActivateInputField(); _view.nameInput.MoveTextEnd(false);
        }
        public void RequestCancel() => _menu.Confirm("캐릭터 생성을 취소하시겠습니까?", () => {
            ClosePicker(); ReleasePreview(); _draft = null; _view.ShowMain();
        });
        private void Next()
        {
            _draft.Profile.playerName = _view.nameInput.text.Trim();
            string error = _draft.Profile.Validate(); _view.validation.text = error ?? "";
            if (error != null) { _menu.Alert(error); return; }
            ShowStep(true); _view.customScroll.verticalNormalizedPosition = 1f;
        }
        private void Back() { ClosePicker(); ShowStep(false); }
        private void Finish() { ClosePicker(); _menu.Complete(_draft); }
        private void ShowStep(bool custom)
        {
            _view.basicPanel.SetActive(!custom); _view.customPanel.SetActive(custom);
            _view.creatorTitle.text = custom ? "캐릭터 커스텀 설정" : "캐릭터 기본 설정";
            _view.next.gameObject.SetActive(!custom); _view.back.gameObject.SetActive(custom); _view.finish.gameObject.SetActive(custom);
        }
        private void RefreshBasic()
        {
            _view.seasonLabel.text = new[] { "봄", "여름", "가을", "겨울" }[_draft.Profile.birthdaySeason];
            _view.dayLabel.text = _draft.Profile.birthdayDay + "일";
        }
        private void Rotate(int delta) { _draft.Rotate(delta); RefreshPreview(); }
        private void RefreshPreview()
        {
            FarmerAppearancePalette.Release(_standing); FarmerAppearancePalette.Release(_portrait);
            _standing = StartScreenPortrait.Render(_view, _draft.Preview(), _draft.Facing, false);
            _portrait = StartScreenPortrait.Render(_view, _draft.Appearance, 0, true);
            _view.characterPreview.texture = _standing; _view.parts[1].portrait.texture = _portrait;
        }
        private void RefreshColors()
        {
            for (int i = 0; i < _view.parts.Length; i++)
            {
                var row = _view.parts[i];
                var color = FarmerAppearancePalette.DisplayColor(CharacterCreationDraft.ColorFor(_draft.Appearance, i), i == 0);
                Color.RGBToHSV(color, out float h, out float s, out float v);
                row.hue.SetValueWithoutNotify(h); row.saturation.SetValueWithoutNotify(s); row.value.SetValueWithoutNotify(v); row.swatch.color = color;
            }
        }
        private void ChangeColor(int part)
        {
            if (_draft == null) return;
            var row = _view.parts[part];
            SetColor(part, Color.HSVToRGB(row.hue.value, row.saturation.value, row.value.value), false);
        }
        private void SetColor(int part, Color color, bool refreshControls = true)
        {
            if (_draft.ActivePart != part) ClosePicker();
            var target = CharacterCreationDraft.ColorFor(_draft.Appearance, part);
            target.custom = true; target.rgb = color; _view.parts[part].swatch.color = color;
            if (refreshControls) RefreshColors();
            RefreshPreview(); if (_view.pickerPanel.activeSelf) RefreshSlotPortraits();
        }
        public void OpenPicker(int part)
        {
            ClearSlots(); _draft.Open(part); var row = _view.parts[part];
            _view.pickerTitle.text = row.label; _view.pickerPanel.SetActive(true);
            var options = row.options;
            for (int i = 0; i < options.Length; i++)
            {
                string option = options[i];
                var slot = Instantiate(_view.slotTemplate, _view.pickerContent, false);
                slot.name = string.IsNullOrEmpty(option) ? "EmptySlot" : "Slot_" + i;
                slot.gameObject.SetActive(true);
                slot.GetComponentInChildren<Text>(true).text = string.IsNullOrEmpty(option) ? "없음" : "";
                slot.onClick.AddListener(() => { _draft.Select(option); RefreshSlotSelection(); RefreshPreview(); });
                _slots.Add(slot);
            }
            RefreshSlotPortraits(); RefreshSlotSelection(); RefreshPreview();
            Canvas.ForceUpdateCanvases(); _view.pickerScroll.verticalNormalizedPosition = 1f;
        }
        private void RefreshSlotPortraits()
        {
            foreach (var texture in _slotTextures) FarmerAppearancePalette.Release(texture);
            _slotTextures.Clear();
            for (int i = 0; i < _slots.Count; i++)
            {
                var image = _slots[i].GetComponentInChildren<RawImage>(true);
                image.texture = null; image.enabled = _draft.ActivePart == 1;
                if (!image.enabled) continue;
                var appearance = _draft.Appearance.Clone(); appearance.eyes = _view.parts[1].options[i];
                var texture = StartScreenPortrait.Render(_view, appearance, 0, true);
                image.texture = texture; _slotTextures.Add(texture);
            }
        }
        private void RefreshSlotSelection()
        {
            var options = _view.parts[_draft.ActivePart].options;
            for (int i = 0; i < _slots.Count; i++) _slots[i].image.sprite = options[i] == _draft.Pending ? _view.slotSelected : _view.slotNormal;
        }
        public void ClosePicker()
        {
            _draft?.Discard(); _view.pickerPanel.SetActive(false); ClearSlots();
            if (_draft != null) RefreshPreview();
        }
        private void ClearSlots()
        {
            foreach (var slot in _slots) { slot.gameObject.SetActive(false); FarmerAppearancePalette.Release(slot.gameObject); }
            foreach (var texture in _slotTextures) FarmerAppearancePalette.Release(texture);
            _slots.Clear(); _slotTextures.Clear();
        }
        public void ReleasePreview()
        {
            ClearSlots(); FarmerAppearancePalette.Release(_standing); FarmerAppearancePalette.Release(_portrait); _standing = _portrait = null;
            if (_view != null) { _view.characterPreview.texture = null; _view.parts[1].portrait.texture = null; }
        }
        private void OnDestroy()
        {
            foreach (var unbind in _unbind) unbind(); _unbind.Clear(); ReleasePreview();
        }
    }
}
