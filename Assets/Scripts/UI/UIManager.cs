using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FarmMVP
{
    /// <summary>
    /// Builds and drives runtime UI from code: equipped-magic indicator, shipping box, shop, dialogue,
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

        // 책(인벤토리/설정/도움말)
        private BookUI _book;
        private readonly List<SlotView> _invViews = new List<SlotView>();

        // 배송함
        private ShippingUI _shipping;
        private readonly List<SlotView> _shippingViews = new List<SlotView>();

        // NPC 대화
        private DialogueUI _dialogue;

        // 상점
        private ShopBookController _shop;
        private CraftingUI _crafting;

        // Banner / toast
        private Text _bannerText;
        private Text _toastText;

        // Yes/No confirm dialog
        private GameObject _confirmPanel;
        private ConfirmationDialogView _confirmView;
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
            BuildBook();
            BuildShipping();
            BuildShop();
            BuildCrafting();
            BuildDialogue();
            BuildBannerAndToast();
            BuildConfirmDialog();

            _game.OnTimeChanged += RefreshTime;
            _game.OnDayChanged += RefreshTime;
            _game.OnHotbarChanged += RefreshSlots;
            _game.OnMoneyChanged += RefreshMoney;
            _game.OnShippingChanged += OnShippingInventoryChanged;
            _game.Inventory.OnChanged += RefreshSlots;

            RefreshTime();
            RefreshMoney();
            RefreshSlots();
            LocalizationBindings.Apply(_canvas.gameObject);
        }

        private Font BuildFont()
        {
            return LocalizedFonts.Get();
        }

        private void OnDestroy()
        {
            if (_confirmView != null)
            {
                _confirmView.yes.onClick.RemoveListener(ConfirmYes);
                _confirmView.no.onClick.RemoveListener(ConfirmNo);
                _confirmView.gameObject.SetActive(false);
            }
            // 타이틀로 돌아갈 때 UIManager와 함께 캔버스도 정리한다 (캔버스는 별도 오브젝트로 만들어짐).
            if (Instance == this) Instance = null;
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void BuildCanvas()
        {
            var go = new GameObject("UICanvas");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = UILayers.Gameplay;
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
            var go = new GameObject("ShopBookController");
            go.transform.SetParent(_canvas.transform, false);
            _shop = go.AddComponent<ShopBookController>();
            _shop.Boot(this, _game, _canvas.transform as RectTransform);
        }

        public bool IsShopOpen => _shop != null && _shop.IsOpen;
        public bool IsStoryRunning => _game != null && _game.Events != null && _game.Events.IsRunning;

        public void OpenShop()
            => OpenShop(ShopDatabase.DefaultShopId);

        public void OpenShop(string shopId)
            => OpenShop(ShopDatabase.Get(shopId));

        public void OpenShop(ShopDefinition shop)
        {
            if (IsStoryRunning) return;
            if (_shop == null || IsBookOpen || IsShippingOpen || IsCraftingOpen || IsDialogueOpen) return;
            _shop.Open(shop);
            SyncPaused();
        }

        public void CloseShop()
        {
            if (_shop == null) return;
            _shop.Close();
            SyncPaused();
        }

        private void BuildCrafting()
        {
            var go = new GameObject("CraftingUI");
            go.transform.SetParent(_canvas.transform, false);
            _crafting = go.AddComponent<CraftingUI>();
            _crafting.Boot(this, _game, _canvas.transform as RectTransform);
        }

        public bool IsCraftingOpen => _crafting != null && _crafting.IsOpen;

        public void OpenCrafting()
        {
            if (IsStoryRunning) return;
            if (_crafting == null || IsBookOpen) return;
            _crafting.Open();
            SyncPaused();
        }

        public void CloseCrafting()
        {
            if (_crafting == null) return;
            _crafting.Close();
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

        public void ShowStoryDialogue(StoryCommand command, Action<string> onChoice, Action onFinished)
        {
            if (_dialogue == null) throw new InvalidOperationException("대화 UI가 준비되지 않았습니다.");
            var def = NpcDatabase.Get(command.actor);
            string name = !string.IsNullOrEmpty(command.speaker) ? command.speaker
                : def != null ? Localization.Resolve(def.displayName) : command.actor == "player" ? Localization.Get("ui.uimanager.me") : "";
            DialogueChoice[] choices = null;
            if (command.op == "Choice")
            {
                choices = new DialogueChoice[command.choices.Length];
                for (int i = 0; i < choices.Length; i++) choices[i] = new DialogueChoice { text = command.choices[i].text };
            }
            _dialogue.Show(def != null ? def.id : "", name, command.portrait, command.lines, choices,
                picked =>
                {
                    int index = Array.IndexOf(choices, picked);
                    _dialogue.Close();
                    SyncPaused();
                    onChoice(command.choices[index].target);
                }, () => { SyncPaused(); onFinished(); });
            SyncPaused();
        }

        public void CloseStoryDialogue()
        {
            if (_dialogue != null) _dialogue.Close();
            SyncPaused();
        }

        /// <summary>NPC 대사를 띄운다. 선택지가 있으면 마지막 줄 뒤에 버튼이 나온다.</summary>
        public void ShowDialogue(NpcDefinition def, string portrait, string[] lines,
            DialogueChoice[] choices, Action<DialogueChoice> onChoice)
        {
            if (_dialogue == null) return;
            _dialogue.Show(def.id, Localization.Resolve(def.displayName), portrait, lines, choices, onChoice, SyncPaused);
            SyncPaused();
        }

        /// <summary>선택지를 고른 뒤 이어지는 반응 대사를 같은 창에 이어서 보여준다.</summary>
        public void ContinueDialogue(NpcDefinition def, string portrait, string[] lines)
        {
            if (_dialogue == null) return;
            _dialogue.Show(def.id, Localization.Resolve(def.displayName), portrait, lines, null, null, SyncPaused);
            SyncPaused();
        }

        /// <summary>배송함을 우클릭했을 때 호출된다.</summary>
        public void OpenShippingBox()
        {
            if (IsStoryRunning) return;
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
            var sv = BuildSlot(parent, index, x, y, size, centered: true, container: SlotContainer.Shipping);
            _shippingViews.Add(sv);
            return sv;
        }

        /// <summary>ShippingUI가 아래쪽 "내 가방" 칸을 만들 때 호출한다.</summary>
        public SlotView CreatePlayerSlot(RectTransform parent, int index, float x, float y, float size)
            => CreateInventorySlot(parent, index, x, y, size);

        /// <summary>I / ESC: 해당 페이지로 책을 연다. 이미 그 페이지가 열려 있으면 닫는다.</summary>
        public void ToggleBook(BookUI.Page page)
        {
            if (IsStoryRunning) return;
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
            _game.Paused = confirmOpen || IsBookOpen || IsShippingOpen || IsDialogueOpen
                           || IsShopOpen || IsCraftingOpen;
        }

        /// <summary>배송함 아래쪽 "내 가방" 칸을 만들 때 호출한다 (드래그&amp;드롭 및 갱신 대상에 등록).</summary>
        public SlotView CreateInventorySlot(RectTransform parent, int index, float x, float y, float size)
        {
            var sv = BuildSlot(parent, index, x, y, size, centered: true);
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
            foreach (var view in FindObjectsByType<ConfirmationDialogView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (view.gameObject.scene == _game.gameObject.scene) { _confirmView = view; break; }
            if (_confirmView == null) _confirmView = ConfirmationDialogView.Build(_canvas.transform);
            _confirmPanel = _confirmView.gameObject;
            _confirmText = _confirmView.message;
            _confirmView.yes.onClick.AddListener(ConfirmYes);
            _confirmView.no.onClick.AddListener(ConfirmNo);
            _confirmPanel.SetActive(false);
        }

        private void ConfirmYes() => OnConfirmClicked(true);
        private void ConfirmNo() => OnConfirmClicked(false);

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
            bool centered = false, SlotContainer container = SlotContainer.Player)
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
            count.font = LocalizedFonts.Get();
            LocalizedText.Attach(count);
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
            _book?.RefreshStatus();
        }

        private void OnShippingInventoryChanged()
        {
            RefreshSlots();
            RefreshShippingValue();
        }

        private void RefreshMoney()
        {
            _book?.RefreshStatus();
        }

        public void RefreshSlots()
        {
            // 같은 인벤토리를 여러 화면(배송함 / 퀵 인벤토리)에서 동시에 보여주므로,
            // 리스트 순번이 아니라 각 칸이 가리키는 실제 슬롯 번호를 기준으로 갱신한다.
            int equipped = _game.Data.farmer.equippedHotbarIndex;
            int unlocked = _game.UnlockedSlots;
            foreach (var v in _invViews)
                UpdateSlotView(v, _game.Inventory.GetSlot(v.index), v.index == equipped, v.index >= unlocked);

            foreach (var v in _shippingViews)
                UpdateSlotView(v, _game.ShippingBox.GetSlot(v.index), false);
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
            if (IsStoryRunning) return;
            var fromInv = InventoryOf(from.container);
            var toInv = InventoryOf(to.container);

            if (ReferenceEquals(fromInv, toInv)) fromInv.MoveSlot(from.index, to.index);
            else Inventory.MoveBetween(fromInv, from.index, toInv, to.index);

            RefreshSlots();
        }

        /// <summary>쉬프트+좌클릭: 배송함이 열려 있을 때 반대편 인벤토리로 한 번에 옮긴다.</summary>
        public bool QuickTransfer(SlotView view)
        {
            if (IsStoryRunning) return false;
            if (!IsShippingOpen) return false;

            var from = InventoryOf(view.container);
            var to = view.container == SlotContainer.Player ? _game.ShippingBox : _game.Inventory;
            Inventory.QuickMove(from, view.index, to);

            RefreshSlots();
            return true;
        }

        private Inventory InventoryOf(SlotContainer container)
            => container == SlotContainer.Shipping ? _game.ShippingBox : _game.Inventory;

        // ---------- banners ----------
        public void ShowDayBanner(int day)
        {
            StopAllCoroutines();
            StartCoroutine(BannerRoutine(Localization.Format("ui.uimanager.day_0_06_00", day)));
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
            LocalizedText.Attach(t);
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
        public SlotContainer container;
        public Image frame;
        public Image icon;
        public Text count;
        public Outline selection;
    }
}
