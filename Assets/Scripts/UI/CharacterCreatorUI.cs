using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 새 게임을 시작할 때 뜨는 캐릭터 만들기 화면.
    ///
    /// 왼쪽에 살아 움직이는 미리보기(서 있는 자세를 네 방향으로 돌려 볼 수 있다), 오른쪽에
    /// 항목 줄이 놓인다. 눈매·머리 모양·장식처럼 <b>그림 자체가 다른 것</b>은 예전처럼
    /// ◀ 값 ▶ 로 고르고, 피부·눈·머리·옷의 <b>색</b>은 네모난 색 견본을 눌러 색상 그래프
    /// (채도·명도 사각형 + 색상 띠 + HEX 입력)에서 직접 고른다.
    ///
    /// 미리보기는 게임과 <b>같은 PlayerAnimator</b>를 쓴다 — 화면 전용 그리기 코드를 따로 두면
    /// 실제 모습과 어긋나기 때문에, 카메라 앞에 진짜 캐릭터를 하나 세워 두고 보여 준다.
    /// 색 역시 게임과 같은 PlayerPaletteSwap이 입히므로, 여기서 고른 색이 곧 게임 속 모습이다.
    /// </summary>
    public class CharacterCreatorUI : MonoBehaviour
    {
        private static readonly Color TextColor = new Color(0.96f, 0.92f, 0.84f);
        private static readonly Color AccentColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.10f, 0.97f);

        private Font _font;
        private Canvas _canvas;
        private Action<PlayerAppearance> _onDone;
        private Action _onCancel;

        private PlayerAppearance _appearance = new PlayerAppearance();

        private GameObject _previewGo;
        private PlayerAnimator _preview;
        private Camera _previewCam;
        private RenderTexture _previewTexture;
        private RawImage _previewImage;
        private Direction _previewFacing = Direction.Down;

        // ---------- 모양 선택 줄 (그림 자체가 다른 것) ----------
        private class ShapeRow
        {
            public string label;
            public AppearanceOption[] options;
            public Func<PlayerAppearance, string> get;
            public Action<PlayerAppearance, string> set;
            public Text valueText;
        }

        private readonly List<ShapeRow> _shapeRows = new List<ShapeRow>();

        // ---------- 색 선택 줄 ----------
        private class ColorRow
        {
            public string label;
            public Func<PlayerAppearance, string> get;
            public Action<PlayerAppearance, string> set;
            public (string label, string hex)[] swatches;
            public Image swatch;
        }

        private readonly List<ColorRow> _colorRows = new List<ColorRow>();

        /// <summary>
        /// 화면을 만들어 띄운다. 만들다 실패하면 <b>null을 돌려준다</b> — 부르는 쪽이 타이틀로
        /// 되돌려야 아무것도 못 하는 빈 화면에 갇히지 않는다.
        /// </summary>
        public static CharacterCreatorUI Create(Font font, PlayerAppearance start,
                                                Action<PlayerAppearance> onDone, Action onCancel)
        {
            var go = new GameObject("CharacterCreator");
            var ui = go.AddComponent<CharacterCreatorUI>();
            try
            {
                ui.Boot(font, start, onDone, onCancel);
                Debug.Log("[캐릭터 만들기] 화면을 띄웠습니다.");
                return ui;
            }
            catch (Exception e)
            {
                Debug.LogError($"[캐릭터 만들기] 화면을 만들지 못했습니다: {e}");
                Destroy(go);
                return null;
            }
        }

        private void Boot(Font font, PlayerAppearance start,
                          Action<PlayerAppearance> onDone, Action onCancel)
        {
            _font = font;
            _appearance = start != null ? start.Clone() : new PlayerAppearance();
            _onDone = onDone;
            _onCancel = onCancel;

            BuildRowDefs();
            BuildCanvas();
            BuildPreview();
            BuildColorPicker();
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (_previewGo != null) Destroy(_previewGo);
            if (_previewCam != null) Destroy(_previewCam.gameObject);
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_previewTexture != null) { _previewTexture.Release(); Destroy(_previewTexture); }
        }

        // ---------- 항목 정의 ----------
        private void BuildRowDefs()
        {
            void Shape(string label, AppearanceOption[] options,
                       Func<PlayerAppearance, string> get, Action<PlayerAppearance, string> set)
                => _shapeRows.Add(new ShapeRow { label = label, options = options, get = get, set = set });

            void Col(string label, (string, string)[] swatches,
                     Func<PlayerAppearance, string> get, Action<PlayerAppearance, string> set)
                => _colorRows.Add(new ColorRow { label = label, swatches = swatches, get = get, set = set });

            Col("피부색", AppearanceCatalog.SkinSwatches, a => a.skin, (a, v) => a.skin = v);
            Shape("눈매", AppearanceCatalog.EyeSets, a => a.eyeSet, (a, v) => a.eyeSet = v);
            Col("눈 색", AppearanceCatalog.EyeColorSwatches, a => a.eyeColor, (a, v) => a.eyeColor = v);
            Shape("머리 모양", AppearanceCatalog.HairStyles, a => a.hairStyle, (a, v) => a.hairStyle = v);
            Col("머리색", AppearanceCatalog.HairColorSwatches, a => a.hairColor, (a, v) => a.hairColor = v);
            Col("옷 색", AppearanceCatalog.ClothesSwatches, a => a.clothes, (a, v) => a.clothes = v);
            Shape("장식", AppearanceCatalog.Accessories, a => a.acc, (a, v) => a.acc = v);
        }

        /// <summary>모양 항목을 앞뒤로 넘긴다 (목록 끝에서 반대쪽으로 돌아간다).</summary>
        private void Step(ShapeRow row, int delta)
        {
            int i = AppearanceCatalog.IndexOf(row.options, row.get(_appearance));
            int n = row.options.Length;
            i = ((i + delta) % n + n) % n;
            row.set(_appearance, row.options[i].value);

            row.valueText.text = row.options[i].label;
            _preview?.SetAppearance(_appearance);
        }

        private void RefreshAll()
        {
            foreach (var row in _shapeRows)
            {
                int i = AppearanceCatalog.IndexOf(row.options, row.get(_appearance));
                row.valueText.text = row.options[i].label;
            }
            foreach (var row in _colorRows)
                if (row.swatch != null && ColorUtility.TryParseHtmlString(row.get(_appearance), out var c))
                    row.swatch.color = c;

            _preview?.SetAppearance(_appearance);
        }

        /// <summary>고를 때마다 무작위로 한 벌 뽑아 준다 — 처음 고르기가 막막할 때 쓴다.</summary>
        private void Randomize()
        {
            foreach (var row in _shapeRows)
                row.set(_appearance, row.options[UnityEngine.Random.Range(0, row.options.Length)].value);
            foreach (var row in _colorRows)
            {
                var c = UnityEngine.Random.ColorHSV(0f, 1f, 0.35f, 0.85f, 0.35f, 0.95f);
                row.set(_appearance, "#" + ColorUtility.ToHtmlStringRGB(c));
            }
            RefreshAll();
        }

        // ---------- 미리보기 ----------
        /// <summary>
        /// 실제 캐릭터를 화면 밖 빈자리에 세워 두고 전용 카메라로 찍어 RenderTexture에 담은 뒤,
        /// 그것을 화면 왼쪽에 붙인다. UI 이미지로 흉내 내지 않고 게임과 <b>같은 PlayerAnimator</b>를
        /// 쓰기 때문에, 여기서 보이는 모습이 곧 게임에서의 모습이다.
        ///
        /// (카메라를 화면에 바로 비추지 않는 이유: 이 화면의 캔버스가 ScreenSpaceOverlay라
        /// 카메라가 그린 것 위에 덮여 버린다. 찍어서 붙이면 순서 문제가 없다.)
        /// </summary>
        private void BuildPreview()
        {
            _previewTexture = new RenderTexture(240, 300, 16) { filterMode = FilterMode.Point };
            _previewTexture.Create();

            var camGo = new GameObject("PreviewCamera");
            _previewCam = camGo.AddComponent<Camera>();
            _previewCam.orthographic = true;
            _previewCam.orthographicSize = 1.5f;
            _previewCam.backgroundColor = new Color(0.13f, 0.17f, 0.15f);
            _previewCam.clearFlags = CameraClearFlags.SolidColor;
            _previewCam.targetTexture = _previewTexture;
            // 세워 둔 캐릭터가 세상 어디와도 겹치지 않도록 멀리 떨어뜨려 둔다.
            camGo.transform.position = new Vector3(500f, 500f, -10f);

            _previewGo = new GameObject("PreviewCharacter");
            _previewGo.transform.position = new Vector3(500f, 499.7f, 0f);
            _preview = _previewGo.AddComponent<PlayerAnimator>();
            _preview.Init(_appearance);
            _preview.SetSortingOrder(0);
            _preview.SetLocomotion(false, false, false, _previewFacing);

            var go = new GameObject("Preview");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.23f, 0.52f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(240, 300);
            _previewImage = go.AddComponent<RawImage>();
            _previewImage.texture = _previewTexture;
        }

        private void TurnPreview(int delta)
        {
            // 보여 주는 순서: 앞 → 오른쪽 → 뒤 → 왼쪽 (제자리에서 한 바퀴 도는 느낌)
            var order = new[] { Direction.Down, Direction.Right, Direction.Up, Direction.Left };
            int i = System.Array.IndexOf(order, _previewFacing);
            if (i < 0) i = 0;
            int n = order.Length;
            _previewFacing = order[((i + delta) % n + n) % n];
            _preview?.SetLocomotion(false, false, false, _previewFacing);
        }

        // ---------- 화면 ----------
        private void BuildCanvas()
        {
            var go = new GameObject("CharacterCreatorCanvas");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = UILayers.CharacterCreator;   // 타이틀 화면 위에 뜬다
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            go.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("BG").AddComponent<Image>();
            bg.transform.SetParent(_canvas.transform, false);
            bg.color = new Color(0.10f, 0.14f, 0.12f, 1f);
            Stretch(bg.rectTransform);

            var title = Label(_canvas.transform as RectTransform, "캐릭터 만들기", 30, TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.offsetMin = new Vector2(0, -70);
            title.rectTransform.offsetMax = new Vector2(0, -18);
            title.color = AccentColor;

            BuildTurnButtons();
            BuildRowList();
            BuildFooter();
        }

        private void BuildTurnButtons()
        {
            var hint = Label(_canvas.transform as RectTransform, "↻ 방향 돌려보기", 15, TextAnchor.MiddleCenter);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.23f, 0.16f);
            hint.rectTransform.sizeDelta = new Vector2(220, 24);
            hint.color = new Color(0.75f, 0.72f, 0.68f);

            MakeButton(_canvas.transform as RectTransform, "◀", new Vector2(0.14f, 0.11f),
                       new Vector2(52, 36), () => TurnPreview(-1));
            MakeButton(_canvas.transform as RectTransform, "▶", new Vector2(0.32f, 0.11f),
                       new Vector2(52, 36), () => TurnPreview(1));
        }

        /// <summary>줄 목록. 색 줄은 ◀▶ 대신 네모난 색 견본 버튼 하나로 색상 그래프를 연다.</summary>
        private void BuildRowList()
        {
            const float rowH = 44f;
            float top = -96f;

            void NextRow(out RectTransform rt, string name)
            {
                var holder = new GameObject(name);
                rt = holder.AddComponent<RectTransform>();
                rt.SetParent(_canvas.transform, false);
                rt.anchorMin = new Vector2(0.44f, 1f);
                rt.anchorMax = new Vector2(0.96f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(0, top - rowH);
                rt.offsetMax = new Vector2(0, top);
                top -= rowH + 6f;
            }

            Text NameLabel(RectTransform rt, string text)
            {
                var name = Label(rt, text, 18, TextAnchor.MiddleLeft);
                name.rectTransform.anchorMin = new Vector2(0, 0);
                name.rectTransform.anchorMax = new Vector2(0.4f, 1);
                name.rectTransform.offsetMin = new Vector2(8, 0);
                name.rectTransform.offsetMax = Vector2.zero;
                name.color = TextColor;
                return name;
            }

            // 순서는 "피부색 · 눈매 · 눈 색 · 머리 모양 · 머리색 · 옷 색 · 장식" — BuildRowDefs와 같다.
            int shapeI = 0, colorI = 0;
            var order = new[] { 'C', 'S', 'C', 'S', 'C', 'C', 'S' }; // C=색, S=모양 — BuildRowDefs 순서 그대로

            foreach (var kind in order)
            {
                if (kind == 'C')
                {
                    var row = _colorRows[colorI++];
                    NextRow(out var rt, "Row_" + row.label);
                    NameLabel(rt, row.label);

                    var swatchGo = new GameObject("Swatch");
                    var srt = swatchGo.AddComponent<RectTransform>();
                    srt.SetParent(rt, false);
                    srt.anchorMin = new Vector2(0.42f, 0.5f);
                    srt.anchorMax = new Vector2(0.42f, 0.5f);
                    srt.pivot = new Vector2(0f, 0.5f);
                    srt.sizeDelta = new Vector2(64, 32);
                    var img = swatchGo.AddComponent<Image>();
                    img.color = Color.white;
                    var btn = swatchGo.AddComponent<Button>();
                    btn.targetGraphic = img;
                    row.swatch = img;
                    var captured = row;
                    btn.onClick.AddListener(() => OpenPicker(captured));

                    var editLabel = Label(rt, "탭해서 색 고르기", 13, TextAnchor.MiddleLeft);
                    editLabel.rectTransform.anchorMin = new Vector2(0.58f, 0);
                    editLabel.rectTransform.anchorMax = new Vector2(1f, 1);
                    editLabel.rectTransform.offsetMin = new Vector2(10, 0);
                    editLabel.rectTransform.offsetMax = Vector2.zero;
                    editLabel.color = new Color(0.7f, 0.68f, 0.62f);
                }
                else
                {
                    var row = _shapeRows[shapeI++];
                    NextRow(out var rt, "Row_" + row.label);
                    NameLabel(rt, row.label);

                    var captured = row;
                    MakeButtonIn(rt, "◀", new Vector2(0.46f, 0.5f), new Vector2(36, 32),
                                 () => Step(captured, -1));

                    row.valueText = Label(rt, "", 16, TextAnchor.MiddleCenter);
                    row.valueText.rectTransform.anchorMin = new Vector2(0.52f, 0);
                    row.valueText.rectTransform.anchorMax = new Vector2(0.86f, 1);
                    row.valueText.rectTransform.offsetMin = Vector2.zero;
                    row.valueText.rectTransform.offsetMax = Vector2.zero;
                    row.valueText.color = AccentColor;

                    MakeButtonIn(rt, "▶", new Vector2(0.92f, 0.5f), new Vector2(36, 32),
                                 () => Step(captured, 1));
                }
            }
        }

        private void BuildFooter()
        {
            MakeButton(_canvas.transform as RectTransform, "무작위", new Vector2(0.56f, 0.11f),
                       new Vector2(140, 44), Randomize);

            MakeButton(_canvas.transform as RectTransform, "이 모습으로 시작", new Vector2(0.76f, 0.11f),
                       new Vector2(200, 44), () =>
                       {
                           var result = _appearance.Clone();
                           var done = _onDone;
                           Destroy(gameObject);
                           done?.Invoke(result);
                       });

            MakeButton(_canvas.transform as RectTransform, "뒤로", new Vector2(0.09f, 0.93f),
                       new Vector2(110, 38), () =>
                       {
                           var cancel = _onCancel;
                           Destroy(gameObject);
                           cancel?.Invoke();
                       });
        }

        // =====================================================================================
        // 색상 그래프 (채도·명도 사각형 + 색상 띠 + HEX 입력 + 빠른 선택)
        // =====================================================================================

        private GameObject _pickerPanel;
        private Text _pickerTitle;
        private RectTransform _svRect;
        private Image _svHueLayer;      // 채도·명도 사각형의 바탕(순색) 층 — 색상이 바뀔 때마다 이 색만 갈아 끼운다
        private RectTransform _svCursor;
        private Slider _hueSlider;
        private Image _hueHandleTint;   // 색상 띠 손잡이 — 지금 색상이 뭔지 보여준다
        private InputField _hexInput;
        private RectTransform _swatchRow;

        private ColorRow _editingRow;
        private float _pickH, _pickS, _pickV;
        private bool _suppressCallbacks;   // 코드로 값을 맞출 때는 되먹임(콜백 연쇄)을 막는다

        /// <summary>색 견본을 눌렀을 때 — 그 항목을 고를 수 있는 색상 그래프를 연다.</summary>
        private void OpenPicker(ColorRow row)
        {
            _editingRow = row;
            _pickerTitle.text = row.label + " 고르기";

            var hex = row.get(_appearance);
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) c = Color.white;
            Color.RGBToHSV(c, out _pickH, out _pickS, out _pickV);

            BuildQuickSwatches(row.swatches);
            SyncPickerVisuals();
            _pickerPanel.SetActive(true);
        }

        private void ClosePicker() => _pickerPanel.SetActive(false);

        /// <summary>지금 색이 바뀌었을 때 — 대상 항목에 반영하고, 미리보기와 견본을 새로 그린다.</summary>
        private void ApplyPickedColor()
        {
            if (_editingRow == null) return;
            var c = Color.HSVToRGB(_pickH, _pickS, _pickV);
            string hex = "#" + ColorUtility.ToHtmlStringRGB(c);
            _editingRow.set(_appearance, hex);
            if (_editingRow.swatch != null) _editingRow.swatch.color = c;
            _preview?.SetAppearance(_appearance);
        }

        /// <summary>H/S/V 상태를 화면(사각형 커서·색상 띠·HEX 글자)에 그대로 옮긴다.</summary>
        private void SyncPickerVisuals()
        {
            _suppressCallbacks = true;

            _svHueLayer.color = Color.HSVToRGB(_pickH, 1f, 1f);
            var size = _svRect.rect.size;
            _svCursor.anchoredPosition = new Vector2(_pickS * size.x, _pickV * size.y);

            _hueSlider.value = _pickH;
            var hueColor = Color.HSVToRGB(_pickH, 1f, 1f);
            if (_hueHandleTint != null) _hueHandleTint.color = hueColor;

            var picked = Color.HSVToRGB(_pickH, _pickS, _pickV);
            _hexInput.text = ColorUtility.ToHtmlStringRGB(picked);

            _suppressCallbacks = false;
            ApplyPickedColor();
        }

        private void OnSquareChanged(float s, float v)
        {
            if (_suppressCallbacks) return;
            _pickS = s; _pickV = v;
            SyncPickerVisuals();
        }

        private void OnHueChanged(float h)
        {
            if (_suppressCallbacks) return;
            _pickH = h;
            SyncPickerVisuals();
        }

        private void OnHexSubmitted(string text)
        {
            if (_suppressCallbacks) return;
            string hex = text.StartsWith("#") ? text : "#" + text;
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) { SyncPickerVisuals(); return; }
            Color.RGBToHSV(c, out _pickH, out _pickS, out _pickV);
            SyncPickerVisuals();
        }

        private void BuildQuickSwatches((string label, string hex)[] swatches)
        {
            foreach (Transform child in _swatchRow) Destroy(child.gameObject);

            float w = 40f, gap = 8f;
            for (int i = 0; i < swatches.Length; i++)
            {
                var (label, hex) = swatches[i];
                var go = new GameObject("Quick_" + label);
                var rt = go.AddComponent<RectTransform>();
                rt.SetParent(_swatchRow, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(i * (w + gap), 0);
                rt.sizeDelta = new Vector2(w, w);

                var img = go.AddComponent<Image>();
                ColorUtility.TryParseHtmlString(hex, out var c);
                img.color = c;

                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() =>
                {
                    Color.RGBToHSV(c, out _pickH, out _pickS, out _pickV);
                    SyncPickerVisuals();
                });
            }
        }

        /// <summary>
        /// 색상 그래프 패널을 만든다 — 채도·명도 사각형(드래그로 고름), 색상 띠(Slider),
        /// HEX 입력칸, 빠른 선택 견본, 닫기 버튼. 캐릭터 만들기 화면 위에 뜬다.
        /// </summary>
        private void BuildColorPicker()
        {
            _pickerPanel = new GameObject("ColorPicker");
            var root = _pickerPanel.AddComponent<RectTransform>();
            root.SetParent(_canvas.transform, false);
            Stretch(root);

            var dim = new GameObject("Dim").AddComponent<Image>();
            dim.transform.SetParent(root, false);
            dim.color = new Color(0, 0, 0, 0.55f);
            Stretch(dim.rectTransform);
            // 어둡게 덮은 곳을 눌러도 닫히게 — 뒤 UI로 클릭이 새지 않는다.
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(ClosePicker);

            var panel = new GameObject("Panel");
            var prt = panel.AddComponent<RectTransform>();
            prt.SetParent(root, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(340, 420);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = PanelColor;

            _pickerTitle = Label(prt, "색 고르기", 20, TextAnchor.UpperCenter);
            _pickerTitle.rectTransform.anchorMin = new Vector2(0, 1);
            _pickerTitle.rectTransform.anchorMax = new Vector2(1, 1);
            _pickerTitle.rectTransform.offsetMin = new Vector2(0, -42);
            _pickerTitle.rectTransform.offsetMax = new Vector2(0, -12);
            _pickerTitle.color = AccentColor;

            BuildSvSquare(prt);
            BuildHueSlider(prt);
            BuildHexField(prt);
            BuildSwatchRow(prt);

            MakeButton(prt, "닫기", new Vector2(0.5f, 0.06f), new Vector2(120, 36), ClosePicker);

            _pickerPanel.SetActive(false);
        }

        private void BuildSvSquare(RectTransform parent)
        {
            var go = new GameObject("SVSquare");
            _svRect = go.AddComponent<RectTransform>();
            _svRect.SetParent(parent, false);
            _svRect.anchorMin = _svRect.anchorMax = new Vector2(0.5f, 1f);
            _svRect.pivot = new Vector2(0.5f, 1f);
            _svRect.anchoredPosition = new Vector2(0, -56);
            _svRect.sizeDelta = new Vector2(260, 200);

            _svHueLayer = go.AddComponent<Image>();
            _svHueLayer.color = Color.red;

            var whiteGo = new GameObject("Saturation");
            var whiteRt = whiteGo.AddComponent<RectTransform>();
            whiteRt.SetParent(_svRect, false);
            Stretch(whiteRt);
            var whiteImg = whiteGo.AddComponent<RawImage>();
            whiteImg.texture = GradientTexture(new Color(1, 1, 1, 1), new Color(1, 1, 1, 0), horizontal: true);
            whiteImg.raycastTarget = false;

            var blackGo = new GameObject("Value");
            var blackRt = blackGo.AddComponent<RectTransform>();
            blackRt.SetParent(_svRect, false);
            Stretch(blackRt);
            var blackImg = blackGo.AddComponent<RawImage>();
            blackImg.texture = GradientTexture(new Color(0, 0, 0, 1), new Color(0, 0, 0, 0), horizontal: false);
            blackImg.raycastTarget = false;

            var drag = go.AddComponent<PointerDragRelay>();
            drag.onChange = OnSquareChanged;

            var cursorGo = new GameObject("Cursor");
            _svCursor = cursorGo.AddComponent<RectTransform>();
            _svCursor.SetParent(_svRect, false);
            _svCursor.anchorMin = _svCursor.anchorMax = new Vector2(0, 0);
            _svCursor.pivot = new Vector2(0.5f, 0.5f);
            _svCursor.sizeDelta = new Vector2(16, 16);
            var cursorImg = cursorGo.AddComponent<Image>();
            cursorImg.color = Color.clear;
            cursorImg.raycastTarget = false;
            var outline = new GameObject("Ring").AddComponent<Image>();
            outline.transform.SetParent(_svCursor, false);
            outline.color = Color.white;
            Stretch(outline.rectTransform);
            outline.raycastTarget = false;
            var inner = new GameObject("Inner").AddComponent<Image>();
            inner.transform.SetParent(_svCursor, false);
            inner.color = new Color(0, 0, 0, 0.6f);
            var irt = inner.rectTransform;
            irt.anchorMin = new Vector2(0.5f, 0.5f); irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(4, 4);
            inner.raycastTarget = false;
        }

        private void BuildHueSlider(RectTransform parent)
        {
            var go = new GameObject("HueSlider");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -268);
            rt.sizeDelta = new Vector2(260, 22);

            var bgGo = new GameObject("Rainbow");
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.SetParent(rt, false);
            Stretch(bgRt);
            var bgImg = bgGo.AddComponent<RawImage>();
            bgImg.texture = HueStripTexture();
            bgImg.raycastTarget = false;

            var handleAreaGo = new GameObject("HandleArea");
            var handleAreaRt = handleAreaGo.AddComponent<RectTransform>();
            handleAreaRt.SetParent(rt, false);
            Stretch(handleAreaRt);

            var handleGo = new GameObject("Handle");
            var handleRt = handleGo.AddComponent<RectTransform>();
            handleRt.SetParent(handleAreaRt, false);
            handleRt.sizeDelta = new Vector2(10, 26);
            _hueHandleTint = handleGo.AddComponent<Image>();
            _hueHandleTint.color = Color.red;

            _hueSlider = go.AddComponent<Slider>();
            _hueSlider.direction = Slider.Direction.LeftToRight;
            _hueSlider.minValue = 0f; _hueSlider.maxValue = 1f;
            _hueSlider.targetGraphic = _hueHandleTint;
            _hueSlider.handleRect = handleRt;
            _hueSlider.transition = Selectable.Transition.None;
            _hueSlider.onValueChanged.AddListener(OnHueChanged);
        }

        private void BuildHexField(RectTransform parent)
        {
            var labelGo = Label(parent, "HEX", 15, TextAnchor.MiddleLeft);
            labelGo.rectTransform.anchorMin = labelGo.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            labelGo.rectTransform.pivot = new Vector2(0.5f, 1f);
            labelGo.rectTransform.anchoredPosition = new Vector2(-100, -306);
            labelGo.rectTransform.sizeDelta = new Vector2(50, 26);
            labelGo.color = TextColor;

            var go = new GameObject("HexInput");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(20, -306);
            rt.sizeDelta = new Vector2(180, 30);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.2f, 1f);

            var textGo = new GameObject("Text");
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.SetParent(rt, false);
            Stretch(textRt);
            textRt.offsetMin = new Vector2(8, 2);
            textRt.offsetMax = new Vector2(-8, -2);
            var text = textGo.AddComponent<Text>();
            text.font = _font;
            text.fontSize = 16;
            text.color = TextColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var placeholderGo = new GameObject("Placeholder");
            var placeholderRt = placeholderGo.AddComponent<RectTransform>();
            placeholderRt.SetParent(rt, false);
            Stretch(placeholderRt);
            placeholderRt.offsetMin = new Vector2(8, 2);
            placeholderRt.offsetMax = new Vector2(-8, -2);
            var placeholder = placeholderGo.AddComponent<Text>();
            placeholder.font = _font;
            placeholder.fontSize = 16;
            placeholder.color = new Color(1, 1, 1, 0.35f);
            placeholder.text = "RRGGBB";
            placeholder.alignment = TextAnchor.MiddleLeft;

            _hexInput = go.AddComponent<InputField>();
            _hexInput.targetGraphic = img;
            _hexInput.textComponent = text;
            _hexInput.placeholder = placeholder;
            _hexInput.characterLimit = 7;
            _hexInput.onEndEdit.AddListener(OnHexSubmitted);
        }

        private void BuildSwatchRow(RectTransform parent)
        {
            var go = new GameObject("QuickSwatches");
            _swatchRow = go.AddComponent<RectTransform>();
            _swatchRow.SetParent(parent, false);
            _swatchRow.anchorMin = _swatchRow.anchorMax = new Vector2(0.5f, 1f);
            _swatchRow.pivot = new Vector2(0.5f, 1f);
            _swatchRow.anchoredPosition = new Vector2(-124, -348);
            _swatchRow.sizeDelta = new Vector2(260, 40);
        }

        /// <summary>
        /// 두 색 사이를 가로(horizontal) 또는 세로로 잇는 아주 작은 그라디언트 텍스처.
        /// 부드럽게 늘어나도록 Bilinear로 읽는다 (다른 곳의 픽셀아트와 달리 이건 UI 그래프다).
        /// </summary>
        private static Texture2D GradientTexture(Color a, Color b, bool horizontal)
        {
            var tex = horizontal ? new Texture2D(2, 1) : new Texture2D(1, 2);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            if (horizontal) { tex.SetPixel(0, 0, a); tex.SetPixel(1, 0, b); }
            else { tex.SetPixel(0, 0, a); tex.SetPixel(0, 1, b); }
            tex.Apply();
            return tex;
        }

        private static Texture2D _hueStripCache;
        private static Texture2D HueStripTexture()
        {
            if (_hueStripCache != null) return _hueStripCache;
            const int w = 64;
            var tex = new Texture2D(w, 1) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < w; x++) tex.SetPixel(x, 0, Color.HSVToRGB((float)x / (w - 1), 1f, 1f));
            tex.Apply();
            _hueStripCache = tex;
            return tex;
        }

        /// <summary>
        /// 채도·명도 사각형의 클릭·드래그를 (0..1, 0..1)로 바꿔 알려 주는 작은 컴포넌트.
        /// SV 사각형 전용이라 이 파일 안에 둔다.
        /// </summary>
        private class PointerDragRelay : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            public Action<float, float> onChange;
            private RectTransform _rt;

            private void Awake() => _rt = GetComponent<RectTransform>();

            public void OnPointerDown(PointerEventData e) => Handle(e);
            public void OnDrag(PointerEventData e) => Handle(e);

            private void Handle(PointerEventData e)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera, out var local))
                    return;
                var rect = _rt.rect;
                float s = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
                float v = Mathf.Clamp01((local.y - rect.yMin) / rect.height);
                onChange?.Invoke(s, v);
            }
        }

        // ---------- 작은 위젯들 ----------
        private Button MakeButton(RectTransform parent, string text, Vector2 anchor, Vector2 size,
                                  UnityEngine.Events.UnityAction onClick)
        {
            var btn = MakeButtonIn(parent, text, anchor, size, onClick);
            return btn;
        }

        private Button MakeButtonIn(RectTransform parent, string text, Vector2 anchor, Vector2 size,
                                    UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + text);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.26f, 0.23f, 0.95f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var label = Label(rt, text, 17, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.raycastTarget = false;
            label.color = TextColor;
            return btn;
        }

        private Text Label(RectTransform parent, string text, int size, TextAnchor anchor)
        {
            var go = new GameObject("Label");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(200, 30);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
