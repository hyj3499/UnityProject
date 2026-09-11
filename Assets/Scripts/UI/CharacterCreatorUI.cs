using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    /// <summary>
    /// 새 게임을 시작할 때 뜨는 캐릭터 만들기 화면.
    ///
    /// 고를 것은 두 가지다 — <b>무엇을 걸칠지</b>(머리 모양·눈·옷·신발·장신구)와 <b>무슨 색으로</b>
    /// (피부·머리·눈·신발). 색은 그림에 딸린 색 대응표에서 하나를 고른 뒤 색상·채도·명도를 더
    /// 틀어 쓰므로, 표에 없는 색도 만들 수 있다.
    ///
    /// 미리보기는 게임과 <b>같은 FarmerAnimator</b>를 쓴다 — 화면 전용 그리기 코드를 따로 두면
    /// 실제 모습과 어긋나기 때문에, 카메라 앞에 진짜 캐릭터를 하나 세워 두고 보여 준다.
    /// </summary>
    public class CharacterCreatorUI : MonoBehaviour
    {
        private static readonly Color TextColor = new Color(0.96f, 0.92f, 0.84f);
        private static readonly Color AccentColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color RowColor = new Color(0.16f, 0.19f, 0.17f, 0.9f);

        private Font _font;
        private Canvas _canvas;
        private Action<PlayerAppearance> _onDone;
        private Action _onCancel;

        private PlayerAppearance _appearance = new PlayerAppearance();

        private GameObject _previewGo;
        private FarmerAnimator _preview;
        private Camera _previewCam;
        private RenderTexture _previewTexture;
        private Direction _previewFacing = Direction.Down;
        private bool _previewWalking;

        /// <summary>"무엇을 걸칠지" 한 줄. 목록에서 앞뒤로 넘긴다.</summary>
        private class ItemRow
        {
            public string label;
            public FarmerCategory[] categories;   // 여러 갈래를 한 줄에서 함께 넘긴다 (하의 = 바지+치마)
            public bool allowNone;                // "없음"을 고를 수 있는지
            public Func<PlayerAppearance, string> get;
            public Action<PlayerAppearance, string> set;
            public Text valueText;

            private List<string> _paths;
            private List<string> _labels;

            /// <summary>이 줄에서 고를 수 있는 것들을 한 줄로 펴 둔다.</summary>
            public void Prepare()
            {
                _paths = new List<string>();
                _labels = new List<string>();
                if (allowNone) { _paths.Add(""); _labels.Add("없음"); }
                foreach (var cat in categories)
                    foreach (var item in FarmerCatalog.Of(cat))
                    {
                        _paths.Add(item.path);
                        _labels.Add(item.label);
                    }

                // 그림을 아직 넣지 않은 갈래는 통째로 비어 있다. 고를 것이 하나도 없으면
                // 줄을 그릴 수 없으므로 "없음"이라도 남겨 둔다.
                if (_paths.Count == 0) { _paths.Add(""); _labels.Add("없음"); }
            }

            public int Count => _paths.Count;
            public string PathAt(int i) => _paths[Mathf.Clamp(i, 0, _paths.Count - 1)];
            public string LabelAt(int i) => _labels[Mathf.Clamp(i, 0, _labels.Count - 1)];

            public int IndexOf(string path)
            {
                int i = _paths.IndexOf(path ?? "");
                return i < 0 ? 0 : i;
            }
        }

        /// <summary>"무슨 색으로" 한 줄. 대응표에서 고른 색 + 색상/채도/명도.</summary>
        private class ColorRow
        {
            public string label;
            public Func<PlayerAppearance, AppearanceColor> get;
            public Func<PlayerAppearance, string> lut;   // 지금 이 부위가 쓰는 대응표
            public Text valueText;
            public Slider hue, sat, val;
        }

        private readonly List<ItemRow> _itemRows = new List<ItemRow>();
        private readonly List<ColorRow> _colorRows = new List<ColorRow>();
        private bool _suppressCallbacks;

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
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (_previewGo != null) Destroy(_previewGo);
            if (_previewCam != null) Destroy(_previewCam.gameObject);
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_previewTexture != null) { _previewTexture.Release(); Destroy(_previewTexture); }
        }

        // ---------- 무엇을 고를 수 있는지 ----------
        private void BuildRowDefs()
        {
            void Item(string label, bool allowNone,
                      Func<PlayerAppearance, string> get, Action<PlayerAppearance, string> set,
                      params FarmerCategory[] cats)
            {
                var row = new ItemRow { label = label, categories = cats, allowNone = allowNone, get = get, set = set };
                row.Prepare();
                if (row.Count > 0) _itemRows.Add(row);
            }

            void Col(string label, Func<PlayerAppearance, AppearanceColor> get,
                     Func<PlayerAppearance, string> lut)
                => _colorRows.Add(new ColorRow { label = label, get = get, lut = lut });

            Item("머리 모양", false, a => a.hair, (a, v) => a.hair = v, FarmerCategory.Hair);
            Item("눈", false, a => a.eyes, (a, v) => a.eyes = v, FarmerCategory.Eyes);
            Item("수염", true, a => a.facialHair, (a, v) => a.facialHair = v, FarmerCategory.FacialHair);

            // 한 벌짜리 옷을 고르면 상·하의 대신 그것을 입는다 (PlayerAppearance.ItemFor).
            Item("한 벌 옷", true, a => a.outfit, (a, v) => a.outfit = v,
                 FarmerCategory.Dress, FarmerCategory.Overalls, FarmerCategory.Suit, FarmerCategory.Robe);
            Item("상의", true, a => a.top, (a, v) => a.top = v, FarmerCategory.Top, FarmerCategory.Underwear);
            Item("하의", true, a => a.pants, (a, v) => a.pants = v, FarmerCategory.Pants, FarmerCategory.Skirt);
            Item("신발", true, a => a.shoes, (a, v) => a.shoes = v, FarmerCategory.Shoes);

            Item("모자", true, a => a.headGear, (a, v) => a.headGear = v, FarmerCategory.HeadGear);
            Item("얼굴 장식", true, a => a.faceGear, (a, v) => a.faceGear = v, FarmerCategory.FaceGear);
            Item("등에 메는 것", true, a => a.backGear, (a, v) => a.backGear = v, FarmerCategory.BackGear);

            Col("피부색", a => a.skin, a => PlayerAppearance.BaseLut);
            Col("머리색", a => a.hairColor, a => FarmerArt.LutPathOr(a.hair, PlayerAppearance.HairLut));
            Col("눈 색", a => a.eyeColor, a => PlayerAppearance.EyesLut);
            Col("신발 색", a => a.shoesColor, a => FarmerArt.LutPath(a.shoes));
        }

        private void StepItem(ItemRow row, int delta)
        {
            int i = row.IndexOf(row.get(_appearance));
            int n = row.Count;
            row.set(_appearance, row.PathAt(((i + delta) % n + n) % n));
            RefreshAll();
        }

        private void StepPalette(ColorRow row, int delta)
        {
            var c = row.get(_appearance);
            int n = Mathf.Max(1, FarmerArt.PaletteCount(row.lut(_appearance)));
            c.palette = ((c.palette - 1 + delta) % n + n) % n + 1;
            RefreshAll();
        }

        private void RefreshAll()
        {
            _suppressCallbacks = true;

            foreach (var row in _itemRows)
                if (row.valueText != null)
                    row.valueText.text = row.LabelAt(row.IndexOf(row.get(_appearance)));

            foreach (var row in _colorRows)
            {
                var c = row.get(_appearance);
                int n = FarmerArt.PaletteCount(row.lut(_appearance));
                if (row.valueText != null)
                    row.valueText.text = n > 0 ? $"{c.palette} / {n}" : "표 없음";
                if (row.hue != null) row.hue.value = c.hue;
                if (row.sat != null) row.sat.value = c.saturation;
                if (row.val != null) row.val.value = c.value;
            }

            _suppressCallbacks = false;
            _preview?.SetAppearance(_appearance);
        }

        private void Randomize()
        {
            foreach (var row in _itemRows)
                row.set(_appearance, row.PathAt(UnityEngine.Random.Range(0, row.Count)));

            foreach (var row in _colorRows)
            {
                var c = row.get(_appearance);
                int n = Mathf.Max(1, FarmerArt.PaletteCount(row.lut(_appearance)));
                c.palette = UnityEngine.Random.Range(1, n + 1);
                c.hue = 0f; c.saturation = 1f; c.value = 1f;
            }
            RefreshAll();
        }

        // ---------- 미리보기 ----------
        private void BuildPreview()
        {
            _previewTexture = new RenderTexture(240, 300, 16) { filterMode = FilterMode.Point };
            _previewTexture.Create();

            var camGo = new GameObject("PreviewCamera");
            _previewCam = camGo.AddComponent<Camera>();
            _previewCam.orthographic = true;
            _previewCam.orthographicSize = 1.3f;
            _previewCam.backgroundColor = new Color(0.13f, 0.17f, 0.15f);
            _previewCam.clearFlags = CameraClearFlags.SolidColor;
            _previewCam.targetTexture = _previewTexture;
            // 세워 둔 캐릭터가 세상 어디와도 겹치지 않도록 멀리 떨어뜨려 둔다.
            camGo.transform.position = new Vector3(500f, 500f, -10f);

            _previewGo = new GameObject("PreviewCharacter");
            _previewGo.transform.position = new Vector3(500f, 499.6f, 0f);
            _preview = _previewGo.AddComponent<FarmerAnimator>();
            _preview.Init(_appearance);
            _preview.SetSortingOrder(0);
            _preview.SetLocomotion(false, false, _previewFacing);

            var go = new GameObject("Preview");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.20f, 0.55f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(240, 300);
            go.AddComponent<RawImage>().texture = _previewTexture;
        }

        private void TurnPreview(int delta)
        {
            // 보여 주는 순서: 앞 → 오른쪽 → 뒤 → 왼쪽 (제자리에서 한 바퀴 도는 느낌)
            var order = new[] { Direction.Down, Direction.Right, Direction.Up, Direction.Left };
            int i = Array.IndexOf(order, _previewFacing);
            if (i < 0) i = 0;
            int n = order.Length;
            _previewFacing = order[((i + delta) % n + n) % n];
            _preview?.SetLocomotion(_previewWalking, false, _previewFacing);
        }

        private void ToggleWalk()
        {
            _previewWalking = !_previewWalking;
            _preview?.SetLocomotion(_previewWalking, false, _previewFacing);
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
            title.rectTransform.offsetMin = new Vector2(0, -66);
            title.rectTransform.offsetMax = new Vector2(0, -16);
            title.color = AccentColor;

            BuildPreviewControls();
            BuildRowList();
            BuildFooter();
        }

        private void BuildPreviewControls()
        {
            MakeButton(_canvas.transform as RectTransform, "◀", new Vector2(0.10f, 0.19f),
                       new Vector2(48, 34), () => TurnPreview(-1));
            MakeButton(_canvas.transform as RectTransform, "▶", new Vector2(0.30f, 0.19f),
                       new Vector2(48, 34), () => TurnPreview(1));
            MakeButton(_canvas.transform as RectTransform, "걸어보기", new Vector2(0.20f, 0.19f),
                       new Vector2(96, 34), ToggleWalk);
        }

        /// <summary>
        /// 고를 것이 많아 한 화면에 다 들어가지 않으므로 줄 목록을 밀어 올려 볼 수 있게 한다.
        /// </summary>
        private void BuildRowList()
        {
            var viewGo = new GameObject("RowView");
            var view = viewGo.AddComponent<RectTransform>();
            view.SetParent(_canvas.transform, false);
            view.anchorMin = new Vector2(0.40f, 0.17f);
            view.anchorMax = new Vector2(0.97f, 0.87f);
            view.offsetMin = view.offsetMax = Vector2.zero;
            viewGo.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.08f, 0.6f);
            viewGo.AddComponent<Mask>().showMaskGraphic = true;

            var scroll = viewGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 28f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var contentGo = new GameObject("Content");
            var content = contentGo.AddComponent<RectTransform>();
            content.SetParent(view, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            scroll.content = content;
            scroll.viewport = view;

            const float ItemH = 36f, ColorH = 78f, Gap = 4f;
            float y = 0f;

            RectTransform NextRow(string name, float height)
            {
                var holder = new GameObject(name);
                var rt = holder.AddComponent<RectTransform>();
                rt.SetParent(content, false);
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(6, -(y + height));
                rt.offsetMax = new Vector2(-6, -y);
                holder.AddComponent<Image>().color = RowColor;
                y += height + Gap;
                return rt;
            }

            Text NameLabel(RectTransform rt, string text, float width)
            {
                var name = Label(rt, text, 16, TextAnchor.MiddleLeft);
                name.rectTransform.anchorMin = new Vector2(0, 0);
                name.rectTransform.anchorMax = new Vector2(0, 1);
                name.rectTransform.pivot = new Vector2(0, 0.5f);
                name.rectTransform.offsetMin = new Vector2(10, 0);
                name.rectTransform.sizeDelta = new Vector2(width, 0);
                name.color = TextColor;
                return name;
            }

            foreach (var row in _itemRows)
            {
                var rt = NextRow("Row_" + row.label, ItemH);
                NameLabel(rt, row.label, 92);

                var captured = row;
                MakeButtonIn(rt, "◀", new Vector2(0f, 0.5f), new Vector2(30, 26), () => StepItem(captured, -1))
                    .GetComponent<RectTransform>().anchoredPosition = new Vector2(112, 0);

                row.valueText = Label(rt, "", 15, TextAnchor.MiddleCenter);
                row.valueText.rectTransform.anchorMin = new Vector2(0, 0);
                row.valueText.rectTransform.anchorMax = new Vector2(1, 1);
                row.valueText.rectTransform.offsetMin = new Vector2(132, 0);
                row.valueText.rectTransform.offsetMax = new Vector2(-44, 0);

                MakeButtonIn(rt, "▶", new Vector2(1f, 0.5f), new Vector2(30, 26), () => StepItem(captured, 1))
                    .GetComponent<RectTransform>().anchoredPosition = new Vector2(-22, 0);
            }

            foreach (var row in _colorRows)
            {
                var rt = NextRow("Row_" + row.label, ColorH);
                NameLabel(rt, row.label, 92);

                var captured = row;
                MakeButtonIn(rt, "◀", new Vector2(0f, 1f), new Vector2(30, 24), () => StepPalette(captured, -1))
                    .GetComponent<RectTransform>().anchoredPosition = new Vector2(112, -18);

                row.valueText = Label(rt, "", 14, TextAnchor.MiddleCenter);
                row.valueText.rectTransform.anchorMin = new Vector2(0, 1);
                row.valueText.rectTransform.anchorMax = new Vector2(0, 1);
                row.valueText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                row.valueText.rectTransform.anchoredPosition = new Vector2(180, -18);
                row.valueText.rectTransform.sizeDelta = new Vector2(90, 22);

                MakeButtonIn(rt, "▶", new Vector2(0f, 1f), new Vector2(30, 24), () => StepPalette(captured, 1))
                    .GetComponent<RectTransform>().anchoredPosition = new Vector2(248, -18);

                row.hue = MakeSlider(rt, "색상", -180f, 180f, -46f,
                                     v => { captured.get(_appearance).hue = v; Applied(); });
                row.sat = MakeSlider(rt, "채도", 0f, 2f, -62f,
                                     v => { captured.get(_appearance).saturation = v; Applied(); });
                row.val = MakeSlider(rt, "명도", 0f, 2f, -78f,
                                     v => { captured.get(_appearance).value = v; Applied(); });
            }

            content.sizeDelta = new Vector2(0, y);
        }

        /// <summary>슬라이더를 움직였을 때 — 줄 표시는 그대로 두고 미리보기만 다시 그린다.</summary>
        private void Applied()
        {
            if (_suppressCallbacks) return;
            _preview?.SetAppearance(_appearance);
        }

        private Slider MakeSlider(RectTransform parent, string label, float min, float max, float y,
                                  UnityEngine.Events.UnityAction<float> onChange)
        {
            var cap = Label(parent, label, 13, TextAnchor.MiddleLeft);
            cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = new Vector2(0, 1);
            cap.rectTransform.pivot = new Vector2(0, 0.5f);
            cap.rectTransform.anchoredPosition = new Vector2(10, y);
            cap.rectTransform.sizeDelta = new Vector2(40, 16);
            cap.color = new Color(0.78f, 0.76f, 0.72f);

            var go = new GameObject("Slider_" + label);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(56, y - 7);
            rt.offsetMax = new Vector2(-14, y + 7);

            var bg = new GameObject("BG").AddComponent<Image>();
            bg.transform.SetParent(rt, false);
            bg.color = new Color(0.10f, 0.12f, 0.11f);
            Stretch(bg.rectTransform);

            var fillArea = new GameObject("FillArea").AddComponent<RectTransform>();
            fillArea.SetParent(rt, false);
            Stretch(fillArea);
            var fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(fillArea, false);
            fill.color = new Color(0.45f, 0.58f, 0.42f);
            Stretch(fill.rectTransform);

            var handle = new GameObject("Handle").AddComponent<Image>();
            handle.transform.SetParent(rt, false);
            handle.color = AccentColor;
            handle.rectTransform.sizeDelta = new Vector2(12, 18);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        private void BuildFooter()
        {
            MakeButton(_canvas.transform as RectTransform, "무작위", new Vector2(0.48f, 0.08f),
                       new Vector2(130, 42), Randomize);

            MakeButton(_canvas.transform as RectTransform, "이 모습으로 시작", new Vector2(0.75f, 0.08f),
                       new Vector2(200, 42), () =>
                       {
                           var result = _appearance.Clone();
                           var done = _onDone;
                           Destroy(gameObject);
                           done?.Invoke(result);
                       });

            MakeButton(_canvas.transform as RectTransform, "뒤로", new Vector2(0.08f, 0.93f),
                       new Vector2(104, 36), () =>
                       {
                           var cancel = _onCancel;
                           Destroy(gameObject);
                           cancel?.Invoke();
                       });
        }

        // ---------- 작은 위젯들 ----------
        private Button MakeButton(RectTransform parent, string text, Vector2 anchor, Vector2 size,
                                  UnityEngine.Events.UnityAction onClick)
            => MakeButtonIn(parent, text, anchor, size, onClick);

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

            var label = Label(rt, text, 16, TextAnchor.MiddleCenter);
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
