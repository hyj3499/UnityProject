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

        private GameManager _game;
        private Canvas _canvas;
        private Font _font;

        // HUD
        private Slider _hpBar, _mpBar;
        private Text _dateText, _timeText;

        // Hotbar
        private readonly List<SlotView> _hotbarViews = new List<SlotView>();
        private RectTransform _hotbarRoot;
        private Text _toolLabel;

        // Inventory panel
        private GameObject _inventoryPanel;
        private readonly List<SlotView> _invViews = new List<SlotView>();

        // Banner / toast
        private Text _bannerText;
        private Text _toastText;

        public void Boot(GameManager game)
        {
            Instance = this;
            _game = game;
            _font = BuildFont();

            BuildCanvas();
            BuildHUD();
            BuildHotbar();
            BuildInventoryPanel();
            BuildBannerAndToast();

            _game.OnTimeChanged += RefreshTime;
            _game.OnDayChanged += RefreshTime;
            _game.OnHotbarChanged += RefreshSlots;
            _game.OnMagicChanged += RefreshMagic;
            _game.Inventory.OnChanged += RefreshSlots;

            RefreshTime();
            RefreshSlots();
            RefreshMagic();
            SetInventoryOpen(false);
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

            // top-right date/time
            var tr = Panel("TimePanel", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-12, -12), new Vector2(150, 60), new Color(0, 0, 0, 0.45f));
            _dateText = Label(tr, "1일차", 20, new Vector2(0, -6), TextAnchor.UpperCenter);
            _dateText.rectTransform.anchorMin = new Vector2(0, 1);
            _dateText.rectTransform.anchorMax = new Vector2(1, 1);
            _dateText.rectTransform.sizeDelta = new Vector2(0, 26);
            _timeText = Label(tr, "06:00", 22, new Vector2(0, -32), TextAnchor.UpperCenter);
            _timeText.rectTransform.anchorMin = new Vector2(0, 1);
            _timeText.rectTransform.anchorMax = new Vector2(1, 1);
            _timeText.rectTransform.sizeDelta = new Vector2(0, 26);
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
            Stretch(bg.rectTransform, 30, 0, 0, 0);

            var fillArea = new GameObject("Fill").AddComponent<Image>();
            fillArea.transform.SetParent(rt, false);
            fillArea.color = fill;
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
            }

            // equipped tool indicator (bottom-right)
            var toolPanel = Panel("ToolIndicator", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-12, 12), new Vector2(120, 52), new Color(0, 0, 0, 0.45f));
            _toolLabel = Label(toolPanel, "-", 14, Vector2.zero, TextAnchor.MiddleCenter);
            Stretch(_toolLabel.rectTransform, 0, 0, 0, 0);
        }

        // ---------- Inventory panel ----------
        private void BuildInventoryPanel()
        {
            _inventoryPanel = new GameObject("InventoryPanel");
            var rt = _inventoryPanel.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            int cols = 9;
            int rows = Inventory.TotalSlots / cols;
            float slot = 50, pad = 6;
            float w = cols * slot + (cols - 1) * pad + 24;
            float h = rows * slot + (rows - 1) * pad + 60;
            rt.sizeDelta = new Vector2(w, h);

            var bg = _inventoryPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.10f, 0.94f);

            var title = Label(rt, "인벤토리 (I 로 닫기)", 18, new Vector2(0, -8), TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.pivot = new Vector2(0.5f, 1);
            title.rectTransform.sizeDelta = new Vector2(0, 30);

            var grid = new GameObject("Grid");
            var grt = grid.AddComponent<RectTransform>();
            grt.SetParent(rt, false);
            grt.anchorMin = new Vector2(0.5f, 1);
            grt.anchorMax = new Vector2(0.5f, 1);
            grt.pivot = new Vector2(0.5f, 1);
            grt.anchoredPosition = new Vector2(0, -40);
            grt.sizeDelta = new Vector2(w - 24, h - 60);

            for (int i = 0; i < Inventory.TotalSlots; i++)
            {
                int c = i % cols;
                int r = i / cols;
                float x = -((cols - 1) * (slot + pad)) / 2f + c * (slot + pad);
                float y = -r * (slot + pad);
                var sv = BuildSlot(grt, i, x, y, slot, isHotbar: false, centered: true);
                _invViews.Add(sv);
            }
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

        // ---------- slot construction ----------
        private SlotView BuildSlot(RectTransform parent, int index, float x, float y, float size,
            bool isHotbar, bool centered = false)
        {
            var go = new GameObject($"Slot_{(isHotbar ? "H" : "I")}_{index}");
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
            frame.color = new Color(0.2f, 0.2f, 0.24f, 0.9f);

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
            _dateText.text = $"{_game.Data.currentDay}일차";
            _timeText.text = _game.TimeString();

            _hpBar.value = Mathf.Clamp01((float)_game.Data.farmer.hp / _game.Data.farmer.maxHp);
            _mpBar.value = Mathf.Clamp01((float)_game.Data.farmer.mp / _game.Data.farmer.maxMp);
        }

        public void RefreshSlots()
        {
            for (int i = 0; i < _hotbarViews.Count; i++)
                UpdateSlotView(_hotbarViews[i], _game.Inventory.GetSlot(i), i == _game.Data.farmer.equippedHotbarIndex);

            for (int i = 0; i < _invViews.Count; i++)
                UpdateSlotView(_invViews[i], _game.Inventory.GetSlot(i), i == _game.Data.farmer.equippedHotbarIndex && i < Inventory.HotbarSize);
        }

        /// <summary>우측 하단에 현재 선택된 마법 이름과 MP 소모량을 표시.</summary>
        private void RefreshMagic()
        {
            switch (_game.CurrentMagic)
            {
                case MagicType.Earth:
                    _toolLabel.text = $"대지마법\nMP {GameManager.MpCostEarth}";
                    break;
                case MagicType.Water:
                    _toolLabel.text = $"물마법\nMP {GameManager.MpCostWater}";
                    break;
                case MagicType.Blade:
                    _toolLabel.text = $"칼날마법\nMP {GameManager.MpCostBlade}";
                    break;
            }
        }

        private void UpdateSlotView(SlotView v, ItemStack stack, bool selected)
        {
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
            v.frame.color = selected ? new Color(0.32f, 0.3f, 0.18f, 0.95f) : new Color(0.2f, 0.2f, 0.24f, 0.9f);
        }

        public void SetInventoryOpen(bool open)
        {
            _inventoryPanel.SetActive(open);
            if (open) RefreshSlots();
        }

        // drag & drop callback from SlotDragHandler
        public void OnSlotDrop(int from, int to)
        {
            _game.Inventory.MoveSlot(from, to);
            RefreshSlots();
        }

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
            return rt;
        }

        private Text Label(RectTransform parent, string text, int size, Vector2 pos, TextAnchor anchor)
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
            return t;
        }

        private void Stretch(RectTransform rt, float top, float bottom, float left, float right)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }

    /// <summary>Lightweight view holder for a slot's widgets.</summary>
    public class SlotView
    {
        public int index;
        public bool isHotbar;
        public Image frame;
        public Image icon;
        public Text count;
        public Outline selection;
    }
}
