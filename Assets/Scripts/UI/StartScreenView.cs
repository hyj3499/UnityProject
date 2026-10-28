using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>Scene-authored references. Runtime controllers never rebuild these panels.</summary>
    public class StartScreenView : MonoBehaviour
    {
        public GameObject mainMenu, creatorPanel, basicPanel, customPanel, settingsPanel, modalPanel, pickerPanel;
        public Button newGame, continueGame, settings, exit, closeCreator, next, back, finish;
        public Button rotateLeft, rotateRight, editName, seasonPrevious, seasonNext, dayPrevious, dayNext;
        public Button settingsBack, modalYes, modalNo, pickerApply, pickerClose;
        public Text creatorTitle, seasonLabel, dayLabel, validation, modalMessage, pickerTitle;
        public InputField nameInput;
        public Toggle male, female;
        public Slider volume;
        public RawImage characterPreview;
        public ScrollRect customScroll, pickerScroll;
        public RectTransform pickerContent;
        public Button slotTemplate;
        public StartScreenPart[] parts;
        public Sprite slotNormal, slotSelected;
        public Texture2D previewBody, previewEyes, previewHeadGuide;
        [Header("Continue / Scene-editable save selection")]
        public GameObject continuePanel;
        public Button closeContinue, previousSave, nextSave, startSave, deleteSave;
        public RawImage savedCharacterPreview;
        public Text savePosition, saveName, saveBirthday, saveDate, saveMoney, savePlayTime;

        public void ShowMain()
        {
            mainMenu.SetActive(true);
            creatorPanel.SetActive(false);
            settingsPanel.SetActive(false);
            modalPanel.SetActive(false);
            pickerPanel.SetActive(false);
            if (continuePanel != null) continuePanel.SetActive(false);
        }
    }

    [Serializable]
    public class StartScreenPart
    {
        public string label;
        public Button select;
        public RawImage portrait;
        public Image swatch;
        public Slider hue, saturation, value;
        public Button[] recommendations;
        public Color[] recommendedColors;
        [Tooltip("눈은 Base/eyes#0, Base/eyes#1. 미제작 파츠는 빈 문자열 하나로 둡니다.")]
        public string[] options;
    }
}
