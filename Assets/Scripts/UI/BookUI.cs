using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// I(가방) / ESC(설정) 로 펼쳐지는 책 UI. 오른쪽 가장자리의 책갈피를 누르면 페이지를 넘길 수 있다.
    /// 모든 위젯은 Book.png 원본 아트의 픽셀 좌표를 기준으로 배치되므로, Scale 상수만 바꿔도
    /// 전체 배치가 그대로 유지된 채 크기만 달라진다.
    /// </summary>
    public class BookUI : MonoBehaviour
    {
        public enum Page { Inventory, Settings, Help }

        private const float Scale = 3f;
        private const float ArtW = 238f, ArtH = 144f;

        // 9-slice 스프라이트를 아트 3배 크기로 그리기 위한 보정값 (캔버스 기준 100 PPU / 스프라이트 16 PPU)
        private const float SlicedPpu = 100f / (16f * Scale);

        // 책 양쪽 페이지의 안쪽 사용 영역 (아트 픽셀 좌표)
        private static readonly Rect LeftPage = new Rect(19, 6, 90, 115);
        private static readonly Rect RightPage = new Rect(129, 6, 89, 115);

        private const string VolumePrefKey = "farm_mvp_volume";

        private UIManager _ui;
        private GameManager _game;

        private RectTransform _root;   // 화면 전체 (어두운 배경 포함)
        private RectTransform _book;   // 책 자체
        private Page _current = Page.Inventory;

        private readonly Dictionary<Page, GameObject> _pages = new Dictionary<Page, GameObject>();
        private readonly Dictionary<Page, Image> _tabs = new Dictionary<Page, Image>();

        private Text _stMoney, _stDate, _stTime, _stHp, _stMp, _stMagic, _stBag;
        private Slider _volumeSlider;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Boot(UIManager ui, GameManager game, RectTransform canvas)
        {
            _ui = ui;
            _game = game;

            AudioListener.volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);

            BuildRoot(canvas);
            BuildTabs();
            BuildInventoryPage();
            BuildSettingsPage();
            BuildHelpPage();

            _root.gameObject.SetActive(false);
        }

        // ---------- 열기 / 닫기 ----------
        public void Toggle(Page page)
        {
            if (IsOpen && _current == page) { Close(); return; }
            Open(page);
        }

        public void Open(Page page)
        {
            _current = page;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            foreach (var kv in _pages)
                kv.Value.SetActive(kv.Key == page);

            foreach (var kv in _tabs)
                kv.Value.color = kv.Key == page ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);

            if (page == Page.Settings && _volumeSlider != null)
                _volumeSlider.SetValueWithoutNotify(AudioListener.volume);

            RefreshStatus();
        }

        public void Close()
        {
            _root.gameObject.SetActive(false);
            PlayerPrefs.Save();
        }

        /// <summary>왼쪽 페이지의 상태 정보를 현재 게임 상태로 갱신한다.</summary>
        public void RefreshStatus()
        {
            if (_stMoney == null || _game == null) return;
            var f = _game.Data.farmer;
            _stMoney.text = $"{_game.Money:N0} G";
            _stDate.text = $"{_game.Data.currentDay}일차";
            _stTime.text = $"{_game.TimeString()} ({(_game.IsDaytime ? "낮" : "밤")})";
            _stHp.text = $"{f.hp} / {f.maxHp}";
            _stMp.text = $"{f.mp} / {f.maxMp}";
            _stMagic.text = MagicSystem.Get(_game.CurrentMagic).displayName;
            _stBag.text = $"{_game.UnlockedSlots}칸 (Lv.{_game.BackpackLevel})";
        }

        // ---------- 뼈대 ----------
        private void BuildRoot(RectTransform canvas)
        {
            var rootGo = new GameObject("BookRoot");
            _root = rootGo.AddComponent<RectTransform>();
            _root.SetParent(canvas, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            // 뒤 배경을 어둡게 덮어 책에 집중되게 한다 (클릭도 여기서 막힌다).
            var dim = new GameObject("Dim").AddComponent<Image>();
            dim.transform.SetParent(_root, false);
            dim.color = new Color(0, 0, 0, 0.45f);
            var drt = dim.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

            var bookGo = new GameObject("Book");
            _book = bookGo.AddComponent<RectTransform>();
            _book.SetParent(_root, false);
            _book.anchorMin = _book.anchorMax = new Vector2(0.5f, 0.5f);
            _book.pivot = new Vector2(0.5f, 0.5f);
            _book.anchoredPosition = Vector2.zero;
            _book.sizeDelta = new Vector2(ArtW * Scale, ArtH * Scale);

            var bg = bookGo.AddComponent<Image>();
            bg.sprite = AssetLibrary.UiBook;
            bg.raycastTarget = false;
        }

        private void BuildTabs()
        {
            // 우측 상단 HUD와 겹치지 않도록 책 중단부터 아래로 배치한다.
            AddTab(Page.Inventory, "가방", AssetLibrary.UiBookmarkGreen, 44);
            AddTab(Page.Settings, "설정", AssetLibrary.UiBookmarkOrange, 74);
            AddTab(Page.Help, "도움", AssetLibrary.UiBookmarkBlue, 104);
        }

        private void AddTab(Page page, string label, Sprite sprite, float artY)
        {
            // 책 오른쪽 모서리에 살짝 걸쳐서 바깥으로 튀어나오게 배치한다.
            var rt = Area(_book, "Tab_" + label, new Rect(230, artY, 22, 22));

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var captured = page;
            btn.onClick.AddListener(() => Open(captured));

            var text = _ui.Label(rt, label, 15, Vector2.zero, TextAnchor.MiddleCenter);
            _ui.Stretch(text.rectTransform, 0, 0, 0, 0);
            text.color = new Color(0.15f, 0.12f, 0.10f);

            _tabs[page] = img;
        }

        // ---------- 가방 페이지 ----------
        private void BuildInventoryPage()
        {
            var page = NewPage(Page.Inventory);

            // 왼쪽: 상태 정보
            var left = Area(page, "Status", LeftPage);
            PageTitle(left, "상태");

            float y = -46f;
            _stMoney = AddStatusRow(left, ref y, "소지금");
            _stDate = AddStatusRow(left, ref y, "날짜");
            _stTime = AddStatusRow(left, ref y, "시간");
            _stHp = AddStatusRow(left, ref y, "체력");
            _stMp = AddStatusRow(left, ref y, "마나");
            _stMagic = AddStatusRow(left, ref y, "마법");
            _stBag = AddStatusRow(left, ref y, "가방");

            // 오른쪽: 아이템 격자 (윗줄 9칸이 곧 핫바)
            var right = Area(page, "Bag", RightPage);
            PageTitle(right, "가방");

            // 배낭 한 페이지(10칸) = 5칸씩 2줄. 페이지 사이는 살짝 띄워 구분한다.
            const int cols = 5;
            const int rowsPerGroup = 2;
            const float gapRatio = 0.45f;

            int groups = Inventory.BackpackPages;
            int rows = groups * rowsPerGroup;

            float availW = right.sizeDelta.x - 10f;
            float availH = right.sizeDelta.y - 74f;
            float pitch = Mathf.Min(availW / cols, availH / (rows + (groups - 1) * gapRatio));
            float groupGap = pitch * gapRatio;
            float slot = pitch - 5f;

            var grid = new GameObject("Grid").AddComponent<RectTransform>();
            grid.SetParent(right, false);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.anchoredPosition = new Vector2(0, -46);
            grid.sizeDelta = new Vector2(cols * pitch, rows * pitch + (groups - 1) * groupGap);

            for (int i = 0; i < Inventory.TotalSlots; i++)
            {
                int c = i % cols;
                int r = i / cols;
                int group = r / rowsPerGroup;

                float slotX = -((cols - 1) * pitch) / 2f + c * pitch;
                float slotY = -(r * pitch + group * groupGap);
                var sv = _ui.CreateInventorySlot(grid, i, slotX, slotY, slot);

                // 첫 10칸은 퀵바이므로 대응하는 숫자키를 적어 준다 (10번째는 0).
                if (i < Inventory.HotbarSize)
                {
                    var num = _ui.Label(sv.frame.rectTransform, i == 9 ? "0" : (i + 1).ToString(), 12, Vector2.zero, TextAnchor.UpperLeft);
                    var nrt = num.rectTransform;
                    nrt.anchorMin = new Vector2(0, 1);
                    nrt.anchorMax = new Vector2(0, 1);
                    nrt.pivot = new Vector2(0, 1);
                    nrt.anchoredPosition = new Vector2(3, -2);
                    nrt.sizeDelta = new Vector2(16, 16);
                    num.color = new Color(1f, 0.93f, 0.75f, 0.9f);
                }
            }

            var hint = _ui.Label(right, "1~0번이 퀵바 · TAB으로 배낭 전환", 13, Vector2.zero, TextAnchor.UpperCenter);
            hint.rectTransform.anchorMin = new Vector2(0, 0);
            hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.pivot = new Vector2(0.5f, 0);
            hint.rectTransform.anchoredPosition = new Vector2(0, 8);
            hint.rectTransform.sizeDelta = new Vector2(0, 40);
            hint.color = new Color(0.45f, 0.32f, 0.25f);
        }

        // ---------- 설정 페이지 ----------
        private void BuildSettingsPage()
        {
            var page = NewPage(Page.Settings);

            var left = Area(page, "SettingsInfo", LeftPage);
            PageTitle(left, "설정");

            var volLabel = _ui.Label(left, "음량", 17, new Vector2(0, -60), TextAnchor.UpperLeft);
            volLabel.rectTransform.anchorMin = new Vector2(0, 1);
            volLabel.rectTransform.anchorMax = new Vector2(1, 1);
            volLabel.rectTransform.pivot = new Vector2(0.5f, 1);
            volLabel.rectTransform.sizeDelta = new Vector2(0, 26);
            volLabel.color = TextColor;

            _volumeSlider = BuildVolumeSlider(left, new Vector2(0, -92));

            var note = _ui.Label(left, "ESC 를 다시 누르면 책이 닫힙니다.", 13, new Vector2(0, -150), TextAnchor.UpperLeft);
            note.rectTransform.anchorMin = new Vector2(0, 1);
            note.rectTransform.anchorMax = new Vector2(1, 1);
            note.rectTransform.pivot = new Vector2(0.5f, 1);
            note.rectTransform.sizeDelta = new Vector2(0, 40);
            note.color = new Color(0.45f, 0.32f, 0.25f);

            var right = Area(page, "SettingsButtons", RightPage);
            PageTitle(right, "게임");

            var size = new Vector2(right.sizeDelta.x - 40, 46);
            BookButton(right, "저장하기", new Vector2(0, -50), size, () =>
            {
                _game.SaveNow();
                _ui.Toast("저장됨");
            });
            BookButton(right, "타이틀로 나가기", new Vector2(0, -106), size, () =>
            {
                Close();
                _ui.SyncPaused();
                _ui.ShowYesNo("저장하고 타이틀로 나가시겠습니까?", onYes: () =>
                {
                    _game.SaveNow();
                    FindObjectOfType<GameBootstrap>()?.ReturnToTitle();
                });
            });
            BookButton(right, "게임 종료", new Vector2(0, -162), size, () =>
            {
                Close();
                _ui.SyncPaused();
                _ui.ShowYesNo("저장하고 게임을 종료하시겠습니까?", onYes: () =>
                {
                    _game.SaveNow();
                    QuitGame();
                });
            });
            BookButton(right, "닫기", new Vector2(0, -218), size, () =>
            {
                Close();
                _ui.SyncPaused();
            });
        }

        // ---------- 도움말 페이지 ----------
        private void BuildHelpPage()
        {
            var page = NewPage(Page.Help);

            var left = Area(page, "HelpBasic", LeftPage);
            PageTitle(left, "조작");
            BodyText(left,
                "이동  W A S D\n\n" +
                "상호작용  E / Space\n(침대에서 잠자기)\n\n" +
                "가방  I\n\n" +
                "설정  ESC\n\n" +
                "빠른 저장  F5\n\n" +
                "핫바 선택  1 ~ 9\n\n" +
                "마법 전환  Q");

            var right = Area(page, "HelpAction", RightPage);
            PageTitle(right, "마법과 농사");
            BodyText(right,
                "좌클릭  마법 사용\n" +
                "우클릭  씨앗 심기 / 수확\n\n" +
                "대지마법으로 밭을 갈고,\n" +
                "씨앗을 심은 뒤\n" +
                "물마법으로 물을 줍니다.\n\n" +
                "짧게 누르면 바로 시전,\n" +
                "길게 누르면 범위를 보며\n" +
                "이동하다가 손을 뗄 때\n" +
                "시전됩니다.\n\n" +
                "물마법은 누르고 있는 동안\n" +
                "계속 물을 줍니다.");
        }

        // ---------- 조각 만들기 ----------
        private static readonly Color TextColor = new Color(0.28f, 0.16f, 0.12f);

        private RectTransform NewPage(Page page)
        {
            var go = new GameObject("Page_" + page);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_book, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _pages[page] = go;
            return rt;
        }

        private RectTransform Area(Transform parent, string name, Rect art)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float cx = art.x + art.width / 2f;
            float cy = art.y + art.height / 2f;
            rt.anchoredPosition = new Vector2((cx - ArtW / 2f) * Scale, (ArtH / 2f - cy) * Scale);
            rt.sizeDelta = new Vector2(art.width * Scale, art.height * Scale);
            return rt;
        }

        private void PageTitle(RectTransform page, string title)
        {
            var t = _ui.Label(page, title, 24, new Vector2(0, -4), TextAnchor.UpperCenter);
            t.rectTransform.anchorMin = new Vector2(0, 1);
            t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.pivot = new Vector2(0.5f, 1);
            t.rectTransform.sizeDelta = new Vector2(0, 34);
            t.color = TextColor;
        }

        private void BodyText(RectTransform page, string body)
        {
            var t = _ui.Label(page, body, 14, new Vector2(0, -42), TextAnchor.UpperLeft);
            t.rectTransform.anchorMin = new Vector2(0, 0);
            t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.pivot = new Vector2(0.5f, 1);
            t.rectTransform.offsetMin = new Vector2(14, 10);
            t.rectTransform.offsetMax = new Vector2(-14, -42);
            t.color = TextColor;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        /// <summary>왼쪽 페이지에 "이름 ......... 값" 형태의 한 줄을 추가하고, 값 쪽 Text를 돌려준다.</summary>
        private Text AddStatusRow(RectTransform page, ref float y, string name)
        {
            var nameText = _ui.Label(page, name, 16, Vector2.zero, TextAnchor.MiddleLeft);
            var nrt = nameText.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(0.55f, 1f);
            nrt.offsetMin = new Vector2(14, y - 26);
            nrt.offsetMax = new Vector2(0, y);
            nameText.color = new Color(0.5f, 0.36f, 0.28f);

            var valueText = _ui.Label(page, "-", 16, Vector2.zero, TextAnchor.MiddleRight);
            var vrt = valueText.rectTransform;
            vrt.anchorMin = new Vector2(0.45f, 1f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = new Vector2(0, y - 26);
            vrt.offsetMax = new Vector2(-14, y);
            valueText.color = TextColor;

            y -= 32f;
            return valueText;
        }

        private Button BookButton(RectTransform parent, string text, Vector2 pos, Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + text);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = AssetLibrary.UiFrame;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = SlicedPpu;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var label = _ui.Label(rt, text, 18, Vector2.zero, TextAnchor.MiddleCenter);
            _ui.Stretch(label.rectTransform, 0, 0, 0, 0);
            return btn;
        }

        private Slider BuildVolumeSlider(RectTransform parent, Vector2 pos)
        {
            var container = new GameObject("VolumeSlider");
            var rt = container.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(parent.sizeDelta.x - 40, 22);

            var bg = new GameObject("BG").AddComponent<Image>();
            bg.transform.SetParent(rt, false);
            bg.color = new Color(0.55f, 0.40f, 0.30f, 1f);
            _ui.Stretch(bg.rectTransform, 0, 0, 0, 0);

            var fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(rt, false);
            fill.color = new Color(0.85f, 0.62f, 0.35f, 1f);
            _ui.Stretch(fill.rectTransform, 0, 0, 0, 0);

            var handle = new GameObject("Handle");
            var handleRt = handle.AddComponent<RectTransform>();
            handleRt.SetParent(rt, false);
            handleRt.sizeDelta = new Vector2(14, 30);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(0.35f, 0.20f, 0.14f, 1f);

            var slider = container.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = AudioListener.volume;
            slider.onValueChanged.AddListener(v =>
            {
                AudioListener.volume = v;
                PlayerPrefs.SetFloat(VolumePrefKey, v);
            });
            return slider;
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
