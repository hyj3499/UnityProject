using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FarmMVP
{
    /// <summary>
    /// Builds and drives all runtime UI from code: HUD (HP/MP, day/time),
    /// quick hotbar, equipped-tool indicator, full inventory with drag & drop,
    /// and transient banners/toasts (design doc §4/§12).
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        /// <summary>낚시 바처럼 다른 스크립트가 만드는 UI가 붙을 캔버스.</summary>
        public Transform CanvasRoot => _canvas != null ? _canvas.transform : null;

        private GameManager _game;
        private Canvas _canvas;
        private Font _font;

        public Font UiFont => _font;

        // HUD
        private Slider _hpBar, _mpBar;
        private Text _dateText, _timeText, _moneyText;
        private Image _dayNightIcon;

        // Hotbar
        private readonly List<SlotView> _hotbarViews = new List<SlotView>();
        private RectTransform _hotbarRoot;
        private Text _toolLabel;
        private Text _hotbarPageLabel;

        // 책(인벤토리/설정/도움말)
        private BookUI _book;
        private readonly List<SlotView> _invViews = new List<SlotView>();

        // 배송함
        private ShippingUI _shipping;
        private readonly List<SlotView> _shippingViews = new List<SlotView>();

        // NPC 대화
        private DialogueUI _dialogue;

        // 상점
        private ShopUI _shop;

        // Banner / toast
        private Text _bannerText;
        private Text _toastText;

        // Yes/No confirm dialog
        private GameObject _confirmPanel;
        private Text _confirmText;
        private Action _confirmYesAction;
        private Action _confirmNoAction;

        public void Boot(GameManager game)
        {
            Instance = this;
            _game = game;
            _font = BuildFont();

            AssetLibrary.EnsureLoaded();

            BuildCanvas();
            BuildHUD();
            BuildHotbar();
            BuildBook();
            BuildShipping();
            BuildShop();
            BuildDialogue();
            BuildBannerAndToast();
            BuildConfirmDialog();

            _game.OnTimeChanged += RefreshTime;
            _game.OnDayChanged += RefreshTime;
            _game.OnHotbarChanged += RefreshSlots;
            _game.OnMagicChanged += RefreshMagic;
            _game.OnMoneyChanged += RefreshMoney;
            _game.OnShippingChanged += OnShippingInventoryChanged;
            _game.Inventory.OnChanged += RefreshSlots;

            RefreshTime();
            RefreshMoney();
            RefreshSlots();
            RefreshMagic();
        }

        private void OnDestroy()
        {
            // 타이틀로 돌아갈 때 UIManager와 함께 캔버스도 정리한다 (캔버스는 별도 오브젝트로 만들어짐).
            if (Instance == this) Instance = null;
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private Font BuildFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        private void BuildCanvas()
        {
            var go = new GameObject("UICanvas");
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
        }

        // ---------- HUD ----------
        private void BuildHUD()
        {
            // top-left status
            var panel = Panel("StatusPanel", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(12, -12), new Vector2(200, 70), new Color(0, 0, 0, 0.45f));

            _hpBar = MakeBar(panel, "HP", 0, new Color(0.85f, 0.2f, 0.2f));
            _mpBar = MakeBar(panel, "MP", -26, new Color(0.25f, 0.5f, 0.95f));

            BuildTopRightHud();
        }

        /// <summary>
        /// 우측 상단: 날짜/시간 패널(왼쪽 칸에 낮=해, 밤=달 아이콘)과 그 아래 소지금 바.
        /// 자식 위젯은 원본 아트의 픽셀 좌표를 앵커로 환산해 배치하므로 패널 크기를 바꿔도 어긋나지 않는다.
        /// </summary>
        private void BuildTopRightHud()
        {
            const float hudScale = 3f;

            // --- 날짜 / 시간 패널 (아트 59x28) ---
            var infoSize = new Vector2(59, 28);
            var info = SpritePanel("HudInfo", AssetLibrary.UiHudInfo, new Vector2(1, 1),
                new Vector2(-12, -12), infoSize * hudScale);

            var iconRt = Child(info, "DayNightIcon");
            PlaceInArt(iconRt, infoSize, new Rect(3, 4, 19, 20));
            _dayNightIcon = iconRt.gameObject.AddComponent<Image>();
            _dayNightIcon.preserveAspect = true;
            _dayNightIcon.raycastTarget = false;
            _dayNightIcon.sprite = AssetLibrary.UiIconSun;

            _dateText = Label(info, "1일차", 17, Vector2.zero, TextAnchor.MiddleCenter);
            PlaceInArt(_dateText.rectTransform, infoSize, new Rect(25, 5, 31, 9));
            _dateText.color = new Color(0.24f, 0.13f, 0.11f);

            _timeText = Label(info, "06:00", 17, Vector2.zero, TextAnchor.MiddleCenter);
            PlaceInArt(_timeText.rectTransform, infoSize, new Rect(25, 15, 31, 9));
            _timeText.color = new Color(0.24f, 0.13f, 0.11f);

            // --- 소지금 바 (아트 60x16) ---
            var moneySize = new Vector2(60, 16);
            var money = SpritePanel("HudMoney", AssetLibrary.UiHudMoney, new Vector2(1, 1),
                new Vector2(-12, -12 - infoSize.y * hudScale - 6), moneySize * hudScale);

            _moneyText = Label(money, "0", 16, Vector2.zero, TextAnchor.MiddleRight);
            PlaceInArt(_moneyText.rectTransform, moneySize, new Rect(19, 3, 35, 10));
            _moneyText.color = new Color(0.24f, 0.13f, 0.11f);
        }

        /// <summary>스프라이트를 배경으로 쓰는 HUD 패널 (화면 모서리 기준 배치).</summary>
        private RectTransform SpritePanel(string name, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>부모 스프라이트의 원본 픽셀 좌표(artRect)에 맞춰 자식을 앵커로 배치한다.</summary>
        private static void PlaceInArt(RectTransform child, Vector2 artSize, Rect artRect)
        {
            child.anchorMin = new Vector2(artRect.xMin / artSize.x, 1f - artRect.yMax / artSize.y);
            child.anchorMax = new Vector2(artRect.xMax / artSize.x, 1f - artRect.yMin / artSize.y);
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
        }

        private RectTransform Child(RectTransform parent, string name)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private Slider MakeBar(RectTransform parent, string label, float yOff, Color fill)
        {
            var container = new GameObject(label + "Bar");
            var rt = container.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(8, yOff - 8);
            rt.sizeDelta = new Vector2(-16, 20);

            var lbl = Label(rt, label, 12, new Vector2(0, 0), TextAnchor.MiddleLeft);
            lbl.rectTransform.anchorMin = new Vector2(0, 0);
            lbl.rectTransform.anchorMax = new Vector2(0, 1);
            lbl.rectTransform.sizeDelta = new Vector2(28, 0);
            lbl.rectTransform.anchoredPosition = new Vector2(0, 0);

            var slider = container.AddComponent<Slider>();
            var bg = new GameObject("BG").AddComponent<Image>();
            bg.transform.SetParent(rt, false);
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            bg.raycastTarget = false; // 상호작용 불가능한 HP/MP 바
            Stretch(bg.rectTransform, 30, 0, 0, 0);

            var fillArea = new GameObject("Fill").AddComponent<Image>();
            fillArea.transform.SetParent(rt, false);
            fillArea.color = fill;
            fillArea.raycastTarget = false;
            Stretch(fillArea.rectTransform, 30, 0, 0, 0);

            slider.fillRect = fillArea.rectTransform;
            slider.targetGraphic = bg;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            slider.interactable = false;
            return slider;
        }

        // ---------- Hotbar ----------
        private void BuildHotbar()
        {
            int n = Inventory.HotbarSize;
            float slot = 48, pad = 4;
            float totalW = n * slot + (n - 1) * pad;

            var root = new GameObject("Hotbar");
            _hotbarRoot = root.AddComponent<RectTransform>();
            _hotbarRoot.SetParent(_canvas.transform, false);
            _hotbarRoot.anchorMin = new Vector2(0.5f, 0);
            _hotbarRoot.anchorMax = new Vector2(0.5f, 0);
            _hotbarRoot.pivot = new Vector2(0.5f, 0);
            _hotbarRoot.anchoredPosition = new Vector2(0, 12);
            _hotbarRoot.sizeDelta = new Vector2(totalW, slot);

            for (int i = 0; i < n; i++)
            {
                float x = i * (slot + pad);
                var sv = BuildSlot(_hotbarRoot, i, x, 0, slot, isHotbar: true);
                _hotbarViews.Add(sv);

                // 어떤 숫자키에 대응하는 칸인지 (10번째는 0)
                var key = Label(sv.frame.rectTransform, i == 9 ? "0" : (i + 1).ToString(), 12, Vector2.zero, TextAnchor.UpperLeft);
                var krt = key.rectTransform;
                krt.anchorMin = krt.anchorMax = new Vector2(0, 1);
                krt.pivot = new Vector2(0, 1);
                krt.anchoredPosition = new Vector2(3, -2);
                krt.sizeDelta = new Vector2(16, 16);
                key.color = new Color(1f, 0.93f, 0.75f, 0.9f);
            }

            _hotbarPageLabel = Label(_hotbarRoot, "", 14, Vector2.zero, TextAnchor.MiddleCenter);
            _hotbarPageLabel.rectTransform.anchorMin = new Vector2(0, 1);
            _hotbarPageLabel.rectTransform.anchorMax = new Vector2(1, 1);
            _hotbarPageLabel.rectTransform.pivot = new Vector2(0.5f, 0);
            _hotbarPageLabel.rectTransform.anchoredPosition = new Vector2(0, 4);
            _hotbarPageLabel.rectTransform.sizeDelta = new Vector2(0, 20);
            _hotbarPageLabel.color = new Color(1f, 0.95f, 0.8f, 0.9f);

            // equipped tool indicator (bottom-right)
            var toolPanel = Panel("ToolIndicator", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-12, 12), new Vector2(120, 52), new Color(0, 0, 0, 0.45f));
            _toolLabel = Label(toolPanel, "-", 14, Vector2.zero, TextAnchor.MiddleCenter);
            Stretch(_toolLabel.rectTransform, 0, 0, 0, 0);
        }

        // ---------- 책 UI ----------
        private void BuildBook()
        {
            var go = new GameObject("BookUI");
            go.transform.SetParent(_canvas.transform, false);
            _book = go.AddComponent<BookUI>();
            _book.Boot(this, _game, _canvas.transform as RectTransform);
        }

        private void BuildShipping()
        {
            var go = new GameObject("ShippingUI");
            go.transform.SetParent(_canvas.transform, false);
            _shipping = go.AddComponent<ShippingUI>();
            _shipping.Boot(this, _game, _canvas.transform as RectTransform);
        }

        private void BuildShop()
        {
            var go = new GameObject("ShopUI");
            go.transform.SetParent(_canvas.transform, false);
            _shop = go.AddComponent<ShopUI>();
            _shop.Boot(this, _game, _canvas.transform as RectTransform);
        }

        public bool IsShopOpen => _shop != null && _shop.IsOpen;

        public void OpenShop()
        {
            if (_shop == null || IsBookOpen) return;
            _shop.Open();
            SyncPaused();
        }

        public void CloseShop()
        {
            if (_shop == null) return;
            _shop.Close();
            SyncPaused();
        }

        private void BuildDialogue()
        {
            var go = new GameObject("DialogueUI");
            go.transform.SetParent(_canvas.transform, false);
            _dialogue = go.AddComponent<DialogueUI>();
            _dialogue.Boot(this, _canvas.transform as RectTransform);
        }

        public bool IsBookOpen => _book != null && _book.IsOpen;
        public bool IsShippingOpen => _shipping != null && _shipping.IsOpen;
        public bool IsDialogueOpen => _dialogue != null && _dialogue.IsOpen;

        /// <summary>NPC 대사를 띄운다. 선택지가 있으면 마지막 줄 뒤에 버튼이 나온다.</summary>
        public void ShowDialogue(NpcDefinition def, int emotion, string[] lines,
            DialogueChoice[] choices, Action<DialogueChoice> onChoice)
        {
            if (_dialogue == null) return;
            _dialogue.Show(def.id, def.displayName, emotion, lines, choices, onChoice, SyncPaused);
            SyncPaused();
        }

        /// <summary>선택지를 고른 뒤 이어지는 반응 대사를 같은 창에 이어서 보여준다.</summary>
        public void ContinueDialogue(NpcDefinition def, int emotion, string[] lines)
        {
            if (_dialogue == null) return;
            _dialogue.Show(def.id, def.displayName, emotion, lines, null, null, SyncPaused);
            SyncPaused();
        }

        /// <summary>배송함을 우클릭했을 때 호출된다.</summary>
        public void OpenShippingBox()
        {
            if (_shipping == null || IsBookOpen) return;
            _shipping.Open();
            _game.CurrentLocation?.SetShippingBoxOpen(true);
            RefreshSlots();
            RefreshShippingValue();
            SyncPaused();
        }

        public void CloseShippingBox()
        {
            if (_shipping == null) return;
            _shipping.Close();
            _game.CurrentLocation?.SetShippingBoxOpen(false);
            SyncPaused();
        }

        private void RefreshShippingValue()
        {
            _shipping?.SetExpectedValue(_game.ShippingBoxValue);
        }

        /// <summary>ShippingUI가 배송함 칸을 만들 때 호출한다.</summary>
        public SlotView CreateShippingSlot(RectTransform parent, int index, float x, float y, float size)
        {
            var sv = BuildSlot(parent, index, x, y, size, isHotbar: false, centered: true, container: SlotContainer.Shipping);
            _shippingViews.Add(sv);
            return sv;
        }

        /// <summary>ShippingUI가 아래쪽 "내 가방" 칸을 만들 때 호출한다.</summary>
        public SlotView CreatePlayerSlot(RectTransform parent, int index, float x, float y, float size)
            => CreateInventorySlot(parent, index, x, y, size);

        /// <summary>I / ESC: 해당 페이지로 책을 연다. 이미 그 페이지가 열려 있으면 닫는다.</summary>
        public void ToggleBook(BookUI.Page page)
        {
            if (_book == null) return;
            if (_confirmPanel != null && _confirmPanel.activeSelf) return; // 팝업이 떠 있으면 무시
            _book.Toggle(page);
            RefreshSlots();
            SyncPaused();
        }

        /// <summary>책이나 팝업이 하나라도 떠 있으면 게임 입력을 멈춘다.</summary>
        public void SyncPaused()
        {
            if (_game == null) return;
            bool confirmOpen = _confirmPanel != null && _confirmPanel.activeSelf;
            _game.Paused = confirmOpen || IsBookOpen || IsShippingOpen || IsDialogueOpen || IsShopOpen;
        }

        /// <summary>BookUI가 인벤토리 페이지의 칸을 만들 때 호출한다 (드래그&amp;드롭 및 갱신 대상에 등록).</summary>
        public SlotView CreateInventorySlot(RectTransform parent, int index, float x, float y, float size)
        {
            var sv = BuildSlot(parent, index, x, y, size, isHotbar: false, centered: true);
            _invViews.Add(sv);
            return sv;
        }

        private void BuildBannerAndToast()
        {
            var banner = new GameObject("Banner");
            var brt = banner.AddComponent<RectTransform>();
            brt.SetParent(_canvas.transform, false);
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(600, 120);
            var bimg = banner.AddComponent<Image>();
            bimg.color = new Color(0, 0, 0, 0.0f);
            bimg.raycastTarget = false; // 화면 중앙(플레이어 위치)을 항상 덮는 장식용 배너라 클릭을 가로채면 안 됨
            _bannerText = Label(brt, "", 48, Vector2.zero, TextAnchor.MiddleCenter);
            Stretch(_bannerText.rectTransform, 0, 0, 0, 0);
            _bannerText.color = new Color(1, 1, 1, 0);

            var toast = new GameObject("Toast");
            var trt = toast.AddComponent<RectTransform>();
            trt.SetParent(_canvas.transform, false);
            trt.anchorMin = new Vector2(0.5f, 0);
            trt.anchorMax = new Vector2(0.5f, 0);
            trt.pivot = new Vector2(0.5f, 0);
            trt.anchoredPosition = new Vector2(0, 80);
            trt.sizeDelta = new Vector2(300, 40);
            _toastText = Label(trt, "", 20, Vector2.zero, TextAnchor.MiddleCenter);
            Stretch(_toastText.rectTransform, 0, 0, 0, 0);
            _toastText.color = new Color(1, 1, 1, 0);
        }

        // ---------- confirm dialog ----------
        private void BuildConfirmDialog()
        {
            _confirmPanel = new GameObject("ConfirmDialog");
            var rt = _confirmPanel.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360, 160);

            var bg = _confirmPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.10f, 0.96f);

            _confirmText = Label(rt, "", 22, Vector2.zero, TextAnchor.MiddleCenter);
            _confirmText.rectTransform.anchorMin = new Vector2(0, 0);
            _confirmText.rectTransform.anchorMax = new Vector2(1, 1);
            _confirmText.rectTransform.offsetMin = new Vector2(16, 56);
            _confirmText.rectTransform.offsetMax = new Vector2(-16, -16);
            _confirmText.rectTransform.anchoredPosition = Vector2.zero;

            MakeButton(rt, "예", new Vector2(-90, 20), () => OnConfirmClicked(true));
            MakeButton(rt, "아니오", new Vector2(90, 20), () => OnConfirmClicked(false));

            _confirmPanel.SetActive(false);
        }

        private Button MakeButton(RectTransform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + text);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(120, 40);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.3f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var label = Label(rt, text, 18, Vector2.zero, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 0, 0, 0, 0);
            label.raycastTarget = false;

            return btn;
        }

        /// <summary>범용 예/아니오 확인 팝업. Yes/No 클릭 시 해당 콜백을 호출한다.</summary>
        public void ShowYesNo(string message, Action onYes, Action onNo = null)
        {
            _confirmText.text = message;
            _confirmYesAction = onYes;
            _confirmNoAction = onNo;
            _confirmPanel.SetActive(true);
            _confirmPanel.transform.SetAsLastSibling(); // 책 위에 표시
            SyncPaused();
        }

        private void OnConfirmClicked(bool yes)
        {
            _confirmPanel.SetActive(false);
            SyncPaused();

            var action = yes ? _confirmYesAction : _confirmNoAction;
            _confirmYesAction = null;
            _confirmNoAction = null;
            action?.Invoke();
        }

        // ---------- slot construction ----------
        private SlotView BuildSlot(RectTransform parent, int index, float x, float y, float size,
            bool isHotbar, bool centered = false, SlotContainer container = SlotContainer.Player)
        {
            var go = new GameObject($"Slot_{container}_{index}");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            if (centered)
            {
                rt.anchorMin = new Vector2(0.5f, 1);
                rt.anchorMax = new Vector2(0.5f, 1);
                rt.pivot = new Vector2(0.5f, 1);
            }
            else
            {
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
            }
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(size, size);

            var frame = go.AddComponent<Image>();
            frame.sprite = AssetLibrary.UiSlot;

            var iconGo = new GameObject("Icon");
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.SetParent(rt, false);
            Stretch(iconRt, 6, 6, 6, 6);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;

            var countGo = new GameObject("Count");
            var countRt = countGo.AddComponent<RectTransform>();
            countRt.SetParent(rt, false);
            countRt.anchorMin = new Vector2(1, 0);
            countRt.anchorMax = new Vector2(1, 0);
            countRt.pivot = new Vector2(1, 0);
            countRt.anchoredPosition = new Vector2(-3, 2);
            countRt.sizeDelta = new Vector2(30, 18);
            var count = countGo.AddComponent<Text>();
            count.font = _font;
            count.fontSize = 14;
            count.alignment = TextAnchor.LowerRight;
            count.color = Color.white;
            count.raycastTarget = false;

            var sel = go.AddComponent<Outline>();
            sel.effectColor = new Color(1f, 0.9f, 0.3f, 1f);
            sel.effectDistance = new Vector2(2, 2);
            sel.enabled = false;

            var view = new SlotView
            {
                index = index,
                isHotbar = isHotbar,
                container = container,
                frame = frame,
                icon = icon,
                count = count,
                selection = sel
            };

            var handler = go.AddComponent<SlotDragHandler>();
            handler.Init(this, view);

            return view;
        }

        // ---------- refresh ----------
        private void RefreshTime()
        {
            int day = _game.Data.currentDay;
            _dateText.text = $"{Seasons.Name(Seasons.Of(day))} {Seasons.DayOfSeason(day)}일";
            _timeText.text = _game.TimeString();
            _dayNightIcon.sprite = _game.IsDaytime ? AssetLibrary.UiIconSun : AssetLibrary.UiIconMoon;

            _hpBar.value = Mathf.Clamp01((float)_game.Data.farmer.hp / _game.Data.farmer.maxHp);
            _mpBar.value = Mathf.Clamp01((float)_game.Data.farmer.mp / _game.Data.farmer.maxMp);

            _book?.RefreshStatus();
        }

        private void OnShippingInventoryChanged()
        {
            RefreshSlots();
            RefreshShippingValue();
        }

        private void RefreshMoney()
        {
            _moneyText.text = _game.Money.ToString("N0");
            _book?.RefreshStatus();
        }

        public void RefreshSlots()
        {
            // 같은 인벤토리를 여러 화면(퀵바 / 책 / 배송함)에서 동시에 보여주므로,
            // 리스트 순번이 아니라 각 칸이 가리키는 실제 슬롯 번호를 기준으로 갱신한다.
            int equipped = _game.Data.farmer.equippedHotbarIndex;
            int unlocked = _game.UnlockedSlots;
            int page = _game.HotbarPage;

            // 퀵바는 지금 보고 있는 배낭 페이지의 10칸을 비춘다.
            for (int i = 0; i < _hotbarViews.Count; i++)
            {
                var v = _hotbarViews[i];
                v.index = page * Inventory.HotbarSize + i;
                UpdateSlotView(v, _game.Inventory.GetSlot(v.index), v.index == equipped, v.index >= unlocked);
            }

            foreach (var v in _invViews)
                UpdateSlotView(v, _game.Inventory.GetSlot(v.index), v.index == equipped, v.index >= unlocked);

            foreach (var v in _shippingViews)
                UpdateSlotView(v, _game.ShippingBox.GetSlot(v.index), false);

            RefreshHotbarPageLabel();
        }

        private void RefreshHotbarPageLabel()
        {
            if (_hotbarPageLabel == null) return;
            bool multiplePages = _game.HotbarPageCount > 1;
            _hotbarPageLabel.enabled = multiplePages;
            if (multiplePages)
                _hotbarPageLabel.text = $"가방 {_game.HotbarPage + 1}/{_game.HotbarPageCount}  (TAB)";
        }

        /// <summary>우측 하단에 현재 선택된 마법 이름과 MP 소모량을 표시.</summary>
        private void RefreshMagic()
        {
            var def = MagicSystem.Get(_game.CurrentMagic);
            _toolLabel.text = $"{def.displayName}\nMP {def.mpCost}";
        }

        private void UpdateSlotView(SlotView v, ItemStack stack, bool selected, bool locked = false)
        {
            if (locked)
            {
                // 아직 증축하지 않은 칸: 흐리게 보여 주고 아이템이 들어오지 못하게 막는다.
                v.icon.enabled = false;
                v.count.text = "";
                v.selection.enabled = false;
                v.frame.sprite = AssetLibrary.UiSlot;
                v.frame.color = new Color(1f, 1f, 1f, 0.25f);
                v.frame.raycastTarget = false;
                return;
            }

            v.frame.color = Color.white;
            v.frame.raycastTarget = true;

            if (stack != null && !stack.IsEmpty)
            {
                var def = stack.Def;
                var sp = def.GetSprite();
                v.icon.sprite = sp;
                v.icon.enabled = sp != null;
                v.count.text = stack.count > 1 ? stack.count.ToString() : "";
            }
            else
            {
                v.icon.enabled = false;
                v.count.text = "";
            }
            v.selection.enabled = selected;
            v.frame.sprite = selected ? AssetLibrary.UiSlotSelected : AssetLibrary.UiSlot;
        }

        // drag & drop callback from SlotDragHandler
        public void OnSlotDrop(SlotView from, SlotView to)
        {
            var fromInv = InventoryOf(from.container);
            var toInv = InventoryOf(to.container);

            if (ReferenceEquals(fromInv, toInv)) fromInv.MoveSlot(from.index, to.index);
            else Inventory.MoveBetween(fromInv, from.index, toInv, to.index);

            RefreshSlots();
        }

        /// <summary>쉬프트+좌클릭: 배송함이 열려 있을 때 반대편 인벤토리로 한 번에 옮긴다.</summary>
        public bool QuickTransfer(SlotView view)
        {
            if (!IsShippingOpen) return false;

            var from = InventoryOf(view.container);
            var to = view.container == SlotContainer.Player ? _game.ShippingBox : _game.Inventory;
            Inventory.QuickMove(from, view.index, to);

            RefreshSlots();
            return true;
        }

        private Inventory InventoryOf(SlotContainer container)
            => container == SlotContainer.Shipping ? _game.ShippingBox : _game.Inventory;

        public void OnHotbarClicked(int index)
        {
            _game.SelectHotbar(index);
            RefreshSlots();
        }

        // ---------- banners ----------
        public void ShowDayBanner(int day)
        {
            StopAllCoroutines();
            StartCoroutine(BannerRoutine($"{day}일차\n06:00"));
        }

        private IEnumerator BannerRoutine(string msg)
        {
            _bannerText.text = msg;
            float t = 0;
            while (t < 1.8f)
            {
                t += Time.unscaledDeltaTime;
                float a = t < 0.3f ? t / 0.3f : (t > 1.4f ? (1.8f - t) / 0.4f : 1f);
                _bannerText.color = new Color(1, 1, 1, Mathf.Clamp01(a));
                yield return null;
            }
            _bannerText.color = new Color(1, 1, 1, 0);
        }

        public void Toast(string msg)
        {
            StartCoroutine(ToastRoutine(msg));
        }

        private IEnumerator ToastRoutine(string msg)
        {
            _toastText.text = msg;
            float t = 0;
            while (t < 1.4f)
            {
                t += Time.unscaledDeltaTime;
                float a = t < 0.2f ? t / 0.2f : (t > 1.0f ? (1.4f - t) / 0.4f : 1f);
                _toastText.color = new Color(1, 1, 1, Mathf.Clamp01(a));
                yield return null;
            }
            _toastText.color = new Color(1, 1, 1, 0);
        }

        // ---------- small UI builders ----------
        private RectTransform Panel(string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = new Vector2(aMin.x, aMin.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = col;
            img.raycastTarget = false; // 장식용 HUD 배경일 뿐, 클릭을 가로채면 안 됨
            return rt;
        }

        public Text Label(RectTransform parent, string text, int size, Vector2 pos, TextAnchor anchor)
        {
            var go = new GameObject("Label");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(140, 30);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; // 텍스트는 클릭을 가로채지 않음 (버튼은 배경 Image가 받는다)
            return t;
        }

        public void Stretch(RectTransform rt, float top, float bottom, float left, float right)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }

    /// <summary>슬롯이 어느 인벤토리를 보여주는지.</summary>
    public enum SlotContainer { Player, Shipping }

    /// <summary>Lightweight view holder for a slot's widgets.</summary>
    public class SlotView
    {
        public int index;
        public bool isHotbar;
        public SlotContainer container;
        public Image frame;
        public Image icon;
        public Text count;
        public Outline selection;
    }
}
