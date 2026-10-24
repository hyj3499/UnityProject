using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>씬에 저장되는 책 UI 참조. 배치/색/폰트는 Hierarchy와 Inspector에서 편집한다.</summary>
    public class BookMenuView : MonoBehaviour
    {
        [Serializable] public class Slot
        {
            public Button button;
            public Image icon;
            public Image selected;
            public Text count;
        }

        public bool configured;
        [HideInInspector] public int inventoryLayoutVersion;
        [Header("Book / animation")]
        public RectTransform book;
        public Image background;
        public Image dim;
        public CanvasGroup content;
        public GameObject bookmarks;
        public Button close;
        public Sprite idle;
        public Sprite[] opening, closing, flipLeft, flipRight;
        [Range(6, 30)] public float framesPerSecond = 12;
        public GameObject[] pages;
        public Button[] tabs;
        [Tooltip("추가로 숨길 HUD 루트. HUD 이름 및 기존 HUD 컴포넌트도 자동 검색합니다.")]
        public GameObject[] hudRoots = Array.Empty<GameObject>();

        [Header("Inventory")]
        public RectTransform portrait;
        public Text playerName, birthday, money, hp, mp;
        public Slot[] slots;
        public RectTransform inventorySlotsRoot;
        public Image inventoryBackground;
        public GameObject[] lockedRows;
        public Text capacity, profileHeader, inventoryHeader, itemHeader, itemName, itemDescription, itemFacts;
        public GameObject itemDetails;
        public RectTransform iconHolder;
        public CanvasGroup detailReveal;
        public Image itemIcon;
        [Min(.1f)] public float itemDropSeconds = .38f;

        [Header("Settings: Gameplay / Display / Audio / Controls / Exit")]
        public Button[] categories;
        public GameObject[] settingPanels;
        public Button previousMode, nextMode, previousResolution, nextResolution, applyDisplay;
        public Text modeLabel, resolutionLabel, scaleLabel, displayMessage;
        public Slider scale;
        public Button saveAndQuit, quit;
        public GameObject confirmation;
        public Text confirmationText;
        public Button confirmYes, confirmNo;
        [NonSerialized] public BookUI controller;

        public void OpenInventory() => controller?.OpenInventory();
        public void OpenSettings() => controller?.OpenSettings();
        public void OpenPage(int index) => controller?.OpenPage(index);
        public void Close() => controller?.Close();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HideScenePreviews()
        {
            // 편집 모드에서는 보이고, 게임의 타이틀에서는 숨긴다. UIManager가 나중에 연결한다.
            foreach (var view in FindObjectsByType<BookMenuView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (view.configured) view.gameObject.SetActive(false);
        }

        public void PreviewPage(int index)
        {
            for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == index);
            for (int i = 0; i < tabs.Length; i++)
                tabs[i].image.color = i == index ? Color.white : new Color(0.82f, 0.76f, 0.63f);
        }

        public void PreviewSettings(int index)
        {
            for (int i = 0; i < settingPanels.Length; i++) settingPanels[i].SetActive(i == index);
            for (int i = 0; i < categories.Length; i++)
                categories[i].image.color = i == index ? Color.white : new Color(0.86f, 0.81f, 0.7f);
        }
    }
}
